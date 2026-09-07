# Revit Convergence Governance — Phase 3 Adapter

Status: ACTIVE / ON_DEMAND
Canonical owner: `E:\chatgpt-local-orchestrator`
Source phase: 3 — Shared RALPH / Loopy Runtime

## Authority

Local Orchestrator owns the canonical `LoopContract`, convergence evaluation, review/repair state and durable workflow identity. RevitAddinSolution must not create a second loop runtime or reactivate the retired dual-agent workflow.

## Revit evidence requirements

When `verified-execution` is routed, progress must be measured from observed evidence appropriate to the active add-in, including as applicable:

- actual changed source/diff in the isolated task/worktree;
- configured build/typecheck/test command results;
- xUnit/RevitTestFramework evidence;
- smoke-test manifest and last-known-good identity for destructive/refactor work;
- expected artifacts/acceptance criteria;
- Revit transaction/thread/unit/plugin invariants;
- owner-local plan/design/acceptance constraints.

Agent completion prose alone is never progress evidence.

## Stop semantics

Respect canonical outcomes exactly:

- `CONVERGED`
- `NO_PROGRESS`
- `OSCILLATING`
- `BLOCKED_INFRA`
- `BUDGET_EXHAUSTED`
- `NEEDS_HUMAN`

Do not silently retry after a terminal outcome. A duplicate relevant snapshot + hypothesis + failure fingerprint is not a fresh attempt and must not consume budget.

## Legacy extraction boundary

The retired `dual-agent` and `dual-agent-pipeline` skill directories are no longer part of `.agents/skills/`. Their preserved migration evidence lives under `docs/reports/legacy/agent-runtime/` and is `HISTORICAL / NOT_ROUTABLE`. Old failure-budget/no-progress/oscillation/stale/duplicate semantics may be consulted only as historical evidence; normal execution/review/repair goes through Local Orchestrator.
