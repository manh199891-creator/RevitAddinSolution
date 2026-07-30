import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch


RUNTIME_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RUNTIME_DIR))

from harness import (
    _record_blocked_guardrails,
    _record_blocked_verify,
    initialize_dual_run_state,
)


class DualInitStateTests(unittest.TestCase):
    def test_new_task_replaces_stale_terminal_report_and_reason(self):
        with TemporaryDirectory() as temp_dir:
            project_root = Path(temp_dir)
            state_dir = project_root / ".agent" / "state"
            reports_dir = project_root / ".agent" / "reports"
            state_dir.mkdir(parents=True)
            reports_dir.mkdir(parents=True)
            (state_dir / "workflow_state.json").write_text(
                '{"task_id":"old_task","dual_status":"blocked_baseline",'
                '"dual_reason":"Guardrails failed: BLOCKED_BASELINE"}',
                encoding="utf-8",
            )
            (reports_dir / "DUAL_AGENT_REPORT.md").write_text(
                "## Status: BLOCKED_BASELINE\n- task_id: old_task\n",
                encoding="utf-8",
            )

            initialize_dual_run_state(
                project_root,
                task_id="new_task",
                feature="New feature",
                mode="plan",
                next_step="plan_artifacts",
            )

            state = __import__("json").loads(
                (state_dir / "workflow_state.json").read_text(encoding="utf-8")
            )
            report = (reports_dir / "DUAL_AGENT_REPORT.md").read_text(
                encoding="utf-8"
            )
            self.assertEqual("new_task", state["task_id"])
            self.assertEqual("initialized", state["dual_status"])
            self.assertNotIn("dual_reason", state)
            self.assertIn("## Status: NOT_STARTED", report)
            self.assertIn("- task_id: new_task", report)
            self.assertNotIn("old_task", report)


class BlockedVerifyStateTests(unittest.TestCase):
    def test_blocked_verify_is_terminal_for_all_state_readers(self):
        with TemporaryDirectory() as temp_dir, patch(
            "harness.write_dual_report"
        ) as write_report, patch(
            "harness.update_task_context"
        ) as update_context, patch(
            "harness.save_dual_terminal_state"
        ) as save_terminal:
            project_root = Path(temp_dir)
            steps = [{"name": "verify", "status": "FAIL"}]

            _record_blocked_verify(
                project_root,
                "task_1",
                "Feature",
                "code",
                steps,
                "Required verification failed",
            )

            write_report.assert_called_once_with(
                project_root,
                "task_1",
                "Feature",
                "BLOCKED_VERIFY",
                steps,
                "Required verification failed",
                "code",
            )
            update_context.assert_called_once()
            save_terminal.assert_called_once_with(
                project_root,
                "task_1",
                "code",
                "BLOCKED_VERIFY",
                "verify",
                "Required verification failed",
            )

    def test_blocked_verify_saves_workflow_even_if_context_update_fails(self):
        with TemporaryDirectory() as temp_dir, patch(
            "harness.write_dual_report"
        ), patch(
            "harness.update_task_context",
            side_effect=RuntimeError("context unavailable"),
        ), patch(
            "harness.save_dual_terminal_state"
        ) as save_terminal:
            with self.assertRaisesRegex(RuntimeError, "context unavailable"):
                _record_blocked_verify(
                    Path(temp_dir), "task_1", "Feature", "code", [],
                    "Required verification failed",
                )

            save_terminal.assert_called_once()

    def test_guardrail_failure_persists_its_specific_terminal_status(self):
        with TemporaryDirectory() as temp_dir, patch(
            "harness.write_dual_report"
        ) as write_report, patch(
            "harness.update_task_context"
        ) as update_context, patch(
            "harness.save_dual_terminal_state"
        ) as save_terminal:
            project_root = Path(temp_dir)
            steps = [{"name": "guardrails", "status": "BLOCKED_BASELINE"}]

            _record_blocked_guardrails(
                project_root,
                "task_1",
                "Feature",
                "code",
                steps,
                "BLOCKED_BASELINE",
            )

            write_report.assert_called_once_with(
                project_root, "task_1", "Feature", "BLOCKED_BASELINE",
                steps, "Guardrails failed: BLOCKED_BASELINE", "code",
            )
            update_context.assert_called_once()
            save_terminal.assert_called_once_with(
                project_root, "task_1", "code", "BLOCKED_BASELINE",
                "guardrails", "Guardrails failed: BLOCKED_BASELINE",
            )


if __name__ == "__main__":
    unittest.main()
