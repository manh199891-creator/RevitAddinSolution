import unittest
import json
import subprocess
from pathlib import Path
import tempfile
import shutil
import hashlib
from unittest.mock import Mock, patch
import sys

sys.path.insert(0, str(Path(__file__).parent.parent.resolve()))

from review_pipeline import (
    aggregate_batch_results, ReviewStatus, generate_finding_id,
    evaluate_focused_retry_progress, run_codex_review, is_review_success
)
import review_pipeline
from harness import cmd_gate, cmd_dual
import harness
from dual_agent_runtime import run_antigravity_fixer
import dual_agent_runtime
from pipeline_policy import scope_projection_hash

class TestConvergenceFull(unittest.TestCase):
    def setUp(self):
        self.temp_dir = Path(tempfile.mkdtemp())
        self.project_root = self.temp_dir / "project"
        self.project_root.mkdir()
        (self.project_root / ".agent" / "context").mkdir(parents=True)
        (self.project_root / ".agent" / "reports").mkdir(parents=True)
        (self.project_root / ".agent" / "state").mkdir(parents=True)
        (self.project_root / "source-code").mkdir(parents=True)

        import harness
        self.orig_validate_scope = review_pipeline.validate_scope
        self.orig_harness_eval = harness.evaluate_task_scope
        review_pipeline.validate_scope = lambda *a, **kw: (True, [])
        mock_eval = lambda *a, **kw: {
            "status": "PASS",
            "snapshot_hash": "hash123",
            "task_files": ["f.py"],
            "excluded_preexisting_files": [],
            "baseline_removed_files": [],
            "issues": [],
            "baseline_present": True,
        }
        harness.evaluate_task_scope = mock_eval
        self.orig_harness_load = harness.load_project
        harness.load_project = lambda n: (n, {"dual_agents": {"fixer_provider": "antigravity", "auto_fix": True}})
        self.orig_rp_eval = review_pipeline.evaluate_task_scope
        review_pipeline.evaluate_task_scope = mock_eval
        self.orig_get_det = review_pipeline.get_deterministic_snapshot
        review_pipeline.get_deterministic_snapshot = lambda repo, included=None: ("hash123", ["f.py"])
        self.orig_dirty_fp = review_pipeline._dirty_file_fingerprint
        review_pipeline._dirty_file_fingerprint = lambda r, p: "dirtyhash"
        self.orig_run_git = review_pipeline._run_git
        review_pipeline._run_git = lambda r, a: b""
        (self.project_root / ".agent/context/TASK_SCOPE.json").write_text(json.dumps({"task_id": "test", "included_files": ["f.py"]}))
        # Synthetic pipeline fixtures now opt into the production plan-lock
        # contract explicitly; real tasks get this lock from plan PASS.
        for name in ("PLAN.md", "TECHNICAL_DESIGN.md", "ACCEPTANCE_CRITERIA.md"):
            (self.project_root / ".agent/context" / name).write_text(name, encoding="utf-8")
        scope = {"task_id": "test", "included_files": ["f.py"]}
        lock = {
            "schema_version": 1, "task_id": "test", "approved_plan_run_id": "fixture",
            "approved_plan_snapshot_hash": "", "plan_sha256": hashlib.sha256(b"PLAN.md").hexdigest(),
            "technical_design_sha256": hashlib.sha256(b"TECHNICAL_DESIGN.md").hexdigest(),
            "acceptance_criteria_sha256": hashlib.sha256(b"ACCEPTANCE_CRITERIA.md").hexdigest(),
            "task_scope_sha256": scope_projection_hash(scope),
            "baseline_commit": "fixture", "approved_at": "fixture",
        }
        (self.project_root / ".agent/state/PLAN_LOCK.json").write_text(json.dumps(lock), encoding="utf-8")
        import yaml
        (self.project_root / "project.yaml").write_text(yaml.dump({"dual_agents": {"fixer_provider": "antigravity", "auto_fix": True}}))

    def tearDown(self):

        import harness
        review_pipeline.validate_scope = self.orig_validate_scope
        harness.evaluate_task_scope = self.orig_harness_eval
        harness.load_project = self.orig_harness_load
        review_pipeline.evaluate_task_scope = self.orig_rp_eval
        review_pipeline.get_deterministic_snapshot = self.orig_get_det
        review_pipeline._dirty_file_fingerprint = self.orig_dirty_fp
        review_pipeline._run_git = self.orig_run_git
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    # =========================================================================
    # CONVERGENCE TESTS
    # =========================================================================


    def test_full_fail_to_focused_retry_empty(self):
        prev = {
            "review_mode": "FULL",
            "status": "FAIL",
            "findings": [
                {"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"}
            ],
            "previous_findings": []
        }
        curr = []
        from review_pipeline import evaluate_focused_retry_progress
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev, curr)
        self.assertEqual(len(curr), 1)
        self.assertEqual(curr[0]["status"], "STILL_OPEN")
        self.assertEqual(status, "FAIL")
        self.assertEqual(reason_code, "FOCUSED_RETRY_CONTRACT_INCOMPLETE")

    def test_multi_retry_history(self):
        prev = {
            "review_mode": "FOCUSED_RETRY",
            "progress": False,
            "status": "FAIL",
            "findings": [
                {"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"}
            ]
        }
        curr = [{"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"}]
        from review_pipeline import evaluate_focused_retry_progress
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev, curr)
        self.assertEqual(status, "BLOCKED_NO_PROGRESS")

    def test_missing_previous_finding_is_still_open(self):
        prev = {
            "review_mode": "FOCUSED_RETRY",
            "findings": [
                {"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"}
            ]
        }
        curr = []
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev, curr)
        self.assertEqual(len(curr), 1)
        self.assertEqual(curr[0]["status"], "STILL_OPEN")
        self.assertEqual(reason_code, "FOCUSED_RETRY_CONTRACT_INCOMPLETE")

    def test_missing_previous_finding_returns_fail(self):
        prev = {
            "review_mode": "FOCUSED_RETRY",
            "findings": [
                {"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"}
            ]
        }
        curr = []
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev, curr)
        self.assertEqual(status, "FAIL")
        
    def test_first_no_progress_remains_fail(self):
        prev = {
            "review_mode": "FOCUSED_RETRY",
            "progress": True, # First retry shows no progress
            "previous_blocking_ids": ["F1"],
            "current_blocking_ids": ["F1"],
            "findings": [{"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"}]
        }
        curr = [{"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"}]
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev, curr)
        self.assertEqual(status, "FAIL")
        self.assertFalse(data["progress"])
        
    def test_second_no_progress_returns_blocked(self):
        prev = {
            "review_mode": "FOCUSED_RETRY",
            "progress": False, # Previous run had no progress
            "previous_blocking_ids": ["F1"],
            "current_blocking_ids": ["F1"],
            "findings": [{"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"}]
        }
        curr = [{"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"}]
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev, curr)
        self.assertEqual(status, "BLOCKED_NO_PROGRESS")
        self.assertEqual(reason_code, "BLOCKING_FINDINGS_UNCHANGED")
        
    def test_finding_oscillation_returns_blocked(self):
        prev = {
            "review_mode": "FOCUSED_RETRY",
            "previous_blocking_ids": ["F1"],
            "current_blocking_ids": ["F1", "F2"],
            "findings": [
                {"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"},
                {"finding_id": "F2", "severity": "P1", "status": "OPEN", "title": "B"}
            ]
        }
        curr = [
            {"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"},
            {"finding_id": "F2", "severity": "P1", "status": "RESOLVED", "title": "B"} # Back to just F1!
        ]
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev, curr)
        self.assertEqual(status, "BLOCKED_OSCILLATION")
        self.assertEqual(reason_code, "FINDING_OSCILLATION")
        
    def test_resolved_finding_counts_as_progress(self):
        prev = {
            "review_mode": "FOCUSED_RETRY",
            "progress": False,
            "previous_blocking_ids": ["F1", "F2"],
            "current_blocking_ids": ["F1", "F2"],
            "findings": [
                {"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"},
                {"finding_id": "F2", "severity": "P1", "status": "OPEN", "title": "B"}
            ]
        }
        curr = [
            {"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"},
            {"finding_id": "F2", "severity": "P1", "status": "RESOLVED", "title": "B"}
        ]
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev, curr)
        self.assertEqual(status, "FAIL") # Not blocked!
        self.assertTrue(data["progress"])

    def test_blocking_count_reduction_counts_as_progress(self):
        prev = {
            "review_mode": "FOCUSED_RETRY",
            "progress": False,
            "previous_blocking_ids": ["F1", "F2"],
            "current_blocking_ids": ["F1", "F2"],
            "findings": [
                {"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"},
                {"finding_id": "F2", "severity": "P1", "status": "OPEN", "title": "B"}
            ]
        }
        curr = [
            {"finding_id": "F1", "severity": "P1", "status": "OPEN", "title": "A"},
            {"finding_id": "F2", "severity": "P1", "status": "RESOLVED", "title": "B"}
        ]
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev, curr)
        self.assertTrue(data["progress"])
        
    def test_p3_does_not_affect_blocking_progress(self):
        prev = {
            "review_mode": "FOCUSED_RETRY",
            "findings": [{"finding_id": "F1", "severity": "P3", "status": "OPEN", "title": "A"}]
        }
        curr = [{"finding_id": "F1", "severity": "P3", "status": "RESOLVED", "title": "A"}]
        status, reason, reason_code, data = evaluate_focused_retry_progress(prev, curr)
        # It's an advisory, resolving it doesn't mean blocking progress is True, but there are 0 blocking
        self.assertEqual(status, ReviewStatus.PASS)
        self.assertEqual(data["blocking_count_before"], 0)
        self.assertEqual(data["blocking_count_after"], 0)

    def _write_mock_manifest(self, status):
        (self.project_root / ".agent/state/review_run.json").write_text(json.dumps({
            "status": status,
            "run_id": "review-fixture",
            "findings": [{"finding_id": "F1", "severity": "P1", "status": "OPEN",
                          "file": "f.py", "problem": "fixture issue"}] if status == "FAIL" else [],
        }))
        return {"status": status}

    # =========================================================================
    # SNAPSHOT TESTS
    # =========================================================================

    def _mock_run_codex_review_core(self, manifest_status, snapshot_hash="hash123", curr_hash="hash123", codex_executable="codex.exe"):
        (self.project_root / ".agent/context/TASK_SCOPE.json").write_text(json.dumps({"task_id": "test", "included_files": ["f.py"]}))
        import yaml
        (self.project_root / "project.yaml").write_text(yaml.dump({"dual_agents": {"fixer_provider": "antigravity", "auto_fix": True}}))
        import yaml
        (self.project_root / "project.yaml").write_text(yaml.dump({"dual_agents": {"fixer_provider": "antigravity", "auto_fix": True}}))
        orig_eval = review_pipeline.evaluate_task_scope
        orig_cap = review_pipeline.capture_task_baseline
        eval_count = 0
        def stateful_eval(*a, **kw):
            nonlocal eval_count
            h = snapshot_hash if eval_count == 0 else curr_hash
            eval_count += 1
            return {"status": "PASS", "snapshot_hash": h, "task_files": ["f.py"], "excluded_preexisting_files": []}
        review_pipeline.evaluate_task_scope = stateful_eval
        review_pipeline.capture_task_baseline = lambda *a, **kw: None
        (self.project_root / ".agent/state/review_run.json").write_text(json.dumps({
            "schema_version": 1,
            "run_id": "test",
            "task_id": "test",
            "repository_root": "a",
            "source_project_root": "b",
            "base_revision": "c",
            "snapshot_hash": snapshot_hash,
            "status": "RUNNING",
            "exit_code": None,
            "included_files": ["f.py"]
        }))
        
        orig_eval = harness.evaluate_task_scope
        orig_batch = review_pipeline.aggregate_batch_results
        
        def mock_eval(*args, **kwargs):
            return {"snapshot_hash": curr_hash}
            
        def mock_batch(*args, **kwargs):
            return manifest_status, [{"status": "MOCK"}]
            
        review_pipeline.aggregate_batch_results = mock_batch
        orig_gate = harness.cmd_gate
        harness.cmd_gate = lambda *args, **kwargs: 0
        try:
            run_codex_review(self.project_root, "test", "test_feature", codex_executable)
        finally:
            harness.cmd_gate = orig_gate
            harness.evaluate_task_scope = orig_eval
            review_pipeline.aggregate_batch_results = orig_batch
            
        return json.loads((self.project_root / ".agent/state/review_run.json").read_text())
        
    def test_pass_stable_snapshot_succeeds(self):
        res = self._mock_run_codex_review_core(ReviewStatus.PASS, "hash1", "hash1", "codex.exe")
        self.assertEqual(res["status"], ReviewStatus.PASS)

    def test_pass_stale_snapshot_returns_stale(self):
        res = self._mock_run_codex_review_core(ReviewStatus.PASS, "hash1", "hash2", "codex.exe")
        self.assertEqual(res["status"], ReviewStatus.STALE)
        self.assertFalse((self.project_root / ".agent/state/reviewed_diff_hash.txt").exists())

    def test_advisory_stable_snapshot_succeeds(self):
        res = self._mock_run_codex_review_core(ReviewStatus.PASS_WITH_ADVISORIES, "hash1", "hash1", "codex.exe")
        self.assertEqual(res["status"], ReviewStatus.PASS_WITH_ADVISORIES)

    def test_advisory_stale_snapshot_returns_stale(self):
        res = self._mock_run_codex_review_core(ReviewStatus.PASS_WITH_ADVISORIES, "hash1", "hash2", "codex.exe")
        self.assertEqual(res["status"], ReviewStatus.STALE)
        self.assertFalse((self.project_root / ".agent/state/reviewed_diff_hash.txt").exists())

    def test_stale_advisory_does_not_write_reviewed_hash(self):
        res = self._mock_run_codex_review_core(ReviewStatus.PASS_WITH_ADVISORIES, "hash1", "hash2", "codex.exe")
        self.assertFalse((self.project_root / ".agent/state/reviewed_diff_hash.txt").exists())

    def test_stable_advisory_writes_reviewed_hash(self):
        res = self._mock_run_codex_review_core(ReviewStatus.PASS_WITH_ADVISORIES, "hash1", "hash1", "codex.exe")
        self.assertTrue((self.project_root / ".agent/state/reviewed_diff_hash.txt").exists())

    # =========================================================================
    # FIXER TESTS
    # =========================================================================
    
    def _mock_fixer_exec(self, repo_files, new_repo_files, report_data):
        repo = self.project_root / "source-code"
        if repo.exists():
            shutil.rmtree(repo)
        repo.mkdir()
        for k, v in repo_files.items():
            (repo / k).write_text(v)
            
        orig_popen = subprocess.Popen
        orig_find = dual_agent_runtime.find_antigravity
        
        class MockProc:
            returncode = 0
            def communicate(self, timeout=None):
                for k, v in new_repo_files.items():
                    target = repo / k
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_text(v)
                rep_json = repo.parent / ".agent/writer-outbox/AGY_FIX_RESULT.json"
                rep_json.parent.mkdir(parents=True, exist_ok=True)
                rep_json.write_text(json.dumps(report_data))
                return "output", ""
                
        def mock_popen(*args, **kwargs):
            return MockProc()
            
        subprocess.Popen = mock_popen
        dual_agent_runtime.find_antigravity = lambda: "dummy"
        
        try:
            return run_antigravity_fixer(self.project_root, {"allowed_files": ["**/*"]})
        finally:
            subprocess.Popen = orig_popen
            dual_agent_runtime.find_antigravity = orig_find

    def test_source_delta_returns_pass(self):
        res = self._mock_fixer_exec({"a.py": "1"}, {"a.py": "2"}, {"status": "SUCCESS", "reason_code": "OK"})
        self.assertTrue(res["ok"])
        self.assertTrue(res["artifact_changed"])
        
    def test_no_delta_returns_structured_blocked_result(self):
        res = self._mock_fixer_exec({"a.py": "1"}, {"a.py": "1"}, {"status": "SUCCESS", "reason_code": "OK"})
        self.assertFalse(res["ok"])
        self.assertEqual(res["status"], "BLOCKED_NO_FIX_DELTA")
        
    def test_report_only_delta_is_no_fix_delta(self):
        # Even if report changed, source code didn't
        res = self._mock_fixer_exec({"a.py": "1"}, {"a.py": "1"}, {"status": "SUCCESS", "reason_code": "OK"})
        self.assertEqual(res["status"], "BLOCKED_NO_FIX_DELTA")
        
    def test_generated_file_only_delta_is_no_fix_delta(self):
        res = self._mock_fixer_exec({"a.py": "1"}, {"a.py": "1", ".agent/reports/something.txt": "2"}, {"status": "SUCCESS", "reason_code": "OK"})
        self.assertEqual(res["status"], "BLOCKED_SELF_MODIFICATION")

    @patch('harness.run_codex_review')
    @patch('harness.run_antigravity_fixer')
    def test_no_fix_delta_reaches_harness(self, mock_fixer, mock_codex):
        mock_codex.side_effect = lambda *a, **kw: self._write_mock_manifest("FAIL")
        mock_fixer.return_value = {"ok": False, "status": "BLOCKED_NO_FIX_DELTA", "reason": "No delta", "reason_code": "WRITER_NO_DELTA", "artifact_changed": False}
        with self.assertRaises(SystemExit) as ctx:
            cmd_dual("project", self.project_root, "--task-id", "test", "--max-cycles", "2")
        self.assertEqual(ctx.exception.code, 1)

    @patch('harness.run_codex_review')
    @patch('harness.run_antigravity_fixer')
    def test_no_fix_delta_stops_next_review(self, mock_fixer, mock_codex):
        mock_codex.side_effect = lambda *a, **kw: self._write_mock_manifest("FAIL")
        mock_fixer.return_value = {"ok": False, "status": "BLOCKED_NO_FIX_DELTA", "reason": "No delta", "reason_code": "WRITER_NO_DELTA", "artifact_changed": False}
        try:
            cmd_dual("project", self.project_root, "--task-id", "test", "--max-cycles", "2")
        except SystemExit:
            pass
        self.assertEqual(mock_codex.call_count, 1) # Only first review

    @patch('harness.run_codex_review')
    @patch('harness.run_antigravity_fixer')
    def test_no_progress_stops_fixer(self, mock_fixer, mock_codex):
        mock_codex.side_effect = lambda *a, **kw: self._write_mock_manifest("BLOCKED_NO_PROGRESS")
        try:
            cmd_dual("project", self.project_root, "--task-id", "test", "--max-cycles", "2")
        except SystemExit:
            pass
        self.assertEqual(mock_codex.call_count, 1)
        mock_fixer.assert_not_called()

    @patch('harness.run_codex_review')
    @patch('harness.run_antigravity_fixer')
    def test_oscillation_stops_fixer(self, mock_fixer, mock_codex):
        mock_codex.side_effect = lambda *a, **kw: self._write_mock_manifest("BLOCKED_OSCILLATION")
        try:
            cmd_dual("project", self.project_root, "--task-id", "test", "--max-cycles", "2")
        except SystemExit:
            pass
        mock_fixer.assert_not_called()

    @patch('harness.run_codex_review')
    @patch('harness.run_antigravity_fixer')
    def test_successful_fixer_allows_next_step(self, mock_fixer, mock_codex):
        mock_codex_returns = ["FAIL", "PASS"]
        def mock_codex_call(*a, **kw):
            return self._write_mock_manifest(mock_codex_returns.pop(0))
        mock_codex.side_effect = mock_codex_call
        def successful_fix(*args, **kwargs):
            contract = json.loads((self.project_root / ".agent/state/FIX_CONTRACT.json").read_text())
            (self.project_root / ".agent/writer-outbox").mkdir(parents=True, exist_ok=True)
            (self.project_root / ".agent/writer-outbox/AGY_FIX_RESULT.json").write_text(json.dumps({
                "task_id": "test", "review_run_id": "review-fixture", "fix_round": 1,
                "previous_snapshot": "before", "result_snapshot": "after",
                "plan_lock_sha256": contract["plan_lock_sha256"],
                "findings": [{"canonical_finding_id": contract["findings"][0]["canonical_finding_id"],
                              "status": "FIXED", "evidence": "fixture", "tests": ["fixture"]}],
            }))
            (self.project_root / ".agent/state/EVIDENCE_MANIFEST.json").write_text("fixture")
            return {"ok": True, "status": "PASS", "reason": "", "reason_code": "", "artifact_changed": True,
                    "snapshot_before": "before", "snapshot_after": "after", "protected_changed": False,
                    "changed_files": []}
        mock_fixer.side_effect = successful_fix
        try:
            cmd_dual("project", self.project_root, "--task-id", "test", "--max-cycles", "2")
        except SystemExit:
            pass
        self.assertEqual(mock_codex.call_count, 2)
        self.assertEqual(mock_fixer.call_count, 1)

    # =========================================================================
    # RELEASE GATE TESTS
    # =========================================================================

    def _setup_gate(self, **manifest_kwargs):
        manifest = {
            "schema_version": 1,
            "run_id": "test",
            "task_id": "test",
            "repository_root": "a",
            "source_project_root": "b",
            "base_revision": "c",
            "snapshot_hash": "hash123",
            "status": "PASS",
            "exit_code": 0,
            "included_files": ["test.py"]
        }
        manifest.update(manifest_kwargs)
        (self.project_root / ".agent/state/review_run.json").write_text(json.dumps(manifest))
        (self.project_root / ".agent/context/TASK_SCOPE.json").write_text(json.dumps({"task_id": "test"}))
        (self.project_root / ".agent/state/task_baseline.json").write_text(json.dumps({"test": "123"}))
        (self.project_root / "source-code").mkdir(exist_ok=True, parents=True)
        (self.project_root / ".agent/reports/GUARDRAILS_REPORT.md").write_text("# GUARDRAILS_REPORT.md\n\n## Status: PASS\n")
        (self.project_root / ".agent/reports/BUILD_REPORT.md").write_text("# BUILD_REPORT.md\n\n## Status: PASS\n")
        (self.project_root / ".agent/reports/QA_REPORT.md").write_text("# QA_REPORT.md\n\n## Status: PASS\n")
        
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
                },
                "evidence_policy": {
                    "required_for_gate": False
                }
            }

        harness.evaluate_task_scope = mock_eval
        harness.load_project = mock_load
        
        return orig_eval, orig_load
        
    def _run_gate(self):
        try:
            cmd_gate("project", self.project_root)
        except SystemExit as e:
            return e.code
        return 0

    def test_strict_release_gate_accepts_pass(self):
        orig_eval, orig_load = self._setup_gate(status="PASS")
        try:
            self.assertEqual(self._run_gate(), 0)
        finally:
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_accepts_advisories(self):
        orig_eval, orig_load = self._setup_gate(status="PASS_WITH_ADVISORIES")
        try:
            self.assertEqual(self._run_gate(), 0)
        finally:
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_rejects_hash_mismatch(self):
        orig_eval, orig_load = self._setup_gate(snapshot_hash="hash999")
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_rejects_files_mismatch(self):
        orig_eval, orig_load = self._setup_gate(included_files=["other.py"])
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_rejects_batch_fail(self):
        # test batch fail logic, but we mocked aggregate_batch_results in other tests
        # We can just change status to FAIL
        orig_eval, orig_load = self._setup_gate(status="FAIL")
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load


    def test_missing_cli_structured_result(self):
        from dual_agent_runtime import run_antigravity_fixer
        import dual_agent_runtime
        orig_find = dual_agent_runtime.find_antigravity
        dual_agent_runtime.find_antigravity = lambda: None
        try:
            res = run_antigravity_fixer(self.project_root, {})
            self.assertIsInstance(res, dict)
            self.assertFalse(res["ok"])
            self.assertEqual(res["reason_code"], "ANTIGRAVITY_CLI_MISSING")
        finally:
            dual_agent_runtime.find_antigravity = orig_find

    def test_strict_release_gate_rejects_task_id_mismatch(self):
        orig_eval, orig_load = self._setup_gate(task_id="wrong_id")
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            import harness
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_rejects_schema_invalid(self):
        orig_eval, orig_load = self._setup_gate(schema_version=999)
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            import harness
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_rejects_exit_code_nonzero(self):
        orig_eval, orig_load = self._setup_gate(exit_code=1)
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            import harness
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_rejects_evidence_truncated(self):
        orig_eval, orig_load = self._setup_gate(evidence_truncated=True)
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            import harness
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_rejects_guardrails_fail(self):
        orig_eval, orig_load = self._setup_gate()
        (self.project_root / ".agent/reports/GUARDRAILS_REPORT.md").write_text("# Status: FAIL")
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            import harness
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_rejects_child_batch_fail(self):
        orig_eval, orig_load = self._setup_gate(
            status="PASS",
            batches=[{"batch_id": "b1", "status": "FAIL"}]
        )
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            import harness
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_rejects_build_fail(self):
        orig_eval, orig_load = self._setup_gate()
        (self.project_root / ".agent/reports/BUILD_REPORT.md").write_text("# Status: FAIL")
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_rejects_qa_fail(self):
        orig_eval, orig_load = self._setup_gate()
        (self.project_root / ".agent/reports/QA_REPORT.md").write_text("# Status: FAIL")
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

    def test_strict_release_gate_rejects_stale(self):
        orig_eval, orig_load = self._setup_gate(status="STALE")
        try:
            self.assertEqual(self._run_gate(), 1)
        finally:
            harness.evaluate_task_scope = orig_eval
            harness.load_project = orig_load

if __name__ == '__main__':
    unittest.main()
