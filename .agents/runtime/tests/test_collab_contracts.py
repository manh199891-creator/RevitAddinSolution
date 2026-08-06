import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from collab.contract_validator import validate_plan_package


BASE = "60b004f1b471ee3bb85f2eae7848972182879a5f"


def plan(**changes):
    value = {"schema_version": 1, "plan_id": "tag-arranger", "base_commit": BASE,
             "acceptance_criteria": [{"id": "AC-1", "text": "Arrange tags deterministically."}],
             "scope": {"owned_files": ["src/TagArranger.cs"]}}
    value.update(changes)
    return value


class ContractTests(unittest.TestCase):
    def test_valid_tag_arranger_plan_package(self):
        self.assertEqual(validate_plan_package(plan()), (True, []))

    def test_missing_acceptance_criteria(self):
        valid, errors = validate_plan_package(plan(acceptance_criteria=[]))
        self.assertFalse(valid)
        self.assertIn("missing acceptance criteria", errors)

    def test_missing_base_commit(self):
        valid, errors = validate_plan_package(plan(base_commit=""))
        self.assertFalse(valid)
        self.assertIn("missing base commit", errors)


if __name__ == "__main__":
    unittest.main()
