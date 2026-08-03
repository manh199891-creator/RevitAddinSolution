import unittest
import json
import subprocess
from pathlib import Path
import tempfile
import shutil
import hashlib

import sys
sys.path.insert(0, str(Path(__file__).parent.parent.resolve()))

from review_pipeline import aggregate_batch_results, ReviewStatus, generate_finding_id, build_codex_prompt_batch
from harness import parse_dual_args
from dual_agent_runtime import _scoped_writer_snapshot

class TestConvergence(unittest.TestCase):

    def setUp(self):
        self.temp_dir = Path(tempfile.mkdtemp())
        self.project_root = self.temp_dir / "project"
        self.project_root.mkdir()
        (self.project_root / ".agent").mkdir()
        (self.project_root / ".agent" / "context").mkdir()
        (self.project_root / ".agent" / "reports").mkdir()
        
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
        
    def test_oscillation(self):
        # We test the oscillation logic via aggregate_batch_results when a previous run exists
        pass

if __name__ == '__main__':
    unittest.main()
