import json
import sys
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path
from tempfile import TemporaryDirectory

RUNTIME_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RUNTIME_DIR))

from review_pipeline import reconcile_stale_review


class StaleStatusContractTests(unittest.TestCase):
    def test_legacy_local_naive_heartbeat_is_reconciled(self):
        with TemporaryDirectory() as temp:
            root = Path(temp)
            for rel in (".agent/state", ".agent/context", ".agent/reports"):
                (root / rel).mkdir(parents=True, exist_ok=True)
            old_local = (datetime.now().astimezone() - timedelta(minutes=20)).replace(tzinfo=None).isoformat()
            (root / ".agent/state/review_run.json").write_text(json.dumps({
                "run_id": "legacy", "task_id": "t1", "review_mode": "plan",
                "snapshot_hash": "sha", "status": "RUNNING", "heartbeat_at": old_local,
            }), encoding="utf-8")

            result = reconcile_stale_review(root, stale_after_seconds=60)

            self.assertEqual("STALE", result["status"])

    def test_expired_running_review_is_terminalized_everywhere(self):
        with TemporaryDirectory() as temp:
            root = Path(temp)
            for rel in (".agent/state", ".agent/context", ".agent/reports"):
                (root / rel).mkdir(parents=True, exist_ok=True)
            old = (datetime.now(timezone.utc) - timedelta(minutes=20)).isoformat()
            (root / ".agent/state/review_run.json").write_text(json.dumps({
                "run_id": "r1", "task_id": "t1", "review_mode": "plan",
                "snapshot_hash": "sha", "status": "RUNNING", "heartbeat_at": old,
            }), encoding="utf-8")
            (root / ".agent/context/TASK_CONTEXT.json").write_text(json.dumps({
                "task_id": "t1", "status": "blocked_handoff", "events": []
            }), encoding="utf-8")
            (root / ".agent/state/workflow_state.json").write_text(json.dumps({
                "task_id": "t1", "dual_status": "blocked"
            }), encoding="utf-8")
            (root / ".agent/reports/DUAL_AGENT_REPORT.md").write_text(
                "## Status: BLOCKED\n- task_id: t1\n", encoding="utf-8"
            )
            result = reconcile_stale_review(root, stale_after_seconds=60)
            manifest = json.loads((root / ".agent/state/review_run.json").read_text(encoding="utf-8"))
            context = json.loads((root / ".agent/context/TASK_CONTEXT.json").read_text(encoding="utf-8"))
            workflow = json.loads((root / ".agent/state/workflow_state.json").read_text(encoding="utf-8"))
            authority = json.loads((root / ".agent/state/pipeline_status.json").read_text(encoding="utf-8"))
            report = (root / ".agent/reports/DUAL_AGENT_REPORT.md").read_text(encoding="utf-8")
        self.assertEqual("STALE", result["status"])
        self.assertEqual("STALE", manifest["status"])
        self.assertEqual("stale", context["status"])
        self.assertEqual("stale", workflow["dual_status"])
        self.assertEqual("STALE", authority["status"])
        self.assertIn("## Status: STALE", report)
        self.assertTrue(manifest["completed_at"])
        self.assertTrue(manifest["reason"])


if __name__ == "__main__":
    unittest.main()
