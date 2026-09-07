# Agent Operating System — Phase 12 Revit Adoption

Date: 2026-08-28
Repository: `E:\Antigravity\RevitAddinSolution`
Canonical capability owner: `E:\chatgpt-local-orchestrator`
Status: COMPLETE / ACCEPTED

## Adoption result

RevitAddinSolution now consumes the consolidated Agent Operating System without retaining a second workflow/review/retry/memory/skill-registry runtime.

Active project routing is controlled by:

- `.agents/AGENTS.md`
- `.agents/capability-profile.json`
- `.agents/skill-manifest.json`
- `.agents/policies/capability-routing.md`
- `.agents/policies/consolidation-governance.md`

`consolidation` is ACTIVE and the router permits only ACTIVE capabilities.

## Retired lifecycle wrappers

The following are RETIRED / NOT ROUTABLE and no longer expose `SKILL.md`:

- `1-spec`
- `2-plan`
- `3-code`
- `4-ship`

Their unique behavior is preserved in the surviving capability model:

- requirements -> `deep-interview` + `project-workflow-governance`;
- complex planning -> `consensus-plan`;
- implementation/debug/repair -> Local Orchestrator + `verified-execution` when needed;
- acceptance/release -> Local Orchestrator review/evidence + project/artifact governance + `agent-security` when security-sensitive.

Autopilot and active specialist adapters were updated so they do not route through the retired lifecycle names.

## Retired dual-agent execution surface

Removed runtime modules:

- `.agents/runtime/harness.py`
- `.agents/runtime/dual_agent_runtime.py`
- `.agents/runtime/review_pipeline.py`
- `.agents/runtime/workflow_governance.py`
- `.agents/runtime/conpty_transport.py`

Removed executable legacy entrypoints/configuration under:

- `.agents/skills/dual-agent/`
- `.agents/skills/dual-agent-pipeline/`

`LEGACY.md` and bounded reference material may remain only as non-executable migration/audit evidence.

Removed runtime-only regression files:

- `test_convergence.py`
- `test_dual_agent_runtime.py`
- `test_dual_status_contract.py`
- `test_review_pipeline_snapshot.py`
- `test_workflow_governance_failure_budget.py`

## Retained bounded adapters

Retained intentionally:

- `.agents/runtime/learning_guard.py`
- `.agents/runtime/evolution_pipeline.py`

These remain project-local safeguard/evidence adapters only. Canonical operational-memory authority is Local Orchestrator Phase 9 and canonical skill registry/promotion authority is Local Orchestrator Phase 10.

The manifest retains `plannedSkills: []` as a compatibility read-model for older Phase 9/10 acceptance consumers; it does not restore planned/routable capability authority.

## Verification

- Cross-repository Phase 12 resurrection regression: 3/3 PASS.
- Allowlisted `dotnet.exe test Antigravity.sln`: PASS 61/61.
  - HoanThien: 6/6.
  - WallMepClash: 10/10.
  - DrawBeams: 45/45.
- Direct Python governance-test invocation was blocked by the existing verification-only policy and the policy was not widened for closure.

Local Orchestrator build and typecheck both PASS. The final Local Orchestrator full regression has two concurrent non-Phase-12 failures: one CheckDrawings CD-2 verification-launcher expectation drift and the known live DrawBeams Phase 9 `sourceSnapshot` mismatch. Neither project lane was modified by this adoption closure.

## DrawBeams boundary

Phase 12 did not modify DrawBeams production C#/XAML or live `PROJECT_STATE.json` to obtain acceptance. DrawBeams remained under its own active development workflow.

## Final routing model

For normal Revit production work:

`project bootstrap -> ACTIVE capability router -> project-workflow-governance -> Local Orchestrator -> evidence/review -> project-local durable artifacts`

No retired dual-agent or `1-spec/2-plan/3-code/4-ship` execution route is valid after this adoption.
