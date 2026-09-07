---
name: consolidation
description: RevitAddinSolution Phase 12 adapter for safe duplicate-skill/runtime cleanup after shared Local Orchestrator equivalence is accepted. Use for legacy capability retirement, routing simplification and unused agent-runtime removal while preserving Revit domain governance, learning safeguards, approvals and unrelated dirty work.
---

# consolidation

Read `.agents/policies/consolidation-governance.md` first. Canonical Phase 12 rules live in `E:\chatgpt-local-orchestrator`.

## Authority

This skill is maintenance/governance only. It does not own a scheduler, workflow engine, review runtime, retry loop, memory store, SkillRegistry, Browser Chat runtime or external capability router.

## Procedure

1. Inventory exact callers, imports, manifests, scripts and router entries for the candidate artifact.
2. Bind the accepted replacement capability/owner.
3. Preserve any unique Revit/build/UI/model-write/artifact rule in surviving project governance.
4. Add/update a resurrection regression before or with retirement.
5. Retire exact duplicate paths only; preserve unrelated dirty work and durable historical evidence.
6. Preserve unique Revit safeguards declaratively under active skill/policy references; do not retain a disconnected project-local runtime once caller inventory and canonical replacement are proven.
7. Run project-scoped governance/runtime checks, then Local Orchestrator canonical build/typecheck/test.

## Current Phase 12 retirement map

- `1-spec` -> `deep-interview` + project/artifact governance.
- `2-plan` -> `consensus-plan` + project/artifact governance.
- `3-code` -> Local Orchestrator workflow + `verified-execution` + project workflow governance.
- `4-ship` -> Local Orchestrator review/evidence + project/artifact governance + `agent-security` when security-sensitive.
- `dual-agent` / `dual-agent-pipeline` -> retired; Local Orchestrator is the single normal workflow/review authority.
- legacy local harness/review/retry runtime -> retired after caller inventory proves only legacy consumers remain.

## Do not

- Do not modify DrawBeams production C#/XAML or active `PROJECT_STATE.json` from this cleanup lane.
- Do not weaken tests, validators, retry/timeout budgets or human gates.
- Do not auto-promote skills/memory.
- Do not create a new generic lifecycle wrapper to replace the four retired wrappers.
- Do not reset, clean, stash, commit, push or tag unless explicitly authorized.
