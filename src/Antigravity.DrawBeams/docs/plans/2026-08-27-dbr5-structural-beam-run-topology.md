# DrawBeams DBR-5 — Structural Beam-Run Reconstruction + Support/Junction Global Topology

Date: 2026-08-27
Status: APPROVED PLAN / IMPLEMENTATION NOT STARTED
Owner: `src/Antigravity.DrawBeams`

## Purpose

Complete the remaining structural-topology gap after DBR-4C raw beam recognition by reconstructing continuous beam runs, resolving support/junction topology globally, segmenting runs at real supports, and verifying Revit framing end behavior without weakening DBR-4C precision or inventing unsupported beams.

This plan is derived from the 2026-08-27 research report and Planner → Architect → Critic consensus.

## Preconditions

- DBR-0 through DBR-3 remain PASS / CLOSED.
- DBR-4A remains PASS / CLOSED.
- DBR-4B remains ACTIVE / REVIEW PENDING.
- WF-a593 DBR-4C actual-source review is PASS and staged for S4 revalidation.
- Current manual S4 evidence is not production acceptance; latest observed dialog is `Accepted 49; Preview warnings 133; Rejected 10` with visible missing/short beam spans.
- DBR-4C raw recognition remains immutable unless a new RED fixture proves the defect is recognition-owned rather than topology-owned.

## Non-goals

- Do not widen global pairing/text/overlap/snap tolerances.
- Do not use nearest-support distance as the ownership rule.
- Do not move a raw beam centerline laterally.
- Do not manufacture beams from unresolved DBR-4C physical-strip evidence.
- Do not use blanket `JoinGeometryUtils` as a topology repair.
- Do not introduce ML/GNN as a production dependency in DBR-5.
- Do not promote canonical production source or advance last-known-good during DBR-5 implementation.
- Do not touch unrelated dirty work.

## Architecture boundary

DBR-5 is inserted after DBR-3 has resolved beam candidates and before final mapping to placement `CadBeamData`:

`CadScene -> DBR-4C strip/candidate generation -> DBR-3 ResolveWithDiagnostics -> DBR-5 topology pipeline -> CadBeamData -> Revit placement`

`CadBeamData` remains the final placement-facing output. Topology inference uses dedicated intermediate models so raw recognition evidence is preserved.

## Mandatory models

### `RecognizedBeamSpan`

- stable recognition identity;
- immutable `RawCenterline`;
- width / height / mark / text semantics;
- physical-strip/candidate evidence;
- source segment/text provenance;
- DBR-3 resolution references.

### `StructuralSupportRegion`

- support kind: column, wall/core, beam run, explicit boundary, unknown;
- bounded geometry / reference axis / center;
- provenance;
- confidence;
- optional weak grid relation.

### `BeamRun`

- ordered recognized spans;
- observed intervals;
- explicit virtual continuation intervals;
- dominant axis/orientation;
- section/semantic compatibility evidence;
- continuation confidence.

### `TopologyNodeCandidate`

- candidate coordinate/region;
- node type;
- incident spans/runs/supports;
- evidence terms.

### `TopologyHypothesis`

- endpoint/run-to-node assignments;
- longitudinal extend/trim/split operations;
- score;
- hard-constraint state.

### `TopologyBeamSpan`

- immutable `RawCenterline`;
- resolved `TopologyCenterline`;
- start/end node IDs;
- support kinds;
- reason/evidence for every longitudinal change or split.

Only the resolved topology span maps to final `CadBeamData`.

## Hard invariants

1. `RawCenterline` is immutable.
2. Lateral/perpendicular centerline displacement is forbidden.
3. DBR-5 may only extend/trim longitudinal endpoints or split a logical run.
4. Virtual continuation requires recognized evidence on both sides plus bounded continuation/support evidence.
5. DBR-4C unresolved physical-strip evidence cannot be turned into a beam by topology convenience.
6. Source IDs and input order cannot determine structural ownership.
7. Equivalent/insufficient topology evidence fails closed.
8. Legitimate free/cantilever ends are allowed.
9. Revit auto-join behavior verifies topology; it does not define topology.
10. Legacy rollback remains available throughout DBR-5.

# DBR-5.0 — Failure Taxonomy + RED Fixtures

## Goal

Classify current real-S4 defects before production code changes and materialize deterministic fixtures from the stair/core and D5-E failure classes.

## Required taxonomy

Every observed defect must be classified as one of:

- `RAW_RECOGNITION_MISSING`
- `PHYSICAL_STRIP_AMBIGUITY`
- `TOPOLOGY_SHORT_ENDPOINT`
- `TOPOLOGY_FRAGMENTED_RUN`
- `SEMANTIC_BXH_MARK_ERROR`
- `REVIT_JOIN_OR_CUTBACK_ONLY`
- `UNKNOWN_NEEDS_EVIDENCE`

