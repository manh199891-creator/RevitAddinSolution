# DBR-4C — Physical Beam Strip Pairing

Status: APPROVED / READY_FOR_EXECUTION  
Date: 2026-08-27  
Approved by user: 2026-08-27  
Owner: `src/Antigravity.DrawBeams`  
Execution lane: Side Panel / ChatGPT Local Orchestrator only for production-source changes  
Continuation source: reviewed staged DBR-4B checkpoint `WF-ded08b56-05be-59c4-b83a-03683bd69a54`

## 1. Why DBR-4C exists

The repaired DBR-4B staged candidate loads and executes in Revit, but the latest real CAD smoke still shows wrong physical beam interpretation in dense linework: a pair of CAD boundary lines that should describe one beam can participate in competing hypotheses, causing wrong/duplicate beam selection and laterally shifted raw centerlines. A separate observed beam-end non-connection remains a structural topology concern and is not owned by DBR-4C unless a RED pure-recognition fixture proves otherwise.

Deep-research evidence and actual-source review are recorded in:

`src/Antigravity.DrawBeams/docs/reports/2026-08-27-dbr4b-s4-deep-research-beam-pairing-topology.md`

The architectural correction is geometry-first: recover a physical beam strip from coherent opposite faces first, then attach BxH/Mark semantics to the resolved strip. Do not continue solving the problem by broadening text radius, partner distance, overlap tolerance, endpoint snap tolerance or blanket coordinate deduplication.

## 2. Approved RequirementSpec

The user approved the research direction on 2026-08-27 and requested a canonical plan/roadmap update. The following requirements are the implementation contract.

- **R4C-01 — Two faces, one physical beam.** Two intended parallel boundary faces representing one beam must resolve to exactly one physical strip and one raw centerline.
- **R4C-02 — Competing parallel lines are globally resolved.** Three or more nearby parallel lines must not independently create adjacent false beams merely because several pairs satisfy local width/overlap rules.
- **R4C-03 — Geometry before semantics.** BxH/Mark text is assigned after a strip is geometrically resolved. Text must not be the primary generator of every anchor×partner hypothesis.
- **R4C-04 — Local semantic ownership.** One annotation in one local strip corridor should normally belong to one local beam strip. One-to-many assignment is allowed only when explicit continuous-run evidence supports it.
- **R4C-05 — Eligible face/provenance boundary.** Selected beam layers and retained CAD provenance define normal face eligibility. Unrelated linework must not become an opposite face merely because it is nearby/parallel; any fallback partner path must be explicit, bounded and test-covered.
- **R4C-06 — Preserve raw centerline invariant.** The raw centerline remains the projected-overlap centerline between the selected physical faces. DBR-4C may choose a better face pair, but must not laterally offset a correct centerline to force a Revit join.
- **R4C-07 — Single-line fallback is subordinate.** `SingleLineWithText` remains available for real single-line representations, but it must not survive as a peer duplicate when a stronger resolved double-face strip owns the same local annotation/span.
- **R4C-08 — Preserve existing safety boundaries.** Pipeline remains the staged DBR-4B default, Legacy `CadInteropService.ProcessScene(...)` remains the explicit rollback path, and no DBR-4C task promotes/lands production source or advances last-known-good.
- **R4C-09 — Topology is deferred.** Beam-end convergence, T-junction extension/snap, support-node splitting and Revit framing joins belong to DBR-5. `RevitBeamBuilder` and `CoordinateService` remain unchanged in DBR-4C unless a new isolated failing test proves a recognition-owned defect.
- **R4C-10 — Deterministic bounded solution.** Strip pairing must be deterministic under input reorder and use a bounded graph/component strategy. No ML dependency, no unbounded combinatorial search and no new third-party runtime dependency.
- **R4C-11 — Compatibility.** Preserve Revit 2024 / .NET Framework 4.8 compatibility and current units/coordinate conventions.
- **R4C-12 — Acceptance from real failure patterns.** Regression fixtures must model the structural geometry behind the latest stair/core, D5-E-4, D5-E-6C and D5-E-9 failure regions; tests must not encode screenshot pixels or drawing-specific screen coordinates.
- **R4C-13 — Owner-scoped verification.** S1 is the focused `tests/Antigravity.DrawBeams.Tests/Antigravity.DrawBeams.Tests.csproj` gate first. Formal S4 remains manual/staged under `src/Antigravity.DrawBeams/smoke-tests/`; solution-wide testing is supplementary unless explicitly requested.

