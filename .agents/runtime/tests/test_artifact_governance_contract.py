from pathlib import Path
import fnmatch
import json
import re
import unittest


ROOT = Path(__file__).resolve().parents[3]
AGENTS = ROOT / ".agents" / "AGENTS.md"
POLICY = ROOT / ".agents" / "policies" / "repository-structure.md"
ROUTING = ROOT / ".agents" / "policies" / "capability-routing.md"
CONSOLIDATION_POLICY = ROOT / ".agents" / "policies" / "consolidation-governance.md"
GOV = ROOT / ".agents" / "skills" / "addin-artifact-governance" / "SKILL.md"
WORKFLOW_GOV = ROOT / ".agents" / "skills" / "project-workflow-governance" / "SKILL.md"
CONSOLIDATION = ROOT / ".agents" / "skills" / "consolidation" / "SKILL.md"
MANIFEST = ROOT / ".agents" / "skill-manifest.json"
DRAWBEAMS_SMOKE = ROOT / "src" / "Antigravity.DrawBeams" / "smoke-tests" / "smoke-manifest.json"
RETIRED_SKILLS = ("1-spec", "2-plan", "3-code", "4-ship", "dual-agent", "dual-agent-pipeline")
RETIRED_RUNTIME = (
    "harness.py",
    "dual_agent_runtime.py",
    "review_pipeline.py",
    "workflow_governance.py",
    "conpty_transport.py",
    "learning_guard.py",
    "evolution_pipeline.py",
)
RETIRED_SCHEMA_FILES = (
    "codex_review_output.schema.json",
    "evidence_manifest.schema.json",
    "review_run.schema.json",
    "task_context.schema.json",
    "task_scope.schema.json",
)
RETIRED_ENTRYPOINTS = (
    "dual_orchestrate.ps1",
    "dual_doctor.ps1",
    "dual_init.ps1",
    "dual_paths.ps1",
    "dual_read_reports.ps1",
    "dual_run.ps1",
    "dual_status.ps1",
    "dual_wait.ps1",
)


def text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


