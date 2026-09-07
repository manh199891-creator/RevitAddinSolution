---
name: addin-artifact-governance
description: "Canonical ownership rules for plans, design, acceptance, reports and smoke-test artifacts for Antigravity projects/add-ins. Apply before planning, coding, review, migration or ship work."
---

# Add-in / Project Artifact Governance

**Purpose:** keep every project/add-in responsible for its own durable engineering artifacts, while reserving repository-level docs for genuinely cross-solution concerns.

## 1. Identify the smallest stable owner first

Before creating or moving any plan/report/design/acceptance/smoke artifact, classify the owner.

- One add-in / one production project only -> owner is `src/Antigravity.<Project>/`.
- Shared library / integration adapter under `src/Antigravity.*` -> that project still owns its local docs and S0/S1 smoke contract.
- Cross-add-in architecture, shared build/deploy, installer-wide migration, solution governance -> repository-level `docs/`.
- `.ai-bridge/` -> transient execution/handoff state only; never the canonical archive.
- `.agent/context/` and `.agents/context/` -> transient runtime planning/review mirrors used by automation; they never replace the canonical owner-local docs tree.

Do not place an add-in-specific artifact at repository root merely because the task was initiated from the repository root.

## 2. Canonical project tree and durable memory ladder

For an actively maintained `src/Antigravity.<Project>/`, establish and use:

```text
src/Antigravity.<Project>/
├── PROJECT.md                     # short project landing page / resume state
├── <production source folders>
├── docs/
│   ├── plans/
│   │   ├── ROADMAP.md             # stable long-term milestone index
│   │   └── YYYY-MM-DD-<feature>.md
│   ├── design/
│   ├── acceptance/
│   └── reports/
└── smoke-tests/
    ├── README.md
    ├── smoke-manifest.json
    ├── baseline/
    │   ├── last-known-good.json
    │   └── expected-behavior.md
    ├── scripts/
    ├── fixtures/
    └── results/          # generated / Git ignored
```

The solution-level entrypoint is `docs/projects/PROJECTS.md`.

Agent resume order is mandatory:

`docs/projects/PROJECTS.md` -> `src/<Owner>/PROJECT.md` -> `src/<Owner>/docs/plans/ROADMAP.md` -> relevant dated plan -> smoke/rollback evidence -> production source.

Empty ownership folders may use a small `README.md` placeholder so the contract is visible in Git.

## 3. Artifact routing rules

- Project landing/resume state -> `src/<Owner>/PROJECT.md`.
- Long-term project milestone index -> `src/<Owner>/docs/plans/ROADMAP.md`.
- Detailed implementation/feature plan -> `src/<Owner>/docs/plans/YYYY-MM-DD-<feature>.md`.
- Technical design / algorithm decision / module ADR -> `src/<Owner>/docs/design/`.
- Acceptance criteria / manual acceptance procedure -> `src/<Owner>/docs/acceptance/`.
- Agent implementation report / review closure / migration report -> `src/<Owner>/docs/reports/`.
- Runtime smoke scripts / fixtures / rollback identity -> `src/<Owner>/smoke-tests/`.
- Generated smoke results -> `src/<Owner>/smoke-tests/results/`, ignored by Git.
- Cross-solution plans only -> `docs/plans/`; other cross-solution artifacts remain under repository-level `docs/...`.

`PROJECT.md` and `ROADMAP.md` are durable indexes, not chat logs. Do not create a new top-level plan file for every chat/session when an existing roadmap owns the work.

## 4. Smoke profile by project type

Use the same folder contract but vary required smoke levels by project type.

### Revit executable / production feature add-in
- S0 Structural identity
- S1 Focused build/tests
- S2 Revit assembly/manifest load when applicable
- S3 Representative command startup / safe cancel
- S4 Feature critical path before production promotion

### Shared library / core / adapter
- S0 Structural identity
- S1 Focused build/tests
- Add higher integration smoke only when the project has an executable/runtime boundary that can be verified meaningfully.

Do not invent fake Revit command smoke for a pure library merely to satisfy a template.

### Project-scoped smoke execution contract

The physical location of test code and the ownership of smoke execution are different concerns.

- Unit / characterization / regression test projects may remain under repository-level `tests/` when they are normal solution test projects, for example `tests/Antigravity.DrawBeams.Tests/Antigravity.DrawBeams.Tests.csproj`.
- Add-in-specific smoke orchestration, scripts, fixtures, baseline identity and generated smoke results belong under the owning `src/<Owner>/smoke-tests/` tree.
- The owner `smoke-manifest.json` must identify the focused test project or owner-local smoke runner used for S1.
- An add-in-specific workflow must resolve verification through the owner's smoke contract first. It must not default to `dotnet test Antigravity.sln` merely because the add-in lives in that solution.
- `dotnet test Antigravity.sln` is reserved for explicitly requested cross-solution regression, final integration, release/ship gates, or another documented solution-wide acceptance requirement.
- A focused add-in workflow may still run inside a Local Orchestrator isolated worktree. Paths such as `E:\chatgpt-local-orchestrator\apps\bridge\runtime\worktrees\...\src\<Owner>\bin\...` are transient execution outputs, not canonical artifact ownership locations.
- Verification evidence must retain the canonical project/owner identity and the smoke level/check that produced it; a temporary worktree path must never redefine the project owner.
- If no owner-specific S1 runner exists yet, add it under `src/<Owner>/smoke-tests/scripts/` or explicitly invoke the focused test project named by `smoke-manifest.json` before treating S1 as satisfied.

