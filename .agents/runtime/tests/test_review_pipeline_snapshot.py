import sys
import json
import subprocess
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import Mock, patch


RUNTIME_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RUNTIME_DIR))

from review_pipeline import (
    _snapshot_status_args,
    _snapshot_visible_mapping,
    _copy_review_workspace,
    _terminate_timed_out_process,
    get_artifact_snapshot,
)


class TimedOutProcessTests(unittest.TestCase):
    def test_cleanup_returns_when_process_pipes_never_close(self):
        proc = Mock(pid=1234)
        proc.communicate.side_effect = __import__("subprocess").TimeoutExpired(
            cmd="codex", timeout=1
        )

        with patch("review_pipeline.subprocess.run"):
            stdout, stderr = _terminate_timed_out_process(proc, grace_seconds=1)

        proc.kill.assert_called_once()
        proc.communicate.assert_called_once_with(timeout=1)
        self.assertEqual(("", ""), (stdout, stderr))


class ReviewWorkspaceCopyTests(unittest.TestCase):
    def test_copy_excludes_agent_factory_and_build_metadata(self):
        with TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            source = root / "source"
            target = root / "target"
            (source / "src").mkdir(parents=True)
            (source / ".agents" / "factory" / "Project").mkdir(parents=True)
            (source / ".git").mkdir()
            (source / "src" / "Feature.cs").write_text("source", encoding="utf-8")
            (source / ".agents" / "factory" / "Project" / "state.json").write_text(
                "internal", encoding="utf-8"
            )
            (source / ".git" / "HEAD").write_text("ref", encoding="utf-8")

            _copy_review_workspace(source, target)

            self.assertTrue((target / "src" / "Feature.cs").exists())
            self.assertFalse((target / ".agents").exists())
            self.assertFalse((target / ".git").exists())


class SnapshotStatusArgsTests(unittest.TestCase):
    def test_excludes_local_factory_when_scanning_all_changes(self):
        args = _snapshot_status_args()

        self.assertIn("--", args)
        self.assertIn(".", args)
        self.assertIn(":(exclude).agents/factory", args)
        self.assertIn(":(exclude).agents/factory/**", args)

    def test_keeps_explicit_files_and_still_excludes_local_factory(self):
        args = _snapshot_status_args(["src/B.cs", "src/A.cs", "src/A.cs"])

        separator = args.index("--")
        self.assertEqual(
            [
                "src/A.cs",
                "src/B.cs",
                ":(exclude).agents/factory",
                ":(exclude).agents/factory/**",
            ],
            args[separator + 1 :],
        )

    def test_legacy_baseline_ignores_local_factory_metadata(self):
        baseline_files = {
            ".agents/factory/RevitAddinSolution/.agent/state.json": "internal",
            "src/Feature.cs": "source",
        }

        self.assertEqual(
            {"src/Feature.cs": "source"},
            _snapshot_visible_mapping(baseline_files),
        )


class ArtifactSnapshotIsolationTests(unittest.TestCase):
    def test_pipeline_dirt_does_not_change_plan_snapshot(self):
        with TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            source = root / "source-code"
            context = root / ".agent/context"
            state = root / ".agent/state"
            source.mkdir(); context.mkdir(parents=True); state.mkdir(parents=True)
            subprocess.run(["git", "init"], cwd=source, check=True, capture_output=True)
            subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=source, check=True)
            subprocess.run(["git", "config", "user.name", "Test"], cwd=source, check=True)
            (source / "src").mkdir()
            (source / "src/Feature.cs").write_text("v1", encoding="utf-8")
            subprocess.run(["git", "add", "."], cwd=source, check=True)
            subprocess.run(["git", "commit", "-m", "base"], cwd=source, check=True, capture_output=True)
            (context / "PLAN.md").write_text("# Plan\n", encoding="utf-8")
            (context / "TASK_SCOPE.json").write_text(json.dumps({
                "allowed_files": ["src/Feature.cs"], "artifact_files": ["PLAN.md"]
            }), encoding="utf-8")
            first, _ = get_artifact_snapshot(root, "plan", ["PLAN.md"])
            (source / ".agents/runtime/__pycache__").mkdir(parents=True)
            (source / ".agents/runtime/__pycache__/x.pyc").write_bytes(b"noise")
            (source / "debug.log").write_text("noise", encoding="utf-8")
            second, _ = get_artifact_snapshot(root, "plan", ["PLAN.md"])
            (source / "src/Feature.cs").write_text("v2", encoding="utf-8")
            third, _ = get_artifact_snapshot(root, "plan", ["PLAN.md"])
            (context / "PLAN.md").write_text("# Plan changed\n", encoding="utf-8")
            fourth, _ = get_artifact_snapshot(root, "plan", ["PLAN.md"])
        self.assertEqual(first, second)
        self.assertNotEqual(second, third)
        self.assertNotEqual(third, fourth)


if __name__ == "__main__":
    unittest.main()
