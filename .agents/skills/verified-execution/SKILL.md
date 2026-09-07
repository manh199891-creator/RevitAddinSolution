---
name: verified-execution
description: RevitAddinSolution adapter for canonical Phase 3 LoopContract convergence. Use for iterative implementation/debug/repair work that needs evidence-backed retries, duplicate-attempt suppression, no-progress/oscillation detection, bounded budget, and independent verification through Local Orchestrator.
---

# verified-execution — Revit Adapter

Status: ACTIVE / ADOPTED 2026-08-25
Canonical owner: `E:\chatgpt-local-orchestrator`

Use this skill only when the current implementation/debug/repair task under `project-workflow-governance` requires more than a single evidence-backed attempt. Local Orchestrator remains the single normal workflow authority and owns the canonical `LoopContract` / convergence state.

## Preconditions

1. Read `.agents/skills/project-workflow-governance/SKILL.md`.
2. Read `.agents/policies/convergence-governance.md`.
3. Resolve the owner through `docs/projects/PROJECTS.md`.
4. Read owner `PROJECT.md`, `docs/plans/ROADMAP.md`, and the active dated plan.
5. For destructive/refactor/migration work, verify owner `smoke-tests/` and last-known-good metadata.
6. Inspect actual source/tests and current Local Orchestrator evidence before starting another attempt.

## Revit execution loop

Normal route:

`project-workflow-governance -> Local Orchestrator -> attempt -> Verifier evidence -> LoopContract decision`

Do not create a project-local scheduler, polling loop, repair database, or alternate review pipeline.

For each attempt, require observed evidence appropriate to the task:

- changed source/diff in the isolated workflow/worktree;
- configured build/typecheck/test results;
- relevant xUnit/RevitTestFramework evidence;
- expected artifacts and acceptance criteria;
- Revit model-write transaction safety;
- main-thread / ExternalEvent safety;
- `UnitUtils` conversions;
- Revit-year/.NET compatibility;
- smoke/rollback evidence when required.

## Canonical stop outcomes

Honor the Local Orchestrator result exactly:

- `CONVERGED` — goal satisfied by observed evidence;
- `NO_PROGRESS` — unchanged/regressed evidence threshold reached;
- `OSCILLATING` — repeated alternating failure pattern;
- `BLOCKED_INFRA` — provider/runtime infrastructure blocks trustworthy execution;
- `BUDGET_EXHAUSTED` — bounded retry budget reached;
- `NEEDS_HUMAN` — human-only decision required.

Any non-converged terminal outcome blocks a "done" claim.

## Duplicate / resume rule

Same relevant snapshot + hypothesis + failure fingerprint is a duplicate attempt and must not consume retry budget. Resume only with a changed relevant snapshot, new evidence, or a new falsifiable hypothesis.

## Verifier boundary

Verifier is an on-demand independent evidence role. It does not write production source, own workflow state, approve human-only gates, or start another retry loop. Coder success prose is not verification.

## Legacy boundary

`dual-agent` and `dual-agent-pipeline` are retired and no longer exist under `.agents/skills/`. Do not route through them for research, planning, code review, repair, or release. Historical migration evidence is archived under `docs/reports/legacy/agent-runtime/` and may be consulted only for Phase 3/12 migration analysis.

## Canonical acceptance provenance

Canonical implementation/evidence owner:

`E:\chatgpt-local-orchestrator\docs\reports\2026-08-25-agent-operating-system-phase-3-shared-loop-runtime.md`
