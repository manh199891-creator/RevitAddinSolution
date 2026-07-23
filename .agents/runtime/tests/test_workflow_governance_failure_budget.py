import json
import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

RUNTIME_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RUNTIME_DIR))

from workflow_governance import record_failed_attempt


class FailureBudgetIdentityTests(unittest.TestCase):
    def test_duplicate_snapshot_hypothesis_does_not_consume_budget(self):
        with TemporaryDirectory() as temp:
            root = Path(temp)
            (root / ".agent/context").mkdir(parents=True)
            (root / ".agent/context/TASK_CONTEXT.json").write_text(json.dumps({
                "task_id": "t", "failure_budget": {
                    "limit": 3, "used": 0, "remaining": 3, "failed_attempts": []
                }
            }), encoding="utf-8")
            first, _ = record_failed_attempt(root, stage="plan", hypothesis="same", evidence="same", snapshot_hash="s1")
            duplicate, _ = record_failed_attempt(root, stage="plan", hypothesis="same", evidence="same", snapshot_hash="s1")
            changed, _ = record_failed_attempt(root, stage="plan", hypothesis="same", evidence="same", snapshot_hash="s2")
        self.assertEqual(1, first["failure_budget"]["used"])
        self.assertEqual("DUPLICATE_ATTEMPT", duplicate["last_attempt_result"])
        self.assertEqual(1, duplicate["failure_budget"]["used"])
        self.assertEqual(2, changed["failure_budget"]["used"])


if __name__ == "__main__":
    unittest.main()
