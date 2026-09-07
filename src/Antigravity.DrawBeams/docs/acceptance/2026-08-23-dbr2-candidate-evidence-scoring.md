# DBR-2 Acceptance — Candidate Evidence + Scoring

Date: 2026-08-23  
Status: PASS / CLOSED

## Automated acceptance

| Gate | Result | Evidence |
| --- | --- | --- |
| DBR-0 golden characterization remains green | PASS | DrawBeams focused suite 83/83 |
| DBR-1 extraction/normalization remains green | PASS | DrawBeams focused suite 83/83 |
| Every emitted candidate receives evidence | PASS | `BeamCandidateGenerator.AddCandidate` + evidence tests |
| Evidence is finite and bounded | PASS | `EveryGeneratedCandidate_HasFiniteBoundedEvidence` |
| Candidate identity/evidence deterministic under input reorder | PASS | fragmented/reversed-order evidence test + existing determinism tests |
| Width tolerance boundary explicit | PASS | 30% relative pass/fail test |
| Pair-angle tolerance boundary explicit | PASS | 2° accepted / 3° rejected test |
| Adaptive overlap explicit | PASS | valid 150 mm overlap short-pair test |
| Fragmented source identity retained | PASS | four-source evidence assertion |
| Fallback is explicitly penalized | PASS | direct-pair vs fallback evidence test |
| DBR-1 provenance reaches candidate evidence | PASS | source-kind/text-ID preservation test |
| Input `CadScene` remains unmutated | PASS | existing candidate-generator mutation regression remains green |
| No global conflict resolver introduced | PASS | generator only deduplicates identical candidate IDs; distinct candidates remain |
| Legacy `ProcessScene` remains production path | PASS | source-reference scan; no production `BeamCandidateGenerator` call site |
| DrawBeams build/tests | PASS | 83/83 |
| Solution build | PASS | `dotnet build Antigravity.sln --no-restore`, 0 errors |

## Runtime/manual boundary

DBR-2 does not switch the Revit production path. Real Revit S2/S3/S4 acceptance is therefore **not claimed or required to prove DBR-2 candidate-layer closure**. Revit-facing acceptance becomes mandatory when DBR-4 switches production recognition to the new pipeline.

## Known deferred behavior

The following remain intentionally open:

- DBR-3 global conflict graph/resolver;
- DBR-3 projected-overlap centerline;
- DBR-4 production integration/preview diagnostics;
- DBR-5 structural support-aware split/merge.

## Closure

DBR-2 meets its defined candidate-layer acceptance gates without changing current Revit beam creation behavior.
