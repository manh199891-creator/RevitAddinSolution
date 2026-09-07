# DBR-3 — Conflict Graph + Global Resolver + Projected-Overlap Centerline

**Owner:** `Antigravity.DrawBeams`  
**Canonical path:** `src/Antigravity.DrawBeams/docs/plans/2026-08-23-dbr3-conflict-graph-global-resolver-centerline.md`  
**Status:** PASS / CLOSED  
**Execution lane:** Local Orchestrator implementation complete; final ChatGPT/CodexPro authority granted 2026-08-26  
**Production switch:** FORBIDDEN in DBR-3; DBR-4 owns production integration

## 1. Goal

Replace traversal-order-dependent candidate conflict handling with a deterministic conflict graph and component-level resolver, and build paired-face beam centerlines from projected geometric overlap rather than legacy endpoint averaging, while preserving the current production recognition path until DBR-4.

## 2. User-approved product / domain decisions

The user explicitly approved all four decisions below on 2026-08-23. These are part of the acceptance contract, not optional implementation suggestions.

1. **Precision before recall.** When evidence is ambiguous, prefer rejecting an uncertain candidate over creating a false or duplicate beam.
2. **Score-led winner selection.** `BeamCandidateEvidence.FinalScore` is the primary selection signal. `BeamCandidateKind`, provenance, source identity and stable IDs may be used only for deterministic tie-breaking / explainability; do not hardcode a blanket rule such as "Polyline always wins".
3. **Crossing beams are not conflicts by geometry alone.** Two non-collinear beams that merely intersect in plan are both valid unless they compete for the same source geometry, near-duplicate centerline, incompatible text/width claim, or another explicit conflict rule.
4. **Invalid projected overlap is rejected.** If paired faces have insufficient or unreliable projected overlap, reject the hypothesis at resolver/centerline validation; do not fall back to legacy four-endpoint averaging.

## 3. Baseline and safety gate

Before implementation starts inside Local Orchestrator:

- Read `src/Antigravity.DrawBeams/PROJECT.md`.
- Read `src/Antigravity.DrawBeams/docs/plans/ROADMAP.md`.
- Read `src/Antigravity.DrawBeams/docs/plans/2026-08-22-drawbeams-recognition-engine-v14.md`.
- Read DBR-2 plan/report/acceptance.
- Confirm the workflow baseline contains reviewed DBR-0/DBR-1/DBR-2 DrawBeams source, tests and durable docs while excluding unrelated repository dirt.
- Preserve the current last-known-good identity; do not promote PENDING smoke status merely from unit/build success.
- Run focused DrawBeams tests before editing; expected reviewed pre-DBR-3 baseline is 83/83 PASS.
- Build the solution before editing; expected gate is 0 errors.
- Do not reset, clean, stash, commit, push or tag unless the active workflow policy or user explicitly authorizes it.

If the approved continuation baseline is ambiguous or does not contain reviewed DBR-2 artifacts, stop implementation and report the baseline problem rather than layering DBR-3 on an uncertain source state.

## 4. In scope

### 4.1 CandidateConflictGraph

Introduce a pure deterministic graph representation over `BeamCandidate` hypotheses.

Conflict rules must cover at least:

- shared source geometry;
- duplicate candidate identity / equivalent hypothesis;
- near-duplicate collinear centerlines representing the same physical beam;
- incompatible width claims for the same competing physical hypothesis;
- incompatible text claims where candidates compete for the same semantic evidence;
- overlapping competing collinear hypotheses;
- conflicting candidates derived from the same fragmented source identity.

Graph requirements:

- pure C#;
- deterministic under input reordering;
- no AutoCAD COM;
- no Revit API;
- no dependence on original traversal order;
- explicit reason/evidence for conflicts where practical so DBR-4 diagnostics can consume it later;
- non-collinear crossing beams must not conflict solely because their centerlines intersect.

### 4.2 BeamCandidateResolver

Resolve candidates by conflict-connected component rather than legacy greedy `usedIds` traversal semantics.

Resolver requirements:

- deterministic for any input ordering;
- resolve each connected component independently;
- preserve all candidates belonging to independent non-conflicting components;
- use `BeamCandidateEvidence.FinalScore` as the primary winner signal;
- deterministic tie-break after score equality / tolerance, using stable domain attributes such as evidence quality, provenance completeness, source identity and stable candidate ID;
- do not introduce a renamed traversal-order greedy algorithm;
- do not hardcode universal candidate-kind dominance;
- precision-first: reject unresolved/unsafe ambiguity rather than emit duplicate/false positives;
- preserve explainability for why a candidate won/lost/rejected.

### 4.3 BeamCenterlineBuilder

Introduce a pure centerline builder for resolved paired-face hypotheses.

For paired edges/faces:

- determine a common beam axis;
- project both faces onto that axis;
- compute the valid intersection/overlap interval;
- build the centerline from that shared projected interval at the midpoint between faces;
- maintain stable orientation independent of segment direction;
- reject insufficient/invalid/non-finite overlap rather than falling back to legacy endpoint averaging.

