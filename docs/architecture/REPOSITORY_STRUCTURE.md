# RevitAddinSolution — Canonical Repository Structure

Status: ACTIVE GOVERNANCE / AGENT-RESUMABILITY OPTIMIZED

This file defines the durable repository structure. The primary goal is that a human or agent can return after a long pause, identify a project, recover its roadmap and verification state, and continue without reconstructing context from chat/runtime mirrors.

## 1. Canonical solution tree

```text
RevitAddinSolution/
├── .agents/
│   ├── AGENTS.md
│   ├── policies/
│   └── skills/
├── .ai-bridge/                    # transient execution/handoff state only
├── docs/                          # cross-solution durable knowledge only
│   ├── projects/
│   │   └── PROJECTS.md            # solution-level project registry
│   ├── architecture/
│   ├── adr/
│   ├── plans/                     # cross-solution plans only
│   ├── reports/                   # cross-solution reports only
│   ├── specs/
│   ├── standards/
│   └── templates/
├── src/
│   └── Antigravity.<Project>/     # production projects remain physically flat
│       ├── PROJECT.md             # durable project landing/resume page
│       ├── <production source folders>
│       ├── docs/
│       │   ├── plans/
│       │   │   ├── ROADMAP.md     # stable milestone index
│       │   │   └── YYYY-MM-DD-<feature>.md
│       │   ├── design/
│       │   ├── acceptance/
│       │   └── reports/
│       └── smoke-tests/
│           ├── README.md
│           ├── smoke-manifest.json
│           ├── baseline/
│           │   ├── last-known-good.json
│           │   └── expected-behavior.md
│           ├── scripts/
│           ├── fixtures/
│           └── results/           # generated; Git ignored
├── tests/
├── packaging/
├── scripts/
│   ├── architecture/
│   ├── diagnostics/
│   ├── deploy/
│   ├── maintenance/
│   └── verification/
└── artifacts/                     # generated; Git ignored
```

## 2. Durable agent memory ladder

For project-specific work, read in this order:

1. `docs/projects/PROJECTS.md` — identify project/type/activity and entrypoint.
2. `src/<Project>/PROJECT.md` — understand purpose, identity, test/smoke links and resume checklist.
3. `src/<Project>/docs/plans/ROADMAP.md` — understand current/history/deferred milestone state.
4. Only the relevant dated plan/design/report for the active milestone.
5. `smoke-tests/smoke-manifest.json` and `baseline/last-known-good.json` before destructive/production work.
6. Production source.

Do not infer current project state from chat history or a filename alone.

## 3. Artifact ownership

A durable artifact belongs to the smallest stable owner.

- Project landing/resume state -> `src/<Project>/PROJECT.md`
- Long-term project roadmap -> `src/<Project>/docs/plans/ROADMAP.md`
- Detailed feature/implementation plan -> `src/<Project>/docs/plans/YYYY-MM-DD-<feature>.md`
- Technical design -> `src/<Project>/docs/design/`
- Acceptance -> `src/<Project>/docs/acceptance/`
- Implementation/review/closure report -> `src/<Project>/docs/reports/`
- Smoke/rollback metadata -> `src/<Project>/smoke-tests/`
- Cross-solution plan -> `docs/plans/`
- Cross-solution architecture/report/spec/standard -> corresponding repository `docs/` folder

`.ai-bridge/`, `.agent/context/` and `.agents/context/` are transient mirrors only.

## 4. Physical source-layout rule

Production projects stay physically flat under `src/Antigravity.*` by default. Logical categories such as Host, Shared, Modeling, Coordination and Documentation are navigation concepts and may be represented by Visual Studio Solution Folders or `MODULE_PLACEMENT.md`.

Do not physically move every feature project just to mirror those categories. Path churn provides no Revit runtime benefit and creates unnecessary solution/project-reference/script churn.

Physical relocation is a separate reviewed migration only when there is a real semantic boundary, for example:

- test-only project -> `tests/`;
- installer/package project -> `packaging/`;
- tightly coupled integration family -> optional reviewed grouping.

No physical move implies namespace/assembly/AddInId behavior change.

## 5. Smoke and rollback rule

Use project-type-appropriate smoke levels:

- Revit executable/feature: S0 structural, S1 focused build/test, S2 load when applicable, S3 command/safe-cancel, S4 feature critical path before production promotion.
- Shared/core/contracts: S0 + S1; add integration smoke only for a real runtime boundary.
- Installer/support/test-only: use meaningful project-specific checks; never invent a Revit command smoke.

`last-known-good.json` records rollback identity. `PENDING_CAPTURE` / `PENDING_REVALIDATION` are truthful non-PASS states. Compilation alone never promotes last-known-good.

## 6. Repository-level conventions

- `docs/plans/` is framework-neutral; durable repository plans do not depend on an agent/framework directory name.
- `scripts/architecture/` owns architecture-map tooling.
- `scripts/verification/` owns build/test verification entrypoints.
- `scripts/deploy/` owns deploy/package entrypoints.
- `scripts/diagnostics/` owns reusable Revit/AutoCAD diagnostic tools.
- Root-level executable scripts are not canonical; keep solution root limited to solution/config entrypoints.
- `docs/standards/` owns durable solution-wide standards such as UI guidelines.
- `packaging/` is the target semantic home for installer/package source when a separately reviewed physical move is approved.
- `tests/` is the target semantic home for test-only project source when references/build gates are captured.
- `artifacts/` is generated and ignored.

## 7. Mandatory preflight

Before project production edits:

1. read `.agents/AGENTS.md` and `.agents/policies/repository-structure.md`;
2. read `docs/projects/PROJECTS.md`;
3. identify owner and read its `PROJECT.md`;
4. read its `docs/plans/ROADMAP.md` and active/relevant dated plan;
5. verify smoke/last-known-good requirements;
6. protect unrelated dirty work;
7. keep runtime mirrors transient.

This structure is the canonical governance contract. Physical source relocation is not required for agent readability.