## 3. Consensus planning result

### Planner

Split the problem into two phases rather than mixing recognition and Revit topology:

- DBR-4C owns physical face-pair/strip recognition and geometry-first semantic assignment.
- DBR-5 owns support/junction topology and Revit framing endpoint behavior.

Keep implementation inside the latest reviewed DBR-4B isolated lineage so the Legacy rollback boundary and unpromoted canonical tree remain intact.

### Architect — ACCEPT with constraints

- Reuse `CadSegment`, `BeamGeometryService`, `BeamCandidate`, `BeamCandidateEvidence`, `BeamCenterlineBuilder` and the existing resolver/pipeline rather than creating a second recognition runtime.
- Add a small beam-strip abstraction and deterministic face-adjacency/strip-selection layer inside `Antigravity.DrawBeams` only.
- Do not add `Antigravity.Core` coupling for recognition logic.
- Do not touch Revit transactions, `RevitBeamBuilder`, `CoordinateService`, family/type, level, offset, justification or mark placement in DBR-4C.
- Preserve the raw projected-overlap centerline as the recognition geometry authority.

### Critic — ACCEPT for user approval with hard gates

- Every new pairing rule must first be demonstrated by a RED fixture derived from a real failure pattern.
- The implementation must prove both false-positive suppression and preservation of legitimate adjacent beams.
- Text-first over-generation must be removed/narrowed rather than hidden by a later blanket dedupe.
- A solution that fixes the screenshot by widening tolerances fails the plan.
- Manual S4 is still required after automated PASS; DBR-4C automated success does not authorize promotion.

Consensus state: **APPROVED / READY_FOR_EXECUTION**. User approval is recorded; production-source implementation is authorized only through the Side Panel / Local Orchestrator execution lane defined by this plan.

## 4. Target architecture

```text
CadScene
  -> selected-layer/provenance filtering
  -> existing deterministic preprocessing
  -> continuous eligible beam faces
  -> BeamFaceAdjacencyGraph
  -> BeamStripResolver
  -> raw projected-overlap centerline per selected strip
  -> BeamStripSemanticMatcher (BxH / Mark after geometry)
  -> BeamCandidate / evidence compatibility layer
  -> existing CandidateConflictGraph + resolver safety net
  -> BeamRecognitionPipeline
  -> DBR-4B production router (staged only)

DBR-5 later:
accepted raw centerlines
  -> structural support/junction graph
  -> topology centerlines / common endpoints
  -> Revit placement and end-join verification
```

The DBR-3 conflict graph remains a safety layer for candidate conflicts that still exist after strip construction. DBR-4C does not replace or weaken DBR-3 score-led deterministic resolution.

## 5. Exact file map

### Create

- `src/Antigravity.DrawBeams/Models/BeamStrip.cs` — immutable/logically immutable representation of one selected physical face pair, its raw centerline, source IDs and geometry score/provenance.
- `src/Antigravity.DrawBeams/Services/BeamFaceAdjacencyGraph.cs` — builds bounded plausible opposite-face relations from eligible continuous `CadSegment` faces.
- `src/Antigravity.DrawBeams/Services/BeamStripResolver.cs` — deterministic component-level selection of mutually compatible physical strips.
- `src/Antigravity.DrawBeams/Services/BeamStripSemanticMatcher.cs` — assigns BxH/Mark after geometry using a bounded local centerline/span corridor.
- `tests/Antigravity.DrawBeams.Tests/Dbr4cBeamStripPairingTests.cs` — primary RED/GREEN fixture suite.
- `src/Antigravity.DrawBeams/docs/design/2026-08-27-dbr4c-beam-strip-recognition-boundary.md` — durable implementation design updated from actual implemented behavior before review closure.
- `src/Antigravity.DrawBeams/docs/acceptance/2026-08-27-dbr4c-physical-beam-strip-pairing.md` — automated + staged manual acceptance record.
- `src/Antigravity.DrawBeams/docs/reports/DrawBeams_DBR4C_Physical_Beam_Strip_Pairing_20260827.md` — implementation/review evidence.

