# DBR-4B Production Activation & Preview Diagnostics

Status: ACTIVE / REVIEW PENDING
Last durable update: 2026-08-27

## Goal

Activate the hardened DBR-1 through DBR-4A recognition pipeline as a reviewed production candidate while retaining one explicit Legacy rollback route and concise pre-creation diagnostics. Production promotion/landing remains a separate human gate after required smoke evidence.

## Reviewed candidate boundary

Local Orchestrator workflow `WF-ded08b56-05be-59c4-b83a-03683bd69a54` is the latest reviewed DBR-4B candidate lineage after S4 repair hardening. In that candidate:

- `CadInteropService.GetCadBeamRecognition` captures one AutoCAD `CadScene` and delegates it to `RecognizeScene`.
- `BeamRecognitionRouter` selects `Pipeline` by default and invokes exactly one recognizer.
- Explicit `Legacy` mode invokes the retained `ProcessScene` rollback implementation.
- `MainWindow` creates Revit beams only from `BeamRecognitionResult.AcceptedBeams`.
- Preview warnings and rejected diagnostics remain non-creation evidence.
- The existing confirmation shows accepted/warning/rejected counts plus a bounded reason summary.
- `RevitBeamBuilder`, coordinate transforms, family/type, level, offset, justification and mark placement semantics remain outside this activation change.

## Canonical ownership / promotion rule

The reviewed candidate may execute from a Local Orchestrator isolated worktree for staged S2-S4 smoke. Such worktree paths and DLLs are transient execution outputs, not canonical artifact ownership locations.

The canonical owner remains `E:\Antigravity\RevitAddinSolution\src\Antigravity.DrawBeams`. DBR-4B production source is **not promoted/landed into the canonical tree until the required S4 gate passes and final human/ChatGPT/CodexPro approval authorizes promotion**.

## S0-S4 gate

- S0: PASS for the reviewed candidate.
- S1: PASS; latest focused supporting evidence is 191/191 `Antigravity.DrawBeams.Tests`.
- S2_REVIT_LOAD: PASS on the reviewed staged candidate.
- S3_COMMAND_SAFE_CANCEL: PENDING.
- S4_CAD_TO_REVIT_BEAM_CRITICAL_PATH: PENDING_REVALIDATION. The prior visual smoke found missing, shifted/span, and duplicate beam defects; subsequent automated recognition repairs require a fresh Revit/CAD rerun.

DBR-4B remains REVIEW PENDING. Smoke/LKG remains `PENDING_REVALIDATION` and is not advanced.

## Verification ownership

For add-in-scoped S1, resolve verification through `src/Antigravity.DrawBeams/smoke-tests/smoke-manifest.json` and the focused test project it names. `dotnet test Antigravity.sln` is not the default DBR-4B S1 gate and is reserved for an explicitly requested solution-wide integration/release check.

## Promotion exit criteria

DBR-4B can proceed to canonical source promotion only after:

1. S3 safe-cancel is recorded.
2. S4 real CAD -> recognition -> confirmation -> Revit beam creation passes the repaired defect areas.
3. Final actual-source review confirms the promoted source exactly matches the reviewed candidate that passed smoke.
4. The explicit Legacy rollback path remains intact.
5. Last-known-good is advanced only after the promotion/acceptance gate, never from build/tests alone.