### Verified package publication contract

When an isolated Local Orchestrator worktree produces a package intended for manual Revit smoke, publication into the canonical owner tree must be deterministic and project-scoped:

- Publish only from the exact workflow/task worktree recorded by Local Orchestrator runtime state.
- Require terminal job `COMPLETED`, execution success, review `PASS`, and required tests `PASS` before copying any DLL.
- The owner-local publisher must validate an explicit expected file set and SHA-256 values; agent prose alone is not publication authority.
- Publish into `src/<Owner>/smoke-tests/results/<workflow-id>/package`, never a repository-root or Local Orchestrator folder.
- Existing canonical packages are immutable by identity: an exact byte-for-byte match is idempotent; any mismatch fails closed and must not overwrite.
- Use a temporary same-parent directory, verify copied hashes, then move the complete package into place so partial packages are not exposed as valid smoke candidates.
- Publication must emit a receipt beside the package and must not modify production source, `baseline/last-known-good.json`, production switches, tags, or Approved Baseline state.
- Dynamic publish identity belongs in ignored/generated smoke results or runtime request state, not in durable tracked plans/skills.

This contract prevents one add-in's focused workflow from being blocked or falsely satisfied by unrelated solution test projects while preserving solution-wide regression as a separate higher-level gate.

## 5. Migration rules for existing repositories

When applying this governance to existing projects:

1. Inventory first; do not blindly bulk-move every root document.
2. Move only artifacts whose owner is unambiguous.
3. Preserve filenames and historical dates when practical.
4. Update obvious references after a move.
5. If ownership is ambiguous or a file spans multiple projects, leave it in place and record it in the migration report for later review.
6. Do not alter production source, namespaces, assembly names, references or runtime behavior as part of a documentation/smoke-tree migration.
7. Do not reset, clean, stash, commit, push or tag unrelated dirty work unless the user explicitly asks.

For each migrated project, write a concise migration report under that project's `docs/reports/`.

## 6. Transient runtime mirror rule

`.ai-bridge/current-plan.md` may mirror or point to the active canonical project plan for workflow execution. `.agent/context/` and `.agents/context/` may contain generated `PLAN.md`, `TECHNICAL_DESIGN.md`, `ACCEPTANCE_CRITERIA.md`, research or memory files for runtime review. These locations are transient mirrors only.

For any add-in/project-specific task, generated runtime planning artifacts must identify or synchronize back to the durable project memory ladder: `src/Antigravity.<Owner>/PROJECT.md`, `src/Antigravity.<Owner>/docs/plans/ROADMAP.md`, canonical project-local docs, and smoke/rollback evidence. A runtime `PLAN.md` is never allowed to become the only durable copy merely because a dual-agent or handoff pipeline generated it.

After a phase closes, durable knowledge belongs under the owning project's `PROJECT.md`, `docs/` and `smoke-tests/` tree.

## 7. Agent execution / review rule

Agent reports are evidence, not final authority.

Final ChatGPT/CodexPro review must:

1. inspect the actual changed source/worktree, not only `review-package.json` or agent prose;
2. compare changed files against the canonical plan and acceptance contract;
3. re-run relevant verification where practical;
4. preserve prior approved baseline/smoke identity;
5. promote/advance an Approved Baseline only after actual source review and verification.

## 8. Preflight gate before production-source edits

Before touching production source, answer all of these:

- `docs/projects/PROJECTS.md` read and owner project identified? YES/NO
- Owner `PROJECT.md` read? YES/NO
- Owner `docs/plans/ROADMAP.md` read? YES/NO
- Canonical active/relevant dated plan path known? YES/NO
- `docs/{plans,design,acceptance,reports}` ownership established? YES/NO
- `smoke-tests/` contract present for actively maintained production/beta work? YES/NO
- Last-known-good identity preserved? YES/NO
- Unrelated dirty work protected? YES/NO

If a required answer is NO, establish the missing governance artifact before destructive/refactor work continues.

## 9. Required references

Also obey:

- `.agents/AGENTS.md`
- `.agents/policies/repository-structure.md`
- `docs/architecture/REPOSITORY_STRUCTURE.md`

If an older skill or plan conflicts with this ownership rule, this project-local governance plus the canonical repository-structure policy wins.
