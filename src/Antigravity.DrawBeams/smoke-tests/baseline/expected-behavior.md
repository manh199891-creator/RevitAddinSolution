# DrawBeams Last-Known-Good Expected Behavior

This baseline describes behavior that must remain available while the recognition engine is refactored.

## Load / command

- DrawBeams assembly loads without missing dependency or type-load failures.
- DrawBeams command can start in Revit and can cancel safely without mutating the document.

## Recognition compatibility

- Legacy `CadInteropService.ProcessScene(...)` remains callable for comparison.
- `GetCadBeams(...)` remains on the legacy recognition path until the production integration gate.
- DBR-0 golden characterization remains unchanged unless a later phase intentionally updates an expectation with explicit review.
- DBR-1 extraction/normalization preserves entity provenance while not introducing DBR-2+ scoring/resolution behavior.

## Revit creation compatibility

- `RevitBeamBuilder` behavior is not changed by DBR-0/DBR-1.
- Existing family/type BxH, centerline, level, offset, justification and mark behavior remain the production reference until replacement acceptance passes.
- No duplicate beam creation is introduced.

## Rollback signal

If focused tests, Revit load, representative command smoke, or the CAD-to-Revit critical path regresses, do not promote the candidate. Compare/revert against the approved baseline identity in `last-known-good.json` and keep the previously deployed immutable release active.
