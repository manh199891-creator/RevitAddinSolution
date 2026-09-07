# Antigravity.CheckFloorElevation - Implementation Summary

Date: 2026-06-06

## Summary

Implemented a new Revit add-in module named `Antigravity.CheckFloorElevation`.

The tool checks the top elevation difference between:

- Host architectural floors in the active Revit model.
- Structural floors from a selected loaded Revit linked model.

If the absolute elevation delta is greater than the user tolerance, the host floor is flagged as an error. The tool can apply view color overrides and export an HTML report.

## Added Project

Project path:

```text
src/Antigravity.CheckFloorElevation/Antigravity.CheckFloorElevation.csproj
```

Build output:

```text
src/Antigravity.CheckFloorElevation/bin/Debug/net48/Antigravity.CheckFloorElevation.dll
```

Use this DLL directly in Revit Addin Manager when testing the newest local build:

```text
E:/Antigravity/RevitAddinSolution/src/Antigravity.CheckFloorElevation/bin/Debug/net48/Antigravity.CheckFloorElevation.dll
```

Deployed DLL path:

```text
%APPDATA%/Autodesk/Revit/Addins/2024/Antigravity/Antigravity.CheckFloorElevation.dll
```

## Main Files

```text
src/Antigravity.CheckFloorElevation/CheckFloorElevationCommand.cs
src/Antigravity.CheckFloorElevation/Models/CheckSettings.cs
src/Antigravity.CheckFloorElevation/Models/FloorCheckResult.cs
src/Antigravity.CheckFloorElevation/Services/FloorCollectorService.cs
src/Antigravity.CheckFloorElevation/Services/ElevationService.cs
src/Antigravity.CheckFloorElevation/Services/ElevationComparisonService.cs
src/Antigravity.CheckFloorElevation/Services/ColorOverrideService.cs
src/Antigravity.CheckFloorElevation/Services/HostFloorViewService.cs
src/Antigravity.CheckFloorElevation/Services/ReportService.cs
src/Antigravity.CheckFloorElevation/UI/FloorCheckerDialog.xaml
src/Antigravity.CheckFloorElevation/UI/FloorCheckerDialog.xaml.cs
```

## Integration Changes

Updated `Antigravity.sln`:

- Added `Antigravity.CheckFloorElevation`.
- Nested it under the `src` solution folder.
- Added Debug/Release configuration mappings.

Updated `src/Antigravity.Main/Antigravity.Main.csproj`:

- Added a project reference to `Antigravity.CheckFloorElevation`.

Updated `src/Antigravity.Main/App.cs`:

- Added a ribbon button in tab `VILAIVIET`.
- Panel: `KIỂM SOÁT XUNG ĐỘT`.
- Button label: `Kiem Tra / Cao Do San`.
- Command class:

```text
Antigravity.CheckFloorElevation.CheckFloorElevationCommand
```

Updated `scripts/deploy/Deploy-ToRevit.ps1`:

- Added module name `CheckFloorElevation` to the deploy list.

## Logic

The checker runs in Revit API context and does not use background threads for Revit API calls.

Core behavior:

- Loads only Revit links where `GetLinkDocument() != null`.
- Collects host floors from the whole model or active view, based on the UI checkbox.
- Uses `HostObjectUtils.GetTopFaces()` to calculate floor top elevation.
- Falls back to `BoundingBox.Max.Z` if top faces cannot be read.
- Uses `ReferenceIntersector` with `FindReferencesInRevitLinks = true`.
- Filters raycast hits so only floors from the selected link instance are accepted.
- Converts Revit internal feet to millimeters using `304.8`.
- Calculates:

```text
DeltaZMm = ZTopHostMm - ZTopLinkMm
IsError = Abs(DeltaZMm) > ToleranceMm
```

## UI Features

The dialog supports:

- Select loaded structural Revit link.
- Set tolerance in millimeters.
- Optionally check host floors visible in the active view only.
- Run check.
- View results in a data grid.
- Select a result row and use `Show 3D` to switch to a 3D view, section box around the host floor, temporarily isolate it, select it, and zoom to its location.
- Apply color overrides to host floors in the active view.
- Reset color overrides.
- Export an HTML report.

## Report Output

HTML reports are exported to:

```text
%LOCALAPPDATA%/Antigravity/CheckFloorElevation/Reports
```

The report includes:

- Host model title.
- Link model name.
- Tolerance.
- Generated time.
- OK/Error/No Match counts.
- Host floor id.
- Link floor id.
- Level.
- ZTop host.
- ZTop link.
- Delta Z.
- Status.
- Error/no-match message.

## Verification

Build command:

```powershell
dotnet build Antigravity.sln -c Debug
```

Result:

```text
Build succeeded.
0 Error(s)
```

## Update (2026-06-06) - Improve Text Contrast

Runtime symptom:

- Result rows, especially `No Match`, were difficult to read because the row background was yellow/brown and the grid header text was low contrast.

Updated:

```text
src/Antigravity.CheckFloorElevation/UI/FloorCheckerDialog.xaml
```

Changes:

- DataGrid background changed to a darker blue `#101047`.
- Alternating rows changed to `#1F1F6E`.
- Header uses light background `#E8ECFF` with dark text `#0F172A`.
- Grid cells use explicit white foreground `#FFFFFF`.
- Selected cell uses gold `#FFD700` with black text.
- `No Match` row background changed from brown/yellow to purple `#3B2F78`.
- Error row background changed to darker red `#6F1616`.
- Summary/status text changed to white and semi-bold.

