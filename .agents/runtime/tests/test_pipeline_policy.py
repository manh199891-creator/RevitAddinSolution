import hashlib
import json
import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from pipeline_policy import (
    canonical_finding_id, immutable_scope_projection, protected_path,
    scope_projection_hash, validate_fix_result,
)


class PipelinePolicyTests(unittest.TestCase):
    def test_protected_dot_paths(self):
        for path in (".agents/runtime/harness.py", ".agent/state/PLAN_LOCK.json",
                     ".github/workflows/test.yml", ".gitignore", "AGENTS.md",
                     "./.agents/runtime/harness.py"):
            self.assertTrue(protected_path(path))
        self.assertFalse(protected_path("src/Test.cs"))

    def test_scope_projection_ignores_runtime_fields(self):
        base = {"task_id": "t", "allowed_files": ["src/A.cs"], "forbidden": [".agents/**"],
                "repository_root": "repo", "mode": "plan", "artifact_files": ["PLAN.md"]}
        changed = dict(base, mode="release", artifact_files=["QA_REPORT.md"])
        self.assertEqual(immutable_scope_projection(base), immutable_scope_projection(changed))
        self.assertEqual(scope_projection_hash(base), scope_projection_hash(changed))
        self.assertNotEqual(scope_projection_hash(base), scope_projection_hash(dict(base, allowed_files=["src/B.cs"])))
        self.assertNotEqual(scope_projection_hash(base), scope_projection_hash(dict(base, forbidden=["src/**"])))

    def test_finding_id_is_line_and_model_id_stable(self):
        first = {"finding_id": "model-a", "rule_id": "R1", "file": "src/A.cs", "symbol": "Run",
                 "acceptance_criterion": "AC-1", "line": 10, "problem": "Null reference."}
        second = dict(first, finding_id="model-b", line=30, problem="Null reference")
        self.assertEqual(canonical_finding_id(first), canonical_finding_id(second))
        self.assertNotEqual(canonical_finding_id(first), canonical_finding_id(dict(first, file="src/B.cs")))
        self.assertNotEqual(canonical_finding_id(first), canonical_finding_id(dict(first, rule_id="R2")))

    def test_fix_result_requires_complete_evidence(self):
        contract = {"task_id": "t", "review_run_id": "r", "plan_lock_sha256": "p",
                    "findings": [{"canonical_finding_id": "f"}]}
        result = {"schema_version": 1, "task_id": "t", "review_run_id": "r", "fix_round": 1,
                  "plan_lock_sha256": "p", "declared_changed_files": [],
                  "declared_tests": [], "completed_at": "now",
                  "finding_results": [{"canonical_finding_id": "f", "status": "FIXED"}]}
        self.assertEqual(validate_fix_result(result, contract), (True, "FIX_RESULT_GATE_A_PASS"))
        self.assertFalse(validate_fix_result(dict(result, fix_round=2), contract)[0])


if __name__ == "__main__":
    unittest.main()
