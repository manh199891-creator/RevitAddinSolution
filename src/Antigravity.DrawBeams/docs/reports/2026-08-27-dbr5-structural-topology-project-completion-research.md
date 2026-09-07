# DrawBeams Completion Research — DBR-5 Structural Topology and Production Hardening

Date: 2026-08-27  
Status: RESEARCH COMPLETE / NO PRODUCTION SOURCE CHANGE / DBR-5 CONSENSUS PLAN RECOMMENDED

## Executive conclusion

The next DrawBeams phase should not be implemented as a local "snap every short beam end to the nearest support" patch. The stronger target is a deterministic structural-topology reconstruction stage that sits after DBR-4C raw recognition and before final `CadBeamData` placement.

Recommended target architecture:

`DBR-4C Physical Strip Recognition -> DBR-5A Structural Context -> DBR-5B Beam-Run Reconstruction -> DBR-5C Topology Hypothesis Graph -> DBR-5D Global Topology Resolver -> DBR-5E Support-Aware Segmentation -> DBR-5F Revit Join Verification -> DBR-6 Production Acceptance`

The core new idea is to reconstruct a *continuous beam run* before deciding where individual Revit beam elements start and end. Supports then split/terminate a run; they do not invent the run from scratch.

This preserves the current geometry-first/fail-closed design while adding the structural reasoning missing from the observed S4 result.

## Current project evidence

The reviewed WF-a593 DBR-4C candidate has actual-source review PASS and is staged for Revit revalidation. Its final owner evidence is 57/57 focused DBR-4C tests and 250/250 owner tests PASS, with DrawBeams build PASS. It remains unpromoted and must not advance last-known-good before required manual gates.

Latest manual S4 evidence shown on 2026-08-27 reports:

- Accepted: 49
- Preview warnings: 133
- Rejected: 10
- Many warnings are `AmbiguousPhysicalStrip`.
- The drawing still contains visible missing/short beam spans.

This result is materially different from the earlier wrong-pair problem. DBR-4C now fails closed much more often, but a production system still needs to recover structurally coherent spans without reintroducing unsafe geometric guessing.

## Research findings

### 1. Continuous beam reconstruction before support segmentation is supported by structural CAD-to-BIM research

Yang et al., *Semiautomatic Structural BIM-Model Generation Methodology Using CAD Construction Drawings* (Journal of Computing in Civil Engineering, 2020), explicitly treats concrete beams represented by fragmented/overlapped parallel lines by first modifying beam lines, identifying continuous beam graphs, generating continuous beam centerlines, then using supports/semantic information to split the continuous centerline into individual beam axes.

Relevant sources:

- https://ascelibrary.org/doi/abs/10.1061/%28ASCE%29CP.1943-5487.0000885
- https://www.researchgate.net/publication/341081381_Semiautomatic_Structural_BIM-Model_Generation_Methodology_Using_CAD_Construction_Drawings

Design implication: DBR-5 should reconstruct `BeamRun` objects first; endpoint snapping is a downstream operation.

### 2. Graph-based methods are a better fit than local pairwise heuristics

Domínguez et al. introduced a segment adjacency graph for vector floor-plan topology. More recent reviews identify graph-based approaches as the most topology-centric family and cite global relationship graphs between beams and load-bearing columns as a useful structural pattern.

Sources:

- https://www.sciencedirect.com/science/article/abs/pii/S0010448511003253
- https://www.mdpi.com/2673-4117/5/2/42

Design implication: DBR-5 should resolve a connected topology component globally rather than independently resolving each endpoint.

### 3. Grid/axis context is valuable but should be weak evidence, not a beam generator

The 2024 state-of-the-art review describes grid-based structural recognition as a common engineering-drawing method: columns cluster around grid intersections and beams often run between supports on grid axes. The older SINEHIR work recognized coordinate systems before structural objects and achieved high recognition rates on large real construction drawings.

Sources:

- https://www.sciencedirect.com/science/article/abs/pii/S0010448504002258
- https://www.mdpi.com/2673-4117/5/2/42

Design implication: add optional `GridAxisEvidence` for support/topology disambiguation. Do not create a beam merely because two columns/grid nodes can be connected.

