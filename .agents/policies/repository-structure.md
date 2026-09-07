# Repository Structure Enforcement Policy

This policy is mandatory for ChatGPT, CodexPro, Codex, Antigravity and any local implementation agent working in RevitAddinSolution.

## Canonical references

- Repository structure: `docs/architecture/REPOSITORY_STRUCTURE.md`
- Logical module grouping: `docs/architecture/MODULE_PLACEMENT.md`
- Project registry: `docs/projects/PROJECTS.md`
- Master migration plan: `docs/plans/2026-08-22-revitaddinsolution-vnext-restructure-master-plan.md`
- Project rules: `.agents/AGENTS.md`

## Rule 1 — Resume from durable project memory

Before planning or editing production source for one project, read in this order:

1. `docs/projects/PROJECTS.md`
2. `src/<Addin>/PROJECT.md`
3. `src/<Addin>/docs/plans/ROADMAP.md`
4. the active/relevant dated plan under `src/<Addin>/docs/plans/`
5. the project smoke contract / last-known-good when applicable

Do not reconstruct project state from chat history or transient runtime files when durable project memory exists.

## Rule 2 — Identify the owner before creating artifacts

If a task belongs to one add-in/project, canonical durable artifacts must stay below that owner:

```text
src/<Addin>/
├── PROJECT.md
├── docs/
│   ├── plans/
│   │   ├── ROADMAP.md
│   │   └── YYYY-MM-DD-<feature>.md
│   ├── design/
│   ├── acceptance/
│   └── reports/
└── smoke-tests/
```

Do not create add-in-specific `PLAN.md`, design, acceptance or closure reports at repository root.

Cross-solution plans only belong under `docs/plans/`. Repository-level `docs/` is for genuinely cross-solution concerns.

## Rule 3 — Runtime planning mirrors are transient only

`.ai-bridge/`, `.agent/context/`, and `.agents/context/` may mirror active plans/research/design/acceptance state for handoff, review or runtime automation. They must never be the only durable home of project knowledge.

When a runtime mirror refers to one project, synchronize durable state back to that project's `PROJECT.md`, `docs/plans/ROADMAP.md` and canonical project-local artifacts before the phase closes.

## Rule 4 — Smoke baseline before destructive refactor

Every production/beta add-in must own:

```text
src/<Addin>/smoke-tests/
├── README.md
├── smoke-manifest.json
├── baseline/
│   ├── last-known-good.json
│   └── expected-behavior.md
├── scripts/
├── fixtures/
└── results/
```

`results/` is generated and Git ignored except for its placeholder.

Before destructive refactor, dependency change, framework migration or behavior replacement, preserve last-known-good identity and define the smoke gate. A build PASS alone never promotes last-known-good.

## Rule 5 — Keep production source physically flat

Production projects remain physically under `src/Antigravity.*` by default. Host/Shared/Modeling/Coordination/Documentation classifications are logical groupings for navigation and Solution Folders, not a reason to lengthen every disk path.

Physical moves are considered separately only when they express a real semantic boundary, such as:

- test-only projects -> `tests/`;
- installer/package source -> `packaging/`;
- tightly coupled integration families -> optional reviewed integration grouping.

Do not move production feature folders merely to mirror logical categories.

## Rule 6 — Preserve rollback and unrelated work

A refactor is not complete merely because it builds. Production promotion requires a verified rollback identity and actual required smoke evidence.

Do not overwrite/delete the previous known-good release during promotion. Do not reset, clean, stash, commit, push or tag unrelated dirty work unless explicitly authorized.

## Rule 7 — One migration dimension at a time

Do not combine these in one phase unless explicitly approved:

- physical folder move;
- namespace rename;
- assembly rename;
- Revit version/framework migration;
- functional feature rewrite.

## Required preflight checklist

Before editing production source, an agent must answer:

- Owner project identified from `docs/projects/PROJECTS.md`? YES/NO
- `src/<Addin>/PROJECT.md` read? YES/NO
- `src/<Addin>/docs/plans/ROADMAP.md` read? YES/NO
- Canonical active/relevant plan path known? YES/NO
- Smoke-test contract present or explicitly not required? YES/NO
- Last-known-good baseline preserved? YES/NO
- Unrelated dirty work protected? YES/NO

If any required answer is NO, establish the missing governance artifact before destructive implementation.
