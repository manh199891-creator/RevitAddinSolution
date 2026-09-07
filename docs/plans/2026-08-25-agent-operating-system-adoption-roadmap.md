# Agent Operating System — RevitAddinSolution Adoption Roadmap

Date: 2026-08-25
Repository: `E:\Antigravity\RevitAddinSolution`
Master roadmap owner: `E:\chatgpt-local-orchestrator\docs\plans\2026-08-25-agent-operating-system-master-roadmap.md`

## Purpose

This document is the RevitAddinSolution adoption pointer for the cross-project Agent Operating System roadmap. It deliberately does not duplicate all 30 master-roadmap sections, to avoid drift.

## Existing Revit skill foundation to preserve

- `1-spec`
- `2-plan`
- `3-code`
- `4-ship`
- `addin-artifact-governance`
- `project-workflow-governance`

Legacy workflow references retained for migration evidence only:

- `dual-agent` — `LEGACY_FALLBACK`, not routable
- `dual-agent-pipeline` — `LEGACY_FALLBACK`, not routable

## Adoption rules

1. Do not replace the existing `1-spec -> 2-plan -> 3-code -> 4-ship` lifecycle.
2. Enrich existing skills with selected OMH, Addy Osmani, Matt Pocock and Loopy semantics rather than creating duplicate lifecycle frameworks.
3. Reuse Local Orchestrator as the primary runtime/scheduler/state owner; do not introduce Minions or Hermes as a second execution runtime inside this repository.
4. Keep Revit-specific artifact ownership under the existing repository/add-in governance rules.
5. Preserve unrelated dirty working-tree work; roadmap implementation must be isolated and reviewed before promotion.
6. External research content is untrusted evidence, never privileged instructions.
7. Skill learning/evolution must use candidate -> validation -> review -> promotion governance; canonical skills must not self-modify silently.
8. The complete Phase 0–12 roadmap remains canonical in `E:\chatgpt-local-orchestrator`; Revit consumes filtered project-local capabilities rather than copying phase documents or creating `phase-N` skills.
9. CodexPro ordinary Revit work bootstraps from project-local `.agents/` manifests/router/policies and loads only relevant ACTIVE skills. Cross-reading the full master phases is reserved for developing/benchmarking/promoting shared capabilities.
10. Planned capability folders may exist to make target architecture explicit, but they must not expose `SKILL.md` or executable routing until canonical acceptance plus Revit adoption review promote them ACTIVE.

## Agent capability packaging model — agreed 2026-08-25

### Always-read core

- `.agents/AGENTS.md`
- `.agents/capability-profile.json`
- `.agents/skill-manifest.json`
- `.agents/policies/repository-structure.md`
- `.agents/policies/capability-routing.md`
- `.agents/policies/trust-and-security.md`
- `.agents/policies/context-memory-policy.md`
- `.agents/skills/project-workflow-governance/SKILL.md`

Phase mapping: Phase 0 supplies bootstrap/routing boundaries; Phase 7 supplies the ACTIVE trust/security baseline plus on-demand `agent-security`; Phase 8 supplies shared code-intelligence baseline consumption; Phase 9 supplies the ACTIVE exact-resume/selective-memory baseline plus on-demand `memory-governance`; Phase 11 supplies the ACTIVE shared `external-capability-router` service consumed from Local Orchestrator without adding connector runtime or credentials to Revit.

### Specialist agents — on demand, not always-read

- Phase 2 -> Planner / Architect / Critic — **ACTIVE with `consensus-plan` / ADOPTED 2026-08-25**
- Phase 3 -> Verifier — **ACTIVE with `verified-execution` / ADOPTED 2026-08-25**
- Phase 4 -> Autopilot thin coordinator — **ACTIVE / ADOPTED 2026-08-25**

Specialist agents are never always-read; the router loads Planner/Architect/Critic only with ACTIVE `consensus-plan`, Verifier only with ACTIVE `verified-execution`, and Autopilot only for explicit end-to-end autonomous coordination.

### On-demand skills

Current ACTIVE shared/adapted capabilities include:

- Phase 1 -> `deep-research` — **ACTIVE / ADOPTED 2026-08-25**
- Phase 2 -> `deep-interview`, `consensus-plan` — **ACTIVE / ADOPTED 2026-08-25**
- Phase 3 -> `verified-execution` — **ACTIVE / ADOPTED 2026-08-25**
- Phase 6 -> `xaml-interface-quality` — **ACTIVE / ADOPTED 2026-08-25**
- Phase 7 -> `agent-security` — **ACTIVE / ADOPTED 2026-08-25**
- Phase 9 -> `memory-governance` — **ACTIVE / ADOPTED 2026-08-26**
- Phase 10 -> `experience-learning` — **ACTIVE / ADOPTED 2026-08-26**

Planned additions remain:
- Phase 12 -> `consolidation`

