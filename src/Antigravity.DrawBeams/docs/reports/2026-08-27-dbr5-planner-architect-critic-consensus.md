# DrawBeams DBR-5 — Planner → Architect → Critic Consensus

Date: 2026-08-27
Status: CONSENSUS COMPLETE / PLANNING ONLY / NO PRODUCTION SOURCE CHANGE

## Inputs reviewed

- `src/Antigravity.DrawBeams/docs/reports/2026-08-27-dbr5-structural-topology-project-completion-research.md`
- `src/Antigravity.DrawBeams/docs/reports/2026-08-27-dbr4b-s4-deep-research-beam-pairing-topology.md`
- `src/Antigravity.DrawBeams/docs/plans/ROADMAP.md`
- WF-a593 actual-source evidence and staged DLL lineage.
- Latest real-S4 observation supplied by the user: `Accepted 49; Preview warnings 133; Rejected 10`, with visible beam spans still missing/short in representative dense regions.

The WF-a593 candidate remains staged evidence only. Final reviewed workflow evidence is 57/57 focused DBR-4C tests PASS, 250/250 owner tests PASS and DrawBeams build PASS. Manual S4 remains revalidation evidence, not production acceptance.

---

## Pass 1 — Planner

### Problem statement

DBR-4C has materially improved precision by failing closed on ambiguous physical strips, but the real drawing still contains missing/short beam spans and a high volume of `AmbiguousPhysicalStrip` preview warnings. Continuing to widen recognition tolerances risks reintroducing wrong-pair and duplicate-beam defects.

The next phase must recover structurally coherent beam extents without allowing structural assumptions to manufacture raw beam identity.

### Planner proposal

Implement DBR-5 as a structural topology reconstruction pipeline, not as nearest-endpoint snapping:

1. **DBR-5A Structural Context Extraction**
   - derive bounded column/core/wall/support evidence from normalized CAD context;
   - accepted DBR-4C beams can themselves become support context for secondary beams;
   - grid/axis evidence is optional and weak;
   - no Revit API dependency in the pure stage.

2. **DBR-5B Continuous Beam-Run Reconstruction**
   - group compatible accepted raw spans into candidate structural runs;
   - retain observed intervals separately from virtual continuation intervals;
   - never hide invented/bridged geometry.

3. **DBR-5C Topology Hypothesis Graph**
   - candidate relations include beam-column, beam-beam T, beam-beam X, collinear continuation, wall/core support, expected free end and unresolved endpoint;
   - distance is evidence only, never the ownership rule.

4. **DBR-5D Deterministic Global Topology Resolver**
   - resolve each connected topology component coherently;
   - preserve DBR-3/DBR-4C deterministic/fail-closed philosophy;
   - weak/equivalent alternatives remain unresolved with one component-level diagnostic.

5. **DBR-5E Support-Aware Segmentation**
   - split a continuous run at real support nodes;
   - terminate secondary beam at hosting primary beam topology node;
   - preserve legitimate free/cantilever ends.

6. **DBR-5F Revit Placement / Join Verification**
   - create Revit framing from resolved topology geometry;
   - verify join allowance and location-curve endpoints after regeneration;
   - distinguish Revit cutback/setback appearance from topology failure.

### Planner non-goals

- no global DBR-4C pairing/text/overlap/snap tolerance widening;
- no topology-based creation of a beam with no raw recognition evidence;
- no lateral centerline movement;
- no blanket `JoinGeometryUtils` repair;
- no ML/GNN dependency in the current production path;
- no canonical promotion/LKG advancement during DBR-5 implementation.

### Planner gate

Before implementation, create RED fixtures from the real stair/core and D5-E failure classes. Every defect must first be classified as recognition, topology, semantic, or Revit-only behavior.

---

## Pass 2 — Architect

### Primary architecture decision: topology seam

The current WF-a593 `BeamRecognitionPipeline` generates candidates, resolves them through DBR-3 and then maps resolved candidates relatively early to `CadBeamData`.

DBR-5 should be inserted **after DBR-3 `ResolveWithDiagnostics(...)` and before final mapping to `CadBeamData`**.

Target seam:

`CadScene -> DBR-4C candidate/strip generation -> DBR-3 resolved candidates -> DBR-5 topology pipeline -> final CadBeamData -> Revit placement`

This keeps topology close enough to raw evidence to retain centerline/provenance, while preserving `CadBeamData` as the placement/legacy-facing output model.

### Intermediate model boundary

Add topology-specific intermediate models instead of mutating `CadBeamData.Start/End` during inference.

#### `RecognizedBeamSpan`

Carries:

- stable recognition identity;
- immutable `RawCenterline`;
- section/mark/text semantics;
- physical-strip/candidate evidence;
- source segment/text provenance;
- DBR-3 resolution references.

#### `StructuralSupportRegion`

Carries:

- kind: column, wall/core, beam-run, explicit boundary, unknown;
- bounded geometry/reference axis/center;
- source provenance and confidence;
- optional grid relation.

A support is a region/reference, not just a nearest point.

#### `BeamRun`

Carries:

- ordered recognized spans;
- observed intervals;
- explicit virtual intervals;
- dominant axis/orientation;
- compatible width/semantic evidence;
- continuation evidence/confidence.

#### `TopologyNodeCandidate`

Carries:

- canonical candidate coordinate/region;
- node kind;
- incident spans/runs/supports;
- evidence terms.

#### `TopologyHypothesis`

Carries:

- endpoint/run-to-node assignments;
- required longitudinal extend/trim/split operations;
- score and hard-constraint state.

#### `TopologyBeamSpan`

Carries:

- immutable `RawCenterline`;
- resolved `TopologyCenterline`;
- start/end topology node IDs;
- support kinds;
- reason/evidence for every endpoint change or split.

Only this final topology result maps to placement `CadBeamData`.

### Geometry invariants

1. `RawCenterline` is immutable.
2. DBR-5 may change only longitudinal start/end coordinates or split a run.
3. Perpendicular/lateral displacement from raw axis is forbidden.
4. A virtual continuation must have raw recognized evidence on both sides plus bounded continuation/support evidence.
5. A DBR-4C unresolved physical-strip component cannot be converted into a beam merely because topology would be convenient.
6. Source IDs and input order are serialization/debug identities only, never structural evidence.

### Beam-run continuation evidence

A bridge between raw spans may use:

- collinearity/coincident projected axes;
- continuation of both physical faces when available;
- compatible measured/semantic width;
- bounded longitudinal gap;
- a plausible occluder/support between fragments;
- compatible local mark/BxH evidence;
- absence of a contradictory blocking support.

Text can disambiguate an existing geometric continuation hypothesis but cannot create one from nothing.

### Structural connection types

Minimum explicit topology types:

- `BeamColumn`
- `BeamBeamT`
- `BeamBeamX`
- `CollinearContinuation`
- `BeamWallCore`
- `ExpectedFreeEnd`
- `Unresolved`

Do not reduce these to a generic nearest-snap relation.

### Global resolver objective

Rewards:

- preservation of observed geometry;
- coherent support relationships;
- face/run continuity;
- reuse of one canonical node by all intended incident members;
- compatible section/semantic continuity.

Penalties:

- virtual extension length;
- dangling endpoint despite uniquely strong nearby support;
- contradictory connections;
- crossing a nearer blocking support;
- invented geometry;
- unresolved/equivalent hypotheses.

Initial implementation should use bounded deterministic component search/branch-and-bound rather than immediately adding an external ILP dependency. Introduce a solver abstraction only if profiling later proves necessary.

### Diagnostics architecture

Add topology-specific reasons and aggregate them by physical/topology component:

- `AmbiguousBeamRunContinuation`
- `AmbiguousBeamSupport`
- `UnresolvedBeamEndpoint`
- `ExpectedFreeEnd`
- `TopologyExtendedToColumn`
- `TopologyConnectedToBeam`
- `TopologySplitAtSupport`

Diagnostics must expose both raw and topology centerlines where applicable.

### Revit boundary

Pure structural-context/run/topology services must not depend on Revit API.

Revit-specific verification occurs only after topology is resolved:

1. map `TopologyBeamSpan` to placement line/data;
2. create framing;
3. regenerate;
4. verify expected join allowance/end state;
5. enable join only if unexpectedly disabled;
6. report location-curve topology separately from visible cutback/setback.

---

## Pass 3 — Critic

### Criticism 1: support detection can create a new false-positive class

Dense stair/core drawings contain hatch rectangles, shafts, blocks and annotation geometry. A naive rectangle detector could misclassify these as columns and attract beam endpoints incorrectly.

**Required correction:** support extraction must be provenance/layer/context aware, bounded, diagnostic, and independently fixture-tested. Unknown rectangles remain unknown. Support confidence cannot be promoted solely from rectangular shape.

### Criticism 2: BeamRun can over-merge structurally distinct spans

Collinear beams may have a real support, section transition, mark transition or legitimate gap. A generic collinear merge would erase physical element boundaries.

**Required correction:** BeamRun is a logical continuous route, not automatically one Revit beam. Preserve transitions/supports, then segment after topology resolution. Incompatible width/mark is negative evidence unless an explicit support/transition explains it.

### Criticism 3: topology could accidentally hide DBR-4C recall failures

If DBR-5 bridges large gaps too aggressively, missing recognition becomes invisible and structural assumptions become a second beam generator.

**Required correction:** DBR-5 cannot create a run when there is no recognized evidence on both sides. Large or unsupported virtual intervals fail closed. Any DBR-4C unresolved strip remains unresolved unless a separately approved recognition repair resolves it.

### Criticism 4: the rule “beam must connect” is not universally true

