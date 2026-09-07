# Capability Routing Policy — RevitAddinSolution

Status: ACTIVE / CONSOLIDATED — Phase 12
Date: 2026-08-28

This file is the project-local routing authority for CodexPro and authorized agents.

## 1. Bootstrap rule

Before choosing an implementation/review capability:

1. read `.agents/AGENTS.md`;
2. read `.agents/capability-profile.json`;
3. read `.agents/skill-manifest.json`;
4. read `.agents/policies/consolidation-governance.md`;
5. identify the owner project/add-in and active durable plan/state;
6. route only to capabilities marked `ACTIVE`.

Do not load every skill. Retired capabilities are historical names only and are never routable.

## 2. Current ACTIVE routing

| Task type | Required active capability | Optional active capability |
|---|---|---|
| Unknown external/API/domain behavior or conflicting external claims | `deep-research` | Local Orchestrator review/evidence path when independent validation is required |
| Requirements materially unclear after durable context is loaded | `deep-interview` + `project-workflow-governance` | `deep-research` first when the gap depends on external technical facts |
| New feature / material behavior change | `project-workflow-governance` | `deep-interview` for material requirement gaps; `consensus-plan` for complex/high-risk planning; `deep-research` for material external uncertainty |
| User explicitly requests end-to-end autonomous coordination | Autopilot thin coordinator -> current ACTIVE capabilities | Autopilot must stop at RequirementSpec, plan, Run Workflow, convergence-exception, and release human gates |
| Complex/cross-module/migration/high-regression planning | `consensus-plan` -> Planner -> Architect -> Critic -> owner-local canonical plan under `project-workflow-governance` | requires explicitly approved RequirementSpec; simple/local plans may skip consensus |
| Implementation / bug fix / refactor | `project-workflow-governance` -> Local Orchestrator workflow | invoke `verified-execution` -> Verifier only when iterative correction/repair requires additional evidence-backed attempts |
| Build / acceptance / deploy / release | `project-workflow-governance` + `addin-artifact-governance` -> Local Orchestrator evidence/review | `agent-security` for security-sensitive release/deploy/dependency/MCP/model-write changes |
| WPF/XAML/UI quality, clipping, DPI, shared style/resource or interaction polish | `xaml-interface-quality` | keep product behavior/bindings/events/Revit host semantics unchanged unless an approved requirement says otherwise |
| Security-sensitive MCP/tool, auth/session, credential, dependency, installer/deploy, model-mutation, provenance or skill/memory promotion work | `agent-security` | pair with the relevant active capability; Local Orchestrator remains the execution-enforcement authority |
| Project resume after interruption, repeated failure, regression history, decision lookup, prevention evidence or checkpoint/session-closure maintenance | `memory-governance` | use Local Orchestrator Phase 9 ProjectMemory/MemoryRetriever; only `EXACT` permits exact continuation |
| Post-workflow lesson mining, SkillCandidate evaluation, canary/promotion/rollback maintenance | `experience-learning` | pair with `agent-security` when trust/permission/deploy/model-mutation or other security-sensitive skill content is involved; Local Orchestrator Phase 10 remains registry/promotion authority |
| Duplicate/legacy capability cleanup, adapter retirement, unused agent-runtime removal | `consolidation` | apply only after replacement equivalence/caller inventory is proven |
| External SaaS/tool capability (for example Gmail, Slack, GitHub, MCP-backed service, Composio-backed service, or direct API adapter) | Local Orchestrator `external-capability-router` shared runtime | exact project/principal/capability/connection grant is required; WRITE/ADMIN must be workflow-bound and idempotent; use `agent-security` before enabling/installing a new provider adapter |
| Repository/system/workflow architecture visualization | Local Orchestrator `architecture-visualization` shared capability | consume Phase 8/source/workflow evidence; renderer output is a projection only; do not create a project-local Archify runtime/skill authority or infer topology without relationship evidence |
| Plan/design/report/smoke artifact ownership | `addin-artifact-governance` | none |
| Planning or production execution governance | `project-workflow-governance` | none |
| Migration Nice3point / plugin entry-point work | `project-workflow-governance` | `consensus-plan` if complex; inspect `.agents/examples/StandardAddInPlugin.cs` |
| Multi-version Revit support | `project-workflow-governance` | `consensus-plan` if architecture-sensitive; verify target framework and Revit-year compatibility |

For production-source changes, Local Orchestrator / Side Panel is the single normal workflow authority under `project-workflow-governance`.

## 3. Retired capability mapping — NOT ROUTABLE

The following names are retained only in manifest/history to explain migrations; their active `SKILL.md` / execution entrypoints are retired:

- `1-spec` -> `deep-interview` + project/artifact governance;
- `2-plan` -> `consensus-plan` + project/artifact governance;
- `3-code` -> Local Orchestrator workflow + `verified-execution` + project workflow governance;
- `4-ship` -> Local Orchestrator review/evidence + project/artifact governance + `agent-security` when needed;
- `dual-agent` -> Local Orchestrator workflow/review;
- `dual-agent-pipeline` -> Local Orchestrator workflow/review + canonical `LoopContract` / `verified-execution`.

Do not create a new generic lifecycle wrapper merely to rename the four retired lifecycle skills.

## 4. Shared-runtime-only capabilities

Do not route project agents to create local duplicates of:

- workflow/scheduler/job/execution state ownership;
- ReviewRuntime / ReviewPackage / evidence authority;
- `LoopContract` / convergence-retry runtime;
- Browser Chat / Side Panel runtime;
- recovery coordinator;
- Mission Control;
- shared code-intelligence service/provider;
- Phase 9 operational-memory authority;
- Phase 10 SkillRegistry/promotion authority;
- Phase 11 external capability router;
- Phase 13 architecture-visualization evidence/IR/renderer-receipt authority.

These are consumed from `E:\chatgpt-local-orchestrator` when available.

Revit-specific learning safeguards are declarative under `.agents/skills/experience-learning/references/revit-learning-safeguards.md`. Do not recreate the retired project-local learning/evolution Python runtime; Phase 9/10 execution remains shared-runtime-only in Local Orchestrator.

## 5. Escalation / authority

- Supporting coder is not reviewer authority.
- Agent-produced success text is not acceptance evidence.
- Human-only decisions remain human-only.
- If no ACTIVE capability covers a requested behavior, use current governance safely and record the gap; do not reactivate a retired capability.
- Consolidation must not weaken validation/retry/timeout/security/approval rules or mutate active DrawBeams production/state just to satisfy a cross-repository fixture.
