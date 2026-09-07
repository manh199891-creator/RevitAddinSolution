# RevitAddinSolution Working-Tree Optimization Plan

Date: 2026-08-23
Scope: cross-solution repository hygiene, ownership, build semantics, agent resumability, test/packaging placement
Status: PROPOSED / REVIEWED

## 1. Objective

Bring the current repository from a successfully governed but still migration-heavy working tree to a stable, low-noise, agent-resumable repository without mixing cleanup with active DrawBeams, IssueManager, or dual-agent runtime feature work.

This plan does not reintroduce physical `Host/Shared/Features` grouping. Production projects remain flat under `src/Antigravity.*` unless a separately reviewed semantic move has clear value.

## 2. Current assessment

### Strong foundations already in place

- Project-local durable ownership through `PROJECT.md`, `docs/plans/ROADMAP.md`, project docs and smoke contracts.
- Cross-solution plans/reports/architecture under repository `docs/`.
- Root executable scripts moved into categorized `scripts/` folders.
- Repository governance validator passes with 32 indexed directories, one deferred no-csproj directory and zero errors.
- Solution build and focused automated tests pass.
- `.ai-bridge` is declared transient rather than durable project memory.

### Remaining structural debt

1. Tracked/generated Python bytecode still pollutes `.agents/runtime/**/__pycache__` even though ignore rules now exist.
2. `Directory.Build.props` unconditionally injects `Nice3point.Revit.Extensions` into every project, including contracts/core libraries with incompatible target frameworks.
3. Transient runtime areas and durable repository knowledge are still mixed: `.agent/reports`, `.agents/context`, `.brain`, `.ai-bridge`.
4. Test-only projects are physically inconsistent (`src/Antigravity.WallMepClash.Tests`, `src/Antigravity.ZoneSplit/tests`).
5. Installer/package ownership is inconsistent across `installer/`, `src/Antigravity.IssueManager.Installer`, `src/Antigravity.Installer`, and `packaging/`.
6. Legacy/fixture directories remain at root: `.codex-bcf-sample`, `TestFiles`, `scratch`, `Workflow`, `lib`.
7. Generated project docs contain more placeholder README/.gitkeep files than agents need for durable resume context.
8. Several ambiguous or obsolete cross-solution plans remain, including a two-line `docs/plans/test_codex.md` probe.
9. LOQN1 packaging/assembly/manifest naming remains a separate legacy risk.
10. MSB3270 processor architecture warnings and NU1701 package compatibility warnings remain build-hygiene debt.

## 3. Working-tree lanes

Do not treat the current dirty tree as one logical change. Preserve these lanes independently:

### Lane G — governance and repository organization

- `.agents/AGENTS.md`, policies, governance skills
- project-local PROJECT/ROADMAP/docs/smoke ownership
- repository docs/plans/reports/standards/templates
- root/script organization and validators

### Lane R — dual-agent runtime

- `.agents/runtime/*.py`
- `conpty_transport.py`
- runtime tests

Generated `__pycache__/*.pyc` is not part of Lane R and must be retired from versioned ownership.

### Lane DB — DrawBeams feature work

- Cad extraction/normalization models/services
- DBR tests
- encoding regression
- DrawBeams project-local plan/report artifacts

### Lane IM — IssueManager feature work

- Excel exporter
- CreateIssueDialog
- IssueManagerWindow
- project-local IssueManager technical artifacts

### Lane ZS — ZoneSplit compile guard

- `Antigravity.ZoneSplit.csproj` documentation-source exclusion only

Do not reset, stash, or overwrite one lane to clean another lane.

## 4. Optimization phases

### OPT-0 — Working-tree and transient-state hygiene

Priority: HIGH
Risk: LOW if allowlisted
Production behavior: none

Tasks:

