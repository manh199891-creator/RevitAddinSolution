# DBR-4A Recognition Integration Boundary

Status: PASS / CLOSED

Closure authority: ChatGPT/CodexPro actual-source inspection in workflow `WF-e42bc8e2-437b-5bfa-e1b0-0ba90893ffcb` plus Local Orchestrator authoritative verification. DBR-4A remains non-production; DBR-4B is NEXT / READY FOR PLANNING.

## Decision

DBR-4A composes the pure DBR-2 candidate generator and DBR-3 resolver into `BeamRecognitionPipeline`. It accepts only `CadScene` and explicit layer selection, has no AutoCAD COM or Revit API dependency, and returns deterministic accepted `CadBeamData` plus separate structured diagnostics.

`CadBeamData` remains the backwards-compatible production DTO. Candidate identity, evidence, provenance, conflicts, and rejection reasons remain in `BeamRecognitionDiagnostic`/`ResolvedBeamCandidate`; they are not transiently attached to production DTOs.

## Production boundary

- Production remains legacy in 4A: `GetCadBeams`, `MainWindow`, and `RevitBeamBuilder` do not change.
- `CadInteropService.ProcessScene(...)` remains available and is not deleted or quarantined.
- No user-facing preview or confidence UI is activated. `Accepted`, `PreviewWarning`, and `RejectedDiagnostic` are a non-production contract based only on resolver outcome and mappability/finite validity; 4A invents no score threshold.

## Deterministic mapping and comparison

Accepted resolved finite centerlines map to `CadBeamData` start/end coordinates, width, height, mark, text, measured width, and paired semantics. Output is sorted by a stable value key. Diagnostics retain the source candidate and resolver evidence.

`BeamRecognitionLayerSelection` takes normalized beam/text layer lists (and retains a single-string compatibility constructor). Beam selection limits anchor eligibility but deliberately preserves all scene segments as potential partners, matching legacy `ProcessScene`; text selection limits text eligibility. Empty selection means all applicable scene content and layer matching is trimmed/case-insensitive.

`BeamRecognitionParityComparer` uses canonical finite centerline endpoints with invariant round-trip durable keys. It uses a deterministic successive-shortest-augmenting-path bipartite matcher: maximum one-to-one cardinality first, then minimum endpoint cost, in polynomial `O(V*E*F)` time and at most `min(legacy,new)` augmentations. Canonical node and edge order resolves exact-cost ties lexically and reproducibly; the dense 40-by-40 compatibility regression establishes full matching/cardinality and permutation-stable output without a timeout-only assertion.

Exact `Equivalent` geometry is strict (0.01mm endpoint tolerance). Logical identity requires dimensions, near-parallel direction, <=50mm lateral separation, >=200mm / >=25% projected interval overlap, and for non-close endpoints, >=80% overlap plus credible span/midpoint continuity and metadata support. Contradictory meaningful marks or text prevent a distant cross-pair. Thus the intended DBR-3 unequal-overlap geometry improvement remains `GeometryChanged`, but adjacent or partially overlapping independent beams with matching dimensions do not become false pairs. An otherwise identical beam with changed metadata remains `MetadataChanged`.

Null/non-finite comparison rows are excluded from match edges but accounted for explicitly in `BeamRecognitionParityResult` through per-side invalid counts and canonical bounded explanations. The six existing categories remain comparison evidence rather than automatic failures: `Equivalent`, `LegacyOnly`, `NewOnly`, `GeometryChanged`, `DimensionsChanged`, and `MetadataChanged` have bounded explanations and stable keys.

## Future activation / rollback boundary

DBR-4B may propose production activation only after curated-baseline parity has no unexplained high-risk delta, every intentional delta is documented, focused tests/build are green, and the explicit rollback remains restoring the existing legacy `ProcessScene` call path without changing `RevitBeamBuilder`. Production activation is a separate explicit workflow. Until then, the pipeline is callable only as a comparison/diagnostic harness.
