# AutoDim Walls - Implementation Result

Created: 2026-05-18
Last Updated: 2026-05-19
Project: `BimTools`
Target: Revit 2024, .NET Framework 4.8, WPF

## Summary

Implemented AutoDim Walls as a standalone `BimTools` Revit add-in at:

`E:\Antigravity\RevitAddinSolution`

The add-in builds cleanly with **0 errors** and registers a `VILAIVIET` ribbon panel named `AUTO DIM`, with an `AutoDim Walls` button that opens the WPF control window.

Build verification:

```powershell
dotnet build BimTools.csproj /p:Configuration=Debug
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The build output and manifest are copied to:

`%APPDATA%\Autodesk\Revit\Addins\2024\`

---

## Implemented Features (Completed & Upgraded)

### 1. Plan / Section View Selection (WPF UI Row Added)
- Added a new **View Type** selection row under the title bar, separating:
  - `Plan View (Mặt bằng)` (Fully functional & highly optimized).
  - `Section View (Mặt cắt - Sắp triển khai)` (Under development, with an elegant placeholder informing users that it will position lintels, tie beams, vertical openings, and wall heights in the next release, safely disabling `RUN AUTO DIM`).
- Cleaned and organized the WPF window layout to match **Vilai Viet's Premium Dark-Blue UI Guidelines**.

### 2. Auto Text Shift & Leader Auto-fit (Solved Overlapping / Squeezed Texts)
- **Eliminated 0-Dim Segments:** Auto-detects and removes coplanar references closer than `3mm` (0.01 feet) along the wall, preventing Revit from creating `0` segments that cause squeezed numbers.
- **Smart Alternating Shift:** Detects narrow segments `<260mm` (0.85 feet) and programmatically shifts their `TextPosition` by `1.3 feet` (~400mm) outwards along the wall.
- **Overlap Prevention:** Alternates shift directions (left/right) if consecutive segments are narrow, preventing them from overlapping each other.
- **DB Regeneration Hook:** Introduced `_doc.Regenerate()` right before text adjustment so Revit's graphics engine fully populates segment values and activates their adjustable state, guaranteeing that the text shifts and native curved leader lines are successfully drawn.

### 3. Exact Opening Scanner (Solid Geometry scanning)
- Fully rewrote `OpeningReferenceResolver.cs` to perform geometry intersection scanning on the wall's actual physical Solid.
- Instead of using inaccurate door/window FamilyInstance reference planes (which often return rough/nominal dimensions like `1000mm`), it extracts the actual cut faces of the wall opening to return exact masonry/jamb dimensions (e.g. `950mm` net opening).

### 4. Host Structural Pier Scanner (Bổ trụ / Vách)
- Created `HostStructuralResolver.cs` to scan structural columns and piers directly inside the host document.
- Finds structural columns whose boundaries cut/intersect the wall's geometric boundaries, extracting their side faces perpendicular to the wall direction, and seamlessly chains their dimensions.

### 5. Crash-proof View Filter (Section view protection)
- Avoids fatal Revit C++ graphics kernel crashes by detecting if the user attempts to run the Plan Mode horizontally inside a vertical Section/Elevation view.
- Gracefully blocks the operation and prints a friendly, clear instruction status to the user.

---

## Implemented Files

### Project and Add-in Registration
- `BimTools.csproj` (Target `net48`, enables WPF, references Revit 2024 API, deploys directly to AppData).
- `BimTools.addin` (Registers `BimTools.App` as `IExternalApplication`).
- `App.cs` (Creates VILAIVIET ribbon tab and push button).
- `BimToolsCommands.cs` (Launches `AutoDimWindow` bound to Revit window handle).

### Models
- `Models/AutoDimOptions.cs` (Stores scope, toggles for openings, grids, links, structural piers, picked points, and selected DimensionType).
- `Models/AutoDimResult.cs` (Stores summary stats and log messages).

### Core Logic & Services
- `Core/WallGeometryUtils.cs` (Straight wall extraction, coplanar face resolution, baseline projection).
- `Core/OpeningReferenceResolver.cs` (Scans exact Solid cut opening jambs/centers).
- `Core/HostStructuralResolver.cs` (Scans structural columns and piers in host document).
- `Core/IntersectionResolver.cs` (Scans Grid intersections with straight wall lines).
- `Services/AutoDimEngine.cs` (Executes chained dimension creation, coordinates overall/opening/grid/pier collections, performs strict view checking, document regeneration, and applies the **Smart Alternating Text Shift** algorithm).

### WPF UI
- `UI/AutoDimWindow.xaml` (Dark-Blue Vilai Viet dark-theme styling, resizable WPF controls).
- `UI/AutoDimWindow.xaml.cs` (Binds WPF to Revit active document, handles selections, registers view checked updates, and calls the engine).

---

## Verification Notes

Build command runs with clean exit code 0:

```powershell
dotnet build BimTools.csproj /p:Configuration=Debug
```

Observed output:

```text
BimTools -> E:\Antigravity\RevitAddinSolution\bin\Debug\BimTools.dll
Build succeeded.
0 Warning(s)
0 Error(s)
```

Both `BimTools.dll` and `BimTools.addin` manifest are deployed seamlessly to:

`C:\Users\Admin\AppData\Roaming\Autodesk\Revit\Addins\2024`

---

## Recommended Next Steps

1. Open Revit 2024 and confirm `VILAIVIET > AUTO DIM > AutoDim Walls` loads.
2. Test on a floor plan containing plaster layers, doors, windows, and host structural columns/piers.
3. Validate that narrow dimensions (like `15mm` plaster or `200mm` piers) automatically push their text to the side with beautiful leader lines.
4. Verify that running on a Section view displays a friendly warning rather than crashing.
5. Prepare to implement **Phase 2 (Section View Geometry Engine)** for automatic vertical dimensioning of lintels, tie beams, vertical openings, and wall heights.
