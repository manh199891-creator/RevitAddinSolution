# DrawBeams DBR-4B/S4 Deep Research — Beam Pairing, Centerline and Revit Topology

Date: 2026-08-27  
Status: RESEARCH COMPLETE / IMPLEMENTATION NOT STARTED

## Problem observed in real S4 evidence

The staged DBR-4B candidate reaches Revit and creates beams, but the latest real drawing still shows three materially different defect classes:

1. **Wrong face pairing / duplicate physical beam interpretation** — two CAD boundary lines that should define one beam can participate in competing beam hypotheses, creating an extra or wrong beam axis.
2. **Shifted beam axis/span** — a generated axis can be geometrically valid for the selected hypothesis but still be the wrong physical beam because the wrong face pair or wrong local span was selected.
3. **Beam ends do not converge at structural nodes** — recognition can generate plausible finite centerlines but their endpoints stop at projected face overlap instead of a shared support/junction node, so Revit cannot reliably form the intended framing join.

The first two are recognition/pairing problems. The third is a topology/end-node problem and should not be solved by widening recognition tolerances or blanket Revit geometry joining.

## Repository findings from the reviewed DBR-4B candidate

### A. Semantic text is introduced too early

`BeamCandidateGenerator` currently calls `BeamTextMatcher.FindMatches(anchor, allTexts)` for each anchor before final physical face pairing, then iterates all possible partners. `BeamTextMatcher` uses a 5000 mm search radius, accepts projected text positions from -0.5 to 1.5 of anchor length, and may therefore attach one beam annotation to several nearby parallel line anchors.

This makes text a generator of geometry hypotheses rather than primarily a semantic label for a geometry hypothesis. In dense structural drawings this increases competing pairs and single-line candidates.

### B. Partner search is intentionally broad

When beam layers are selected, anchor lines are filtered but `potentialPartners` remains all preprocessed scene segments. This helps with mixed/block geometry but allows a selected beam face to pair with unrelated nearby parallel linework if width/angle/overlap/text checks happen to pass.

### C. Global conflict resolution happens after over-generation

`CandidateConflictGraph` suppresses exact/shared-source and near-duplicate centerlines, but it operates after all anchor/text/partner combinations are generated. With three or more nearby parallel lines, multiple geometrically plausible strips may not share source identities and can survive as separate physical beams.

### D. Centerline endpoints are conservative projected-overlap endpoints

`BeamCenterlineBuilder` computes the centerline on the shared projected interval of a selected face pair. This is a good recognition primitive and should be preserved as the raw geometric axis, but it intentionally does not know columns, beam intersections, support nodes or continuation topology. Therefore it cannot guarantee that adjacent Revit beams share exact endpoints.

### E. Revit creation currently relies on raw recognized axes

`MainWindow` converts each accepted CAD beam axis directly into a Revit `Line` and `RevitBeamBuilder.CreateBeam` creates the framing instance. There is no structural node graph or endpoint reconciliation pass before creation, and no use of `StructuralFramingUtils` to validate/restore end-join allowance after placement.

## External evidence

### Structural CAD-to-BIM research

Yang et al. (Journal of Computing in Civil Engineering, 2020) propose a structural CAD-to-BIM beam workflow that is especially relevant to the observed failure. The method first modifies fragmented beam lines into continuous lines, groups adjacent parallel continuous lines into a beam graph, generates continuous beam centerlines, then associates drawing codes near the generated centerline, detects structural supports and finally splits the continuous centerline into individual beam axes at those supports. This ordering is the key design signal: **geometry grouping first, semantics second, support-aware axis segmentation third**.

Official paper: https://ascelibrary.org/doi/abs/10.1061/%28ASCE%29CP.1943-5487.0000885  
Public research copy with algorithm details: https://www.researchgate.net/publication/341081381_Semiautomatic_Structural_BIM-Model_Generation_Methodology_Using_CAD_Construction_Drawings  
Data/code repository referenced by the paper: https://github.com/WlbdW/Data-availability-of-A-semiautomatic-structural-BIM-model-generation-methodology-using-CAD-construc

### CAD topology / adjacency-graph research

