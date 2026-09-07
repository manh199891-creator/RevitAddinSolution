# DrawBeams Recognition Engine V14 — DBR-0 Baseline

Date: 2026-08-22
Status: PASS / CLOSED
Scope: `Antigravity.DrawBeams` only
Canonical owner: `src/Antigravity.DrawBeams/docs/reports/`

## Production call path frozen for comparison

The production path at the DBR-0 boundary is:

`CreateBeamCommand -> MainWindow -> CadInteropService.GetCadBeams -> CadInteropService.ProcessScene -> CadBeamData -> CoordinateService.CadToRevit -> RevitBeamBuilder`

DBR-0 intentionally does not change that path. `CadInteropService.ProcessScene` remains the production recognition authority while later DBR phases are developed behind deterministic comparison tests.

## Golden baseline coverage

Existing `BeamSceneProcessorTests` already freezes these behaviors:

- normal two-parallel-edge recognition with BxH text;
- reversed partner direction;
- closed rectangular/grouped polyline pairing;
- text-layer filtering;
- safe scene processing when text is absent;
- polyline `GlobalWidth`/width fast path;
- BxH + mark extraction;
- list-overload layer selection.

`DrawBeamsDbr0GoldenTests` adds the DBR-0 cases that were missing:

1. unequal-length paired edges;
2. fragmented collinear edges with a small (<= 200 mm) gap;
3. no-text common-width fallback;
4. single beam-layer line + BxH text false-positive characterization;
5. multiple competing parallel partners / greedy `usedIds` characterization;
6. two collinear equal-size spans across a 1500 mm support-like gap.

## Known legacy behaviors intentionally characterized

These are not fixed in DBR-0. They are recorded so later refactors must make any behavior change explicit.

### KDBR-001 — Unequal-length endpoint averaging

`SetupBeamCenterline` averages paired endpoints. For faces `[0,4000]` and `[500,3500]`, the legacy centerline becomes `[250,3750]` rather than the projected overlap `[500,3500]`.

Target phase: DBR-3 (`BeamCenterlineBuilder`).

### KDBR-002 — Selected-layer single-line promotion

A long single line on a selected beam layer with a nearby BxH text can be promoted to an unpaired beam even when no second beam face exists.

Target phase: DBR-2/DBR-4 (evidence scoring + confidence/diagnostics).

### KDBR-003 — Greedy conflict handling leaves an extra beam

With three parallel lines around one BxH text, the legacy candidate selection consumes the first preferred pair via `usedIds`; the remaining line can subsequently be promoted by the single-line fallback.

Target phase: DBR-3 (`CandidateConflictGraph` + `BeamCandidateResolver`).

### KDBR-004 — Collinear merge crosses a support-like gap

`MergeCollinearBeams` can merge equal-size collinear beams across gaps up to 2000 mm without support context. The golden fixture records a 1500 mm gap merging into one beam.

Target phase: DBR-5 (structural topology/support nodes).

## Closure evidence

The authoritative Local Orchestrator workflow completed, ChatGPT/CodexPro independently reviewed the actual source/worktree, and focused DrawBeams verification was re-run before DBR-0 closure. The DBR-0 characterization remains part of the approved baseline carried into later phases.

## Migration gate

DBR-1 was permitted only after:

- the focused suite was green;
- golden expectations were reviewed;
- known legacy behaviors were accepted as characterization rather than accidental fixes;
- no unrelated dirty work was cleaned, reset, stashed, committed, or overwritten.
