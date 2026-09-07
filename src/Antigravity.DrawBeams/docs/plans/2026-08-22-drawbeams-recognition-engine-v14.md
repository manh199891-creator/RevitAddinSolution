# DrawBeams Recognition Engine V14 — Canonical Implementation Roadmap

**Owner add-in:** `Antigravity.DrawBeams`  
**Canonical location:** `src/Antigravity.DrawBeams/docs/plans/`  
**Status:** ACTIVE — DBR-0 through DBR-3 PASS / CLOSED; DBR-4A PASS / CLOSED; DBR-4B ACTIVE / REVIEW PENDING with staged candidate smoke revalidation required before canonical production promotion.  
**Source handoff:** `.ai-bridge/current-plan.md` may mirror this plan for execution but is not canonical.

## Objective

Refactor beam-line recognition from the large sequential heuristic in `CadInteropService.ProcessScene` into a vector-first candidate/evidence pipeline without changing Revit beam creation behavior prematurely.

## Safety / scope

- Preserve unrelated dirty work; no reset/clean/stash/commit/push/tag unless explicitly approved.
- Do not rewrite the module wholesale.
- Keep current production recognition available until the replacement pipeline is proven by deterministic tests and comparison fixtures.
- AutoCAD COM extraction stays separated from pure geometry/candidate logic.
- Vector geometry remains authoritative; image/AI detection is not the primary path.
- `MainWindow`, `GetCadBeams`, legacy `ProcessScene`, and `RevitBeamBuilder` remain callable until the production switch gate.

## Target architecture

```text
AutoCAD COM
  -> CadScene extraction + provenance
  -> pure CadScene normalization
  -> BeamCandidateGenerator
  -> BeamCandidateScorer
  -> CandidateConflictGraph
  -> BeamCandidateResolver
  -> BeamCenterlineBuilder
  -> CadBeamData
  -> existing RevitBeamBuilder
```

## Delivery sequence

### DBR-0 — Baseline + golden fixtures — CLOSED

- Characterize intentional legacy behavior before refactor.
- Cover parallel edges, reversed direction, unequal lengths, fragmented edges, closed rectangle polyline, polyline global width, BxH+mark text, candidate conflicts, no-text fallback, false positives, and support-gap merge behavior.
- Do not change production recognition.
- Keep known legacy defects frozen for later phases rather than silently fixing them in baseline work.

Closure evidence recorded in existing DrawBeams DBR-0 reports and tests. The authoritative baseline characterization is preserved for all later phases.

### DBR-1 — CAD extraction normalization — CLOSED

- Introduce backward-compatible entity provenance for `CadSegment` and `CadText`.
- Refactor `CadInteropService` toward entity-specific COM-boundary extraction helpers.
- Capture handle/id, parent block/entity handle, layer, color, linetype, lineweight, source kind, polyline identity/index/width when available.
- Property reads fail soft rather than discarding valid entities.
- Add bounded/cycle-safe nested block extraction with deterministic 2D transforms.
- Preserve parent provenance and stable identities through polyline/hatch expansion.
- Add pure `CadSceneNormalizer` outside COM loops for canonical orientation, bounded endpoint snapping, stable ordering, and provenance preservation.
- Keep the legacy recognition path active.

Reviewed closure: DBR-1 working-copy verification passed 75/75 focused tests with no production switch to the new candidate/resolver path. The Local Orchestrator approved-baseline snapshot is `912a14a7b52b04366d8d338aec87b07966569bcd` under `refs/local-orchestrator/approved/revit-addin-solution`; verified repository HEAD at closure was `a1b06f11af425853bd988d66e30a367ac3e99b00`.

### Pre-DBR-2 — Add-in-local artifact ownership — IMPLEMENTED / BASELINE REFRESH PENDING

Before DBR-2, DrawBeams owns its durable engineering artifacts locally:

```text
src/Antigravity.DrawBeams/
├── docs/
│   ├── plans/
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
    └── results/   # generated / ignored
```

DBR-0 and DBR-1 reports have been migrated out of repository-level `docs/reports/`. `.ai-bridge/current-plan.md` is now a transient pointer to this canonical plan. Before DBR-2, Local Orchestrator was extended with reviewed working-copy baseline overlays so only explicitly reviewed DrawBeams source/test paths are promoted while unrelated repository dirt stays excluded. The DBR-2 plan and implementation started from that reviewed continuation baseline.

### DBR-2 — Candidate generation + evidence scoring — PASS / CLOSED

1. Confirm the new workflow contains DBR-0 + DBR-1 source/tests before any scoring edits.
2. Make `BeamCandidateGenerator` the production-quality candidate source while keeping the legacy path available for comparison.
3. Generate candidates for:
   - polyline-width;
   - closed-pair/rectangle;
   - paired edges;
   - single-line + text;
   - fragmented paired edges;
   - common-width fallback.
4. Introduce `BeamCandidateEvidence` with explicit geometry, semantic, topology and penalty components.
5. Use adaptive width/angle/overlap tolerances instead of unrelated magic thresholds where practical.
6. Do not perform final global conflict resolution yet; DBR-3 owns resolver semantics.
7. Add deterministic tests and compare against DBR-0 legacy/golden expectations.

**DBR-2 gate:** all DBR-0 + DBR-1 tests remain green; candidate evidence is deterministic; legacy Revit creation behavior remains unchanged.

Closure evidence: focused DrawBeams suite 83/83 PASS; solution build PASS with 0 errors; `BeamCandidateGenerator` remains outside the production call path; report and acceptance evidence are stored under this add-in's `docs/reports/` and `docs/acceptance/`.