class ArtifactGovernanceContractTests(unittest.TestCase):
    def test_authoritative_governance_is_wired_from_agents_and_policy(self):
        agents = text(AGENTS)
        policy = text(POLICY)
        self.assertIn(".agents/skills/addin-artifact-governance/SKILL.md", agents)
        self.assertIn(".agents/policies/consolidation-governance.md", agents)
        self.assertIn("src/<Addin>/docs/", policy)
        self.assertIn(".ai-bridge", policy)
        self.assertIn("transient", policy.lower())

    def test_consolidated_lifecycle_preserves_unique_project_rules(self):
        workflow = text(WORKFLOW_GOV)
        for marker in (
            "Consolidated lifecycle gates",
            "deep-interview",
            "deep-research",
            "consensus-plan",
            "RED -> GREEN -> REFACTOR",
            "verified-execution",
            "LoopContract",
            "agent-security",
            "addin-artifact-governance",
            "Commit, merge, push",
        ):
            self.assertIn(marker, workflow)
        self.assertIn("Local Orchestrator / Side Panel", workflow)
        self.assertIn("explicit user/current-workflow actions", workflow)

    def test_phase12_retires_duplicate_skill_and_runtime_authority(self):
        manifest = json.loads(text(MANIFEST))
        active = {item["name"] for item in manifest["activeSkills"]}
        retired = {item["name"]: item.get("replacement", "") for item in manifest["retiredCapabilities"]}
        skills_root = ROOT / ".agents" / "skills"
        actual_skill_dirs = {path.name for path in skills_root.iterdir() if path.is_dir()}
        self.assertEqual(active, actual_skill_dirs, "skill manifest and active skill directories must match exactly")

        for name in RETIRED_SKILLS:
            self.assertNotIn(name, active)
            self.assertIn(name, retired)
            self.assertTrue(retired[name], name)
            self.assertFalse((skills_root / name).exists(), f"retired skill directory resurrected: {name}")

        runtime_root = ROOT / ".agents" / "runtime"
        for name in RETIRED_RUNTIME:
            self.assertFalse((runtime_root / name).exists(), name)
        self.assertEqual(set(), {path.name for path in runtime_root.glob("*.py")}, "no project-local runtime Python authority may remain")
        self.assertEqual(
            {"test_artifact_governance_contract.py"},
            {path.name for path in (runtime_root / "tests").glob("test_*.py")},
            "only the consolidated governance regression may remain as runtime test source",
        )
        self.assertFalse((runtime_root / "tests" / "fixtures" / "agent_context_v1").exists())

        schemas_root = runtime_root / "schemas"
        self.assertFalse(schemas_root.exists(), "retired project-local runtime schema authority must not resurrect")
        for schema_name in RETIRED_SCHEMA_FILES:
            matches = list((ROOT / ".agents").rglob(schema_name))
            self.assertEqual([], matches, f"retired schema resurrected: {schema_name}")

        for entrypoint in RETIRED_ENTRYPOINTS:
            matches = list((ROOT / ".agents").rglob(entrypoint))
            self.assertEqual([], matches, f"retired execution entrypoint resurrected: {entrypoint}")

    def test_consolidation_is_active_and_legacy_routes_cannot_resurrect(self):
        manifest = json.loads(text(MANIFEST))
        active = {item["name"] for item in manifest["activeSkills"]}
        self.assertIn("consolidation", active)
        routing = text(ROUTING)
        consolidation = text(CONSOLIDATION)
        policy = text(CONSOLIDATION_POLICY)
        self.assertIn("Retired capability mapping", routing)
        self.assertIn("NOT ROUTABLE", routing)
        self.assertIn("Local Orchestrator / Side Panel is the single normal workflow authority", routing)
        self.assertIn("revit-learning-safeguards.md", routing)
        self.assertIn("retired project-local learning/evolution Python runtime", routing)
        self.assertIn("Do not modify DrawBeams production", consolidation)
        self.assertIn("shared authority", policy.lower())
        self.assertIn("DrawBeams", policy)

    def test_runtime_contexts_are_explicitly_transient_not_canonical(self):
        gov = text(GOV)
        for marker in (".ai-bridge/", ".agent/context/", ".agents/context/"):
            self.assertIn(marker, gov)

    def test_transient_plan_scope_contract_survives_legacy_fixture_retirement(self):
        plan = (
            "## Goal\n"
            "## Implementation Steps\n"
            "Modify src/Antigravity.Core/Services/Mock.cs\n"
            "## Test Strategy\n"
            "Run tests/Antigravity.Core.Tests/MockTests.cs\n"
            "## Rollback Procedure\n"
        )
        scope = {
            "schema_version": 1,
            "allowed_files": ["src/Antigravity.Core/**", "tests/Antigravity.Core.Tests/**"],
            "forbidden": ["src/Antigravity.Main/**"],
        }
        for heading in ("## Goal", "## Implementation Steps", "## Test Strategy", "## Rollback Procedure"):
            self.assertIn(heading, plan)
        paths = re.findall(r"(?:src|tests)/[^\s`\n]+", plan)
        self.assertGreaterEqual(len(paths), 1)
        for candidate in paths:
            clean = candidate.strip("`*.,")
            self.assertTrue(any(fnmatch.fnmatchcase(clean, pattern) for pattern in scope["allowed_files"]), clean)
            self.assertFalse(any(fnmatch.fnmatchcase(clean, pattern) for pattern in scope["forbidden"]), clean)

        escaped = "src/Antigravity.Main/Module.cs"
        self.assertFalse(any(fnmatch.fnmatchcase(escaped, pattern) for pattern in scope["allowed_files"]))
        self.assertTrue(any(fnmatch.fnmatchcase(escaped, pattern) for pattern in scope["forbidden"]))

    def test_transient_acceptance_contract_uses_current_verification_language(self):
        acceptance = (
            "Scenario: User invokes the scoped action.\n"
            "Expected Result: The expected project behavior is observed.\n"
            "Automated Validation: Run the configured owner-scoped verification.\n"
            "Independent Verification: Review actual source/worktree and verification evidence through Local Orchestrator.\n"
        )
        for marker in ("Scenario:", "Expected Result:", "Automated Validation:", "Independent Verification:"):
            self.assertIn(marker, acceptance)
        self.assertNotIn("Dual-Agent Verification:", acceptance)

    def test_commit_or_push_is_never_automatic_after_lifecycle_consolidation(self):
        workflow = text(WORKFLOW_GOV).lower()
        agents = text(AGENTS).lower()
        self.assertIn("explicit", workflow)
        self.assertIn("commit", workflow)
        self.assertIn("push", workflow)
        self.assertIn("do not reset, clean, stash, commit, push, tag", agents)

    def test_governance_preserves_review_source_truth(self):
        gov = text(GOV)
        self.assertIn("actual changed source/worktree", gov)
        self.assertIn("Agent reports are evidence, not final authority", gov)
        self.assertIn("Approved Baseline", gov)

    def test_addin_specific_verification_routes_through_owner_smoke_contract(self):
        gov = text(GOV)
        manifest = text(DRAWBEAMS_SMOKE)
        self.assertIn("Project-scoped smoke execution contract", gov)
        self.assertIn("tests/Antigravity.DrawBeams.Tests/Antigravity.DrawBeams.Tests.csproj", gov)
        self.assertIn("src/<Owner>/smoke-tests/scripts/", gov)
        self.assertIn("must not default to `dotnet test Antigravity.sln`", gov)
        self.assertIn("reserved for explicitly requested cross-solution regression", gov)
        self.assertIn('"S1_DRAWBEAMS_FOCUSED_TESTS"', manifest)
        self.assertIn('"focusedTestProject": "tests/Antigravity.DrawBeams.Tests/Antigravity.DrawBeams.Tests.csproj"', manifest)

    def test_agent_resumability_memory_ladder_exists_for_every_src_project(self):
        registry = ROOT / "docs" / "projects" / "PROJECTS.md"
        self.assertTrue(registry.is_file(), registry.as_posix())
        for csproj in sorted((ROOT / "src").glob("Antigravity.*/*.csproj")):
            project_dir = csproj.parent
            self.assertTrue((project_dir / "PROJECT.md").is_file(), project_dir.as_posix())
            self.assertTrue((project_dir / "docs" / "plans" / "ROADMAP.md").is_file(), project_dir.as_posix())

    def test_cross_solution_plans_use_framework_neutral_docs_plans(self):
        agents = text(AGENTS)
        policy = text(POLICY)
        gov = text(GOV)
        for content in (agents, policy, gov):
            self.assertIn("docs/plans/", content)
            self.assertNotIn("docs/superpowers/plans/", content)

    def test_preflight_reads_project_memory_before_project_plan(self):
        agents = text(AGENTS)
        gov = text(GOV)
        for content in (agents, gov):
            self.assertIn("docs/projects/PROJECTS.md", content)
            self.assertIn("PROJECT.md", content)
            self.assertIn("docs/plans/ROADMAP.md", content)


if __name__ == "__main__":
    unittest.main()
