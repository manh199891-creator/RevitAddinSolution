# Consolidation Governance — RevitAddinSolution

Status: ACTIVE — Phase 12
Date: 2026-08-28
Canonical authority: `E:\chatgpt-local-orchestrator\.agents\policies\consolidation-governance.md`

## Purpose

RevitAddinSolution is a downstream consumer of the Agent Operating System. Consolidation removes duplicate project-local execution authority after accepted shared replacements exist while preserving Revit-specific build/UI/model-write/artifact governance and declarative local learning safeguards.

## Shared authority — consume only

The following remain owned by Local Orchestrator and must not be reimplemented or reactivated locally:

- workflow lifecycle and durable workflow state;
- scheduler/DAG task readiness;
- provider execution durability;
- review/evidence pipeline;
- `LoopContract` / convergence-retry authority;
- recovery coordination;
- Browser Chat / Side Panel runtime;
- Mission Control projection;
- shared code-intelligence service;
- Phase 9 operational memory authority;
- Phase 10 SkillRegistry/promotion authority;
- Phase 11 external capability router;
- Phase 13 architecture-visualization evidence/IR/renderer-receipt authority.

## Project-local responsibilities — keep

Keep local Revit rules that are not shared runtime authority:

- repository/add-in artifact ownership;
- Revit host/API/thread/transaction/version rules;
- WPF/XAML interface quality adaptation;
- project workflow/approval/production-lane governance;
- owner-local smoke/rollback/deploy evidence;
- declarative Revit learning safeguards under `.agents/skills/experience-learning/references/revit-learning-safeguards.md`.

The retired project-local learning/evolution Python runtime must not be recreated. Declarative safeguards cannot override current project/source truth, become a second shared memory/registry runtime, or auto-promote skills.

## Phase 12 retirements

Retired duplicate skills:

- `1-spec` -> `deep-interview` + `project-workflow-governance` + `addin-artifact-governance`;
- `2-plan` -> `consensus-plan` + `project-workflow-governance` + `addin-artifact-governance`;
- `3-code` -> Local Orchestrator workflow + `verified-execution` + `project-workflow-governance`;
- `4-ship` -> Local Orchestrator review/evidence + `project-workflow-governance` + `addin-artifact-governance` + `agent-security`;
- `dual-agent` and `dual-agent-pipeline` -> Local Orchestrator workflow/review plus `verified-execution` where convergence is needed.

Retired duplicate runtime authority:

- `.agents/runtime/harness.py`;
- `.agents/runtime/dual_agent_runtime.py`;
- `.agents/runtime/review_pipeline.py`;
- `.agents/runtime/workflow_governance.py`;
- `.agents/runtime/conpty_transport.py` when it has no caller outside the retired dual-agent runtime;
- `.agents/runtime/learning_guard.py` and `.agents/runtime/evolution_pipeline.py` after final caller inventory proved zero live consumers and their unique Revit safeguards were migrated to the declarative experience-learning reference.

Historical dual-agent plans/reports may remain as non-executable audit/migration evidence.

## Removal/maintenance gate

Use `.agents/skills/consolidation/SKILL.md` for duplicate/legacy cleanup. Before retirement:

1. prove caller/routing inventory;
2. name the accepted replacement;
3. preserve any unique domain rule in surviving governance;
4. add a regression preventing resurrection;
5. use exact-path edits/deletes only;
6. preserve unrelated dirty work and active project state;
7. verify with project-scoped tests and Local Orchestrator canonical regression.

## DrawBeams concurrent-work boundary

Phase 12 governance/agent-runtime cleanup must not modify DrawBeams production C#/XAML or `src/Antigravity.DrawBeams/PROJECT_STATE.json` while the DrawBeams development lane is active. A live downstream fixture failure is recorded truthfully; it is not repaired by cross-cutting cleanup.

## Safety

- Never weaken validation, retry/timeout budgets, source identity checks or human-only gates to make consolidation pass.
- Never auto-land, commit, push, tag, reset, clean or stash.
- Never treat agent/test PASS as release, promotion or destructive-cleanup approval.
- Never delete durable acceptance/rollback evidence solely because it is old.