Required legacy-defect correction:

`KDBR-001 — Unequal-length endpoint averaging`

Representative case:

- face A projected interval: `[0, 4000]`
- face B projected interval: `[500, 3500]`
- legacy endpoint averaging produces approximately `[250, 3750]`
- DBR-3 expected span is the projected overlap `[500, 3500]` or a mathematically equivalent centerline representation.

### 4.4 Resolved output / provenance

Resolved output must retain enough information for later DBR-4 integration and diagnostics, including at minimum:

- source candidate identity;
- final/evidence score;
- source segment IDs;
- source text ID when present;
- recognized width/height/mark when present;
- source/provenance information;
- conflict/resolution explanation or machine-readable reason where practical;
- finite geometry only.

Do not erase DBR-2 evidence in favor of a simplified resolved DTO that cannot explain the selection.

## 5. Explicitly out of scope

DBR-3 must not:

- switch `MainWindow` to the new recognition pipeline;
- switch `CadInteropService.GetCadBeams` to the new resolver;
- replace legacy `CadInteropService.ProcessScene(...)` as production authority;
- alter Revit beam creation behavior;
- broadly refactor `RevitBeamBuilder`;
- add production preview UI or confidence UI;
- add structural support/column/grid topology owned by DBR-5;
- repair support-gap merge semantics owned by DBR-5;
- remove or quarantine legacy recognition code;
- rewrite the DrawBeams module wholesale.

Production recognition must remain on the legacy path until DBR-4.

## 6. Planned file responsibilities

Exact names may be adjusted only when necessary to fit existing namespace conventions, but responsibilities must remain separated.

### Create

- `src/Antigravity.DrawBeams/Models/BeamCandidateConflict.cs`
  - conflict edge/reason representation without Revit/COM dependencies.
- `src/Antigravity.DrawBeams/Models/ResolvedBeamCandidate.cs`
  - resolution result retaining source candidate/evidence/provenance and resolved centerline.
- `src/Antigravity.DrawBeams/Services/CandidateConflictGraph.cs`
  - deterministic graph construction and connected-component discovery.
- `src/Antigravity.DrawBeams/Services/BeamCandidateResolver.cs`
  - deterministic component-level selection/rejection.
- `src/Antigravity.DrawBeams/Services/BeamCenterlineBuilder.cs`
  - projected-overlap centerline geometry.
- `tests/Antigravity.DrawBeams.Tests/CandidateConflictGraphTests.cs`
- `tests/Antigravity.DrawBeams.Tests/BeamCandidateResolverTests.cs`
- `tests/Antigravity.DrawBeams.Tests/BeamCenterlineBuilderTests.cs`

### Modify only if required by the minimal contract

- `src/Antigravity.DrawBeams/Models/BeamCandidate.cs`
- `src/Antigravity.DrawBeams/Models/BeamCandidateEvidence.cs`
- `src/Antigravity.DrawBeams/Services/BeamCandidateGenerator.cs`

Any modification outside this list requires an explicit scope explanation in the implementation report. Production entrypoints are not approved for modification.

## 7. TDD task plan

### Task 1: Conflict model and deterministic graph
**Files:** Create conflict model/graph tests and implementation.
**Steps:**
- [ ] Write RED tests for shared-source, duplicate/near-duplicate, incompatible text/width, fragmented-source, unrelated candidates and crossing-beam non-conflict.
- [ ] Run focused tests and verify the new tests FAIL for the expected missing behavior.
- [ ] Implement minimal deterministic graph construction.
- [ ] Add connected-component extraction with stable ordering.
- [ ] Run tests and verify PASS.
- [ ] Add input-reordering tests and verify graph/conflict reasons/components are stable.
- [ ] Review actual diff before moving to Task 2.

### Task 2: Global/component resolver
**Files:** Create resolver model/tests/implementation; minimally modify evidence/candidate model only if required.
**Steps:**
- [ ] Write RED tests proving legacy traversal order cannot determine the winner.
- [ ] Add RED tests for score-led winner selection, deterministic tie-break, precision-first rejection and independent component preservation.
- [ ] Include a three-parallel-line/competing-partner case covering the DBR-0 greedy `usedIds` defect.
- [ ] Implement minimal component-level deterministic resolver using `FinalScore` as the primary selection signal.
- [ ] Ensure candidate kind is not a blanket priority rule.
- [ ] Run tests and verify PASS under multiple input permutations.
- [ ] Review actual diff before moving to Task 3.

### Task 3: Projected-overlap centerline
**Files:** Create centerline tests/implementation.
**Steps:**
- [ ] Write RED test for KDBR-001 unequal-length faces with expected projected overlap.
- [ ] Write RED tests for reversed direction, invalid/no overlap, very short/insufficient overlap and non-finite geometry rejection.
- [ ] Implement pure projected-axis overlap centerline logic.
- [ ] Do not call/fallback to legacy four-endpoint averaging.
- [ ] Run tests and verify PASS.
- [ ] Review actual diff before moving to Task 4.