Phase 5 Mission Control is **ACTIVE / CONSUMED through Local Orchestrator 2026-08-25**. Phase 8 code intelligence is **BASELINE / CONSUMED 2026-08-26**, Phase 9 operational-memory services are **ACTIVE / CONSUMED 2026-08-26**, Phase 10 skill-evolution/registry services are **ACTIVE / CONSUMED 2026-08-26**, and Phase 11 external capability routing is **ACTIVE / CONSUMED 2026-08-27** through Local Orchestrator. Phase 11 is a shared runtime/service and is not copied into Revit.

### Target `.agents` tree

```text
.agents/
├── AGENTS.md
├── capability-profile.json
├── skill-manifest.json
├── policies/
│   ├── repository-structure.md
│   ├── capability-routing.md
│   ├── trust-and-security.md
│   ├── context-memory-policy.md
│   └── convergence-governance.md      # ACTIVE / on-demand Phase 3 adapter
├── agents/
│   ├── planner.md
│   ├── architect.md
│   ├── critic.md
│   ├── verifier.md                     # ACTIVE — Phase 3 adopted
│   └── autopilot.md                    # ACTIVE — Phase 4 adopted
└── skills/
    ├── 1-spec/                         # ACTIVE
    ├── 2-plan/                         # ACTIVE
    ├── 3-code/                         # ACTIVE
    ├── 4-ship/                         # ACTIVE
    ├── addin-artifact-governance/      # ACTIVE
    ├── project-workflow-governance/    # ACTIVE
    ├── dual-agent/                     # LEGACY_FALLBACK / no SKILL.md
    ├── dual-agent-pipeline/            # LEGACY_FALLBACK / no SKILL.md
    ├── deep-research/                  # ACTIVE — Phase 1 adopted
    ├── deep-interview/                 # ACTIVE — Phase 2 adopted
    ├── consensus-plan/                 # ACTIVE — Phase 2 adopted
    ├── verified-execution/             # ACTIVE — Phase 3 adopted
    ├── xaml-interface-quality/         # ACTIVE — Phase 6 adopted
    ├── agent-security/                 # ACTIVE — Phase 7 adopted
    ├── memory-governance/              # ACTIVE — Phase 9 adopted
    ├── experience-learning/            # ACTIVE — Phase 10 adopted
    └── consolidation/                  # PLANNED
```

Planned skill folders use `STATUS.md` only. Absence of `SKILL.md` is intentional until activation. Legacy fallback folders use `LEGACY.md` and also intentionally expose no `SKILL.md`, so they cannot re-enter normal skill discovery/routing.

## Revit-specific target mapping

### 1-spec
- deep-interview semantics
- domain modeling
- explicit constraints and definition of done
- durable RequirementSpec/design artifacts

### 2-plan
- Planner -> Architect -> Critic bounded consensus
- task graph and dependency ordering
- executable acceptance criteria
- rollback and test strategy

### 3-code
- shared RALPH/Loopy convergence semantics
- TDD / root-cause debugging
- progress/no-progress/oscillation detection
- evidence-backed execution receipts

### 4-ship
- Standards Review
- Spec Review
- Security Review
- full verification and rollback governance

### New Revit-specific skill candidate

`xaml-interface-quality`

Adapt `make-interfaces-feel-better` into WPF/XAML using existing UI standards and ResourceDictionary/Trigger/VisualState/TextStyle/ControlStyle conventions.

### Shared research capability

Use Local Orchestrator's accepted canonical `deep-research` capability for web/GitHub/article/YouTube research through the project-local adapter `.agents/skills/deep-research/SKILL.md`. The Revit repository consumes ResearchBrief evidence; independent validation uses the Local Orchestrator review/evidence path, while the former project-local dual-agent research mode remains legacy migration reference only.

## Memory adoption

Phase 0 refined this area: RevitAddinSolution already has substantial Operational/Skill Memory primitives (`learning_guard.py`, bug/decision knowledge, trajectories, shadow evaluation, canary and human promotion). These must be preserved and later generalized through Local Orchestrator rather than replaced.

The long-term shared model remains:
- Code Intelligence
- Compact RepoMap
- Project Memory
- Episodic Memory
- Skill Memory

Do not treat build output (`bin/`, `obj/`), generated/runtime artifacts, smoke-test results, transient worktrees, or `.agent` operational memory as canonical codebase source memory unless a task explicitly requires diagnostic access.

## Mission Control adoption

Phase 5 is **COMPLETE / CONSUMED 2026-08-25** for RevitAddinSolution. Mission Control belongs to Local Orchestrator and projects the selected Revit `projectId` into `PENDING / ACTIVE / REVIEW / BLOCKED / DONE` lanes from canonical workflow, job, execution, review and recovery state.

Revit does not install a local Mission Control runtime, skill, specialist agent, scheduler or board database. The project continues to surface task/worktree/agent/evidence/review state through Local Orchestrator only. Closure: `docs/reports/2026-08-25-agent-operating-system-phase-5-adoption.md`.