### Modify

- `src/Antigravity.DrawBeams/Services/BeamCandidateGenerator.cs` — replace broad paired-edge anchor×partner over-generation with resolved strip input; retain explicit single-line/common-width fallbacks under stricter ownership rules.
- `src/Antigravity.DrawBeams/Services/BeamCandidateScorer.cs` — reuse existing evidence and only add strip-source evidence if required by RED tests; do not retune global score weights without a separate failing test.
- `src/Antigravity.DrawBeams/Services/BeamCandidateScoringOptions.cs` — only add a bounded geometry/semantic option when the new algorithm cannot derive it from existing width/overlap/angle contracts; every new option requires a named regression.
- `src/Antigravity.DrawBeams/Services/CandidateConflictGraph.cs` — preserve current DBR-3 semantics; modify only if strip provenance must be recognized as shared physical-source identity and a failing test requires it.
- `src/Antigravity.DrawBeams/docs/plans/ROADMAP.md` — reflect DBR-4C and DBR-5 phase boundary.
- `src/Antigravity.DrawBeams/PROJECT.md` and `src/Antigravity.DrawBeams/PROJECT_STATE.json` — durable resume state.

### Must remain unchanged in DBR-4C

- `src/Antigravity.DrawBeams/Services/RevitBeamBuilder.cs`
- `src/Antigravity.Core/Services/CoordinateService.cs`
- DBR-4B production routing boundary except for wiring the improved recognition result through the same Pipeline route.
- `src/Antigravity.DrawBeams/smoke-tests/baseline/last-known-good.json`

## 6. TDD implementation tasks

### Task 1: Lock real S4 failure classes as pure recognition fixtures
**Files:** Create `tests/Antigravity.DrawBeams.Tests/Dbr4cBeamStripPairingTests.cs`; read/retain `tests/Antigravity.DrawBeams.Tests/Dbr4bS4RecognitionRegressionTests.cs`.

**Requirements:** R4C-01, R4C-02, R4C-05, R4C-06, R4C-07, R4C-12.

**Steps:**
- [ ] Write RED fixture: exactly two intended parallel faces plus nearby distractor lines -> expected one strip/one centerline.
- [ ] Write RED fixture: three parallel lines A/B/C where only A/B are the physical beam -> B/C must not produce a second accepted beam.
- [ ] Write RED fixture: unrelated-layer parallel distractor -> not eligible without explicit fallback provenance.
- [ ] Write RED fixture: one BxH/Mark annotation near multiple anchors but inside one physical strip corridor -> one semantic owner.
- [ ] Write RED fixtures modeling stair/core and D5-E-4/D5-E-6C/D5-E-9 structural patterns without screenshot-specific pixel coordinates.
- [ ] Run focused test selection and verify the intended new cases FAIL while prior DBR-3/4A/4B regressions remain unchanged.
- [ ] Do not implement production code in this task beyond minimal fixture support.
- [ ] Commit only if the current execution policy or user explicitly authorizes a commit; otherwise record the reviewed checkpoint without committing.

### Task 2: Build the beam-face adjacency graph
**Files:** Create `src/Antigravity.DrawBeams/Models/BeamStrip.cs`, `src/Antigravity.DrawBeams/Services/BeamFaceAdjacencyGraph.cs`; modify `src/Antigravity.DrawBeams/Services/BeamCandidateScoringOptions.cs` only if required by a named RED test.

**Requirements:** R4C-01, R4C-02, R4C-05, R4C-06, R4C-10, R4C-11.