### Task 4: Resolver + centerline composition and provenance
**Files:** Resolver/resolved-output tests and minimal implementation integration.
**Steps:**
- [ ] Write RED tests that resolved output preserves score/evidence/source IDs/text/provenance.
- [ ] Verify fragmented-source identity survives resolution.
- [ ] Verify all output geometry/scores are finite.
- [ ] Compose winning candidates with `BeamCenterlineBuilder` without wiring production entrypoints.
- [ ] Run tests and verify PASS.
- [ ] Review actual diff before moving to Task 5.

### Task 5: Regression and non-production-switch gate
**Files:** Tests only unless a scoped defect is found.
**Steps:**
- [ ] Run all focused DrawBeams tests; DBR-0/DBR-1/DBR-2 regressions must remain green.
- [ ] Run solution build; require 0 errors.
- [ ] Search production references and prove `MainWindow` / `GetCadBeams` / `ProcessScene` still use the legacy recognition path.
- [ ] Confirm new resolver is not a production call-site before DBR-4.
- [ ] Inspect actual changed-file list and ensure unrelated dirty work was not absorbed.

### Task 6: Durable evidence
**Files:** DrawBeams-local docs only.
**Steps:**
- [ ] Create `src/Antigravity.DrawBeams/docs/reports/DrawBeams_DBR3_ConflictGraph_GlobalResolver_Centerline_20260823.md`.
- [ ] Create `src/Antigravity.DrawBeams/docs/acceptance/2026-08-23-dbr3-conflict-graph-global-resolver-centerline.md`.
- [ ] Update `ROADMAP.md` with implementation state/evidence, but do not mark DBR-3 PASS/CLOSED until final ChatGPT/CodexPro review.
- [ ] Do not promote smoke `last-known-good` to PASS without the required higher-level production evidence.

## 8. Required acceptance tests

At minimum, automated DBR-3 coverage must prove:

1. identical/equivalent candidates conflict or collapse deterministically;
2. candidates sharing source geometry conflict;
3. incompatible competing text claims conflict;
4. incompatible competing width claims conflict;
5. conflict graph is stable under input reorder;
6. resolver output is stable under input reorder;
7. resolver tie-break is deterministic;
8. stronger direct/high-evidence candidate beats weaker fallback when they conflict;
9. independent graph components preserve their valid winners;
10. three-parallel-line / competing-partner case is no longer traversal-order-dependent;
11. projected-overlap centerline fixes unequal-length endpoint averaging;
12. reversed face direction produces the same canonical centerline;
13. fragmented-source identity and provenance survive resolution;
14. resolved output contains no NaN/Infinity;
15. crossing non-collinear beams do not conflict merely because they intersect;
16. insufficient/invalid projected overlap rejects the hypothesis rather than using legacy averaging;
17. DBR-0 tests remain green;
18. DBR-1 tests remain green;
19. DBR-2 tests remain green;
20. production call path remains legacy `CadInteropService.ProcessScene(...)`.

Do not weaken existing tests or tune assertions merely to make the new implementation pass.

## 9. Completion / review gate

A Side Panel / Local Orchestrator run may report implementation complete only when it returns evidence for:

- workflow/run identity;
- isolated worktree/baseline identity;
- participating implementation/review agents;
- exact changed files;
- focused test command and result;
- solution build command and result;
- deterministic reorder test evidence;
- production-reference scan proving no DBR-4 switch occurred;
- known limitations / rejected ambiguous cases;
- implementation diff/review package when supported.

The agent run must stop at review handoff. It must not self-authorize DBR-3 closure.

Final authority remains ChatGPT/CodexPro review of the actual source/worktree and rerun verification. Only that final review may change DBR-3 from implementation-complete/review-pending to `PASS / CLOSED`.

## 10. Next phase boundary

After DBR-3 passes final review, DBR-4 may plan production integration + preview diagnostics. No production switch is authorized by this document.

## 11. 2026-08-26 final-conformance closure

DBR-3 is PASS / CLOSED. Final ChatGPT/CodexPro authority was granted after actual-source inspection of workflow `WF-db098295-4d71-bec8-4ff6-3e0e062b165c` and the later closure-record workflow `WF-40622ddb-bb76-f7a3-1a16-6b98fcd7cd2a`. Reviewed focused evidence was 131/131 DrawBeams tests PASS with authoritative build PASS. The durable product decisions remain precision before recall, `BeamCandidateEvidence.FinalScore` as the primary winner signal, non-collinear crossings not conflicting by geometry alone, and invalid projected overlap rejected without legacy endpoint averaging.

This closure does not itself authorize production promotion. DBR-4 owns production integration, smoke remains `PENDING_REVALIDATION`, and the reviewed DBR-4B candidate remains subject to the S4 promotion gate.
