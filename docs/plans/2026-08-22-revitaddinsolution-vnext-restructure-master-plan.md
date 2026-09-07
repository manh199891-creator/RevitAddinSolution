# RevitAddinSolution vNext — Master Restructure, Plan Ownership, Smoke-Test & Rollback Plan

**Date:** 2026-08-22  
**Source workspace (BLUE):** `E:\Antigravity\RevitAddinSolution`  
**Target workspace (GREEN):** `E:\Antigravity\RevitAddinSolution-vNext`  
**Target branch:** `refactor/vnext-architecture`  
**Status:** APPROVED FOR PREPARATION — implementation must happen in GREEN, not BLUE.

---

## 1. Objective

Restructure the Revit add-in solution without putting the current working repository at risk. The current repository remains the BLUE baseline; all architecture and folder migrations happen in a cloned GREEN workspace. GREEN is promoted only after build, smoke, Revit integration, rollback, and deployment acceptance gates pass.

This master plan adds two mandatory governance rules:

1. **An add-in's active plan belongs to that add-in.** Add-in-specific plans, technical designs, acceptance criteria, implementation notes, and closure reports must be stored inside the owning add-in folder rather than in `.ai-bridge` or at repository root.
2. **Every add-in/project must own a smoke-test area and a rollback baseline.** A restructuring step is not accepted until the affected add-in can prove startup/load/command behavior and identify a last-known-good rollback point.

`.ai-bridge` remains a transient coordination channel only. It is not a canonical plan archive.

---

## 2. BLUE / GREEN Safety Model

```text
E:\Antigravity\
|
+-- RevitAddinSolution\                 BLUE
|   +-- current production/source baseline
|   +-- no architecture refactor
|   +-- read/reference only during vNext migration
|
+-- RevitAddinSolution-vNext\           GREEN
|   +-- refactor/vnext-architecture
|   +-- all structural migration and refactoring
|   +-- add-in-local plans and smoke tests
|
+-- RevitAddinArchive\                  historical artifacts
|
+-- RevitAddinDeployments\
    +-- staging\
    +-- releases\
    +-- backups\
```

Rules:

- Never restructure BLUE in place.
- Do not delete BLUE after the first successful GREEN deployment.
- GREEN must retain independent Git metadata and full history.
- GREEN must preserve meaningful WIP but exclude generated caches and transient `.ai-bridge` state.
- Promotion to production is a separate operation from build/test.

---

## 3. Canonical Plan Ownership — NEW MANDATORY RULE

### 3.1 Cross-repository plans

Only plans that genuinely affect the entire solution stay in:

```text
docs/plans/
```

Examples:

```text
docs/plans/
  2026-08-22-revitaddinsolution-vnext-restructure-master-plan.md
  2026-xx-xx-multi-version-build-matrix.md
  2026-xx-xx-host-composition-root-refactor.md
```

### 3.2 Add-in-specific plans

Each add-in owns its implementation history under its own project folder:

```text
src/<Addin>/
  docs/
    plans/
    design/
    acceptance/
    reports/
```

Example for DrawBeams:

```text
src/Antigravity.DrawBeams/
  Models/
  Services/
  UI/
  docs/
    plans/
      2026-xx-xx-beam-feature-x.md
    design/
      beam-feature-x-technical-design.md
    acceptance/
      beam-feature-x-acceptance.md
    reports/
      beam-feature-x-closure.md
  smoke-tests/
  Antigravity.DrawBeams.csproj
  CreateBeamCommand.cs
```

Example for TagArranger:

```text
src/Antigravity.TagArranger/
  docs/
    plans/
      2026-08-22-deterministic-arranger.md
    design/
      deterministic-arranger-design.md
    acceptance/
      deterministic-arranger-acceptance.md
    reports/
  smoke-tests/
  ...
```

### 3.3 `.ai-bridge` policy

`.ai-bridge` is allowed to contain only transient orchestration/handoff state such as:

```text
.ai-bridge/
  current-plan.md          # temporary pointer/copy for an active handoff only
  agent-status.md
  codex-status.md
  decisions.md             # temporary session decisions; canonical ADRs move elsewhere
  execution-log.jsonl
  implementation-diff.patch
  open-questions.md
  session-log.jsonl
```

