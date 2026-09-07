# Agent Operating System — Phase 13 Revit Adoption

Date: 2026-08-31
Project: RevitAddinSolution
Status: FINAL_CLOSED / ACTIVE_CONSUMED / ARCHIFY_V2.15_BOUNDED_ACTIVE / HTML_OUTPUT_ACTIVE
Canonical owner: `E:\chatgpt-local-orchestrator`

## Adoption decision

RevitAddinSolution consumes Phase 13 Architecture Visualization from Local Orchestrator. It does not install Archify, add a local Archify skill, vendor a renderer, or create project-local visualization/runtime authority.

## Routing

Active `.agents/policies/capability-routing.md` now routes repository/system/workflow visualization to Local Orchestrator `architecture-visualization`.

The shared capability consumes Phase 8/source/workflow evidence and returns a projection. Revit agents must not infer topology merely to make a diagram look complete.

## Capability profile

`.agents/capability-profile.json` records:

- capability: `architecture-visualization`
- phase: 13
- owner: `local-orchestrator`
- status: `ACTIVE_CONSUMED_2026-08-31`

This is `sharedRuntimeOnly`; it is not added to the 11 project-local active skill directories.

## Artifact ownership

- Cross-solution architecture views: repository `docs/architecture/`.
- Owner-specific views: owning project docs/architecture area when introduced.
- Render cache/live preview: transient only.
- Generated diagram/HTML/SVG/PNG/WebM never replaces `PROJECT_STATE.json`, source code, plan, review evidence or Local Orchestrator state.

## External renderer boundary

Archify is active only from the bounded Local Orchestrator provider runtime; it is still not installed or vendored as Revit project-local authority.

Active render evidence:

- provider: `archify@2.15.0`
- current source snapshot: `0e6fbd8d993d06c9aeb4ac25a4090837d6606dbacb236c02c535554f17af81e1`
- current canonical IR hash: `9d4829f9fa8b4d193dbe1b1e2e6fef6fddb15a7a070a696ac60b3dfa95879207`
- architecture fingerprint: `c4c9c0a46b043cd2eb8488b4eceb0963b576d3c89d6e8ceef23d02e1aeafae9c`
- current projection: `23` nodes / `20` verified connections
- output: `docs/architecture/revit-addin-solution.archify.html`
- final HTML SHA-256: `9a0fd0be60d4b078e74da3b16ef7c939486f08b5fe7043f7614671f98cd8e428`
- output bytes: `655279`
- receipt: `docs/architecture/revit-addin-solution.architecture-receipt.json`
- topology inferred: false
- approved reviewed format: HTML

## Backend refresh behavior

RevitAddinSolution remains consume-only. Local Orchestrator now performs an Architecture Impact Check after successful workflow session closure for active consumers. The project does not run a filesystem watcher and does not gain project-local renderer authority.

- implementation-only/content changes that preserve the architecture fingerprint do not force Archify rendering;
- structural/project/dependency changes that alter the architecture fingerprint become stale and trigger a refreshed projection;
- `.csproj` `ProjectReference` relationships and local package dependencies are evidence-backed before they can appear as edges;
- generated Phase 13 IR/renderer-input/HTML/receipt files are ignored so automatic refresh does not dirty the Revit working tree solely due to generated projection output;
- current renderer layout is dependency-layered and retains `topologyInferred=false`.

The production auto-refresh runtime is loaded by restarting/reloading the Local Orchestrator Bridge. Observation of the first real session-closure classification is recorded when the next genuine Revit workflow closes; no synthetic Revit workflow is created solely to manufacture acceptance evidence.

Forbidden project-local adoption patterns remain:

- `.agents/skills/archify/`
- a local `archify` execution runtime treated as authority
- vendoring renderer code into an add-in project
- silently invoking `npx skills add`
- allowing renderer output to create source relationships or approvals

## Concurrent-work boundary

This adoption changes only governance/profile/report metadata. It does not modify DrawBeams production C#/XAML, `PROJECT_STATE.json`, model-write behavior, tests, or active feature implementation.

## Acceptance

Acceptance is provided by Local Orchestrator real-repository Phase 13 regression plus Revit solution regression. The project-local active skill set remains unchanged so Phase 12 exact skill-manifest/filesystem invariants continue to hold.

Final closure evidence is the real `ARCHIFY_RENDER_PASS` projection with 23 nodes / 20 verified connections, `topologyInferred=false`, architecture fingerprint `c4c9c0a46b043cd2eb8488b4eceb0963b576d3c89d6e8ceef23d02e1aeafae9c`, final HTML SHA-256 `9a0fd0be60d4b078e74da3b16ef7c939486f08b5fe7043f7614671f98cd8e428`, followed by a successful Local Orchestrator Bridge scheduled-task restart with automatic recovery re-enabled. Phase 13 adoption is FINAL_CLOSED; the first genuine workflow session-closure auto-refresh classification is future operational observation only and does not reopen this acceptance.