Cantilevers, edge beams and intentional terminations exist.

**Required correction:** `ExpectedFreeEnd` is a first-class topology outcome. The resolver must compare support hypotheses against free-end evidence rather than penalizing every dangling endpoint as invalid.

### Criticism 5: global search can become combinatorial

Dense core components can contain many candidate beams/supports.

**Required correction:** spatially bound candidate relations; decompose by orientation/bay/support component; prune impossible hypotheses before search; cap component search deterministically; if cap is exceeded, emit `TopologyComplexityLimit`/preview rather than choosing heuristically.

### Criticism 6: Revit visual beam ends can mislead acceptance

Revit cutback/setback can make a physically joined framing element appear short.

**Required correction:** DBR-5F acceptance must inspect location-curve endpoints/join state separately from rendered solid endpoints. Do not loop recognition/topology repair based solely on visual cutback.

### Criticism 7: the current S4 133-warning result needs component metrics, not dialog totals

Raw Accepted/Warning/Rejected counts are not enough to tell whether recall/topology improved safely.

**Required correction:** DBR-5 acceptance must separately measure physical recognition precision/recall, wrong-support connections, dangling endpoints excluding expected free ends, duplicate beam rate, BxH/Mark ownership, raw-centerline lateral error and shared-node coordinate consistency.

### Criticism 8: implementation must not begin with source refactoring alone

Introducing all new models/services before proving real failure ownership risks a large architecture change with weak acceptance.

**Required correction:** RED fixtures are the first implementation artifact. Each DBR-5 subphase must have a narrow exit gate and preserve all prior DBR-3/4A/4C tests.

---

## Consensus decisions

Planner, Architect and Critic converge on the following:

1. DBR-5 is **Structural Beam-Run Reconstruction + Support/Junction Global Topology Resolution**, not nearest-endpoint snapping.
2. DBR-5 begins only after a failure is classified as topology-owned; DBR-4C remains immutable unless a RED fixture proves recognition ownership.
3. The integration seam is after DBR-3 resolved candidates and before final `CadBeamData` mapping.
4. `RawCenterline` is immutable and topology can never move it laterally.
5. Continuous `BeamRun` reconstruction precedes support-aware segmentation.
6. Supports are typed regions/references, not nearest points.
7. Endpoint/run relations are resolved globally per bounded connected component.
8. Equivalent/insufficient topology evidence fails closed with one component-level warning.
9. Legitimate free/cantilever ends are first-class outcomes.
10. Revit auto-join/cutback behavior verifies topology; it does not define topology.
11. DBR-5 must not manufacture beams from unresolved DBR-4C physical-strip evidence.
12. ML/GNN remains a later optional sidecar, not a DBR-5 production dependency.
13. No canonical production promotion or LKG advancement occurs until revised S3/S4 and final review pass.

## Mandatory RED fixture set

Before production implementation, cover at least:

1. fragmented paired beam through annotation/hatch gap -> one logical BeamRun;
2. fragmented beam through real column -> one run, split into two topology beam spans at one support node;
3. secondary beam endpoint -> exact hosting primary-beam node;
4. true X crossing with no support intent -> independent members;
5. three-way intended node -> identical canonical node coordinate for all incident spans;
6. two equally plausible supports -> no auto-correction + one component warning;
7. legitimate cantilever -> preserved free end;
8. incompatible section/mark across collinear fragments -> no silent merge;
9. remote support outside bounded context -> cannot attract endpoint;
10. no lateral movement from DBR-4C raw axis;
11. source-ID permutation invariance;
12. input-order permutation invariance;
13. virtual continuation crossing a blocking support -> invalid;
14. dense stair/core hatch rectangles -> no false column classification;
15. unresolved DBR-4C strip -> topology cannot manufacture beam;
16. correct location-curve topology with Revit cutback -> not classified as recognition/topology failure;
17. component complexity cap -> fail closed, deterministic diagnostic;
18. support/semantic transition -> BeamRun preserved but physical spans split correctly.

## Recommended implementation order

- **DBR-5.0:** real-S4 failure taxonomy + deterministic RED fixtures only.
- **DBR-5A:** structural context/support extraction only.
- **DBR-5B:** BeamRun reconstruction only.
- **DBR-5C:** topology hypothesis graph only.
- **DBR-5D:** deterministic component resolver.
- **DBR-5E:** support-aware segmentation + topology diagnostics.
- **DBR-5F:** Revit placement/join verification.
- **DBR-6:** real-CAD production acceptance/promotion review.

Each subphase must be independently reviewable and preserve the Legacy rollback and unrelated repository dirt.

## Consensus verdict

**APPROVE FOR CANONICAL DBR-5 PLANNING. DO NOT START PRODUCTION IMPLEMENTATION UNTIL DBR-5.0 RED FIXTURES AND FAILURE TAXONOMY ARE MATERIALIZED.**