It must NOT be the only location for:

- feature plan;
- technical design;
- acceptance criteria;
- implementation report;
- closure report;
- rollback instructions.

Before a task is considered complete, the canonical artifacts must exist in the owning add-in's `docs/` subtree.

### 3.4 Task-to-owner routing rule

For every new plan, determine ownership first:

```text
Task touches one add-in only
        -> src/<Addin>/docs/...

Task touches multiple add-ins but one clearly owns the feature
        -> owning add-in docs + cross-links to dependent modules

Task changes shared architecture/build/deploy/contracts across solution
        -> docs/plans/...
```

A plan must never be created at repository root merely because the responsible add-in has not yet been identified.

---

## 4. Per-Add-in Smoke-Test Area — NEW MANDATORY RULE

Every production or beta project under `src/` gets:

```text
src/<Addin>/smoke-tests/
```

Minimum structure:

```text
smoke-tests/
  README.md
  smoke-manifest.json
  baseline/
    last-known-good.json
    expected-behavior.md
  scripts/
  fixtures/
  results/                  # generated; gitignored
```

Not every module needs a large automated suite. The folder is a stable contract that makes the smallest viable smoke gate explicit.

### 4.1 `README.md`

Must state:

- purpose of the add-in;
- Revit versions targeted;
- how to load/trigger the add-in;
- smoke-test prerequisites;
- commands/buttons covered;
- manual vs automated parts;
- rollback procedure;
- known limitations.

### 4.2 `smoke-manifest.json`

Machine-readable metadata, minimum fields:

```json
{
  "schemaVersion": 1,
  "project": "Antigravity.DrawBeams",
  "assembly": "Antigravity.DrawBeams.dll",
  "revitYears": [2024],
  "entrypoints": [],
  "requiredModules": [],
  "smokeChecks": [],
  "timeoutSeconds": 120
}
```

### 4.3 `baseline/last-known-good.json`

Must record the rollback identity, not binary blobs:

```json
{
  "schemaVersion": 1,
  "gitCommit": "<sha>",
  "branch": "<branch>",
  "releaseId": "<release-id>",
  "assemblyVersion": "<version>",
  "revitYear": 2024,
  "verifiedAt": "<timestamp>",
  "smokeStatus": "PASS"
}
```

The actual rollback binaries belong in immutable release storage, not inside Git:

```text
E:\Antigravity\RevitAddinDeployments\releases\<release-id>\
```

### 4.4 `baseline/expected-behavior.md`

Human-readable contract for the last-known-good behavior, for example:

- Ribbon panel loads.
- Button exists with expected label.
- Command can start without TypeLoadException.
- Required DLL dependencies resolve.
- Basic selection path succeeds/cancels safely.
- Revit remains responsive.
- No unhandled exception is added to journal/log.

### 4.5 `scripts/`

Contains only add-in-specific smoke automation, such as:

```text
Run-Smoke.ps1
Prepare-Fixture.ps1
Check-Logs.ps1
```

Shared smoke infrastructure belongs under:

```text
tests/smoke/
```

or future shared build tooling, while add-in-local scripts remain close to the module.

### 4.6 `fixtures/`

Contains small text/config fixtures when possible. Large `.rvt` or binary fixtures may stay in a shared fixture store and be referenced from the manifest instead of duplicated per add-in.

### 4.7 `results/`

Generated output only, including:

- smoke logs;
- screenshots;
- journal excerpts;
- timing data;
- PASS/FAIL JSON.

`results/` is gitignored. Canonical closure reports live in the add-in's `docs/reports/`.

---

## 5. Smoke-Test Acceptance Levels

### Level S0 — Structural

Required for every project after folder movement:

- `.csproj` resolves;
- project references resolve;
- expected source files exist;
- assembly name unchanged unless explicitly migrated;
- no accidental `bin/obj/.vs/__pycache__` introduced.

### Level S1 — Build

For buildable projects:

- clean build succeeds;
- expected DLL is generated;
- required dependencies are present;
- no new compiler warnings classified as migration blockers.

### Level S2 — Load

For Revit add-ins:

- Revit starts;
- add-in manifest resolves assembly;
- assembly loads without `FileNotFoundException`, `FileLoadException`, or `TypeLoadException`;
- ribbon/entrypoint registration succeeds.

### Level S3 — Command

At least one representative happy-path or safe-cancel path:

- command can start;
- required UI opens when applicable;
- safe cancellation leaves the model unchanged;
- no Revit API thread violation;
- transaction boundaries remain valid.

### Level S4 — Feature Critical Path

Required before production promotion for important modules:

- feature-specific fixture opens;
- critical command completes;
- expected model mutation/result is verified;
- logs/journal show no unhandled exception;
- Undo/rollback behavior is verified where relevant.

---

## 6. Rollback Contract

Smoke tests detect failure; rollback data makes recovery possible. Both are required.

For every module changed in a phase:

```text
BEFORE CHANGE
  -> record last-known-good Git SHA
  -> record currently deployed releaseId
  -> execute/confirm baseline smoke

IMPLEMENT
  -> change only GREEN

VERIFY
  -> build
  -> module smoke
  -> dependent-module smoke
  -> solution gate when required

FAIL
  -> no production promotion
  -> revert GREEN phase or fix forward
  -> production remains on previous immutable release

PASS
  -> package immutable candidate release
  -> stage in Revit
  -> production acceptance
  -> update last-known-good metadata only after confirmed PASS
```

Never update `last-known-good.json` merely because compilation succeeded.

---

## 7. Target vNext Repository Shape — Final Agent-Resumability Decision

The earlier proposal to physically group every production project under `Host/Shared/Features/...` is superseded. Production projects remain physically flat under `src/Antigravity.*`; logical categories are maintained in `docs/architecture/MODULE_PLACEMENT.md` and may be represented by Visual Studio Solution Folders.

```text
RevitAddinSolution-vNext/
|
+-- .agents/
+-- .ai-bridge/                       transient only
|
+-- docs/                             cross-solution durable knowledge only
|   +-- projects/
|   |   +-- PROJECTS.md
|   +-- architecture/
|   +-- adr/
|   +-- plans/                        cross-solution plans only
|   +-- reports/
|   +-- specs/
|   +-- standards/
|   +-- templates/
|
+-- src/
|   +-- Antigravity.<Project>/        production projects stay flat
|       +-- PROJECT.md                durable project landing/resume page
|       +-- <production source>
|       +-- docs/
|       |   +-- plans/
|       |   |   +-- ROADMAP.md
|       |   |   +-- YYYY-MM-DD-*.md
|       |   +-- design/
|       |   +-- acceptance/
|       |   +-- reports/
|       +-- smoke-tests/
|
+-- tests/
+-- packaging/
+-- scripts/
|   +-- architecture/
|   +-- maintenance/
+-- artifacts/                         generated / gitignored
```

Agent resume path is now mandatory:

```text
docs/projects/PROJECTS.md
-> src/<Project>/PROJECT.md
-> src/<Project>/docs/plans/ROADMAP.md
-> active/relevant dated plan
-> smoke / last-known-good
-> production source
```

Only semantic boundaries justify later physical moves: test-only projects to `tests/`, installer/package source to `packaging/`, or a tightly coupled integration family after separate review. Folder movement never implicitly renames namespaces, assemblies, command classes, AddInId or manifests.

---

## 8. Plan Migration from Existing Locations

Before restructuring each add-in:

1. Inventory relevant root-level plans, `.ai-bridge` artifacts, `docs/`, `plans/`, and add-in-local documents.
2. Identify the owning add-in.
3. Move/copy the canonical content to the owning add-in's `docs/` subtree.
4. Preserve meaningful history and date in filename.
5. Replace duplicate stale versions with a short pointer or archive classification rather than silently deleting potentially useful design history.
6. Leave `.ai-bridge/current-plan.md` as a transient handoff pointer/copy only when the orchestrator requires it.
7. Closure report must link the canonical add-in-local plan/design/acceptance files.

Example migration for the current TagArranger artifacts:

```text
PLAN.md
  -> src/Antigravity.TagArranger/docs/plans/2026-08-22-deterministic-tag-arranger.md

TECHNICAL_DESIGN.md
  -> src/Antigravity.TagArranger/docs/design/deterministic-tag-arranger-design.md

ACCEPTANCE_CRITERIA.md
  -> src/Antigravity.TagArranger/docs/acceptance/deterministic-tag-arranger-acceptance.md
```

Do not perform this migration in BLUE merely to make folders look cleaner; perform it in GREEN as part of the owning module's migration phase.

---

## 9. Revised Phase Plan

### Phase 0 — BLUE Cleanup & Immutable Baseline

Status: IN PROGRESS / mostly complete.

- remove generated build/cache artifacts;
- archive old installers/packages outside repo;
- normalize `.gitignore`;
- preserve WIP and fixtures;
- record BLUE HEAD and dirty-state inventory;
- do not restructure source folders.

**Gate:** BLUE remains usable and all removals are explainable/recoverable.

### Phase 1 — Create GREEN Clone

- create `E:\Antigravity\RevitAddinSolution-vNext`;
- preserve Git history;
- create `refactor/vnext-architecture`;
- overlay meaningful current WIP;
- exclude transient `.ai-bridge`, caches and archived outputs;
- record baseline metadata.

**Gate:** BLUE/GREEN source and WIP comparison passes.

### Phase 2 — Establish Governance Skeleton

Before moving feature source:

- update plan ownership rules;
- create templates for add-in-local docs;
- create smoke-test template;
- define smoke manifest schema;
- define `last-known-good.json` schema;
- define results ignore rules;
- classify every `src/*` project as production / beta / experimental / stub.

**Gate:** every module has an assigned owner folder and planned smoke level.

### Phase 3 — Per-Add-in Documentation Migration

For each module, one at a time:

- migrate its active plan/design/acceptance/report into `src/<Addin>/docs/`;
- do not use `.ai-bridge` as canonical storage;
- add cross-links from repository-wide documents if necessary;
- remove only proven duplicate/stale root artifacts after review.

**Gate:** no active module work exists only in `.ai-bridge` or repository root.

### Phase 4 — Per-Add-in Smoke Baseline Creation

For every production/beta module:

- create `smoke-tests/`;
- create `README.md`;
- create `smoke-manifest.json`;
- create baseline contract;
- identify last-known-good Git SHA/release;
- define S0-S4 level required for the module.

Do this **before** architecture movement/refactor of that module.

**Gate:** affected module has a known-good baseline and executable/repeatable smoke procedure.

### Phase 5 — Build & Revit Version Matrix

- establish supported Revit years;
- map Revit year -> target framework -> Revit API -> dependencies;
- centralize build scripts;
- preserve current functional target first;
- do not mix .NET 8 migration with physical folder restructuring.

**Gate:** clean reproducible build command exists for supported baseline.

### Phase 6 — Host / Antigravity.Main Refactor

- preserve command contracts;
- split ribbon/bootstrap/updater/debug automation responsibilities;
- eliminate unnecessary string-based assembly wiring where safely possible;
- add/upgrade `Antigravity.Main/smoke-tests/` first.

**Gate:** ribbon/load/representative command smoke parity with BLUE.

### Phase 7 — Physical Module Folder Restructure

Move modules in small groups only:

1. Shared/Core;
2. Host;
3. Modeling features;
4. Coordination features;
5. Documentation features;
6. Integrations/experimental modules.

For each add-in:

```text
record baseline
-> ensure local docs
-> ensure smoke-tests
-> move folder
-> repair project references
-> S0/S1 smoke
-> S2/S3 where applicable
-> only then continue
```

**Gate:** no batch proceeds with a failed module.

### Phase 8 — Module Boundary Stabilization

- enforce intentional logical dependency direction between Host, feature modules and Shared/Core without requiring matching disk folders;
- reduce feature-to-feature coupling;
- introduce contracts only where justified;
- no broad namespace rename merely for cosmetic consistency.

**Gate:** dependency map is intentional and documented.

### Phase 9 — TagArranger vNext

Canonical artifacts belong in:

```text
src/Antigravity.TagArranger/docs/
```

Smoke-test folder must be established before solver replacement.