### 4. Autodesk itself uses object-specific structural connectivity rules

Revit analytical automation distinguishes:

- main beam -> supporting wall/column;
- secondary beam -> supporting beam;
- trim/extend to produce connectivity;
- alignment to grids/levels as a separate rule stage.

Sources:

- https://help.autodesk.com/cloudhelp/2025/ENU/Revit-StructEng/files/GUID-3F23CA53-BC04-4708-8E63-D3E20EF634F9.htm
- https://help.autodesk.com/cloudhelp/2025/ENU/Revit-StructEng/files/GUID-BE343C51-5F71-488E-BFCA-5DB4D5CF1106.htm

Design implication: avoid one generic nearest-distance rule. Use explicit connection types such as beam-column, beam-beam T, beam-beam X, collinear continuation and legitimate free end.

### 5. Revit joining should validate topology, not replace it

`StructuralFramingUtils.AllowJoinAtEnd` only enables a framing end to join nearby framing. Autodesk documents that framing elements are join-enabled by default. Beam cutback/setback may make physical geometry appear visually short even when the location-line endpoint topology is correct.

Sources:

- https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/dab802b2-9731-94b6-3e56-f584d6f19676.htm
- https://help.autodesk.com/cloudhelp/2026/ENU/RevitLT-StructEng/files/GUID-4AC57D04-063C-4029-817E-8CF3D2B28984.htm

Design implication: first generate identical topology endpoints; then verify Revit join state. Do not use blanket `JoinGeometryUtils` or Revit auto-join behavior as recognition/topology logic.

### 6. Constraint/global optimization is useful, but an external solver is not required initially

Research on topology reconstruction shows that confidence, geometry and topology terms can be solved globally with constrained optimization. For DrawBeams the component sizes are likely small enough to start with deterministic bounded enumeration/branch-and-bound, reusing the DBR-3 philosophy.

Source:

- https://www.mdpi.com/2072-4292/14/18/4675

Design implication: define the objective/constraints now but defer OR-Tools/ILP dependency until profiling proves it is needed.

### 7. Vector-native graph ML is promising as a later semantic classifier, not the DBR-5 core

VectorGraphNET (2025) builds graph representations directly from vector technical drawings and reports strong line-level semantic segmentation performance on FloorplanCAD/TUM-style drawings. TopoGNN (2026) demonstrates structural topology prediction with a graph neural network trained from real architectural/structural drawing pairs.

Sources:

- https://ascelibrary.com/doi/full/10.1061/JCCEE5.CPENG-6508
- https://www.sciencedirect.com/science/article/pii/S0926580525006673

Design implication: preserve graph features/evidence so a learned classifier can be added later, but keep DBR-5 deterministic and explainable now. The current vector CAD input already provides higher-quality geometry than raster-first ML approaches.

## Important architecture gap in WF-a593

The current accepted pipeline maps resolved candidates relatively early to `CadBeamData`, whose durable placement fields are essentially Start/End, BxH, Mark, measured width and paired state. `BeamRecognitionDiagnostic` retains evidence/provenance, but `CadBeamData` itself does not preserve the intermediate topology state.

DBR-5 therefore should not overwrite `CadBeamData.Start/End` in-place during inference. Add topology-specific intermediate models and map to final placement data only after topology resolution.

Recommended conceptual models:

### `RecognizedBeamSpan`

- stable recognition identity
- `RawCenterline`
- width / height / mark
- physical-strip evidence
- source segment/text provenance
- confidence/diagnostic references

### `StructuralSupportRegion`

- support kind: column, wall/core, beam run, explicit boundary, unknown
- bounded geometry / reference axis / center
- source provenance
- confidence
- optional grid relation

### `BeamRun`

- ordered recognized spans
- observed intervals
- virtual continuation intervals
- dominant axis/orientation
- compatible section/semantic evidence
- continuation confidence

### `TopologyNodeCandidate`

- candidate point/region
- node kind: beam-column, T, X, collinear support, beam-wall/core, free-end
- incident runs/spans
- evidence terms

### `TopologyHypothesis`

- endpoint/run -> node assignment
- extension/split operations
- score
- hard-constraint violations

### `TopologyBeamSpan`