1. Remove all historically tracked `.pyc` and `__pycache__` artifacts from durable repository ownership. Keep only Python source/tests.
2. Delete physical `.pytest_cache/` and other generated cache directories through the existing allowlisted cleanup path.
3. Add a validation gate that fails when generated Python bytecode is versioned/present in durable review scope.
4. Treat `.agent/reports/`, `.agent/state/`, and `.agent/context/` as generated runtime state. The runtime contract may continue writing there, but Git must not use those locations as durable evidence.
5. Review current `.agent/reports/FINAL_*` files. They refer to an older branch (`fix/dual-agent-pipeline-convergence`) and should either be archived as one durable historical report under `docs/reports/` or removed as stale generated output.
6. Remove stale `.agents/context/PLAN.md`, `TECHNICAL_DESIGN.md`, and `ACCEPTANCE_CRITERIA.md` after confirming their durable owner-local equivalents. The current PLAN is an old AutoFoundation snapshot and is not canonical.
7. Ignore `.ai-bridge/` as transient runtime state. Do not require it to exist in a clean clone.
8. Correct `Test-RepositoryGovernance.ps1`: if `.ai-bridge/current-plan.md` exists, validate it; if absent, do not fail repository governance. Runtime/handoff validation belongs in a separate runtime validator.
9. Review `.brain/` as a legacy AWF memory cache. Extract any unique still-valid knowledge into project ROADMAP/design/standards, then archive/remove `.brain` rather than maintaining two competing memory systems.
10. Delete the empty `{}` `.vscode/settings.json` unless a shared editor setting is intentionally added.

Acceptance:

- no generated `.pyc` in durable working-tree scope;
- clean clone does not require `.ai-bridge`, `.brain`, `.agent/reports`, or `.agents/context` to understand a project;
- governance validator passes without transient runtime directories;
- agent runtime tests remain green.

### OPT-1 — Build dependency semantics

Priority: HIGH
Risk: MEDIUM
Production behavior: dependency/build metadata only

Problem:

Root `Directory.Build.props` currently contains an unconditional:

```xml
<PackageReference Include="Nice3point.Revit.Extensions" Version="2024.0.0" />
```

This reaches non-Revit projects such as `Antigravity.HatchPatterns.Contracts` (`netstandard2.0`) and `Antigravity.HatchPatterns.Core` (`net10.0`), producing NU1701 warnings.

Tasks:

1. Introduce explicit project semantics, for example:

```xml
<IsRevitAddin>true</IsRevitAddin>
```

or an `AntigravityProjectKind` property.
2. Condition Revit-only package injection on that property rather than physical folder location.
3. Keep shared/contracts/core libraries free of Revit packages unless they have a real Revit boundary.
4. Consider `Directory.Packages.props` for package version centralization after dependency ownership is clean.
5. Define x64 policy for projects/tests that directly load Revit API references; do not force x64 onto pure contracts libraries.
6. Re-run restore/build/test and compare warning inventory before and after.

Acceptance:

- no Nice3point/Revit package leaks into pure contracts/core libraries;
- NU1701 count is reduced to zero where caused by global injection;
- processor-target warnings are explicitly resolved or documented per project;
- solution build and tests remain green.

### OPT-2 — Legacy root directory ownership

Priority: MEDIUM-HIGH
Risk: LOW/MEDIUM depending binary fixtures

#### `.codex-bcf-sample/`

No live repository references were found. Treat it as a likely BCF fixture, not agent state. If still useful, move to `tests/fixtures/bcf/codex-sample/`; otherwise archive/delete after content verification.

#### `TestFiles/`

Contains `LOQN1-PK00-B01-A-001.rvt` and is used by TagArranger smoke tooling. Replace the root convention with `tests/fixtures/revit/` plus a fixture manifest/acquisition note. If the RVT is too large for normal Git, use Git LFS or retain an external/local fixture contract instead of silently ignoring an indispensable file.

#### `scratch/`

- `bcf_inspect_gamuda/`: promote to a named BCF fixture only if it is reusable evidence; otherwise delete as extracted scratch data.
- `dump.ps1`, `ForceDeploy.ps1`, `TestAppend.cs`: no live references were found; review contents and retire one-off probes unless they implement a unique supported operation.

#### `Workflow/`

The current `0-orchestrate.md` references missing workflows such as `1-design`, `2-code`, `4-debug`, and `rules.md`, while current governance already lives in `.agents/skills`. Extract still-valid Revit API review/compatibility knowledge into active skills or `docs/standards/revit/`, update the one historical Autojoin reference if necessary, then retire the obsolete `Workflow/` tree.

