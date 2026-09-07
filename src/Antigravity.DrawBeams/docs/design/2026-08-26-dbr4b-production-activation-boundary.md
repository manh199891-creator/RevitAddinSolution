# DBR-4B Production Activation Boundary

Status: ACTIVE / REVIEW PENDING
Last durable update: 2026-08-27

## Candidate route

The reviewed DBR-4B candidate uses one routing boundary:

```text
CadScene -> BeamRecognitionRouter (Pipeline default) -> BeamRecognitionPipeline -> BeamRecognitionResult
                                      |
                                      +-> Legacy explicit rollback -> ProcessScene -> accepted legacy beams
```

`CadInteropService.GetCadBeamRecognition` captures one COM-derived AutoCAD `CadScene` per command action, then passes that same pure scene to `RecognizeScene`. `RecognizeScene` owns the sole route decision through `BeamRecognitionRouter`. The router invokes exactly one selected recognizer and never merges cross-mode results.

No environment variable, registry value, hidden feature flag, or mutable global state controls routing. `Legacy` is explicit and stateless.

## Layer-selection contract

- Null/empty beam selection means all applicable anchor content.
- Explicit beam layers are trimmed/case-insensitive and limit anchor eligibility only; valid partner geometry remains available.
- Null/empty text selection means all applicable text.
- Explicit text layers are trimmed/case-insensitive and are filtered before preprocessing so unselected annotation layers cannot redefine geometry fragmentation.

## Creation and diagnostics contract

`MainWindow` receives a `BeamRecognitionResult` and passes only `AcceptedBeams` to the existing Revit creation flow. Preview warnings and rejected diagnostics remain evidence only.

The pre-creation confirmation keeps the existing BxH summary and adds deterministic bounded recognition diagnostics: accepted, preview-warning, rejected counts, plus at most three stable reasons. Candidate payloads, unbounded text, and stack traces are not shown.

`RevitBeamBuilder`, coordinate transforms, family/type creation, level, offset, justification, mark placement, and Revit join/topology behavior remain outside DBR-4B recognition activation scope.

## S4 repair boundary

The prior S4 visual run identified missing beams, shifted/incorrect spans or centerlines, and duplicate/double beams. The latest reviewed repair lineage ends at workflow `WF-ded08b56-05be-59c4-b83a-03683bd69a54` and hardens:

- selected-text-layer isolation before preprocessing;
- zero-gap support/bay boundaries;
- aligned small support gaps on paired faces;
- overlapping/contained fragment handling only with covering-partner evidence;
- local text evidence rather than infinite-axis text influence;
- adjacent same-size/same-mark bay separation;
- deterministic reorder behavior.

Beam-end non-connection remains unproven as recognition ownership and is deferred toward DBR-5/Revit topology unless the next S4 run demonstrates a candidate/centerline defect.

## Canonical promotion boundary

Local Orchestrator worktrees are valid staged execution locations for S2-S4 evidence, but they are transient and do not redefine project ownership. Canonical owner is `E:\Antigravity\RevitAddinSolution\src\Antigravity.DrawBeams`.

The reviewed DBR-4B production source is not landed into the canonical production tree before S4 passes. Promotion is a separate explicit human gate. Last-known-good remains `PENDING_REVALIDATION` until promotion/acceptance completes.