- immutable `RawCenterline`
- `TopologyCenterline`
- start/end node IDs
- support kinds
- reason/evidence for any longitudinal extension or split

Only `TopologyBeamSpan` should be mapped to final `CadBeamData` used by Revit placement.

## Recommended DBR-5 phase split

### DBR-5A — Structural Context Extraction

Goal: derive support context without changing accepted beam geometry.

Inputs:

- normalized `CadScene` segments/text/provenance;
- accepted DBR-4C beam strips/spans.

Detect bounded evidence for:

- closed rectangular/polygonal column candidates;
- column-like block/polyline provenance;
- wall/core support regions where reliable;
- accepted beam axes usable as beam supports;
- optional grid axes/intersections as weak context.

Important: current `CadScene` only exposes segments + text, so support recognition must either reconstruct closed support geometry from provenance-preserving segments or add a backward-compatible structural-context extraction representation. Do not make AutoCAD/Revit APIs part of the pure topology resolver.

### DBR-5B — Continuous Beam-Run Reconstruction

Group DBR-4C raw spans/fragments into potential continuous structural runs using bounded evidence:

- collinearity;
- two-face continuity where available;
- compatible width/section;
- small longitudinal gaps;
- support/occlusion explanation;
- local semantic continuity;
- no contradictory geometry/support.

Store every unobserved bridge as a virtual interval. Never silently replace raw geometry.

### DBR-5C — Topology Hypothesis Graph

Generate explicit candidate relations:

- beam endpoint -> column/support region;
- secondary beam endpoint -> primary beam axis;
- collinear run continuation;
- X intersection;
- T junction;
- legitimate free end;
- unresolved endpoint.

Distance is one feature, never the ownership rule.

### DBR-5D — Deterministic Global Topology Resolver

Resolve each connected topology component coherently.

Suggested objective rewards:

- preservation of observed geometry;
- support consistency;
- face/run continuity;
- reuse of one structural node by incident beams;
- compatible semantic/section continuity.

Suggested penalties:

- virtual extension length;
- unsupported dangling endpoints where a strong support exists;
- contradictory connections;
- crossing a nearer blocking support;
- invented geometry;
- ambiguity.

Hard invariants:

- lateral centerline shift = 0;
- each physical endpoint has at most one selected topology target;
- no support target can be selected only because it is lexically/ID-ordered first;
- source IDs/input order cannot change the physical result;
- weak/equivalent evidence -> fail closed + topology warning;
- topology cannot create a beam from no raw geometric evidence.

### DBR-5E — Support-Aware Segmentation

After the winning beam run/topology is known:

- split continuous run at real column/support nodes;
- terminate secondary beam at hosting primary beam node;
- retain true collinear spans as separate Revit beam elements across real supports;
- preserve legitimate free/cantilever ends;
- preserve BxH/Mark ownership unless an explicit semantic transition requires a split.

### DBR-5F — Revit Placement and Join Verification

Keep Revit-specific behavior outside the pure graph resolver.

Verification sequence:

1. map `TopologyBeamSpan` to final `CadBeamData`/Revit line;
2. create framing;
3. regenerate;
4. check expected end join allowance via `StructuralFramingUtils.IsJoinAllowedAtEnd`;
5. call `AllowJoinAtEnd` only when unexpectedly disabled;
6. distinguish location-curve topology from visible cutback/setback;
7. report a Revit-specific diagnostic if a geometrically correct topology does not result in the expected framing behavior.

## Human-in-the-loop / production usability

The 133-preview-warning S4 result shows that a production-quality tool should not depend on forcing every uncertain case into automatic acceptance.

Recommended acceptance bands:

- **AutoAccept:** high-confidence physical strip + topology.
- **PreviewReview:** structurally plausible but ambiguous topology/strip.
- **Reject:** invalid or unsupported candidate.

A future preview should ideally expose graph-level reasons such as:

- `AmbiguousPhysicalStrip`
- `AmbiguousBeamRunContinuation`
- `AmbiguousBeamSupport`
- `UnresolvedBeamEndpoint`
- `ExpectedFreeEnd`
- `TopologyExtendedToColumn`
- `TopologyConnectedToBeam`
- `TopologySplitAtSupport`