#### `lib/ClashNavigator.dll`

No textual source reference to `ClashNavigator` was found. Do not delete the binary from filename evidence alone. Perform binary/manifest/deployment forensic review. If unused, archive then remove; if required, assign it to the owning project/package rather than keeping an ownerless root `lib/`.

Acceptance:

- root contains no ownerless legacy directory except intentional transient/tooling directories;
- reusable fixtures live under `tests/fixtures/` with documented ownership;
- one-off scratch probes are removed;
- required binary dependencies have an explicit owner.

### OPT-3 — Test and packaging normalization

Priority: MEDIUM
Risk: MEDIUM
Physical migration: perform only after active WIP lanes are protected, preferably in the structural/vNext lane

Tasks:

1. Move `src/Antigravity.WallMepClash.Tests` to `tests/Antigravity.WallMepClash.Tests`.
2. Move `src/Antigravity.ZoneSplit/tests/ZoneSplit.Tests` to a repository-level test project such as `tests/Antigravity.ZoneSplit.Tests`.
3. Review `installer/AntigravitySetup.cs` + `BuildInstaller.ps1`. The current installer embeds only a small legacy module set and should not be treated as the canonical current installer without revalidation.
4. Move active installer/package source to `packaging/` with explicit owners.
5. Move or recreate `Antigravity.IssueManager.Installer` under `packaging/IssueManager/` if it remains an active packaging project.
6. Resolve `src/Antigravity.Installer`: once installer ownership is established under `packaging/`, remove this no-csproj placeholder from `src` and from `docs/projects/PROJECTS.md` rather than presenting a non-project as a source project.
7. Update solution/project references, project dashboard and agent memory only after each physical move passes build/test.

Acceptance:

- `src/` contains production/shared/integration source projects, not test-only or packaging-only projects;
- `tests/` owns test projects;
- `packaging/` owns installers/packages;
- no deferred fake project remains solely to hold governance boilerplate.

### OPT-4 — Documentation footprint reduction

Priority: MEDIUM
Risk: LOW

Current mandatory project structure is useful but over-generated. Agents need a durable resume ladder, not dozens of empty placeholder documents.

Keep mandatory:

- `PROJECT.md`
- `docs/plans/ROADMAP.md`
- real dated plan/design/acceptance/report artifacts when they exist
- `smoke-tests/smoke-manifest.json`
- `smoke-tests/baseline/last-known-good.json`
- `smoke-tests/baseline/expected-behavior.md`
- `smoke-tests/results/.gitkeep` only when needed to materialize the generated-results directory

Review for removal/simplification:

- repeated `docs/plans/README.md`
- repeated `docs/design/README.md`
- repeated `docs/acceptance/README.md`
- repeated `docs/reports/README.md`
- placeholder `smoke-tests/scripts/README.md` and `fixtures/README.md` when there is no project-specific instruction
- redundant `.gitkeep` where a tracked README/file already materializes the folder

Move common explanations into `docs/templates/` and governance skills instead of copying the same prose into every project.

Also resolve cross-solution document noise:

1. Delete `docs/plans/test_codex.md` (`Hello, world!`) after final reference check.
2. Reclassify `CODEX_RESEARCH_PLAN.md`; it is a handoff/report pointing to the real UI consistency spec, not a durable implementation plan.
3. Review `CODEX_RESEARCH_PLAN_CAD.md`, `implementation_plan.md`, `Plan_WallProfiler_IntersectionEngine_v1.0.md`, and `Task_Apply_UI_Guidelines_v1.0.md` by content and assign a real owner, archive/report classification, or deletion.
4. Classify loose `docs/` root files such as `Fix_AppLogger_TypeLoad_Plan.md`, `MigrationGuide.md`, and historical project review material into plans/reports/guides/standards as appropriate.
5. Update generators and validators first, then remove boilerplate so the structure cannot regenerate unwanted noise.

Acceptance:

- an agent can resume a project from three primary files without scanning placeholder docs;
- no test/probe plan remains in canonical `docs/plans/`;
- generated project skeleton is minimal and meaningful.

### OPT-5 — LOQN1 legacy closure

