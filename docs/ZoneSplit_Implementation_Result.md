# ZoneSplit Implementation Result

## Scope

Implemented the ZoneSplit volume workflow described in `ZoneSplit_CodeX_Context.md`:

- Read zone volumes from `Generic Model` elements that have `Mark`; fallback to `BIM_ZoneID` is kept for old models.
- Classify structural elements by majority intersection volume.
- Write the winning zone to element parameters.
- Write `Zone: [ZoneID] | Vol: [Value] m3` to `Comments`.
- Generate a Markdown QTO report grouped by zone.
- Build `Antigravity.ZoneSplit.dll`.

## Code Changes

### New Models

- `Models/ZoneVolume.cs`
  - Represents one zone volume element, its `ZoneId`, `ZoneName`, and cached `Solid`.
- `Models/ZoneElementContribution.cs`
  - Stores per-element, per-zone QTO contribution.
- `Models/ZoneProcessResult.cs`
  - Stores processing counters, warnings, contributions, and output report path.

### New Services

- `Services/SolidExtractor.cs`
  - Extracts solids from normal geometry and `GeometryInstance`.
  - Unions multi-solid elements when possible.
  - Runs bounding-box pre-check before Boolean intersection.
- `Services/ZoneVolumeReader.cs`
  - Collects `OST_GenericModel` zone volumes using built-in `Mark` as the zone name/id.
  - Falls back to `BIM_ZoneID` when `Mark` is empty.
- `Services/ZoneVolumeProcessor.cs`
  - Uses `ElementIntersectsElementFilter` as fast pass.
  - Processes structural categories:
    - Structural Columns
    - Structural Framing
    - Floors
    - Structural Foundation
    - Walls
  - Computes intersection volume in m3.
  - Computes projected length in m for beams and columns.
  - Writes `BIM_ZoneID`, `BIM_ZoneName` when available.
  - Writes result text to built-in `Comments`.
- `Services/ZoneReportWriter.cs`
  - Exports a Markdown report under `ZoneSplitReports` beside the model file, or under Documents when the model has no saved path.

### New Command

- `Commands/ZoneProcessCommand.cs`
  - Main Revit command for the new volume-based workflow.
  - Shows a Revit summary dialog after processing.

### Ribbon Wiring

- `Antigravity.Main/App.cs`
  - `Chia Zone` now points to `Antigravity.ZoneSplit.Commands.ZoneProcessCommand`.
- `Antigravity.ZoneSplit/App.cs`
  - Standalone ZoneSplit ribbon button also points to `ZoneProcessCommand`.

### Build Behavior

- `Antigravity.ZoneSplit.csproj`
  - Post-build copy to Revit Addins folder now runs only when `DeployToRevitAddins=true`.
  - This avoids build failure while Revit is locking the loaded DLL.

## Build Output

Successful Release build:

```text
dotnet build .\src\Antigravity.ZoneSplit\Antigravity.ZoneSplit.csproj -c Release
```

Generated DLL:

```text
src\Antigravity.ZoneSplit\bin\Release\net48\Antigravity.ZoneSplit.dll
```

Debug build also passed:

```text
src\Antigravity.ZoneSplit\bin\Debug\net48\Antigravity.ZoneSplit.dll
```

## Deployment Note

Direct copy into:

```text
C:\Users\Admin\AppData\Roaming\Autodesk\Revit\Addins\2024\Antigravity
```

was blocked because Autodesk Revit was running and locking the existing DLL.

After closing Revit, deploy with:

```text
dotnet build .\src\Antigravity.ZoneSplit\Antigravity.ZoneSplit.csproj -c Release /p:DeployToRevitAddins=true
```

## Runtime Output Report

When the command runs inside Revit, it creates a project QTO report named like:

```text
ZoneSplit_Report_yyyyMMdd_HHmmss.md
```

The report includes:

- processing counters,
- summary by zone,
- per-element contribution table,
- warnings.