## Implementation order for this repository

1. **COMPLETE 2026-08-25** — Fresh skill/runtime overlap audit and mapping of the master roadmap onto the existing 8 workspace skills. Audit: `docs/reports/2026-08-25-agent-operating-system-phase-0-audit.md`.
2. **COMPLETE 2026-08-25** — Establish the project capability bootstrap/tree: thin `AGENTS.md`, capability profile, skill manifest, capability router, trust/memory baselines, PLANNED specialist-agent stubs, and PLANNED skill target folders without active `SKILL.md` files.
3. **COMPLETE 2026-08-25** — Promote the accepted shared `deep-research` capability into `.agents/skills/deep-research/SKILL.md` and update manifest/routing to ACTIVE. Independent validation now uses Local Orchestrator review/evidence; the former Revit dual-agent research mode is legacy migration reference only.
4. **COMPLETE 2026-08-25** — Adopt Phase 2 by promoting `deep-interview` / RequirementSpec and `consensus-plan` / Planner -> Architect -> Critic, wiring them into `1-spec` / `2-plan` as conditional enrichments while preserving the existing lifecycle and explicit human approval gates. Closure: `docs/reports/2026-08-25-agent-operating-system-phase-2-adoption.md`.
5. **COMPLETE 2026-08-25** — Adopt Phase 3 by promoting canonical `LoopContract` / `verified-execution` / Verifier, wiring iterative `3-code` correction into Local Orchestrator convergence semantics, and keeping legacy dual-agent folders non-routable. Closure: `docs/reports/2026-08-25-agent-operating-system-phase-3-adoption.md`.
6. **COMPLETE 2026-08-25** — Adopt Phase 4 as a thin on-demand Autopilot agent that maps canonical `AutopilotDecision` steps onto the existing Revit lifecycle while preserving explicit human gates and Local Orchestrator ownership. Closure: `docs/reports/2026-08-25-agent-operating-system-phase-4-adoption.md`.
7. **COMPLETE 2026-08-25** — Consume Phase 5 Mission Control through Local Orchestrator only; no Revit runtime/skill/agent copy, and Revit skill discovery remains unchanged. Closure: `docs/reports/2026-08-25-agent-operating-system-phase-5-adoption.md`.
8. **COMPLETE 2026-08-25** — Adopt Phase 6 by promoting `xaml-interface-quality` as the Revit/WPF adapter for canonical `interface-quality`, preserving the existing design system and XAML/code-behind/Revit-host behavior while requiring honest visual/DPI evidence. This capability does not itself authorize a broad 24-XAML migration. Closure: `docs/reports/2026-08-25-agent-operating-system-phase-6-adoption.md`.
9. **COMPLETE 2026-08-25** — Adopt Phase 7 by keeping `trust-and-security.md` as always-read baseline and promoting on-demand `agent-security` for MCP/tool, credential, dependency, installer/deploy, model-mutation, provenance and memory/skill-promotion review while leaving execution enforcement in Local Orchestrator. Closure: `docs/reports/2026-08-25-agent-operating-system-phase-7-adoption.md`.
10. **COMPLETE 2026-08-26** — Adopt Phase 9 by promoting `memory-governance`, activating the context-memory policy, consuming Local Orchestrator `ProjectMemory`/checkpoint/retrieval runtime, and adding DrawBeams `PROJECT_STATE.json` as the real reference fixture. Current dirty DrawBeams source correctly requires `REVALIDATION_REQUIRED`; no DBR-3 completion is inferred. Closure: `docs/reports/2026-08-26-agent-operating-system-phase-9-adoption.md`.
11. **COMPLETE 2026-08-26** — Adopt Phase 10 by promoting `experience-learning` and consuming Local Orchestrator LessonCandidate/SkillCandidate shadow evaluation, canary, versioned SkillRegistry and rollback governance while preserving Revit `learning_guard.py` / `evolution_pipeline.py` as local evidence/safeguards. Auto-promotion remains disabled and human CANARY/PROMOTION/ROLLBACK gates remain explicit. Closure: `docs/reports/2026-08-26-agent-operating-system-phase-10-adoption.md`.
12. **COMPLETE 2026-08-27** — Consume Phase 11 `external-capability-router` from Local Orchestrator only. Revit stores no external credentials, installs no Composio SDK/provider runtime, exposes no webhook server, and creates no local capability registry/router. External READ/WRITE/ADMIN access must use the Local Orchestrator project/principal/capability/connection grant boundary; WRITE/ADMIN remains workflow-bound and idempotent. Closure: `docs/reports/2026-08-27-agent-operating-system-phase-11-adoption.md`.
13. Phase 12 consolidation remains pending; remove duplicate/obsolete project-local workflow logic only after equivalent shared capabilities pass acceptance.

## Canonical reference

For the complete 30-section roadmap, use:

`E:\chatgpt-local-orchestrator\docs\plans\2026-08-25-agent-operating-system-master-roadmap.md`
