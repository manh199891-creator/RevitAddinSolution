import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from collab.ownership_validator import validate_ownership, validate_work_split


def ownership(codex=None, anti=None, **extra):
    value = {"owners": {"codex": {"owned_files": codex or ["src/Codex.cs"]},
                         "antigravity": {"owned_files": anti or ["src/Anti.cs"]}}}
    value.update(extra)
    return value


class OwnershipTests(unittest.TestCase):
    def assert_invalid(self, value, fragment):
        valid, errors = validate_ownership(value)
        self.assertFalse(valid)
        self.assertTrue(any(fragment in error for error in errors), errors)

    def test_exact_ownership_overlap(self):
        self.assert_invalid(ownership(["src/shared.cs"], ["src/shared.cs"]), "ownership overlap")

    def test_parent_child_ownership_overlap(self):
        self.assert_invalid(ownership(["src/lib"], ["src/lib/file.cs"]), "ownership overlap")

    def test_windows_case_insensitive_overlap(self):
        self.assert_invalid(ownership(["src/Foo.cs"], ["src/foo.cs"]), "ownership overlap")

    def test_protected_path_assignment(self):
        self.assert_invalid(ownership([".agents/runtime/collab_state.py"], ["src/Anti.cs"]), "protected path")

    def test_absolute_path_rejection(self):
        self.assert_invalid(ownership(["C:/repo/file.cs"], ["src/Anti.cs"]), "invalid repository-relative")

    def test_broad_scope_rejection(self):
        self.assert_invalid(ownership(["**/*"], ["src/Anti.cs"]), "broad repository-wide")

    def test_codex_only_integration_file_assigned_to_anti(self):
        value = ownership(["src/Codex.cs"], ["src/integration.cs"], codex_only_integration_files=["src/integration.cs"])
        self.assert_invalid(value, "Codex-only")

    def test_valid_disjoint_split(self):
        valid, errors = validate_work_split({"ownership": ownership()})
        self.assertTrue(valid, errors)


if __name__ == "__main__":
    unittest.main()
