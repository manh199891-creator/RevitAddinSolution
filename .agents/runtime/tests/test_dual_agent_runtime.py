import json
import subprocess
import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import Mock, patch

RUNTIME_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RUNTIME_DIR))

import dual_agent_runtime as runtime


class DoctorInferenceTests(unittest.TestCase):
    def test_models_success_but_inference_timeout_is_not_ready(self):
        with TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "source-code").mkdir()
            with patch.object(runtime, "find_codex", return_value="codex"), \
                 patch.object(runtime, "find_antigravity", return_value="agy"), \
                 patch.object(runtime, "_probe", side_effect=[
                     (0, "codex 1"), (0, "models listed"),
                     (-1, "Probe timed out after 30s"),
                 ]):
                result = runtime.diagnose(root)
        check = next(item for item in result["checks"] if item["name"] == "antigravity")
        self.assertEqual("INFERENCE_TIMEOUT", check["status"])
        self.assertFalse(result["ready"])

    def test_inference_requires_exact_sentinel(self):
        with TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "source-code").mkdir()
            with patch.object(runtime, "find_codex", return_value="codex"), \
                 patch.object(runtime, "find_antigravity", return_value="agy"), \
                 patch.object(runtime, "_probe", side_effect=[
                     (0, "codex 1"), (0, "models listed"), (0, "helpful but wrong"),
                 ]):
                result = runtime.diagnose(root)
        check = next(item for item in result["checks"] if item["name"] == "antigravity")
        self.assertEqual("BROKEN_OUTPUT", check["status"])


class WriterTimeoutTests(unittest.TestCase):
    def test_timeout_terminates_tree_and_writes_terminal_report(self):
        with TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "source-code").mkdir()
            proc = Mock(pid=4321, returncode=None)
            proc.communicate.side_effect = subprocess.TimeoutExpired("agy", 1)
            with patch.object(runtime, "find_antigravity", return_value="agy"), \
                 patch.object(runtime.subprocess, "Popen", return_value=proc), \
                 patch.object(runtime, "_terminate_process_tree", return_value=("", "")) as terminate:
                ok, reason = runtime.run_antigravity_fixer(
                    root, {"handoff_id": "h1", "allowed_files": []}, timeout_seconds=1
                )
            report = json.loads((root / ".agent/reports/FIXER_COMMAND_REPORT.json").read_text(encoding="utf-8"))
        self.assertFalse(ok)
        self.assertIn("timeout", reason.lower())
        terminate.assert_called_once_with(proc)
        self.assertEqual("WRITER_TIMEOUT", report["reason_code"])
        self.assertTrue(report["completed_at"])


if __name__ == "__main__":
    unittest.main()
