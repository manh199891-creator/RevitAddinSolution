# ZoneSplit BIM Parameter Implementation - 2026-05-16

## Scope

Implemented the BIM parameter-only workflow from `ZoneSplit_BIM_Parameter_Plan.md`.

This workflow preserves original Revit element geometry and writes per-zone volume quantities into dynamic numeric parameters named:

```text
BIM_[ZoneName]
```

Example:

```text
BIM_Zone 1 = 15.5
```

## Implemented Changes

### 1. Dynamic BIM Volume Parameters

File:

```text
src/Antigravity.ZoneSplit/Services/ParameterSetupService.cs
```

Changes:

- Changed dynamic volume prefix from `Vol_` to `BIM_`.
- `EnsureDynamicZoneVolumeParameters(...)` now creates numeric shared parameters for all detected zone names.
- Parameter names are generated through `GetVolumeParameterName(zoneName)`.
- Basic setup no longer creates a new `BIM_ZoneName` parameter; the main retained identity parameter is `BIM_ZoneID`.

### 2. Parameter-Only Processing Flow

File:

```text
src/Antigravity.ZoneSplit/Services/ZoneVolumeProcessor.cs
```

Changes:

- Removed physical split behavior from the active processing path.
- Elements are no longer copied, shortened, deleted, or reshaped by the BIM parameter workflow.
- The processor still computes the best zone by majority intersection volume and writes `BIM_ZoneID`.
- Every valid element-zone solid intersection is converted from cubic feet to cubic meters and written into the matching `BIM_[ZoneName]` numeric parameter.

### 3. Zombie Data Reset

File:

```text
src/Antigravity.ZoneSplit/Services/ZoneVolumeProcessor.cs
```

Changes:

- Before writing current volumes, the processor scans all target elements.
- Writable numeric parameters whose names start with `BIM_` are reset to `0.0`.
- Text identity parameters such as `BIM_ZoneID` and `BIM_ZoneName` are excluded from reset.

This prevents stale values when zones move or when an element no longer intersects a previous zone.

### 4. Plan Document Update

File:

```text
docs/ZoneSplit_BIM_Parameter_Plan.md
```

Changes:

- Rewrote the plan file in clean ASCII Markdown because the previous file content had mojibake.
- Marked Task 1, Task 2, and Task 3 as implemented.
- Documented the final parameter-only behavior and checkpoint status.

## Runtime Behavior

When ZoneSplit runs:

1. Reads zone volumes from Generic Model zone elements.
2. Creates dynamic numeric shared parameters for all zone names:

   ```text
   BIM_[ZoneName]
   ```

3. Collects target structural/category elements.
4. Resets existing writable numeric `BIM_` quantity parameters to `0.0`.
5. Computes intersection volume for each element-zone pair.
6. Converts volume from ft3 to m3.
7. Writes per-zone m3 values to `BIM_[ZoneName]`.
8. Writes the majority-zone ID to `BIM_ZoneID`.
9. Produces the Markdown QTO report as before.

## Verification

Build:

```powershell
dotnet build src\Antigravity.ZoneSplit\Antigravity.ZoneSplit.csproj
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Tests:

```powershell
dotnet test src\Antigravity.ZoneSplit\tests\ZoneSplit.Tests.csproj
```

Result:

```text
Passed! - Failed: 0, Passed: 13, Skipped: 0, Total: 13
```

## Notes

- The active workflow now prioritizes preserving model geometry.
- `PhysicalSplitService.cs` still exists in the codebase, but it is not used by the current BIM parameter-only processor flow.
- Dynamic `BIM_[ZoneName]` parameters are numeric `Number` shared parameters storing m3 values directly.

