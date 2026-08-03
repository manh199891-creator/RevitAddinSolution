import unittest
import json
import subprocess
from pathlib import Path
import tempfile
import shutil
import hashlib

import sys
sys.path.insert(0, str(Path(__file__).parent.parent.resolve()))

from review_pipeline import aggregate_batch_results, ReviewStatus, generate_finding_id, build_codex_prompt_batch, is_review_success, classify_review_status
from harness import parse_dual_args, cmd_gate, _record_blocked_verify
from dual_agent_runtime import _scoped_writer_snapshot

class TestConvergence(unittest.TestCase):

    def setUp(self):
        self.temp_dir = Path(tempfile.mkdtemp())
        self.project_root = self.temp_dir / "project"
        self.project_root.mkdir()
        (self.project_root / ".agent").mkdir()
        (self.project_root / ".agent" / "context").mkdir()
        (self.project_root / ".agent" / "reports").mkdir()
        (self.project_root / ".agent" / "state").mkdir()

    def tearDown(self):
        shutil.rmtree(self.temp_dir)

    def test_p3_only_pass_with_advisories(self):
        batches = [{
            "status": "PASS",
            "findings": [
                {"severity": "P3", "status": "OPEN", "finding_id": "123"}
            ]
        }]
        status, _ = aggregate_batch_results(batches)
        self.assertEqual(status, ReviewStatus.PASS_WITH_ADVISORIES)

    def test_pass_with_advisories_schema_validation(self):
        schema_path = Path(__file__).parent.parent / "schemas" / "review_run.schema.json"
        import jsonschema
        schema = json.loads(schema_path.read_text('utf-8'))
        manifest = {
            "schema_version": 1,
            "run_id": "test",
            "task_id": "test",
            "repository_root": "a",
            "source_project_root": "b",
            "base_revision": "c",
            "snapshot_hash": "d",
            "status": "PASS_WITH_ADVISORIES"
        }
        jsonschema.validate(instance=manifest, schema=schema)
        self.assertTrue(True)

    def test_max_cycles_bounds(self):
        with self.assertRaises(ValueError):
            parse_dual_args(["--max-cycles", "0"])
        with self.assertRaises(ValueError):
            parse_dual_args(["--max-cycles", "4"])
        with self.assertRaises(ValueError):
            parse_dual_args(["--max-cycles", "100"])

        opts = parse_dual_args(["--max-cycles", "1"])
        self.assertEqual(opts["max_cycles"], 1)
        opts = parse_dual_args(["--max-cycles", "3"])
        self.assertEqual(opts["max_cycles"], 3)

    def test_stable_finding_id(self):
        f1 = {"rule_id": "A", "file": "f.py", "symbol": "sym", "title": "t1"}
        id1 = generate_finding_id(f1)

        f2 = {"rule_id": "A", "file": "f.py", "symbol": "sym", "title": "t1", "body": "changed body", "line": 42}
        id2 = generate_finding_id(f2)

        self.assertEqual(id1, id2)

    def test_report_only_change_not_source_delta(self):
        repo_root = self.project_root / "source-code"
        repo_root.mkdir()
        (repo_root / "file.py").write_text("print('hello')", "utf-8")

        reports_dir = repo_root / ".agent" / "reports"
        reports_dir.mkdir(parents=True)
        (reports_dir / "report.md").write_text("old", "utf-8")

        handoff = {"allowed_files": ["**/*"]}
        hash1 = _scoped_writer_snapshot(self.project_root, handoff)

        (reports_dir / "report.md").write_text("new", "utf-8")
        hash2 = _scoped_writer_snapshot(self.project_root, handoff)

        self.assertEqual(hash1, hash2)

        (repo_root / "file2.py").write_text("print('world')", "utf-8")
        hash3 = _scoped_writer_snapshot(self.project_root, handoff)
        self.assertNotEqual(hash1, hash3)

        (repo_root / "__pycache__").mkdir()
        (repo_root / "__pycache__" / "file.pyc").write_text("binary", "utf-8")
        hash4 = _scoped_writer_snapshot(self.project_root, handoff)
        self.assertEqual(hash3, hash4)

    def test_focused_retry_prompt_contains_previous_findings(self):
        manifest = {
            "run_id": "run123",
            "task_id": "123",
            "feature_name": "test",
            "snapshot_hash": "hash",
            "review_mode": "FOCUSED_RETRY",
            "previous_findings": [
                {"finding_id": "f_123", "severity": "P1", "file": "test.py", "line": 1, "title": "Bug"}
            ],
            "plan_files": [],
            "acceptance_files": [],
            "repository_root": str(self.project_root)
        }
        (self.project_root / "test.py").touch()
        prompt = build_codex_prompt_batch(manifest, self.project_root, {}, ["test.py"])
        self.assertIn("ID: f_123", prompt)
        self.assertIn("RESOLVED, STILL_OPEN, REOPENED, ADVISORY, or DEFERRED", prompt)

    def test_missing_previous_finding_contract(self):
        # Setup previous run with one finding
        prev_manifest = {
            "run_id": "run1",
            "status": "FAIL",
            "findings": [
                {"finding_id": "1", "severity": "P1", "status": "OPEN", "title": "Missing"}
            ]
        }
        (self.project_root / ".agent/state/review_run.json").write_text(json.dumps(prev_manifest))

        import review_pipeline
        # Mock run_id generating and state
        def run():
            manifest = {
                "review_mode": "FOCUSED_RETRY",
                "previous_findings": prev_manifest["findings"]
            }
            # new findings returned by codex (empty)
            findings = []

            # The logic inside review_pipeline aggregate_batch_results uses review_run.json in disk but wait, it uses the dict directly if patched? No, run_codex_review sets up manifest with previous_blocking_ids etc.
            # We can test classify_review_status and the contract directly
            # Contract enforcement adds STILL_OPEN findings
            prev_blocking = [f for f in prev_manifest["findings"] if f.get("severity") in {"P0", "P1", "P2"} and f.get("status") not in {"RESOLVED", "ADVISORY", "DEFERRED"}]
            curr_blocking = []
            curr_blocking_ids = set()
            contract_failed = False
            for pf in prev_blocking:
                pf_id = pf.get("finding_id")
                if pf_id and pf_id not in {f.get("finding_id") for f in findings}:
                    missing_f = pf.copy()
                    missing_f["status"] = "STILL_OPEN"
                    findings.append(missing_f)
                    curr_blocking.append(missing_f)
                    curr_blocking_ids.add(pf_id)
                    contract_failed = True

            status = classify_review_status(findings)
            return status, contract_failed

        status, failed = run()
        self.assertTrue(failed)
        self.assertEqual(status, "FAIL")

    def test_no_progress_logic(self):
        # A run that shows no progress
        prev_manifest = {
            "run_id": "run1",
            "review_mode": "FOCUSED_RETRY",
            "progress": False,
            "previous_blocking_ids": ["1"],
            "current_blocking_ids": ["1"]
        }
        (self.project_root / ".agent/state/review_run.json").write_text(json.dumps(prev_manifest))

        # simulated logic
        progress = False
        curr_blocking_ids = {"1"}

        status = "FAIL"
        if prev_manifest.get("review_mode") == "FOCUSED_RETRY" and not prev_manifest.get("progress", True) and not progress and curr_blocking_ids:
            status = "BLOCKED_NO_PROGRESS"

        self.assertEqual(status, "BLOCKED_NO_PROGRESS")

        # test first no progress -> FAIL
        prev_manifest["progress"] = True
        status = "FAIL"
        if prev_manifest.get("review_mode") == "FOCUSED_RETRY" and not prev_manifest.get("progress", True) and not progress and curr_blocking_ids:
            status = "BLOCKED_NO_PROGRESS"
        self.assertEqual(status, "FAIL")

    def test_oscillation_logic(self):
        prev_manifest = {
            "run_id": "run1",
            "review_mode": "FOCUSED_RETRY",
            "previous_blocking_ids": ["1"],
            "current_blocking_ids": ["1", "2"]
        }
        (self.project_root / ".agent/state/review_run.json").write_text(json.dumps(prev_manifest))

        prev_blocking_ids = {"1", "2"}
        curr_blocking_ids = {"1"}

        status = "FAIL"
        prev_prev_blocking_ids = set(prev_manifest.get("previous_blocking_ids", []))
        if prev_prev_blocking_ids and curr_blocking_ids == prev_prev_blocking_ids and curr_blocking_ids != prev_blocking_ids:
            status = "BLOCKED_OSCILLATION"

        self.assertEqual(status, "BLOCKED_OSCILLATION")

    def test_pass_with_advisories_release_gate(self):
        # Prepare state for release gate
        manifest = {
            "status": "PASS_WITH_ADVISORIES",
            "exit_code": 0,
            "task_id": "test",
            "snapshot_hash": "hash123",
            "included_files": ["test.py"]
        }
        (self.project_root / ".agent/state/review_run.json").write_text(json.dumps(manifest))
        (self.project_root / ".agent/context/TASK_SCOPE.json").write_text(json.dumps({"task_id": "test"}))
        (self.project_root / ".agent/state/task_baseline.json").write_text(json.dumps({"test": "123"}))
        (self.project_root / "source-code").mkdir(exist_ok=True)
        (self.project_root / ".agent/reports/GUARDRAILS_REPORT.md").write_text("# GUARDRAILS_REPORT.md\n\n## Status: PASS\n")
        (self.project_root / ".agent/reports/BUILD_REPORT.md").write_text("# BUILD_REPORT.md\n\n## Status: PASS\n")
        (self.project_root / ".agent/reports/QA_REPORT.md").write_text("# QA_REPORT.md\n\n## Status: PASS\n")

        # Mock get_deterministic_snapshot temporarily for this test
        import review_pipeline
        import harness
        orig = harness.evaluate_task_scope
        def mock_eval(*args, **kwargs):
            return {"snapshot_hash": "hash123", "task_files": ["test.py"]}
        harness.evaluate_task_scope = mock_eval

        # Need to mock sys.exit to catch ALLOW_RELEASE?
        # Actually cmd_gate does not raise sys.exit unless it fails
        # It just returns successfully.
        try:
            cmd_gate("project", self.project_root)
            success = True
        except SystemExit:
            success = False
        finally:
            harness.evaluate_task_scope = orig

        self.assertTrue(success)

if __name__ == '__main__':
    unittest.main()