The system should aggregate warnings by physical/topology component, not produce hundreds of near-duplicate messages for one structural region.

This is consistent with the practical semi-automatic direction in structural CAD-to-BIM literature: automate high-confidence structure and make ambiguity inspectable instead of guessing.

## Required RED fixtures before implementation

At minimum:

1. fragmented two-face beam through annotation gap -> one `BeamRun`;
2. fragmented beam across column -> one run, two topology beam spans split at one column node;
3. short secondary beam -> endpoint terminates exactly on primary beam topology node;
4. true X crossing -> remains independent unless support intent exists;
5. three-way beam node -> all intended incident spans share one node coordinate;
6. two equally plausible nearby supports -> accept neither correction and emit one component warning;
7. legitimate cantilever/free end -> no forced extension;
8. same-axis fragments with incompatible width/mark -> do not merge without explicit transition evidence;
9. remote support -> cannot attract endpoint outside bounded context;
10. no lateral movement of DBR-4C raw centerline;
11. source-ID permutation -> identical topology;
12. input-order permutation -> identical topology;
13. virtual continuation crosses a blocking support -> invalid hypothesis;
14. column/core dense linework -> support extraction does not classify arbitrary hatch rectangles as columns;
15. DBR-4C unresolved physical faces -> DBR-5 cannot use topology to manufacture a beam;
16. Revit cutback appearance -> correct topology endpoints must not be reported as raw recognition failure.

The current stair/core and D5-E real-S4 regions should be converted into deterministic vector fixtures wherever possible.

## Metrics for completion

Do not use only total Accepted/Rejected counts.

Track separately:

### Recognition metrics

- physical beam precision;
- physical beam recall;
- duplicate-beam rate;
- BxH/Mark ownership accuracy;
- raw centerline lateral error.

### Topology metrics

- intended support connection precision;
- intended support connection recall;
- dangling endpoint count excluding legitimate free ends;
- wrong-support connection count;
- shared-node coordinate consistency;
- unsupported virtual-extension length/count;
- topology ambiguity count by connected component.

### Revit metrics

- S2 load failures;
- S3 safe-cancel mutation count;
- correctly joined beam-column/beam-beam ends;
- duplicate Revit framing instances;
- location-curve endpoint correctness independent of visual cutback.

For production acceptance, wrong beam/wrong support connections should be treated as more severe than unresolved warnings. Precision remains the safety priority; topology should improve recall without undoing DBR-4C fail-closed behavior.

## Recommended completion roadmap

### Immediate

1. Preserve WF-a593 as the staged DBR-4C evidence candidate.
2. Complete manual DBR-4C S4 classification of failures into:
   - raw recognition missing;
   - physical strip ambiguity;
   - correct raw axis but short endpoint/topology;
   - semantic BxH/Mark error;
   - Revit-only join/cutback behavior.
3. Do not tune global DBR-4C tolerances based on endpoint failures.

### Then

4. Write a formal DBR-5 consensus plan using DBR-5A..5F above.
5. Build RED fixtures first from the current S4 regions.
6. Implement pure structural-context/run/topology stages without Revit API dependencies.
7. Add Revit verification only after pure topology fixtures pass.
8. Re-run S2/S3/S4 with explicit recognition-vs-topology metrics.

### DBR-6 — Production Acceptance

DBR-6 should close only when:

- real CAD beam identity/semantics are acceptable;
- topology connections are acceptable;
- unresolved cases are inspectable and bounded;
- Revit placement/join behavior is verified;
- Legacy rollback still works;
- no unrelated project dirt is included;
- canonical landing and LKG advancement receive explicit approval.

### Optional post-production evolution

After deterministic DBR-5/DBR-6 closure, consider a learned vector-graph semantic classifier as a sidecar for difficult layer/provenance classification. Do not make ML/GNN a blocker for current production completion.

## Decision recommended

Proceed with DBR-5 as **Structural Beam-Run Reconstruction + Support/Junction Global Topology Resolution**, not as nearest-endpoint snapping.

Before implementation, run one Planner -> Architect -> Critic consensus pass over this research and the latest WF-a593 S4 evidence. The implementation plan should keep DBR-4C raw recognition immutable unless a new RED fixture proves the defect belongs to recognition rather than topology.