**Steps:**
- [ ] Keep Task 1 pair/distractor tests RED.
- [ ] Build graph nodes from eligible preprocessed continuous `CadSegment` faces using selected layer/provenance rules already carried by the scene.
- [ ] Create an edge only for a plausible opposite-face relation satisfying existing parallelism, width and projected-overlap contracts plus local span compatibility.
- [ ] Store deterministic edge identity and original source/provenance references.
- [ ] Do not use beam text to decide whether the geometric edge exists.
- [ ] Bound graph construction by orientation/spatial bands so unrelated remote lines are not compared globally.
- [ ] Run Task 1 graph-focused tests -> expected pair/eligibility cases PASS.
- [ ] Re-run existing fragment, support-gap, unequal-overlap and reorder tests -> PASS.
- [ ] Commit only if authorized; otherwise record the reviewed checkpoint.

### Task 3: Resolve graph components into physical beam strips
**Files:** Create `src/Antigravity.DrawBeams/Services/BeamStripResolver.cs`; modify `src/Antigravity.DrawBeams/Services/CandidateConflictGraph.cs` only if a RED provenance test proves necessary.

**Requirements:** R4C-01, R4C-02, R4C-06, R4C-10.

**Steps:**
- [ ] Write/retain RED cases where A/B and B/C are both locally plausible but only one physical strip is valid.
- [ ] Implement deterministic component-level strip selection with geometry/provenance score as the primary evidence.
- [ ] Enforce mutual exclusion when competing edges consume the same physical face over the same local interval unless an explicit shared-boundary fixture proves both are legitimate.
- [ ] Preserve distinct adjacent bays/spans that merely share alignment or dimension.
- [ ] Generate each selected strip raw centerline using the existing projected-overlap centerline builder; do not extend/snap to supports here.
- [ ] Verify input reorder produces identical selected strip identities and centerlines.
- [ ] Run focused tests -> PASS.
- [ ] Commit only if authorized; otherwise record the reviewed checkpoint.

### Task 4: Move BxH/Mark assignment after geometry
**Files:** Create `src/Antigravity.DrawBeams/Services/BeamStripSemanticMatcher.cs`; modify `src/Antigravity.DrawBeams/Services/BeamCandidateGenerator.cs` and, only when proven necessary, `src/Antigravity.DrawBeams/Services/BeamCandidateScorer.cs`.

**Requirements:** R4C-03, R4C-04, R4C-07, R4C-08, R4C-10.

**Steps:**
- [ ] Write RED semantic ownership cases before changing generator behavior.
- [ ] Match text to resolved strip centerlines inside a bounded local corridor and projected span.
- [ ] Rank geometry-local ownership deterministically; text no longer creates every anchor×partner pair.
- [ ] Suppress `SingleLineWithText` only when a stronger physical strip owns the same local annotation/span; preserve real single-line CAD representations.
- [ ] Keep common-width fallback as a lower-confidence path and prevent it from duplicating an already resolved physical strip.
- [ ] Preserve `BeamCandidateEvidence.FinalScore` as the existing candidate winner signal after strip construction; do not globally retune DBR-3 weights.
- [ ] Run semantic + legacy fallback + reorder tests -> PASS.
- [ ] Commit only if authorized; otherwise record the reviewed checkpoint.

### Task 5: Integrate with the staged DBR-4B pipeline without changing Revit topology
**Files:** Modify `src/Antigravity.DrawBeams/Services/BeamCandidateGenerator.cs`; verify existing `BeamRecognitionPipeline`, `BeamRecognitionRouter`, `CadInteropService` and `UI/MainWindow.xaml.cs` behavior; create/update `src/Antigravity.DrawBeams/docs/design/2026-08-27-dbr4c-beam-strip-recognition-boundary.md`.

**Requirements:** R4C-06, R4C-08, R4C-09, R4C-11.

**Steps:**
- [ ] Add a RED production-routing regression proving the Pipeline path consumes the improved strip-derived candidates while Legacy still calls `ProcessScene`.
- [ ] Wire strip-derived candidates through the existing generator/pipeline contract; do not create a second production router.
- [ ] Verify `AcceptedBeams` remains the only Revit creation projection.
- [ ] Verify `RevitBeamBuilder.cs`, `CoordinateService.cs`, transaction flow, family/type, level, offset, justification and mark placement are unchanged.
- [ ] Preserve bounded user-facing diagnostics and add strip/pair reason evidence only when needed for investigation.
- [ ] Run routing tests -> PASS.
- [ ] Commit only if authorized; otherwise record the reviewed checkpoint.