Domínguez et al. show that pair-to-object mapping in vector floor plans is not generally bijective: consecutive objects can share segments and raw pair enumeration is ambiguous. Their Wall Adjacency Graph represents segments as graph nodes and geometric relations as edges before recovering wall objects and joint points. The same principle supports replacing local anchor×partner enumeration with an explicit beam-face adjacency/strip graph.

Reference: https://www.sciencedirect.com/science/article/pii/S0010448511003253

### Practical centerline extraction workflow

Current WebCAD/VJMAP centerline extraction documentation uses a staged sequence: split/merge line segments, identify parallel pairs, compute centerlines, merge disconnected centerline fragments, merge endpoints to nearby segments, then final connected-centerline merging. This reinforces separation between face-pair recognition and endpoint/topology reconciliation.

Reference: https://vjmap.com/app/docscad/en/geometry/centerline.html

### Revit framing behavior

Autodesk documents that structural framing joins are end-based. `StructuralFramingUtils.AllowJoinAtEnd` allows an end to join nearby framing and Revit framing elements are join-enabled by default. Revit documentation also states that beam-to-beam end joins occur when endpoints snap together. Therefore the primary requirement is to produce common structural endpoints; calling `JoinGeometryUtils.JoinGeometry` is not a substitute for framing-end topology because it is a solid-geometry join API.

References:
- https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/dab802b2-9731-94b6-3e56-f584d6f19676.htm
- https://help.autodesk.com/cloudhelp/2022/ENU/RevitLT-StructEng/files/GUID-ABA2701D-F760-4722-853B-75EB6B1F21DC.htm
- https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/c45b6484-3efd-1d81-0b47-ba678857fff1.htm

## Recommended architecture

Do not keep adding local tolerances to the current anchor-first pipeline. Introduce two explicit stages.

### Stage R — Beam Strip Recognition (DBR-4C candidate)

**Goal:** one physical double-line beam representation produces one raw beam axis.

1. **Eligible face extraction**
   - selected beam layers/provenance define eligible beam-face segments;
   - permit controlled fallback partners only through an explicit provenance/layer rule, not `allSegments` by default;
   - retain closed-polyline/global-width candidates as high-confidence direct evidence.

2. **Face normalization and continuous-face construction**
   - merge exact/contained/overlapping fragments only when they represent one physical face;
   - retain real support/bay boundaries;
   - preserve original source IDs in each continuous face.

3. **Beam-face adjacency graph**
   - node = continuous face segment;
   - edge = plausible opposite face relation satisfying parallelism, bounded perpendicular distance, projected overlap and local span compatibility;
   - edge score uses geometry/provenance first. Text is not required to create the edge.

4. **Strip selection / global pairing**
   - resolve the graph into physical beam strips rather than accepting every valid face pair;
   - competing edges that use the same physical face over the same local interval are mutually exclusive unless topology proves a valid shared-boundary case;
   - optimize deterministic coverage/score at component level;
   - a simple implementation can decompose collinear orientation bands and solve interval-local matching; no ML is required.

5. **Raw centerline generation**
   - preserve projected-overlap centerline as the raw centerline for each selected strip;
   - do not extend to supports in this stage.

6. **Semantic assignment after geometry**
   - match BxH/Mark to the generated raw centerline using a bounded corridor around the centerline and its projected span;
   - prefer one annotation -> one local beam span assignment;
   - allow explicit one-to-many only for clearly continuous same-mark runs;
   - `SingleLineWithText` becomes a fallback only when no strong double-face strip exists in the same local region, rather than a peer hypothesis generated for every annotated anchor.

This stage directly targets the user's annotation: **two boundary lines = one pair = one beam**.

### Stage T — Structural Topology / Junction Graph (DBR-5)

**Goal:** recognized beam axes terminate at common structural nodes so Revit can form intended joins.

1. Build a topology graph from accepted raw beam axes plus available support context.
2. Node candidates:
   - centerline intersections between accepted beams;
   - T-junction projections where an endpoint lies near another beam axis;
   - column/support centers or support regions when detectable from CAD/project context;
   - explicit bay/support boundaries retained by preprocessing.