Key style additions:

```xml
<Setter Property="ColumnHeaderStyle">
    <Setter.Value>
        <Style TargetType="DataGridColumnHeader">
            <Setter Property="Background" Value="#E8ECFF"/>
            <Setter Property="Foreground" Value="#0F172A"/>
            <Setter Property="FontWeight" Value="Bold"/>
        </Style>
    </Setter.Value>
</Setter>
```

```xml
<DataTrigger Binding="{Binding IsNoMatch}" Value="True">
    <Setter Property="Background" Value="#3B2F78"/>
    <Setter Property="Foreground" Value="#FFFFFF"/>
</DataTrigger>
```

Verification:

```powershell
dotnet build Antigravity.sln -c Debug
```

Result:

```text
Build succeeded.
0 Error(s)
```

Regression test command:

```powershell
dotnet test src/Antigravity.ZoneSplit/tests/ZoneSplit.Tests.csproj
```

Result:

```text
Passed: 13
Failed: 0
Skipped: 0
```

Deploy command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts/deploy/Deploy-ToRevit.ps1 -NoPause
```

Result:

```text
[OK] Antigravity.CheckFloorElevation.dll
[OK] Antigravity.Main.dll
[OK] Antigravity.addin da duoc cap nhat.
```

## DLL / Addin Manager

When testing with Revit Addin Manager, load the local build DLL:

```text
E:/Antigravity/RevitAddinSolution/src/Antigravity.CheckFloorElevation/bin/Debug/net48/Antigravity.CheckFloorElevation.dll
```

Command class:

```text
Antigravity.CheckFloorElevation.CheckFloorElevationCommand
```

Expected bottom buttons in the newest dialog:

```text
Run Check | Show 3D | Apply Color | Export HTML | Reset Color
```

If the dialog does not show `Show 3D`, Revit is loading an old DLL.

Observed DLL comparison after adding `Show 3D`:

```text
New local DLL:
E:/Antigravity/RevitAddinSolution/src/Antigravity.CheckFloorElevation/bin/Debug/net48/Antigravity.CheckFloorElevation.dll
LastWriteTime: 2026-06-06 09:18:59
Length: 39424

Old deployed DLL:
C:/Users/Admin/AppData/Roaming/Autodesk/Revit/Addins/2024/Antigravity/Antigravity.CheckFloorElevation.dll
LastWriteTime: 2026-06-06 08:52:18
Length: 37376
```

Revit must be fully closed before running deploy. If Revit is still open, the deployed DLL can remain old even when the local build DLL is newer.

## Manual Revit Check

After deploy:

1. Start Revit 2024.
2. Open a project with at least one loaded structural Revit link.
3. Go to tab `VILAIVIET`.
4. In panel `KIỂM SOÁT XUNG ĐỘT`, click `Kiem Tra / Cao Do San`.
5. Select the structural link.
6. Use tolerance `20` mm for the first test.
7. Run the check.
8. Confirm result rows show OK/Error/No Match.
9. Select a result row and click `Show 3D`; confirm Revit switches to a 3D view and zooms to the host floor location.
10. Apply color and verify host floors are colored in the active view.
11. Export HTML and confirm the report opens with ElementIds and elevation deltas.

## Notes

- The module does not create a separate `.addin` manifest.
- It is loaded through `Antigravity.Main`.
- Only host floors are colored. Linked model elements are not modified.
- Revit must be closed before running deploy, otherwise DLLs may be locked.

## Chat Implementation Details

This section captures the implementation logic and the key code added during the chat.

### Naming Decision

The original plan used:

```text
Antigravity.FloorElevationChecker
```

The implemented module uses the requested name:

```text
Antigravity.CheckFloorElevation
```

This affects:

- Project folder.
- Assembly name.
- Root namespace.
- Ribbon command class path.
- DLL path for Addin Manager.

### Project Setup Code

Project file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AssemblyName>Antigravity.CheckFloorElevation</AssemblyName>
    <RootNamespace>Antigravity.CheckFloorElevation</RootNamespace>
    <Nullable>disable</Nullable>
    <LangVersion>9.0</LangVersion>
    <PlatformTarget>x64</PlatformTarget>
    <OutputPath>bin\$(Configuration)\</OutputPath>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Antigravity.Core\Antigravity.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="Microsoft.CSharp" />
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Xaml" />
    <Reference Include="PresentationCore" />
    <Reference Include="PresentationFramework" />
    <Reference Include="WindowsBase" />
    <Reference Include="RevitAPI">
      <HintPath>C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="RevitAPIUI">
      <HintPath>C:\Program Files\Autodesk\Revit 2024\RevitAPIUI.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

### Command Entry Code

The add-in command opens the WPF dialog in Revit API context:

```csharp
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CheckFloorElevationCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                message = "No active Revit document is open.";
                return Result.Failed;
            }

            var dialog = new FloorCheckerDialog(uiDoc);
            IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
            new WindowInteropHelper(dialog).Owner = handle;
            dialog.ShowDialog();
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "[CheckFloorElevation] Command failed");
            message = ex.Message;
            return Result.Failed;
        }
    }
}
```

### Ribbon Integration Code

The button is added in `src/Antigravity.Main/App.cs` inside the clash-control panel:

```csharp
PushButtonData btnCheckFloorElevation = new PushButtonData(
    "btnCheckFloorElevation",
    "Kiem Tra\nCao Do San",
    assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.CheckFloorElevation.dll"),
    "Antigravity.CheckFloorElevation.CheckFloorElevationCommand");