Priority: MEDIUM
Risk: HIGH
Do separately from general cleanup.

Known issues include assembly/root namespace/manifest naming inconsistency and legacy binaries/build scripts mixed with source.

Tasks:

1. establish the actual Revit-loaded assembly and manifest contract;
2. reconcile csproj `AssemblyName`, namespace, `.addin` assembly path and build/deploy scripts;
3. classify checked-in DLL/deps artifacts as required compatibility payload vs generated output;
4. move durable plan/report material into the existing project docs tree;
5. only remove binaries after a real Revit load/command acceptance proves replacement behavior.

Do not combine this with framework migration or feature behavior changes.

### OPT-6 — Warning and build-matrix hardening

Priority: MEDIUM-LOW after OPT-1
Risk: MEDIUM

Tasks:

1. close remaining MSB3270 architecture mismatch warnings with explicit project/test platform policy;
2. define a truthful Revit version/build matrix rather than relying on mixed historical assumptions;
3. consider central package management after project dependency semantics are stable;
4. keep deploy disabled during normal verification;
5. separate S0/S1 automated evidence from S2/S3/S4 Revit-hosted evidence.

## 5. Recommended execution order

Execute in this order:

1. OPT-0 — transient/generated working-tree hygiene
2. OPT-1 — dependency/build semantics
3. OPT-2 — legacy directory ownership
4. OPT-4 — documentation noise reduction
5. OPT-3 — test/packaging physical moves after active feature WIP is isolated
6. OPT-5 — LOQN1 separate forensic closure
7. OPT-6 — build matrix and warning hardening

Rationale: first reduce noise and false dependencies, then decide ownership, then perform the few physical moves that have real semantic value.

## 6. Explicit non-goals

Do not:

- move all production projects under physical `Host/Shared/Features/...` directories;
- rename namespaces or assemblies merely to match folder organization;
- mix DrawBeams DBR work, IssueManager feature changes, agent-runtime changes and repository cleanup into one review unit;
- delete `.codex-bcf-sample`, `TestFiles`, or `lib/ClashNavigator.dll` without semantic/fixture/binary verification;
- promote `last-known-good` or S2/S3/S4 from build/unit-test evidence alone;
- reset/stash/clean unrelated dirty work as a shortcut.

## 7. Target steady-state tree

```text
RevitAddinSolution/
├── .agents/                    # durable governance, skills, runtime source
├── .ai-bridge/                 # local/transient, ignored
├── docs/
│   ├── projects/
│   ├── architecture/
│   ├── adr/
│   ├── plans/
│   ├── reports/
│   ├── specs/
│   ├── standards/
│   └── templates/
├── src/                        # flat production/shared/integration projects
│   └── Antigravity.<Project>/
│       ├── PROJECT.md
│       ├── production source
│       ├── docs/
│       │   ├── plans/ROADMAP.md
│       │   ├── design/
│       │   ├── acceptance/
│       │   └── reports/
│       └── smoke-tests/
├── tests/
│   ├── fixtures/
│   │   ├── revit/
│   │   ├── bcf/
│   │   └── cad/
│   └── Antigravity.*.Tests/
├── packaging/
│   ├── Antigravity/
│   └── IssueManager/
├── scripts/
│   ├── architecture/
│   ├── diagnostics/
│   ├── deploy/
│   ├── maintenance/
│   └── verification/
├── artifacts/                  # generated, ignored
├── Antigravity.sln
├── Directory.Build.props
├── Directory.Packages.props    # optional after OPT-1
├── .gitattributes
├── .gitignore
└── CHANGELOG.md
```

## 8. Definition of stable repository

The optimization is complete when:

- working-tree review contains no generated bytecode/cache noise;
- every durable artifact has one stable owner;
- transient agent/runtime state can be deleted without losing project knowledge;
- pure libraries do not inherit Revit-only dependencies accidentally;
- `src`, `tests`, and `packaging` have clear semantic boundaries;
- root has no ownerless legacy directories/files;
- project resume requires `PROJECTS.md -> PROJECT.md -> ROADMAP.md`, not chat history;
- build/test warnings are either resolved or explicitly owned technical debt;
- active feature WIP remains independently reviewable and rollback-safe.
