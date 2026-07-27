# Phase 2 — Plan: Continuous CAD Beam Recognition (Phase 1)

### Task 1: Create Models and Options
**Files:** Create `src/Antigravity.DrawBeams/Models/CadBeamSegment.cs`, `src/Antigravity.DrawBeams/Models/BeamContinuityOptions.cs`
**Steps:**
- [ ] Define `CadBeamSegment` with `StartX`, `StartY`, `EndX`, `EndY`, `Width`, `Height`, `MeasuredWidth`, `Mark`, `TextContent`, `IsPaired`, `SourceLineIds`, `Direction`, `Confidence`.
- [ ] Define `BeamContinuityOptions` with default values for `AngularToleranceDegrees`, `EndpointGapToleranceMm`, `LateralOffsetToleranceMm`, `WidthToleranceRatio`, `MinimumOverlapMm`.

### Task 2: Create Tests Project and Write Tests
**Files:** Create `tests/Antigravity.DrawBeams.Tests/Antigravity.DrawBeams.Tests.csproj`, `tests/Antigravity.DrawBeams.Tests/BeamChainBuilderTests.cs`
**Steps:**
- [ ] Write `Antigravity.DrawBeams.Tests.csproj` (xUnit, references `Antigravity.DrawBeams`).
- [ ] Write failing tests for 10 criteria:
  1. Two collinear segments joined at endpoints with same width -> 1 chain.
  2. Three consecutive collinear segments -> 1 chain.
  3. Two segments with gap < tolerance -> 1 chain.
  4. Two segments with gap > tolerance -> 2 chains.
  5. Angular deviation > tolerance -> 2 chains.
  6. Width change > tolerance -> 2 chains.
  7. Parallel segments with lateral offset > tolerance -> 2 chains.
  8. Partially overlapping segments -> stable handling (1 chain, no dupes).
  9. Shuffled input order -> consistent chain output.
  10. Reversed endpoint directions -> successfully chained.
- [ ] Run `dotnet test` -> verify FAIL.

### Task 3: Create BeamChainBuilder Implementation
**Files:** Create `src/Antigravity.DrawBeams/Services/BeamChainBuilder.cs`
**Steps:**
- [ ] Write minimal implementation (GREEN) for `BuildChains` method.
  - Group segments into chains.
  - Adjacency checking by projecting endpoints.
  - Enforce gap, offset, width, and angle tolerances.
  - Retain all segments in the chain.
- [ ] Run `dotnet test` -> verify PASS.

### Known Limitations
- Not splitting by text changes in this phase.
- Not yet integrated into `CadInteropService.cs`.

### Suggested Phase 2 Integration Point
- Inside `CadInteropService.GetCadBeams()`, replace `MergeCollinearBeams(beams)` with a conversion from `CadBeamData` to `CadBeamSegment`, passing them to `BeamChainBuilder.BuildChains()`, and then converting the resulting chains back to `CadBeamData`.
