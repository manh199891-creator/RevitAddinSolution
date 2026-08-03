import fnmatch
import json
import re
import unittest
from pathlib import Path

RUNTIME_DIR = Path(__file__).resolve().parents[1]
FIXTURE_CONTEXT = RUNTIME_DIR / "tests" / "fixtures" / "agent_context_v2"

# Evidence of migration:
# The old AutomaticFormworkArtifactContractTests relied on a rigid `.agents/factory` or `.agents/formwork` structure
# which has been replaced by the dynamic task-based `.agents/context` with version 2 artifact templates.
# The new contract validates generic document headings and scopes instead of specific features like 'FaceKey' or 'L-junction'.

class AgentContextContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plan = (FIXTURE_CONTEXT / "PLAN.md").read_text(encoding="utf-8")
        cls.design = (FIXTURE_CONTEXT / "TECHNICAL_DESIGN.md").read_text(encoding="utf-8")
        cls.acceptance = (FIXTURE_CONTEXT / "ACCEPTANCE_CRITERIA.md").read_text(encoding="utf-8")
        cls.scope = json.loads((FIXTURE_CONTEXT / "TASK_SCOPE.json").read_text(encoding="utf-8"))

    def validate_plan(self, plan_text, scope_data):
        self.assertIn("## Goal", plan_text, "Plan must define a Goal")
        self.assertIn("## Implementation Steps", plan_text, "Plan must define Implementation Steps")
        self.assertIn("## Test Strategy", plan_text, "Plan must define a Test Strategy")
        self.assertIn("## Rollback Procedure", plan_text, "Plan must define a Rollback Procedure")
        
        # Must have at least one implementation path (e.g. `src/...` or src/...)
        paths = re.findall(r"(?:src|tests|docs)/[^\s`\n]+", plan_text)
        self.assertGreaterEqual(len(paths), 1, "Plan must contain at least one implementation path")
        
        allowed = scope_data.get("allowed_files", [])
        forbidden = scope_data.get("forbidden_paths", [])
        
        for path in paths:
            path_clean = path.strip("`*.,")
            is_allowed = any(fnmatch.fnmatch(path_clean, pattern) or path_clean.startswith(pattern) for pattern in allowed)
            self.assertTrue(is_allowed, f"Path {path_clean} in Plan is outside allowed scope")
            
            is_forbidden = any(fnmatch.fnmatch(path_clean, pattern) or path_clean.startswith(pattern) for pattern in forbidden)
            self.assertFalse(is_forbidden, f"Path {path_clean} in Plan matches a forbidden path")

    def validate_design(self, design_text, scope_data):
        self.assertIn("## Feature Definition", design_text, "Design must define Feature Definition")
        self.assertIn("## Architecture & Components", design_text, "Design must outline Architecture & Components")
        self.assertIn("## Unresolved Decisions", design_text, "Design must outline Unresolved Decisions")
        
        paths = re.findall(r"(?:src|tests|docs)/[^\s`\n]+", design_text)
        self.assertGreaterEqual(len(paths), 1, "Design must contain at least one component path")
        
        allowed = scope_data.get("allowed_files", [])
        for path in paths:
            path_clean = path.strip("`*.,")
            is_allowed = any(fnmatch.fnmatch(path_clean, pattern) or path_clean.startswith(pattern) for pattern in allowed)
            self.assertTrue(is_allowed, f"Path {path_clean} in Design is outside allowed scope")

    def validate_acceptance(self, acceptance_text):
        lines = [line.strip() for line in acceptance_text.splitlines() if line.strip()]
        self.assertGreater(len(lines), 1, "Acceptance criteria must not be empty or a single line")
        self.assertIn("Scenario:", acceptance_text, "Acceptance criteria must have a Scenario")
        self.assertIn("Expected Result:", acceptance_text, "Acceptance criteria must have an Expected Result")
        self.assertIn("Automated Validation:", acceptance_text, "Acceptance criteria must have Automated Validation")
        self.assertIn("Dual-Agent Verification:", acceptance_text, "Acceptance criteria must have Dual-Agent Verification")

    def validate_scope(self, scope_data):
        self.assertIn("allowed_files", scope_data, "TASK_SCOPE must contain allowed_files")
        self.assertIsInstance(scope_data["allowed_files"], list)
        self.assertGreater(len(scope_data["allowed_files"]), 0, "allowed_files must not be empty")

    def test_fixture_is_fully_compliant(self):
        """The valid fixture must pass all validations."""
        self.validate_scope(self.scope)
        self.validate_plan(self.plan, self.scope)
        self.validate_design(self.design, self.scope)
        self.validate_acceptance(self.acceptance)

    def test_missing_goal_fails(self):
        bad_plan = self.plan.replace("## Goal", "## Objective")
        with self.assertRaises(AssertionError) as ctx:
            self.validate_plan(bad_plan, self.scope)
        self.assertIn("Plan must define a Goal", str(ctx.exception))

    def test_empty_or_single_line_acceptance_fails(self):
        bad_acc_empty = ""
        with self.assertRaises(AssertionError) as ctx:
            self.validate_acceptance(bad_acc_empty)
        self.assertIn("Acceptance criteria must not be empty or a single line", str(ctx.exception))
        
        bad_acc_single = "Scenario: everything works. Expected Result: OK. Automated Validation: yes. Dual-Agent Verification: yes."
        with self.assertRaises(AssertionError) as ctx:
            self.validate_acceptance(bad_acc_single)
        self.assertIn("Acceptance criteria must not be empty or a single line", str(ctx.exception))

    def test_missing_automated_validation_fails(self):
        bad_acc = self.acceptance.replace("Automated Validation:", "Manual Validation:")
        with self.assertRaises(AssertionError) as ctx:
            self.validate_acceptance(bad_acc)
        self.assertIn("Acceptance criteria must have Automated Validation", str(ctx.exception))

    def test_implementation_path_outside_allowed_scope_fails(self):
        bad_plan = self.plan + "\n3. Update src/Antigravity.Main/App.cs"
        with self.assertRaises(AssertionError) as ctx:
            self.validate_plan(bad_plan, self.scope)
        self.assertIn("outside allowed scope", str(ctx.exception))
        
    def test_implementation_path_in_forbidden_paths_fails(self):
        # We also want to check the forbidden path logic specifically
        bad_plan = self.plan + "\n3. Change src/Antigravity.Main/Module.cs"
        scope_with_forbidden = {
            "allowed_files": ["src/Antigravity.Core/*", "src/Antigravity.Main/*", "tests/*"],
            "forbidden_paths": ["src/Antigravity.Main/*"]
        }
        with self.assertRaises(AssertionError) as ctx:
            self.validate_plan(bad_plan, scope_with_forbidden)
        self.assertIn("matches a forbidden path", str(ctx.exception))

if __name__ == "__main__":
    unittest.main()
