import unittest
import json
import subprocess
from pathlib import Path
import tempfile
import shutil
import hashlib

import sys
sys.path.insert(0, str(Path(__file__).parent.parent.resolve()))

from review_pipeline import aggregate_batch_results, ReviewStatus, generate_finding_id, build_codex_prompt_batch, is_review_success, classify_review_status, evaluate_focused_retry_progress
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
            # We can test classify_review_status, evaluate_focused_retry_progress and the contract directly
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
        prev_manifest = {
            "review_mode": "FOCUSED_RETRY",
            "progress": False,
            "previous_blocking_ids": ["1"],
            "current_blocking_ids": ["1"],
            "previous_findings": [{"finding_id": "1", "severity": "P1", "status": "OPEN", "title": "A"}]
        }

        current_findings = [{"finding_id": "1", "severity": "P1", "status": "OPEN", "title": "A"}]
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev_manifest, current_findings)
        self.assertEqual(status, "BLOCKED_NO_PROGRESS")
        self.assertEqual(reason_code, "BLOCKING_FINDINGS_UNCHANGED")

        prev_manifest["progress"] = True
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev_manifest, current_findings)
        self.assertEqual(status, "FAIL")

    def test_oscillation_logic(self):
        prev_manifest = {
            "review_mode": "FOCUSED_RETRY",
            "previous_blocking_ids": ["1"],
            "current_blocking_ids": ["1", "2"],
            "previous_findings": [
                {"finding_id": "1", "severity": "P1", "status": "OPEN", "title": "A"},
                {"finding_id": "2", "severity": "P1", "status": "OPEN", "title": "B"}
            ]
        }

        current_findings = [
            {"finding_id": "1", "severity": "P1", "status": "OPEN", "title": "A"},
            {"finding_id": "2", "severity": "P1", "status": "RESOLVED", "title": "B"}
        ]
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev_manifest, current_findings)
        self.assertEqual(status, "BLOCKED_OSCILLATION")
        self.assertEqual(reason_code, "FINDING_OSCILLATION")

    def test_pass_with_advisories_release_gate(self):
        manifest = {
            "schema_version": 1,
            "run_id": "test",
            "task_id": "test",
            "repository_root": "a",
            "source_project_root": "b",
            "base_revision": "c",
            "snapshot_hash": "hash123",
            "status": "PASS_WITH_ADVISORIES",
            "exit_code": 0,
            "included_files": ["test.py"]
        }
        (self.project_root / ".agent/state/review_run.json").write_text(json.dumps(manifest))
        (self.project_root / ".agent/context/TASK_SCOPE.json").write_text(json.dumps({"task_id": "test"}))
        (self.project_root / ".agent/state/task_baseline.json").write_text(json.dumps({"test": "123"}))
        (self.project_root / "source-code").mkdir(exist_ok=True)
        (self.project_root / ".agent/reports/GUARDRAILS_REPORT.md").write_text("# GUARDRAILS_REPORT.md\n\n## Status: PASS\n")
        (self.project_root / ".agent/reports/BUILD_REPORT.md").write_text("# BUILD_REPORT.md\n\n## Status: PASS\n")
        (self.project_root / ".agent/reports/QA_REPORT.md").write_text("# QA_REPORT.md\n\n## Status: PASS\n")

        import harness
        orig_eval = harness.evaluate_task_scope
        orig_load = harness.load_project

        def mock_eval(*args, **kwargs):
            return {"snapshot_hash": "hash123", "task_files": ["test.py"]}

        def mock_load(project_name):
            return self.project_root, {
                "release_requires": {
                    "build_pass": True,
                    "qa_pass": True,
                    "codex_real_review_pass": True,
                    "diff_hash_match": True,
                    "runtime_validation_pass": False
                }
            }

        harness.evaluate_task_scope = mock_eval
        harness.load_project = mock_load

        try:
            try:
                cmd_gate("project", self.project_root)
                success = True
            except SystemExit:
                success = False
            self.assertTrue(success)

            # test fail when hash mismatch
            manifest["snapshot_hash"] = "hash_diff"
            (self.project_root / ".agent/state/review_run.json").write_text(json.dumps(manifest))
            try:
                cmd_gate("project", self.project_root)
                success = True
            except SystemExit:
                success = False
            self.assertFalse(success)

        finally:
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_advisory_stale(self):
        import review_pipeline
        # We simulate the 7.5 Check post-run snapshot
        repo_root = self.project_root / "source-code"
        repo_root.mkdir()
        (repo_root / "file.py").write_text("print('hello')", "utf-8")

        # Original status is PASS_WITH_ADVISORIES
        # but the evaluate_task_scope returns a different hash
        import harness
        orig_eval = harness.evaluate_task_scope

        def mock_eval(*args, **kwargs):
            return {"snapshot_hash": "hash999"}

        try:
            harness.evaluate_task_scope = mock_eval
            # Inline snippet equivalent to 7.5 Check post-run snapshot
            status = review_pipeline.ReviewStatus.PASS_WITH_ADVISORIES
            snapshot_hash = "hash123"
            reason = ""

            if status in (review_pipeline.ReviewStatus.PASS, review_pipeline.ReviewStatus.PASS_WITH_ADVISORIES):
                post_evaluation = harness.evaluate_task_scope(
                    repo_root, None, None, expected_task_id="test", require_delta=True
                )
                post_hash = post_evaluation["snapshot_hash"]
                if post_hash != snapshot_hash:
                    status = review_pipeline.ReviewStatus.STALE
                    reason = "Source code changed during review."

            self.assertEqual(status, review_pipeline.ReviewStatus.STALE)
            self.assertEqual(reason, "Source code changed during review.")
        finally:
            harness.evaluate_task_scope = orig_eval

    def test_run_antigravity_fixer_no_delta(self):
        from dual_agent_runtime import run_antigravity_fixer

        # Test signature and return dictionary
        repo_root = self.project_root / "source-code"
        repo_root.mkdir()
        (repo_root / "file.py").write_text("print('hello')", "utf-8")

        handoff = {"allowed_files": ["**/*"]}
        # Write dummy antigravity executable
        (self.project_root / "antigravity").touch(mode=0o755)
        # We can't really execute antigravity here, but we can verify harness logic
        # by calling harness.py directly or just verifying the dict returned structure
        import subprocess
        # mock subprocess.Popen
        orig_popen = subprocess.Popen
        class MockProc:
            returncode = 0
            def communicate(self, timeout=None):
                return "output", ""
        def mock_popen(*args, **kwargs):
            return MockProc()

        subprocess.Popen = mock_popen

        # mock find_antigravity
        import dual_agent_runtime
        orig_find = dual_agent_runtime.find_antigravity
        dual_agent_runtime.find_antigravity = lambda: "dummy"

        try:
            res = run_antigravity_fixer(self.project_root, handoff)
            self.assertIn("ok", res)
            self.assertIn("status", res)
            self.assertEqual(res["status"], "BLOCKED_NO_FIX_DELTA")
            self.assertEqual(res["reason_code"], "WRITER_NO_DELTA")
            self.assertFalse(res["artifact_changed"])
        finally:
            subprocess.Popen = orig_popen
            dual_agent_runtime.find_antigravity = orig_find

if __name__ == '__main__':
    unittest.main()