DBR-5 is allowed to address only topology-owned cases.

## Mandatory RED fixtures

1. fragmented paired beam through annotation/hatch gap -> one logical BeamRun;
2. fragmented beam through real column -> one run, split into two topology spans at one support node;
3. secondary beam endpoint -> exact hosting primary-beam node;
4. true X crossing without support intent -> independent members;
5. three-way intended node -> identical canonical node coordinate;
6. two equally plausible supports -> no auto-correction + one component warning;
7. legitimate cantilever -> preserved free end;
8. incompatible section/mark across collinear fragments -> no silent merge;
9. remote support outside bounded context -> cannot attract endpoint;
10. no lateral movement from DBR-4C raw axis;
11. source-ID permutation invariance;
12. input-order permutation invariance;
13. virtual continuation crossing blocking support -> invalid;
14. dense stair/core hatch rectangles -> no false column classification;
15. unresolved DBR-4C strip -> topology cannot manufacture beam;
16. correct location-curve topology with Revit cutback -> not classified as recognition/topology failure;
17. topology component complexity cap -> deterministic fail-closed diagnostic;
18. support/semantic transition -> BeamRun preserved but physical spans split correctly.

## Gate

No DBR-5 production source implementation starts until the RED fixtures are materialized and reviewed.

# DBR-5A — Structural Context Extraction

## Goal

Derive support context from normalized CAD evidence without modifying accepted beam geometry.

## Scope

- column/support regions from reliable polyline/block/provenance/layer evidence;
- wall/core regions only where evidence is sufficiently bounded;
- accepted beam axes as support context for secondary beams;
- optional grid/axis context as weak evidence only;
- no Revit API dependency.

## Safety rules

- rectangular shape alone is not enough to classify a column;
- hatch/annotation/core clutter must remain unknown unless corroborated;
- support regions retain provenance/confidence diagnostics;
- support extraction cannot create beam hypotheses.

## Exit criteria

- known support fixtures are recovered deterministically;
- dense stair/core false rectangles are rejected/unknown;
- source-ID/input-order permutation does not change support classification;
- no DBR-3/DBR-4C regression.

# DBR-5B — Continuous Beam-Run Reconstruction

## Goal

Group compatible accepted raw spans into logical continuous structural runs while preserving every observed and virtual interval separately.

## Continuation evidence

May use:

- collinearity / coincident projected axes;
- continuation of both physical faces when available;
- compatible measured width/section;
- bounded longitudinal gap;
- plausible support/occlusion explanation;
- compatible local BxH/Mark evidence;
- absence of contradictory blocking support.

Text can disambiguate an existing geometric continuation hypothesis but cannot create one from nothing.

## Exit criteria

- annotation/hatch fragmentation reconstructs one logical run when uniquely supported;
- real support/section transition does not get erased;
- incompatible or equivalent continuation evidence fails closed with `AmbiguousBeamRunContinuation`;
- all virtual intervals are explicitly diagnosable;
- no lateral centerline shift.

# DBR-5C — Topology Hypothesis Graph

## Goal

Generate explicit structural node/support hypotheses for run endpoints and intersections.

## Minimum relation types

- `BeamColumn`
- `BeamBeamT`
- `BeamBeamX`
- `CollinearContinuation`
- `BeamWallCore`
- `ExpectedFreeEnd`
- `Unresolved`

## Rules

- distance is one feature only;
- supports are typed regions/references, not nearest points;
- T/X/collinear relations are distinct;
- expected free end competes explicitly with support hypotheses;
- candidate relations are spatially bounded.

## Exit criteria

- all required relation fixtures generate only valid bounded hypotheses;
- remote supports do not appear in candidate sets;
- true X crossing is not automatically treated as support;
- ambiguity remains represented, not prematurely resolved.

# DBR-5D — Deterministic Global Topology Resolver

## Goal

Resolve each bounded connected topology component coherently rather than endpoint-by-endpoint.

## Objective rewards

- preservation of observed geometry;
- coherent support relationships;
- face/run continuity;
- reuse of one canonical structural node by intended incident members;
- compatible section/semantic continuity.

## Penalties

- virtual extension length;
- dangling endpoint despite uniquely strong support;
- contradictory connections;
- crossing nearer blocking support;
- invented geometry;
- unresolved/equivalent alternatives.

## Search strategy

Start with bounded deterministic component enumeration / branch-and-bound. Do not add OR-Tools/ILP unless profiling proves the internal bounded resolver insufficient.

Before search:

- decompose by spatial/orientation/support component;
- prune impossible relations;
- cap component complexity deterministically.

