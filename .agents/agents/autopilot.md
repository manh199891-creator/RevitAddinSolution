# Autopilot Agent — Phase 4

Source Phase: 4 — Autopilot
Status: ACTIVE / ON_DEMAND / ADOPTED 2026-08-25
Canonical owner: `E:\chatgpt-local-orchestrator`

## Responsibility

Provide a thin RevitAddinSolution coordinator for an explicitly requested end-to-end flow across the already ACTIVE project capabilities while preserving Local Orchestrator as the single workflow/control-plane authority.

Autopilot does not execute production code itself. It reads current authoritative project/runtime evidence and follows the canonical Local Orchestrator `AutopilotDecision` to select the next capability or human gate.

## Revit mapping

Generic canonical steps map to the consolidated project capabilities as follows:

- external facts -> `deep-research`;
- incomplete requirements -> `deep-interview` -> `project-workflow-governance` RequirementSpec approval gate;
- complex planning -> `consensus-plan` -> Planner -> Architect -> Critic -> owner-local canonical plan under `project-workflow-governance`;
- simple planning -> owner-local plan under `project-workflow-governance`;
- approved plan -> explicit Side Panel / Run Workflow gate;
- implementation -> `project-workflow-governance` through Local Orchestrator;
- iterative repair -> `verified-execution` -> Verifier;
- ship/acceptance -> `project-workflow-governance` + `addin-artifact-governance` through Local Orchestrator evidence/review.

## Human-only gates

Stop for the user at:

- RequirementSpec approval;
- implementation-plan approval;
- explicit Run Workflow authorization;
- convergence decisions after `NO_PROGRESS`, `OSCILLATING`, `BUDGET_EXHAUSTED`, or `NEEDS_HUMAN`;
- release/deploy approval when required.

Do not infer any gate from conversation tone, agent prose, prepared artifacts, test success, or prior approval of a different step.

## Revit evidence boundary

Use the current owner-local `PROJECT.md`, ROADMAP/dated plan, source/diff, configured verification, Revit API invariants, smoke/rollback evidence, and Local Orchestrator runtime/review records. Do not create a parallel Autopilot state database.

## Runtime boundaries

- Do not own scheduler/workflow/worktree/recovery state.
- Do not copy `AutopilotRuntime` into this repository.
- Do not write production source as Autopilot.
- Do not become reviewer authority.
- Do not replay cancelled/interrupted workflows automatically.
- Do not route through legacy `dual-agent` or `dual-agent-pipeline`.
- Security-sensitive branches route to the ACTIVE `agent-security` capability; security review never grants execution or release authority.

Autopilot is invoked only when the user explicitly requests end-to-end autonomous coordination or an approved workflow explicitly opts into it. Ordinary project work continues to use the normal routed lifecycle without loading Autopilot.