Implement deterministic view-space engine, collision rules, single-transaction mutation, unresolved reporting, Plan/Section/Elevation support and debug/performance gates.

**Gate:** TagArranger S0-S4 plus its documented acceptance criteria pass.

### Phase 10 — Experimental/Stub Classification

- BIMLink;
- HatchPatterns;
- other incomplete modules.

Keep, isolate, or exclude from production packaging based on explicit status rather than deleting unfinished work.

### Phase 11 — Full Regression & Staging

- solution build;
- unit tests;
- per-add-in smoke suite;
- dependent-module smoke;
- Revit startup/ribbon load;
- critical command smoke;
- cold restart;
- staging release;
- rollback rehearsal.

**Gate:** no production promotion on partial PASS.

### Phase 12 — Production Promotion

- package immutable release;
- retain current and previous releases;
- switch active manifest only after staging PASS;
- update each changed module's `last-known-good.json` after production acceptance;
- keep BLUE archived/read-only until multiple stable releases prove GREEN.

---

## 10. Required Templates to Add in GREEN

Create reusable templates during Phase 2:

```text
docs/templates/addin/
  PLAN_TEMPLATE.md
  TECHNICAL_DESIGN_TEMPLATE.md
  ACCEPTANCE_TEMPLATE.md
  CLOSURE_REPORT_TEMPLATE.md
  SMOKE_README_TEMPLATE.md
  smoke-manifest.template.json
  last-known-good.template.json
```

Every template must contain an `Owner Add-in` field so orphan plans cannot be created accidentally.

---

## 11. Automation Guardrails

Future ChatGPT/CodexPro/agent workflow must enforce:

1. Determine owning add-in before creating a plan.
2. Reject add-in-specific canonical plans written only to `.ai-bridge`.
3. Allow `.ai-bridge/current-plan.md` only as temporary handoff material.
4. Require `src/<Addin>/smoke-tests/` before moving/refactoring a production/beta module.
5. Require baseline identity before destructive migration.
6. Do not overwrite last-known-good metadata until verified PASS.
7. Never commit smoke `results/` or generated Revit logs.
8. Never store rollback DLL binaries in Git; reference immutable release storage instead.
9. Never combine folder move + namespace rename + framework migration in one phase.
10. Never delete legacy files merely because a replacement exists; first prove smoke parity and rollback availability.

---

## 12. Promotion Matrix

| Gate | Requirement |
|---|---|
| Repository | clean/generated artifacts controlled |
| Plan ownership | all active add-in plans stored with owner add-in |
| Smoke ownership | every production/beta module has `smoke-tests/` |
| Baseline | every changed module has last-known-good identity |
| Debug build | PASS |
| Release build | PASS |
| Unit tests | PASS |
| Revit load | PASS |
| Ribbon registration | PASS |
| Representative commands | PASS |
| Feature-critical smoke | PASS for changed critical modules |
| Cold restart | PASS |
| Rollback rehearsal | PASS |
| Staging | PASS |
| Production promotion | explicit final gate only |

---

## 13. Definition of Done for the Restructure

The restructure is complete only when:

- GREEN is the canonical working repository;
- BLUE remains available as archived baseline until stability window is satisfied;
- generated/cache/release artifacts are separated from source;
- solution-level plans and add-in-level plans are clearly separated;
- no feature plan relies on `.ai-bridge` as its permanent home;
- every production/beta add-in owns a smoke-test contract;
- each changed add-in can identify a last-known-good release/commit;
- build/version matrix is explicit;
- Host composition root is simplified;
- physical module structure is intentional;
- full Revit staging acceptance passes;
- rollback has been rehearsed successfully;
- production deployment uses immutable releases rather than ad-hoc DLL overwrite.

---

## 14. Immediate Next Step

Do not create all add-in folders in BLUE.

Next execution step:

1. finish/create GREEN clone;
2. verify BLUE vs GREEN WIP preservation;
3. in GREEN, establish governance/templates;
4. migrate active TagArranger artifacts first as the proof of the new add-in-local documentation rule;
5. create TagArranger `smoke-tests/` baseline before any deterministic-engine implementation;
6. then repeat module-by-module.

This order ensures plan ownership and rollback safety are established **before** architecture changes start.