### DBR-3 — Conflict graph + global resolver + centerline — PASS / CLOSED

- Deterministic graph/component resolution replaced traversal-order `usedIds` semantics.
- `BeamCandidateEvidence.FinalScore` is the primary winner signal with deterministic tie-breaking.
- Non-collinear crossings are not conflicts by geometry alone.
- Paired-face centerline uses projected overlap; invalid overlap is rejected without legacy endpoint averaging.
- Final actual-source authority was granted through workflow `WF-db098295-4d71-bec8-4ff6-3e0e062b165c`; closure record followed in `WF-40622ddb-bb76-f7a3-1a16-6b98fcd7cd2a`.

### DBR-4A — Non-production integration + parity harness — PASS / CLOSED

- Composed DBR-1 through DBR-3 into the pure `BeamRecognitionPipeline` without production activation.
- Added deterministic legacy-versus-new parity categories, bounded explanations, multi-layer semantics, invariant keys and explicit invalid-input accounting.
- Replaced exhaustive matching with bounded polynomial max-cardinality/min-cost assignment and dense deterministic regression coverage.
- Final DBR-4A authority was granted after actual-source review of workflow `WF-e42bc8e2-437b-5bfa-e1b0-0ba90893ffcb`.

### DBR-4B — Production activation + preview diagnostics — ACTIVE / REVIEW PENDING / S4 BLOCKED

- A reviewed staged candidate routes `GetCadBeamRecognition` -> `RecognizeScene` -> `BeamRecognitionRouter`, Pipeline default with explicit Legacy `ProcessScene` rollback.
- Revit creation consumes only `AcceptedBeams`; preview/rejected diagnostics are bounded and excluded from creation.
- Automated S4 repair lineage ends at workflow `WF-ded08b56-05be-59c4-b83a-03683bd69a54`; focused DrawBeams supporting evidence reached 191/191 PASS and S2 Revit load PASS.
- Latest real CAD/Revit revalidation still FAILS physical recognition acceptance in dense linework: wrong/competing face pairs can produce duplicate hypotheses and laterally shifted raw centerlines. S3 remains PENDING and formal S4 remains open.
- Deep research recorded in `../reports/2026-08-27-dbr4b-s4-deep-research-beam-pairing-topology.md` concluded that further broad tolerance tuning is the wrong correction; physical beam identity must be resolved geometry-first.
- The candidate remains staged/unpromoted until DBR-4C, DBR-5 where required, S3/S4 and final review pass. Last-known-good remains `PENDING_REVALIDATION`.

### DBR-4C — Physical beam-strip pairing — APPROVED / READY_FOR_EXECUTION

- Recover a physical beam as a coherent strip from eligible opposite CAD faces before assigning BxH/Mark semantics.
- Build a bounded deterministic face-adjacency graph and component-level strip resolver so competing nearby parallel lines cannot independently become duplicate physical beams.
- Keep projected-overlap centerline as the raw geometric axis; DBR-4C may select the correct face pair but must not move the axis laterally to force a join.
- Assign text after strip resolution inside a bounded local centerline/span corridor; `SingleLineWithText` remains only a true fallback when no stronger paired strip owns the same local annotation/span.
- Preserve selected-layer/provenance eligibility, DBR-3 score-led conflict semantics, DBR-4A parity coverage, DBR-4B production router and Legacy rollback.
- Canonical plan: `2026-08-27-dbr4c-physical-beam-strip-pairing.md`.

### DBR-5 — Structural support/junction topology

- Start only after DBR-4C raw physical beam identity/centerlines are accepted on the real failing regions.
- Build structural node candidates from beam-beam intersections, T-junction projections, retained support/bay boundaries and detectable column/support context.
- Treat supports/junctions as graph nodes for endpoint snap/extend and support-aware split decisions.
- Do not merge aligned beams across structural support boundaries solely because centerlines align.
- Never move a beam laterally during topology repair; topology may change only longitudinal endpoints/splits from the accepted raw centerline.
- Verify Revit framing end-join allowance only after common topology endpoints are correct; do not use blanket geometry joining as a substitute for framing topology.
- Keep raw recognition and topology-adjusted centerlines separately diagnosable so rollback remains explicit.

### DBR-6 — Production acceptance

- Run DrawBeams unit tests and module build.
- Compare legacy vs new recognition on curated CAD fixtures and document intentional deltas.
- Real Revit acceptance: correct family/type BxH, centerline, level, offset, justification, mark, no duplicates.
- Real AutoCAD acceptance: LINE/POLYLINE/HATCH/TEXT/BLOCK, fragmented linework and multi-layer selection.
- Production completion requires deterministic evidence and rollback-safe migration.

## Smoke / rollback requirement

Before DBR-2 or any later phase changes production behavior, use `src/Antigravity.DrawBeams/smoke-tests/` as the module-local smoke contract.

Minimum required progression:

- S0: project/source/reference identity.
- S1: focused DrawBeams build/tests.
- S2: Revit assembly/manifest load where applicable.
- S3: representative command startup/safe-cancel.
- S4: real CAD -> recognition -> Revit creation critical path before production promotion.

The last-known-good identity must not be advanced from PENDING to PASS merely because compilation succeeds.

## Implementation contract

- Work in small reviewable phases.
- Keep canonical plan updates in this file.
- `.ai-bridge` may contain temporary status/diff/log copies only.
- Update `docs/design`, `docs/acceptance`, and `docs/reports` under this add-in when those artifacts are created or closed.
- Preserve the legacy path until the corresponding replacement gate passes.
- Run focused verification before handoff.
- Do not approve supporting-agent work as final reviewer authority; final review remains ReviewRuntime + ChatGPT.