### Task 6: Regression, owner-scoped S1 and staged S4 evidence
**Files:** Test `tests/Antigravity.DrawBeams.Tests/Antigravity.DrawBeams.Tests.csproj`; create/update `src/Antigravity.DrawBeams/docs/acceptance/2026-08-27-dbr4c-physical-beam-strip-pairing.md` and `src/Antigravity.DrawBeams/docs/reports/DrawBeams_DBR4C_Physical_Beam_Strip_Pairing_20260827.md`.

**Requirements:** R4C-01 through R4C-13.

**Steps:**
- [ ] Run focused DrawBeams tests through the owner smoke contract -> PASS with zero failed tests.
- [ ] Re-run DBR-3 projected-overlap/conflict tests, DBR-4A parity/matcher tests and DBR-4B fragment/support-gap/layer-isolation tests -> PASS.
- [ ] Verify no broad tolerance/score-weight changes were introduced without named RED evidence.
- [ ] Stage the resulting DLL/dependencies only under `src/Antigravity.DrawBeams/smoke-tests/results/<workflow-id>/package/`; do not treat a Local Orchestrator worktree binary as canonical ownership.
- [ ] Manual S4 recognition recheck on the previously failing regions: no wrong pair, no double beam, no lateral centerline shift, expected BxH/Mark ownership.
- [ ] Record beam-end non-connection separately as DBR-5 evidence; it does not fail DBR-4C if raw centerline identity/span is correct.
- [ ] Keep formal production promotion and LKG blocked.
- [ ] Commit only if authorized; otherwise record the reviewed checkpoint.

## 7. Acceptance gates

### Automated gate

DBR-4C automated review is acceptable only when:

- all new beam-strip fixtures PASS;
- all prior DrawBeams focused regressions PASS;
- deterministic output is unchanged by segment/text input order;
- no unrelated solution failure is used to redefine the owner S1 result;
- no changes to Revit placement/topology files are present without a separately approved scope change.

### Manual staged gate

On the same representative CAD used in the latest S4 report:

- the two intended beam faces resolve as one physical beam;
- dense three-plus parallel line regions do not generate a second false beam;
- raw centerline lies midway between the correct pair and on the correct local span;
- BxH/Mark correspond to that strip;
- no duplicate physical beam is created for the same strip;
- non-connection at beam ends is recorded for DBR-5 rather than repaired by lateral axis movement.

Passing DBR-4C does **not** by itself close DBR-4B or promote the staged candidate. DBR-5 topology work and final S3/S4 production acceptance remain separate gates where required.

## 8. Rollback and failure behavior

- Legacy `ProcessScene` remains available as the narrow rollback implementation in the staged candidate.
- Canonical `E:\Antigravity\RevitAddinSolution` production source is not landed/promoted by DBR-4C planning or implementation workflows.
- `smoke-tests/baseline/last-known-good.json` remains unchanged until final human acceptance/promotion.
- If strip resolution reduces legitimate recall, rollback DBR-4C strip integration at the generator boundary; do not compensate by re-enabling broad anchor×partner enumeration silently.
- Unrelated dirty work must not be reset, cleaned, stashed, committed or absorbed.

## 9. Parallel execution assessment

Core Tasks 2-5 are dependency-ordered and should stay on one primary implementation lineage to avoid contradictory graph/semantic contracts. After Task 5 integrates, an independent supporting agent may run Task 6 evidence, adversarial fixtures and actual-source review in parallel with documentation finalization. Supporting evidence never becomes final reviewer authority.

## 10. Handoff after user approval

After explicit approval of this plan, compile it into one Local Orchestrator workflow continuing from `WF-ded08b56-05be-59c4-b83a-03683bd69a54`:

- Primary: CODEX — Tasks 1-5 and initial focused verification.
- Supporting: ANTIGRAVITY — Task 6 adversarial/regression evidence after primary integration.
- Final authority: ChatGPT/CodexPro actual-source review + manual staged S4 evidence.

User approval is recorded. The next authorized action is Side Panel / Local Orchestrator execution of this plan; canonical production promotion remains blocked until the later smoke and final-review gates pass.
