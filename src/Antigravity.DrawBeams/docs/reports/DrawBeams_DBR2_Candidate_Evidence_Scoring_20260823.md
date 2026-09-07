# DrawBeams DBR-2 — Candidate Evidence + Scoring Report

Date: 2026-08-23  
Status: PASS / CLOSED  
Canonical plan: `../plans/2026-08-23-dbr2-candidate-evidence-scoring.md`

## Delivery lane

DBR-2 was implemented through the project-scoped ChatGPT/CodexPro lane after the reviewed DrawBeams-only continuation baseline was established. This phase was not dispatched as a CODEX + ANTIGRAVITY Local Orchestrator workflow. No agent report is therefore being presented as reviewer evidence for this phase; the closure below is based on actual source review plus fresh local verification.

The reviewed baseline overlay mechanism excluded unrelated repository dirt and promoted only:

- `src/Antigravity.DrawBeams`
- `tests/Antigravity.DrawBeams.Tests`

Reviewed snapshot chain:

- `6b4adf8e5963ae8b508e329ca3a588d65b363d54` — DBR-0/DBR-1 + canonical DrawBeams-local structure
- `70d6c5d41f1058dfb7d14a4eda0c342318472060` — same baseline plus the canonical DBR-2 plan
- `ef2299cd85bfcb654f2a3237fe2d862a5f3e0abe` — first post-implementation reviewed DBR-2 source/tests/docs snapshot after fresh verification

## Implementation

### Evidence model

Added `Models/BeamCandidateEvidence.cs` and attached `BeamCandidate.Evidence` to every emitted candidate.

Evidence is explicit rather than opaque and exposes bounded components for:

- geometry score;
- semantic score;
- topology/source score;
- penalty;
- final score;
- parallelism;
- overlap;
- width agreement;
- text distance;
- text angle;
- direct-text / polyline-width / closed-group / fallback / fragmented-source flags;
- deterministic source segment IDs;
- source text identity;
- source kind when DBR-1 provenance is available.

### Scoring policy

Added:

- `Services/BeamCandidateScoringOptions.cs`
- `Services/BeamCandidateScorer.cs`

The candidate-layer thresholds now have one explicit policy owner rather than remaining scattered through `BeamCandidateGenerator`.

Key bounded/adaptive rules include:

- pair angle tolerance equivalent to the prior `abs(dot) >= 0.999` rule;
- relative width tolerance of 30%;
- common-width fallback relative tolerance of 15%;
- adaptive required overlap derived from the shorter face length and clamped to 100..300 mm;
- explicit min/max paired/fallback/polyline widths;
- explicit length-to-width ratio;
- explicit geometry / semantic / topology weights;
- explicit missing-text and fallback penalties.

All score outputs are clamped to finite `[0,1]` values; NaN/Infinity cannot escape as candidate evidence.

### Candidate generation

`BeamCandidateGenerator` now:

- deterministically orders source segments/text before preprocessing;
- keeps `PolylineWidth`, `ClosedPolylinePair`, `PairedEdges`, `SingleLineWithText` and `CommonWidthFallback` hypotheses;
- keeps fragmented paired-edge recognition through the existing pure preprocessing path;
- attaches evidence at candidate creation;
- retains multiple distinct competing hypotheses for DBR-3;
- deduplicates only identical candidate IDs produced by symmetrical traversal;
- uses invariant formatting for width-bearing candidate IDs;
- preserves source identity for fragmented merged segments through evidence.

No global conflict graph/resolution was added.

## Legacy production boundary

A source-reference scan after implementation found no production call site for `BeamCandidateGenerator`; its references remain the class itself, tests and documentation.

`CadInteropService.GetCadBeams(...)` continues to call legacy `ProcessScene(...)`, and the existing Revit creation route is unchanged. DBR-4 remains responsible for any production switch.

## Fresh verification

Focused DrawBeams suite after implementation:

```text
Failed:   0
Passed:  83
Skipped: 0
Total:   83
```

This includes the pre-existing DBR-0 golden characterization, DBR-1 extraction/normalization regression, and seven DBR-2 evidence/scoring tests.

New DBR-2 coverage proves:

- all observed emitted candidates carry finite bounded evidence;
- direct text+geometry pairing scores above common-width fallback;
- fallback penalty is explicit;
- fragmented pairs preserve deterministic four-segment source identity;
- reversed input order produces the same fragmented candidate/evidence;
- adaptive overlap accepts a valid short pair with 150 mm overlap where the old fixed 200 mm threshold would not;
- 2-degree pair skew is accepted while 3-degree skew is rejected by the explicit angle boundary;
- 30% relative width tolerance has an explicit pass/fail boundary;
- available DBR-1 source provenance and text identity reach candidate evidence.

Solution verification:

```text
dotnet build Antigravity.sln --no-restore
Build succeeded.
0 errors
6 existing compatibility/architecture warnings
```

The warnings are the pre-existing NU1701/MSB3270 compatibility/processor-architecture warnings and are not DBR-2 failures.

## Scope intentionally deferred

DBR-2 does not solve:

- candidate conflict graph / global selection;
- greedy `usedIds` conflict behavior;
- projected-overlap centerline construction;
- production confidence tiers / preview diagnostics;
- support-aware beam span split/merge;
- the production switch from legacy `ProcessScene`.

These remain DBR-3 through DBR-5 responsibilities.

## Reviewer conclusion

Actual candidate-layer source and tests were inspected after implementation. Fresh tests/build are green and the production Revit creation path remains on legacy `ProcessScene`.

DBR-2 is accepted as PASS / CLOSED. The reviewed DrawBeams-only paths were promoted through the Approved Baseline chain while unrelated repository dirt remained excluded. The current immutable snapshot identity is authoritative in Local Orchestrator's approved-baseline runtime record; DBR-3 must start from that reviewed continuation baseline.