If complexity cap is exceeded, fail closed with `TopologyComplexityLimit`; do not select by heuristic shortcut.

## Exit criteria

- equivalent support alternatives emit one component warning and no auto-correction;
- all intended incident beams share identical node coordinates;
- ID/input-order permutations return identical topology;
- no raw centerline lateral movement;
- component complexity is bounded and deterministic.

# DBR-5E — Support-Aware Segmentation + Diagnostics

## Goal

Convert the winning logical run/topology into physical beam spans suitable for Revit placement.

## Rules

- split continuous runs at real column/support nodes;
- terminate secondary beam on hosting primary-beam topology node;
- keep collinear spans as separate physical Revit beams across real supports;
- preserve legitimate free/cantilever ends;
- preserve BxH/Mark ownership unless an explicit transition requires split;
- every topology-induced endpoint change/split retains reason/evidence.

## Diagnostics

At minimum:

- `AmbiguousBeamRunContinuation`
- `AmbiguousBeamSupport`
- `UnresolvedBeamEndpoint`
- `ExpectedFreeEnd`
- `TopologyExtendedToColumn`
- `TopologyConnectedToBeam`
- `TopologySplitAtSupport`
- `TopologyComplexityLimit`

Aggregate diagnostics by physical/topology component rather than producing duplicate warnings per local hypothesis.

## Exit criteria

- all topology fixtures produce correct `TopologyBeamSpan` outputs;
- unresolved components remain preview-only;
- shared topology nodes are coordinate-identical;
- no topology operation silently overwrites raw geometry.

# DBR-5F — Revit Placement + End-Join Verification

## Goal

Verify that correct pure topology produces intended Revit framing behavior.

## Sequence

1. map `TopologyBeamSpan` to final `CadBeamData`/Revit line;
2. create framing with existing builder boundary;
3. regenerate document;
4. verify expected location-curve endpoints;
5. inspect `StructuralFramingUtils.IsJoinAllowedAtEnd` where an end is expected to join;
6. use `AllowJoinAtEnd` only if unexpectedly disabled;
7. distinguish cutback/setback appearance from location-curve topology;
8. emit Revit-specific diagnostic if correct topology does not yield expected end behavior.

## Restrictions

- no blanket `JoinGeometryUtils.JoinGeometry` topology repair;
- no topology inference from rendered solid cutback alone;
- do not alter `CoordinateService` or `RevitBeamBuilder` unless isolated RED evidence proves a Revit-boundary defect.

## Exit criteria

- S2 load PASS;
- S3 safe-cancel PASS;
- expected beam-column/beam-beam topology endpoints verified in Revit;
- no duplicate framing instances;
- visible cutback does not create false topology failure;
- Legacy rollback remains intact.

# Acceptance metrics

Do not rely only on total Accepted/Warning/Rejected counts.

## Recognition metrics

- physical beam precision;
- physical beam recall;
- duplicate-beam rate;
- BxH/Mark ownership accuracy;
- raw centerline lateral error.

## Topology metrics

- intended support connection precision;
- intended support connection recall;
- wrong-support connection count;
- dangling endpoint count excluding `ExpectedFreeEnd`;
- shared-node coordinate consistency;
- virtual continuation count/length;
- topology ambiguity count by component.

## Revit metrics

- S2 load failures;
- S3 safe-cancel mutations;
- location-curve endpoint correctness;
- correctly joined expected ends;
- duplicate Revit framing instances;
- Revit-only join/cutback diagnostics.

Wrong beam identity or wrong support connection is more severe than an unresolved preview warning. Precision remains the safety priority.

# Implementation/verification contract

For every DBR-5 subphase:

1. start from reviewed WF-a593 lineage / approved baseline mechanism;
2. materialize tests before behavior change;
3. run add-in-scoped DrawBeams tests first;
4. preserve DBR-3, DBR-4A and DBR-4C invariants;
5. do not modify unrelated project dirt;
6. do not reset, clean, stash, commit, push, tag or advance LKG without explicit authorization;
7. update add-in-local plan/report/acceptance artifacts only;
8. require actual-source review before transient S4 staging.

# Promotion gate

DBR-5 implementation completion does not itself authorize production promotion.

Canonical landing / LKG advancement remains blocked until:

- DBR-5 pure topology tests pass;
- S2/S3 pass on staged candidate;
- real-S4 recognition and topology metrics are acceptable on the previously failing stair/core and D5-E regions;
- wrong-pair / wrong-support / duplicate regressions are absent;
- final actual-source and manual acceptance review pass;
- explicit promotion approval is given.

# Next action

Begin **DBR-5.0 only**: create the real-S4 failure taxonomy artifact and deterministic RED fixtures. Do not begin DBR-5A production implementation until DBR-5.0 receives review approval.