btnCheckFloorElevation.ToolTip = "Check floor top elevation delta between host architectural floors and linked structural floors.";
btnCheckFloorElevation.LargeImage = logoImage;

clashPanel.AddItem(btnCheckFloorElevation);
```

### Floor Collection Logic

Host floors are collected from either the whole model or active view:

```csharp
public IList<Floor> GetHostFloors(View activeView, bool activeViewOnly)
{
    FilteredElementCollector collector = activeViewOnly && activeView != null
        ? new FilteredElementCollector(_hostDoc, activeView.Id)
        : new FilteredElementCollector(_hostDoc);

    return collector
        .OfClass(typeof(Floor))
        .WhereElementIsNotElementType()
        .Cast<Floor>()
        .ToList();
}
```

Only loaded Revit links are displayed:

```csharp
public IList<RevitLinkInstance> GetLoadedLinks()
{
    return new FilteredElementCollector(_hostDoc)
        .OfClass(typeof(RevitLinkInstance))
        .Cast<RevitLinkInstance>()
        .Where(link => link.GetLinkDocument() != null)
        .OrderBy(link => link.Name)
        .ToList();
}
```

### Elevation Logic

The preferred top elevation source is `HostObjectUtils.GetTopFaces()`.
The fallback is `BoundingBox.Max.Z`.

```csharp
public static double? GetZTopFeet(Floor floor, Transform transform)
{
    if (floor == null)
        return null;

    try
    {
        IList<Reference> topFaceRefs = HostObjectUtils.GetTopFaces(floor);
        if (topFaceRefs == null || topFaceRefs.Count == 0)
            return FallbackZTopFeet(floor, transform);

        double maxZ = double.MinValue;
        foreach (Reference faceRef in topFaceRefs)
        {
            Face face = floor.GetGeometryObjectFromReference(faceRef) as Face;
            if (face == null)
                continue;

            BoundingBoxUV bounds = face.GetBoundingBox();
            if (bounds == null)
                continue;

            UV center = new UV(
                (bounds.Min.U + bounds.Max.U) / 2.0,
                (bounds.Min.V + bounds.Max.V) / 2.0);

            XYZ point = face.Evaluate(center);
            point = TransformPoint(point, transform);
            maxZ = Math.Max(maxZ, point.Z);
        }

        return maxZ == double.MinValue ? FallbackZTopFeet(floor, transform) : (double?)maxZ;
    }
    catch (Exception ex)
    {
        AppLogger.Error(ex, "[CheckFloorElevation] GetZTopFeet failed, using bounding box fallback");
        return FallbackZTopFeet(floor, transform);
    }
}
```

Feet/mm conversion:

```csharp
private const double FeetToMmFactor = 304.8;

public static double FeetToMillimeters(double feet)
{
    return feet * FeetToMmFactor;
}

public static double MillimetersToFeet(double millimeters)
{
    return millimeters / FeetToMmFactor;
}
```

### Comparison Logic

For each host floor:

1. Read host `ZTop`.
2. Raycast downward from host floor midpoint.
3. Accept only hits from the selected linked model.
4. Read linked floor `ZTop` transformed into host coordinates.
5. Compare with tolerance.

```csharp
var intersector = new ReferenceIntersector(
    new ElementClassFilter(typeof(Floor)),
    FindReferenceTarget.Face,
    _view3D)
{
    FindReferencesInRevitLinks = true
};
```

Selected-link filtering:

```csharp
private Floor FindLinkedFloor(ReferenceIntersector intersector, XYZ origin)
{
    IList<ReferenceWithContext> hits = intersector.Find(origin, XYZ.BasisZ.Negate());
    if (hits == null || hits.Count == 0)
        return null;

    foreach (ReferenceWithContext hit in hits.OrderBy(h => h.Proximity))
    {
        Reference reference = hit.GetReference();
        if (reference == null)
            continue;

        if (reference.ElementId != _linkInstance.Id)
            continue;

        ElementId linkedElementId = reference.LinkedElementId;
        if (linkedElementId == ElementId.InvalidElementId)
            continue;

        return _linkedDoc.GetElement(linkedElementId) as Floor;
    }

    return null;
}
```

Delta rule:

```csharp
result.ZTopLinkMm = ElevationService.FeetToMillimeters(zTopLinkFeet.Value);
result.IsError = Math.Abs(result.DeltaZMm) > toleranceMm;
```

Model property:

```csharp
public double DeltaZMm
{
    get { return ZTopHostMm - ZTopLinkMm; }
}
```

### View Override Logic

Errors are colored red. No-match rows are colored orange.
Only host floor elements are overridden.

```csharp
public void ApplyOverrides(IEnumerable<FloorCheckResult> results)
{
    if (results == null)
        return;

    ElementId solidFillPatternId = GetSolidFillPatternId();

    using (var transaction = new Transaction(_doc, "Apply floor elevation check colors"))
    {
        transaction.Start();

        foreach (FloorCheckResult result in results.Where(r => r.IsError || r.IsNoMatch))
        {
            ElementId id = new ElementId((long)result.HostFloorId);
            if (_doc.GetElement(id) == null)
                continue;

            var settings = new OverrideGraphicSettings();
            if (solidFillPatternId != ElementId.InvalidElementId)
            {
                settings.SetSurfaceForegroundPatternId(solidFillPatternId);
                settings.SetSurfaceForegroundPatternColor(result.IsError
                    ? new Color(220, 38, 38)
                    : new Color(245, 158, 11));
            }

            settings.SetProjectionLineColor(result.IsError
                ? new Color(185, 28, 28)
                : new Color(217, 119, 6));

            _view.SetElementOverrides(id, settings);
        }

        transaction.Commit();
    }
}
```

Reset:

```csharp
_view.SetElementOverrides(id, new OverrideGraphicSettings());
```

### Show 3D Logic

The `Show 3D` feature was added after the initial implementation.
It is implemented by `HostFloorViewService`.

Behavior:

1. User selects one result row.
2. Clicks `Show 3D`.
3. The service finds the host floor by ElementId.
4. Finds a suitable 3D view.
5. Activates section box around the host floor.
6. Temporarily isolates the host floor.
7. Switches Revit to that 3D view.
8. Selects and zooms to the floor.

Button added in XAML:

```xml
<Button x:Name="BtnShow3D"
        Content="Show 3D"
        Click="BtnShow3D_Click"
        Background="#7C3AED"
        IsEnabled="False"/>
