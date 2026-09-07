# DrawBeams DBR-2 — Candidate Generation + Evidence Scoring

Date: 2026-08-23  
Owner: `src/Antigravity.DrawBeams`  
Status: PASS / CLOSED

## Goal

Make `BeamCandidateGenerator` a deterministic, production-quality candidate source with explicit evidence/scoring, while preserving the legacy `CadInteropService.ProcessScene(...)` comparison path and existing Revit creation behavior. DBR-3 conflict resolution and DBR-4 production pipeline switching remain out of scope.

## Baseline gate

DBR-2 must start from the reviewed continuation baseline containing:

- DBR-0 authoritative golden behavior/tests;
- DBR-1 provenance-preserving CAD extraction and `CadSceneNormalizer`;
- canonical DrawBeams-local `docs/` and `smoke-tests/` structure;
- DrawBeams focused tests green before DBR-2 edits.

The working-copy continuation snapshot used for DBR-2 must contain only explicitly reviewed DrawBeams source/test paths; unrelated repository dirt must remain excluded.

## Scope

### 1. Candidate evidence model

Introduce a backward-compatible `BeamCandidateEvidence` model owned by DrawBeams. At minimum expose deterministic components for:

- geometry evidence: parallelism/alignment, overlap, measured-width agreement;
- semantic evidence: matched BxH text, text distance/orientation, mark presence where applicable;
- topology/source evidence: closed-polyline grouping, explicit polyline width, provenance/source kind, fragmented-source support;
- penalties: weak/no text, width mismatch, angle mismatch, low overlap/projection confidence, fallback-source penalty;
- final bounded score/confidence value derived from explicit components rather than traversal order.

Evidence must be inspectable in tests and diagnostics. Do not hide important decisions behind one opaque score.

### 2. Deterministic scoring policy

Introduce a pure scoring policy/service. Requirements:

- no COM or Revit API dependencies;
- deterministic for identical geometric/semantic input;
- no dependency on source collection traversal order;
- finite/bounded scores with no NaN/Infinity leaking into accepted candidates;
- explicit thresholds/tolerances grouped in one policy/options object rather than scattered unrelated magic numbers;
- adaptive tolerance may depend on expected/measured width, segment length, overlap ratio or text scale where justified, but must stay bounded and testable.

### 3. Candidate generator coverage

`BeamCandidateGenerator` must continue producing candidate hypotheses, not final winners. Cover:

1. `PolylineWidth`;
2. `ClosedPolylinePair`;
3. `PairedEdges`;
4. `SingleLineWithText`;
5. fragmented paired edges produced from the existing pure preprocessing path;
6. `CommonWidthFallback`.

Every returned candidate must carry evidence. Keep multiple competing candidates when they are independently plausible; DBR-3 owns conflict graph/global resolution.

### 4. Canonical identity and provenance

- Candidate IDs must remain stable under input ordering and reversed segment orientation.
- Preserve source segment/text identity and DBR-1 provenance through candidate evidence where available.
- Do not mutate the input `CadScene`.
- If preprocessing creates a synthetic/merged segment, evidence must retain deterministic source identity sufficient to explain the candidate. Do not silently erase all source information.

### 5. Legacy comparison boundary

Do **not** switch `MainWindow`, `CadInteropService.GetCadBeams`, `ProcessScene`, or `RevitBeamBuilder` to the DBR-2 candidate path. The legacy production path remains available for DBR-0 parity comparison until DBR-4.

## Known defects intentionally not solved here

- DBR-3: greedy/global candidate conflict selection and centerline resolution;
- DBR-3: endpoint-average centerline defect;
- DBR-4: production pipeline switch and preview/rejection diagnostics;
- DBR-5: structural support-aware split/merge semantics;
- DBR-5: support-blind merge across structural boundaries.

DBR-2 may expose evidence that makes those defects measurable, but must not smuggle resolver/topology behavior into scoring.

## Implementation roles

### CODEX — primary implementation

- implement candidate evidence + scoring policy/service;
- refactor `BeamCandidateGenerator` to attach explicit evidence to every emitted candidate;
- centralize bounded/adaptive scoring tolerances;
- preserve deterministic candidate IDs and competing hypotheses;
- preserve legacy production integration boundary;
- add/adjust focused unit tests for evidence/scoring and candidate generation.

### ANTIGRAVITY — supporting implementation/test/docs

Depends on CODEX contract. It is **not reviewer authority**.

- expand deterministic regression coverage for all candidate kinds, reversed/input-order cases, fragmented pairs, fallback penalties and edge tolerances;
- add tests proving input/provenance preservation and no NaN/Infinity;
- add comparison-oriented tests against DBR-0 characterization where the new candidate layer can be exercised without switching production;
- write DBR-2 implementation report under `src/Antigravity.DrawBeams/docs/reports/`;
- write DBR-2 acceptance evidence under `src/Antigravity.DrawBeams/docs/acceptance/` if acceptance criteria need a durable record.

## Expected production files

Likely additions/changes are limited to DrawBeams candidate-layer files, for example:

- `src/Antigravity.DrawBeams/Models/BeamCandidate.cs`
- `src/Antigravity.DrawBeams/Models/BeamCandidateEvidence.cs`
- `src/Antigravity.DrawBeams/Services/BeamCandidateGenerator.cs`
- `src/Antigravity.DrawBeams/Services/BeamCandidateScorer.cs`
- `src/Antigravity.DrawBeams/Services/BeamCandidateScoringOptions.cs`
- targeted supporting geometry/preprocessing code only if required and covered by tests.

Do not expand scope merely to match these example filenames.

## Tests / acceptance

DBR-2 cannot close unless all of the following are true:

1. all DBR-0 golden tests remain green;
2. all DBR-1 extraction/normalization tests remain green;
3. every generated candidate has deterministic inspectable evidence;
4. reversed input order/orientation yields equivalent candidate identity/evidence;
5. width/angle/overlap tolerance boundary tests are explicit;
6. fragmented paired-edge candidate coverage is explicit;
7. `CommonWidthFallback` is visibly penalized versus direct geometric+semantic evidence;
8. no NaN/Infinity evidence/final score;
9. `CadScene` input is not mutated;
10. no global conflict resolution is introduced;
11. no production switch from legacy `ProcessScene` occurs;
12. focused DrawBeams test suite and solution/module build remain green.

## Review requirement

CODEX/ANTIGRAVITY reports are evidence only. Final closure requires ChatGPT/CodexPro to inspect the actual changed source/diff, rerun tests independently, compare against this canonical plan, and only then promote the reviewed DrawBeams paths as the next Approved Baseline.

## Closure evidence

- DrawBeams focused suite: 83/83 PASS.
- Solution build: PASS, 0 errors; existing compatibility/architecture warnings remain visible.
- Source-reference review confirms `BeamCandidateGenerator` is not yet a production call site and legacy `ProcessScene(...)` remains active.
- Durable report: `../reports/DrawBeams_DBR2_Candidate_Evidence_Scoring_20260823.md`.
- Durable acceptance: `../acceptance/2026-08-23-dbr2-candidate-evidence-scoring.md`.
- DBR-3 is the next recognition milestone.
