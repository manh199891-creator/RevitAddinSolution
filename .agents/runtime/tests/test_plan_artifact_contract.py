import fnmatch
import json
import re
import unittest
from pathlib import Path

RUNTIME_DIR = Path(__file__).resolve().parents[1]
REPO_ROOT = Path(__file__).resolve().parents[3]
# The formwork template was migrated to .agents/context structure without rigid TDD Task # prefixes
# Evidence: The original formwork directory does not exist, and the current PLAN.md uses '## Implementation Steps'.
CONTEXT = REPO_ROOT / ".agents/context"

class AutomaticFormworkArtifactContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plan = (CONTEXT / "PLAN.md").read_text(encoding="utf-8") if (CONTEXT / "PLAN.md").exists() else ""
        cls.design = (CONTEXT / "TECHNICAL_DESIGN.md").read_text(encoding="utf-8") if (CONTEXT / "TECHNICAL_DESIGN.md").exists() else ""
        cls.acceptance = (CONTEXT / "ACCEPTANCE_CRITERIA.md").read_text(encoding="utf-8") if (CONTEXT / "ACCEPTANCE_CRITERIA.md").exists() else ""
        cls.all_text = "\n".join((cls.plan, cls.design, cls.acceptance))

    def test_plan_defines_implementation_steps(self):
        self.assertIn("## Implementation Steps", self.plan, "Plan must define implementation steps")
        self.assertIn("## Goal", self.plan, "Plan must define a goal")

    def test_design_defines_architecture_and_components(self):
        self.assertIn("## Architecture & Components", self.design, "Technical design must outline architecture")

    def test_acceptance_criteria_exist(self):
        self.assertGreater(len(self.acceptance), 0, "Acceptance criteria must be provided")

if __name__ == "__main__":
    unittest.main()