```

Button handler:

```csharp
private void BtnShow3D_Click(object sender, RoutedEventArgs e)
{
    if (!(GridResults.SelectedItem is FloorResultViewModel selectedResult))
    {
        MessageBox.Show(this, "Select a result row first.", "No Floor Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    try
    {
        new HostFloorViewService(_uiDoc, _collector).ShowHostFloorIn3D(selectedResult.HostFloorId);
        TxtStatusBar.Text = "Showing host floor " + selectedResult.HostFloorId + " in 3D.";
    }
    catch (Exception ex)
    {
        AppLogger.Error(ex, "[CheckFloorElevation] Show 3D failed");
        MessageBox.Show(this, "Show 3D failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

Core service code:

```csharp
public void ShowHostFloorIn3D(int hostFloorId)
{
    ElementId floorId = new ElementId((long)hostFloorId);
    Element floor = _doc.GetElement(floorId);
    if (floor == null)
        throw new InvalidOperationException("Host floor was not found in the active document.");

    View3D view3D = _collector.FindSuitable3DView();
    if (view3D == null)
        throw new InvalidOperationException("No suitable 3D view was found. Create a printable 3D view and try again.");

    BoundingBoxXYZ elementBox = floor.get_BoundingBox(null);
    if (elementBox == null)
        throw new InvalidOperationException("Host floor has no bounding box.");

    using (var transaction = new Transaction(_doc, "Show host floor in 3D"))
    {
        transaction.Start();

        try
        {
            if (view3D.IsTemporaryHideIsolateActive())
                view3D.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
        }
        catch (Exception ex)
        {
            AppLogger.Warning("[CheckFloorElevation] Could not clear previous temporary isolate: " + ex.Message);
        }

        view3D.IsSectionBoxActive = true;
        view3D.SetSectionBox(CreateExpandedSectionBox(elementBox, 3.0));
        view3D.IsolateElementsTemporary(new List<ElementId> { floorId });

        transaction.Commit();
    }

    _uiDoc.ActiveView = view3D;
    _uiDoc.Selection.SetElementIds(new List<ElementId> { floorId });
    _uiDoc.ShowElements(floorId);
}
```

Section box margin:

```csharp
private static BoundingBoxXYZ CreateExpandedSectionBox(BoundingBoxXYZ sourceBox, double marginFeet)
{
    return new BoundingBoxXYZ
    {
        Min = new XYZ(
            sourceBox.Min.X - marginFeet,
            sourceBox.Min.Y - marginFeet,
            sourceBox.Min.Z - marginFeet),
        Max = new XYZ(
            sourceBox.Max.X + marginFeet,
            sourceBox.Max.Y + marginFeet,
            sourceBox.Max.Z + marginFeet)
    };
}
```

### HTML Report Logic

Reports are written under local app data:

```csharp
string directory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Antigravity",
    "CheckFloorElevation",
    "Reports");
```

Each report includes:

- Host document title.
- Link name.
- Tolerance.
- Timestamp.
- Count summary.
- Host floor id.
- Link floor id.
- Level name.
- ZTop values.
- Delta Z.
- Status.
- Message.

### Deployment and Runtime Issue Found

During the chat, the local build was newer than the deployed DLL.

The old deployed DLL did not show `Show 3D`.

The correct local DLL for Addin Manager is:

```text
E:/Antigravity/RevitAddinSolution/src/Antigravity.CheckFloorElevation/bin/Debug/net48/Antigravity.CheckFloorElevation.dll
```

The command class is:

```text
Antigravity.CheckFloorElevation.CheckFloorElevationCommand
```

Expected buttons in the newest dialog:

```text
Run Check | Show 3D | Apply Color | Export HTML | Reset Color
```

If `Show 3D` is missing, Revit is loading the old DLL.

Observed comparison:

```text
New local DLL:
E:/Antigravity/RevitAddinSolution/src/Antigravity.CheckFloorElevation/bin/Debug/net48/Antigravity.CheckFloorElevation.dll
LastWriteTime: 2026-06-06 09:18:59
Length: 39424

Old deployed DLL:
C:/Users/Admin/AppData/Roaming/Autodesk/Revit/Addins/2024/Antigravity/Antigravity.CheckFloorElevation.dll
LastWriteTime: 2026-06-06 08:52:18
Length: 37376
```

Deploy command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts/deploy/Deploy-ToRevit.ps1 -NoPause
```

Deploy cannot replace DLLs while Revit is running.

## Update (2026-06-06) - Logic Review and Follow-up Implementation

After reviewing the current code and the report content around the later implementation notes, several logic issues were found and fixed.

### Issues Found

1. `FloorCheckerDialog.xaml.cs` referenced `SimpleEventHandler.Enqueue(...)`, but the existing `UI/SimpleEventHandler.cs` only had a single `Action` property.
2. A duplicate `Services/SimpleEventHandler.cs` was created, but the UI namespace class was the one actually used by `FloorCheckerDialog`.
3. Single-action `ExternalEvent` handling could overwrite a pending action if the user clicked multiple buttons quickly.
4. `ElevationService` used Revit mesh members `NumVertices` and `get_Vertex(...)`, which are not available in the current Revit 2024 API reference used by this solution.
5. ZTop was sampled at the top-face UV center only, which is weaker for shaped/sloped floors than using triangulated top-face vertices.
6. Raycast matching used only the host floor bounding-box midpoint, which can miss linked floors when the midpoint falls over an opening, void, or non-overlapping area.
7. `Show 3D` set the active view after modifying the 3D view. The order was changed so Revit switches to the 3D view before applying temporary isolate and view changes.
8. The progress separator `SepProgress` was not toggled with `PanelProgress`.

### Fixes Implemented

#### ExternalEvent Queue

Updated:

```text
src/Antigravity.CheckFloorElevation/UI/SimpleEventHandler.cs
```

The handler now queues actions instead of storing a single overwriteable action:

```csharp
public class SimpleEventHandler : IExternalEventHandler
{
    private readonly object _syncRoot = new object();
    private readonly Queue<Action> _actions = new Queue<Action>();

    public void Enqueue(Action action)
    {
        if (action == null)
            return;

        lock (_syncRoot)
        {
            _actions.Enqueue(action);
        }
    }

    public void Execute(UIApplication app)
    {
        while (true)
        {
            Action action;
            lock (_syncRoot)
            {
                if (_actions.Count == 0)
                    return;

                action = _actions.Dequeue();
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "[CheckFloorElevation] ExternalEvent action failed");
            }
        }
    }

    public string GetName()
    {
        return "Antigravity Check Floor Elevation Event Handler";
    }
}
```

Updated `ExecuteOnRevitThread`:

```csharp
private void ExecuteOnRevitThread(Action action)
{
    _eventHandler.Enqueue(action);
    _externalEvent.Raise();
}
```

Removed duplicate file:

```text
src/Antigravity.CheckFloorElevation/Services/SimpleEventHandler.cs
```

#### Stronger ZTop Calculation

Updated:

```text
src/Antigravity.CheckFloorElevation/Services/ElevationService.cs
```

Top-face elevation now uses triangulated mesh vertices where available:

```csharp
Mesh mesh = face.Triangulate();
if (mesh != null && mesh.Vertices != null && mesh.Vertices.Count > 0)
{
    foreach (XYZ vertex in mesh.Vertices)
    {
        XYZ point = TransformPoint(vertex, transform);
        maxZ = Math.Max(maxZ, point.Z);
    }
}
else
{
    BoundingBoxUV bounds = face.GetBoundingBox();
    if (bounds == null)
        continue;

    UV center = new UV(
        (bounds.Min.U + bounds.Max.U) / 2.0,
        (bounds.Min.V + bounds.Max.V) / 2.0);

    XYZ point = TransformPoint(face.Evaluate(center), transform);
    maxZ = Math.Max(maxZ, point.Z);
}
```

#### Multi-point Raycast Probes

Added probe points above the host floor:

```csharp
public static IList<XYZ> GetProbePointsAbove(Floor floor, double offsetFeet)
{
    var points = new List<XYZ>();
    BoundingBoxXYZ bbox = floor == null ? null : floor.get_BoundingBox(null);
    if (bbox == null)
        return points;

    double centerX = (bbox.Min.X + bbox.Max.X) / 2.0;
    double centerY = (bbox.Min.Y + bbox.Max.Y) / 2.0;
    double z = bbox.Max.Z + offsetFeet;
    points.Add(new XYZ(centerX, centerY, z));

    double marginX = Math.Min(Math.Abs(bbox.Max.X - bbox.Min.X) * 0.2, 3.0);
    double marginY = Math.Min(Math.Abs(bbox.Max.Y - bbox.Min.Y) * 0.2, 3.0);
    double minX = bbox.Min.X + marginX;
    double maxX = bbox.Max.X - marginX;
    double minY = bbox.Min.Y + marginY;
    double maxY = bbox.Max.Y - marginY;

    if (maxX > minX && maxY > minY)
    {
        points.Add(new XYZ(minX, minY, z));
        points.Add(new XYZ(maxX, minY, z));
        points.Add(new XYZ(minX, maxY, z));
        points.Add(new XYZ(maxX, maxY, z));
    }

    return points;
}
```

Updated comparison flow to try all probe points:

```csharp
IList<XYZ> origins = ElevationService.GetProbePointsAbove(hostFloor, 2.0);
if (origins == null || origins.Count == 0)
{
    result.IsNoMatch = true;
    result.ErrorMessage = "Host floor has no bounding box.";
    return result;
}

Floor linkedFloor = FindLinkedFloor(intersector, origins);
```

```csharp
private Floor FindLinkedFloor(ReferenceIntersector intersector, IList<XYZ> origins)
{
    foreach (XYZ origin in origins)
    {
        Floor linkedFloor = FindLinkedFloorAtOrigin(intersector, origin);
        if (linkedFloor != null)
            return linkedFloor;
    }

    return null;
}
```

#### Show 3D Order Fix

Updated:

```text
src/Antigravity.CheckFloorElevation/Services/HostFloorViewService.cs
```

The service now switches to the 3D view before opening the transaction that applies section box and temporary isolation:

```csharp
_uiDoc.ActiveView = view3D;

using (var transaction = new Transaction(_doc, "Show host floor in 3D"))
{
    transaction.Start();

    if (view3D.IsTemporaryHideIsolateActive())
        view3D.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);

    view3D.IsSectionBoxActive = true;
    view3D.SetSectionBox(CreateExpandedSectionBox(elementBox, 3.0));
    view3D.IsolateElementsTemporary(new List<ElementId> { floorId });

    transaction.Commit();
}

_uiDoc.Selection.SetElementIds(new List<ElementId> { floorId });
_uiDoc.ShowElements(floorId);
```

#### Progress Separator Fix

Updated:

```csharp
SepProgress.Visibility = isRunning ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
```

### Verification After Follow-up

Build command:

```powershell
dotnet build Antigravity.sln -c Debug
```

Result:

```text
Build succeeded.
0 Error(s)
```

Regression test:

```powershell
dotnet test src/Antigravity.ZoneSplit/tests/ZoneSplit.Tests.csproj
```

Result:

```text
Passed: 13
Failed: 0
Skipped: 0
```

### Deploy Status

Deploy was not run because Revit was still running:

```text
Revit PID: 48924
```

Close Revit fully before deploying:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts/deploy/Deploy-ToRevit.ps1 -NoPause
```

For immediate testing with Addin Manager, use the local build DLL:

```text
E:/Antigravity/RevitAddinSolution/src/Antigravity.CheckFloorElevation/bin/Debug/net48/Antigravity.CheckFloorElevation.dll
```

## Update (2026-06-06) - Fix All Rows Showing No Match

Runtime symptom:

```text
0 OK | 0 Error | 73 No Match
```

The selected linked model appeared in the combo box, but every host floor returned:

```text
No matching linked structural floor found...
```

### Likely Root Causes

The issue was most likely caused by one or both of these conditions:

1. `ReferenceIntersector` was using `ElementClassFilter(typeof(Floor))`. In Revit links, class filters can fail to return references for linked elements even when `FindReferencesInRevitLinks = true`.
2. The checker selected the first printable 3D view in the model instead of the active 3D view visible to the user. That fallback view may have link visibility, category visibility, crop/section box, or view filters that prevent raycast hits.

### Fix: Prefer Active 3D View

Updated:

```text
src/Antigravity.CheckFloorElevation/Services/FloorCollectorService.cs
```

New behavior:

- If the current active view is a non-template `View3D`, use it.
- Otherwise fallback to the first printable non-template 3D view.

```csharp
public View3D FindSuitable3DView(View activeView = null)
{
    View3D active3D = activeView as View3D;
    if (active3D != null && !active3D.IsTemplate)
        return active3D;

    return new FilteredElementCollector(_hostDoc)
        .OfClass(typeof(View3D))
        .Cast<View3D>()
        .Where(view => !view.IsTemplate && view.CanBePrinted)
        .OrderBy(view => view.Name)
        .FirstOrDefault();
}
```

Updated caller:

```csharp
View3D view3D = _collector.FindSuitable3DView(_uiDoc.ActiveView);
```

The status bar now reports the 3D view used:

```csharp
TxtStatusBar.Text = "Completed. Checked " + _lastResults.Count + " host floor(s). 3D view: " + view3D.Name;
```

### Fix: Remove Class Filter From ReferenceIntersector

Updated:

```text
src/Antigravity.CheckFloorElevation/Services/ElevationComparisonService.cs
```

Old logic:

```csharp
var intersector = new ReferenceIntersector(
    new ElementClassFilter(typeof(Floor)),
    FindReferenceTarget.Face,
    _view3D)
{
    FindReferencesInRevitLinks = true
};
```

New logic:

```csharp
var intersector = new ReferenceIntersector(_view3D)
{
    FindReferencesInRevitLinks = true
};
```

The checker now raycasts all references and manually filters:

- Reference belongs to the selected `RevitLinkInstance`.
- Reference has a valid `LinkedElementId`.
- Linked element is actually a `Floor`.

```csharp
ElementId hostElementId = reference.ElementId;
ElementId linkedElementId = reference.LinkedElementId;

if (hostElementId != _linkInstance.Id)
    continue;

if (linkedElementId == ElementId.InvalidElementId)
    continue;

Element linkedElement = _linkedDoc.GetElement(linkedElementId);
if (linkedElement is Floor linkedFloor)
    return linkedFloor;
```

### Improved No Match Message

The no-match message now includes the 3D view name to make visibility/crop issues easier to diagnose:

```csharp
result.ErrorMessage = "No matching linked structural floor found below host floor probe points in 3D view: " + _view3D.Name;
```

If no matches still occur after this fix, check:

- The selected 3D view shows the linked model.
- The linked model's floor category is visible in that 3D view.
- The 3D view section box does not cut away the linked floors.
- The selected link in the combo box is the structural link that contains floors.

### Verification

Build command:

```powershell
dotnet build Antigravity.sln -c Debug
```

Result:

```text
Build succeeded.
0 Error(s)
```

## Update (2026-06-06) - Modeless UI & Vilai Viet Guidelines

Subsequent to the initial implementation, two major enhancements were made to improve the user experience and ensure consistency with the company's design system.

### 1. Modeless Window Interaction

The dialog was refactored from a modal (`ShowDialog()`) to a modeless (`Show()`) window, allowing users to interact with the Revit workspace (e.g., navigating and clicking the 3D view) while the `CheckFloorElevation` window remains open.

**Key Technical Changes:**
- Changed `dialog.ShowDialog()` to `dialog.Show()` in `CheckFloorElevationCommand.cs`.
- Introduced `SimpleEventHandler.cs` which implements `IExternalEventHandler` to execute any `Action` safely within the Revit API context.
- Modified `FloorCheckerDialog.xaml.cs` to wrap all API calls (Run Check, Show 3D, Apply Color, Export HTML, Reset Color) within an `ExecuteOnRevitThread` method.
- Added `Dispatcher.Invoke` for UI thread updates to ensure thread safety when modifying UI elements (e.g., progress bars, data grids) after background Revit API execution.

### 2. Vilai Viet UI Guidelines Adherence

The interface in `FloorCheckerDialog.xaml` was entirely rewritten to strictly comply with `VilaiViet_UI_Guidelines.md`.

**Key Visual Changes:**
- **Color Palette & Theme:** Adopted the signature Dark Mode `#1A1A5E` background color.
- **Header Component:** Replaced the plain text title with the standard header containing the 🏗 emoji, the text `AUTO FLOOR CHECKER`, and the `@Vilai Viet` highlight in gold `#FFD700`.
- **Resource Dictionary:** Imported `LabelStyle`, `TextBoxStyle`, and `ComboStyle` (including the specific `ControlTemplate` override for ComboBox to prevent Windows' default white background).
- **Action Buttons:** Restructured buttons into a bottom `UniformGrid`. Styled the primary action ("Run Check") with red `#CC0000` and secondary actions ("Show 3D", "Apply Color", etc.) with dark blue `#3A3A8A`. Prefixed button labels with appropriate emojis (▶, 👁, 🎨, 📄, ✖).
- **DataGrid Formatting:** Applied custom styling for row backgrounds and alternating rows to blend seamlessly into the dark theme.

The solution builds successfully and is ready for deployment.
## Update - 2026-06-06 Continued Iteration

### Requested UI and Logic Changes

This update documents the additional changes made after the first implementation of `Antigravity.CheckFloorElevation`.

User requests covered in this update:

- Limit matching so host/link floor `ZTop` values are only compared when they are within `500 mm`.
- Avoid excessive linked-model scanning that can make the file slow or fail to load.
- Add level filtering in the host model.
- Convert host level selection from a single dropdown to a multi-select checkbox dropdown.
- Allow users to choose the HTML export path manually.
- Add floor type columns to the result grid and HTML report.

### 500 mm Match Window

The comparison logic now separates two concepts:

- `MaxComparableDeltaMm = 500`: hard matching window.
- `ToleranceMm`: user-entered pass/fail tolerance.

Flow:

1. Read `ZTopHost_mm`.
2. Raycast against linked floors from the selected Revit link only.
3. Read linked floor `ZTopLink_mm`.
4. Accept the linked floor as a candidate only if:

```text
Abs(ZTopHost_mm - ZTopLink_mm) <= 500
```

5. If no linked floor is found within `500 mm`, return:

```text
Status = No Match
Message = No linked structural floor found within 500 mm ...
```

6. If a linked floor is accepted, then apply tolerance:

```text
DeltaZ_mm = ZTopHost_mm - ZTopLink_mm
IsError = Abs(DeltaZ_mm) > ToleranceMm
```

Example:

- User tolerance = `60 mm`.
- Host/link delta = `350 mm`.
- Since `350 <= 500`, the link is considered a valid match.
- Since `350 > 60`, status is `Error`.

If host/link delta is `900 mm`, it is not considered a comparable floor pair and is returned as `No Match`.

### Raycast Performance Guard

To reduce heavy scanning across many floors/levels in large linked models, the raycast candidate loop now stops when the hit proximity exceeds the local search distance.

Current constant:

```csharp
private const double MaxProbeRayDistanceFeet = 10.0;
```

This reduces cases where `ReferenceIntersector` keeps scanning far below the host floor and creates slow or unstable runs.

### Temporary Hide/Isolate Reset Before Check

The second run previously produced many `No Match` rows because `Show 3D` leaves the active 3D view in Temporary Hide/Isolate and may also enable a section box around one host floor.

Before running raycast, `ElevationComparisonService.PrepareViewForRaycast()` now clears those view states:

```csharp
if (_view3D.IsTemporaryHideIsolateActive())
    _view3D.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);

if (_view3D.IsSectionBoxActive)
    _view3D.IsSectionBoxActive = false;
```

This allows the next run to see linked floors again.

### Host Level Multi-Select

The dialog now includes a `Host level` dropdown using checkbox items.

Behavior:

- `All host levels` is checked by default.
- Checking `All host levels` clears all individual level selections.
- Checking one or more individual levels clears `All host levels`.
- If the user unchecks everything, `All host levels` is automatically re-enabled.

Files updated:

```text
src/Antigravity.CheckFloorElevation/UI/FloorCheckerDialog.xaml
src/Antigravity.CheckFloorElevation/UI/FloorCheckerDialog.xaml.cs
src/Antigravity.CheckFloorElevation/Services/FloorCollectorService.cs
src/Antigravity.CheckFloorElevation/Models/CheckSettings.cs
```

Collector signature:

```csharp
public IList<Floor> GetHostFloors(
    View activeView,
    bool activeViewOnly,
    IList<ElementId> levelIds = null)
```

Filtering logic:

```csharp
if (levelIds != null && levelIds.Count > 0)
    floors = floors.Where(floor => levelIds.Any(levelId => levelId == floor.LevelId));
```

If `levelIds` is empty, all host levels are included.

### Manual HTML Export Path

`Export HTML` now opens a `SaveFileDialog`, allowing the user to choose the export folder and file name.

Default filename:

```text
FloorElevationCheck_yyyyMMdd_HHmmss.html
```

Default initial folder:

```text
Documents
```

`ReportService.ExportHtml()` now accepts an optional output path:

```csharp
public string ExportHtml(
    IList<FloorCheckResult> results,
    CheckSettings settings,
    string hostDocumentTitle,
    string linkName,
    string outputPath = null)
```

If `outputPath` is not provided, it still falls back to:

```text
%LOCALAPPDATA%/Antigravity/CheckFloorElevation/Reports
```

Existing report observed:

```text
C:/Users/Admin/AppData/Local/Antigravity/CheckFloorElevation/Reports/FloorElevationCheck_20260606_110845.html
```

### Result Type Columns

The result grid now includes floor type information:

```text
Host Type
Link Type
```

Purpose:

- `Host Type` shows the selected host floor type visible in Revit Properties, for example:

```text
BYG_FLOOR CONCRETE_PAVEMENT
```

- `Link Type` shows the matched linked structural floor type.
- If there is no linked match, `Link Type` is displayed as `-`.

Model additions:

```csharp
public string HostTypeName { get; set; }
public string LinkTypeName { get; set; }
```

Type lookup:

```csharp
private static string GetTypeName(Floor floor)
{
    ElementType type = floor.Document.GetElement(floor.GetTypeId()) as ElementType;
    return type == null ? string.Empty : type.Name;
}
```

UI columns added:

```xml
<DataGridTextColumn Header="Host Type" Binding="{Binding HostTypeName}" Width="180"/>
<DataGridTextColumn Header="Link Type" Binding="{Binding LinkTypeNameDisplay}" Width="180"/>
```

HTML report columns were also updated:

```text
Host Floor ID | Link Floor ID | Host Type | Link Type | Level | ZTop Host | ZTop Link | Delta Z | Status | Message
```

### Current DLL Paths

Local build DLL:

```text
E:/Antigravity/RevitAddinSolution/src/Antigravity.CheckFloorElevation/bin/Debug/net48/Antigravity.CheckFloorElevation.dll
```

Expected deployed DLL:

```text
C:/Users/Admin/AppData/Roaming/Autodesk/Revit/Addins/2024/Antigravity/Antigravity.CheckFloorElevation.dll
```

### Build Status

The module build was verified after the latest changes:

```text
dotnet build src/Antigravity.CheckFloorElevation/Antigravity.CheckFloorElevation.csproj -c Debug
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

### Deploy Status

Deploy could not be completed while Revit was running because loaded DLLs are locked.

Observed running process:

```text
PID 49016 - Autodesk Revit 2024.1
```

To deploy the latest DLL, close Revit completely, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts/deploy/Deploy-ToRevit.ps1 -NoPause
```

### Current Manual Test Checklist

1. Close Revit and deploy the latest DLL.
2. Open Revit 2024.
3. Open the Check Floor Elevation dialog.
4. Select the structural linked model.
5. Tick one or more host levels.
6. Run with tolerance such as `60 mm`.
7. Confirm only floors from the selected host levels are checked.
8. Confirm `Host Type` matches the Revit Properties type of the selected host floor.
9. Confirm `Link Type` appears only for matched linked floors.
10. Confirm pairs outside `500 mm` are shown as `No Match`.
11. Confirm valid pairs over tolerance are shown as `Error`.
12. Use `Show 3D`, then run check again and confirm the second run still finds matches.
13. Export HTML and choose a custom path.
14. Open the exported HTML and confirm the table includes type columns.