3. Cluster node candidates with a small deterministic tolerance appropriate to CAD precision, not beam width.
4. Snap/extend only beam endpoints to the resolved structural node when topology evidence is strong.
5. Split a continuous beam axis at support nodes / annotation transitions when it represents multiple physical beam elements.
6. Never move the beam's lateral centerline to make a join; topology changes only longitudinal endpoints/splits.
7. Preserve both `RawCenterline` and `TopologyCenterline` in diagnostics so a bad topology correction is visible and reversible.

### Stage Rv — Revit placement verification

1. Create framing from `TopologyCenterline`.
2. After placement/regeneration, verify `StructuralFramingUtils.IsJoinAllowedAtEnd` for expected joined ends; use `AllowJoinAtEnd` only if an end is unexpectedly disabled.
3. Do not use `JoinGeometryUtils.JoinGeometry` as the primary beam-end connection mechanism.
4. Treat Revit cutback/setback appearance separately from axis topology. A visually shortened joined beam may be normal cutback behavior if location-curve endpoints are correct.

## Why this is safer than more tolerance tuning

The current failures are combinatorial/topological, not simply numeric precision failures. Increasing partner distance, text radius, overlap allowance or endpoint snap tolerance can reduce one miss while increasing wrong pairings and duplicates. The graph approach introduces the missing invariant: **a physical beam is an object recovered from a coherent pair/group of faces, not an independent hypothesis emitted by each individual line**.

## Proposed phase split

### DBR-4C — Physical beam-strip pairing hardening

Scope:
- face eligibility/provenance rule;
- beam-face adjacency graph;
- deterministic strip pairing;
- geometry-first semantic assignment;
- explicit suppression of `SingleLineWithText` when a strong paired strip owns the same local annotation;
- real-S4 fixtures for the stair/core and D5-E-4/D5-E-6C/D5-E-9 regions.

Exit criteria:
- two intended faces produce exactly one accepted beam;
- three-plus parallel lines do not create adjacent false beams;
- unrelated layer linework cannot become an opposite face without explicit fallback evidence;
- raw centerline remains centered between the correct faces;
- no regression to DBR-3/DBR-4A parity and current S4 fragment/support-gap tests.

### DBR-5 — Support/junction topology

Scope:
- structural node graph;
- T/X/beam-column node resolution;
- support-aware split/extend/snap;
- topology diagnostics;
- Revit end-join verification.

Exit criteria:
- beams that should meet share identical topology endpoints within deterministic tolerance;
- T-junction beam terminates on the hosting beam axis/support node;
- collinear beams remain split across real supports;
- no lateral centerline movement during topology repair;
- Revit end joins occur from correct endpoints without blanket geometry joining.

## TDD fixtures required from the latest screenshot

1. **Two-face/one-beam fixture**: exactly two intended parallel faces plus nearby distractor lines; expected one strip and one centerline.
2. **Three-parallel-line ambiguity**: A/B/C where only A/B are the beam faces; expected one selected strip, not A/B and B/C.
3. **Wrong-layer parallel distractor**: selected beam face with a close parallel line on an unrelated layer; expected no pair unless explicit fallback provenance allows it.
4. **Annotation ownership**: one BxH/Mark near two parallel anchors but inside one selected strip corridor; expected one semantic assignment.
5. **Lateral-centerline invariant**: topology stage cannot move the raw centerline perpendicular to its direction.
6. **T-junction**: secondary beam endpoint near primary beam axis; expected endpoint snap to one shared node.
7. **Crossing beams**: true non-collinear crossing without endpoint/support intent remains two independent beams.
8. **Support split**: continuous collinear axis crossing a real column/support node splits into two beam elements while preserving one geometric run.

## Recommendation

Do **not** close DBR-4B from the latest S4 image. Also do **not** continue patching DBR-4B with additional broad geometry/text tolerances.

Recommended next move:

1. Freeze the current staged candidate as evidence.
2. Implement a narrow DBR-4C geometry-first beam-strip pairing stage.
3. Re-run S4 specifically for wrong-pair/shift/duplicate regions.
4. Once raw beam axes are correct, implement DBR-5 topology for endpoint convergence and Revit joins.
5. Promote to canonical production only after S3/S4 pass with the revised strip + topology pipeline.

This sequencing keeps recognition identity and structural topology separate, which is consistent with both the existing DrawBeams roadmap intent and external CAD-to-BIM evidence.
