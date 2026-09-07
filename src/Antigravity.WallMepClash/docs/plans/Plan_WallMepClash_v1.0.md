# Implementation Plan: Antigravity.WallMepClash
## Xét Va Chạm Song Song Tường (Host) ↔ MEP (Linked Model)

**Ngày tạo:** 2026-06-22  
**Dựa trên:** Kiến trúc CheckFloorElevation (DLL riêng, WPF Dialog, ExternalEvent, Show3D)

---

## Overview

Xây dựng module **`Antigravity.WallMepClash`** phát hiện va chạm song song giữa:
- **Tường (Wall)** trong **host model** (model hiện tại đang mở)
- **Elements MEP** trong **một file Revit Link được chọn** (cơ điện, plumbing, v.v.)

**Logic lọc:**
1. User chọn **file link** muốn kiểm tra (1 file tại 1 lần)
2. User chọn **categories** từ file link đó cần kiểm tra
3. Với mỗi cặp (Wall host, MEP link): kiểm tra **Bounding Box giao nhau**
4. Nếu giao → tính **góc giữa trục wall và trục MEP**:
   - **Vuông góc (80°–90°)** → bỏ qua (MEP xuyên qua tường, thi công được)
   - **Chéo (10°–80°)** → bỏ qua
   - **Song song (0°–10°)** → **VA CHẠM**, đưa vào kết quả
5. Hiển thị kết quả dạng **WPF DataGrid** (giống CheckFloorElevation)
6. Tính năng **Show 3D**: isolate cặp clash được chọn trong 3D view

---

## Architecture

Hoàn toàn clone structure từ `Antigravity.CheckFloorElevation`:

```
Antigravity.WallMepClash/
├── Antigravity.WallMepClash.csproj
├── WallMepClashCommand.cs              ← IExternalCommand (entry point)
├── Models/
│   ├── ClashCheckSettings.cs           ← Tham số kiểm tra
│   └── ClashResult.cs                  ← Kết quả 1 cặp va chạm
├── Services/
│   ├── MepLinkCollectorService.cs      ← GetLoadedLinks, GetCategories, GetMepElements
│   ├── WallCollectorService.cs         ← GetHostWalls, GetHostLevels, FindSuitable3DView
│   ├── BoundingBoxHelper.cs            ← BB intersect, transform link→host coords
│   ├── AngleClassifier.cs              ← Parallel/Perpendicular/Skew logic
│   ├── WallMepClashDetector.cs         ← Service chính chạy detection
│   ├── ClashViewService.cs             ← Show3D: isolate cặp wall+MEP
│   ├── ColorOverrideService.cs         ← Apply color cho clash elements
│   └── ReportService.cs                ← Export HTML
└── UI/
    ├── WallMepClashDialog.xaml         ← WPF Window chính (clone style CheckFloorElevation)
    ├── WallMepClashDialog.xaml.cs      ← Code-behind
    └── SimpleEventHandler.cs           ← ExternalEvent handler (copy từ CheckFloorElevation)
```

---

## UI Layout (WallMepClashDialog)

Thiết kế theo cùng theme dark blue (`#1A1A5E`) của CheckFloorElevation:

```
┌─────────────────────────────────────────────────────────────────┐
│  WALL MEP CLASH CHECKER @Vilai Viet          X Clash | Y OK     │
│  Check parallel clash between host walls and linked MEP         │
├─────────────────────────────────────────────────────────────────┤
│ 🔗 Linked MEP model          Categories           Parallel ≤ °  │
│ [ComboBox: link files ▼]     [CheckedListBox ▼]   [10      ]    │
│                              □ Pipe  □ Duct                      │
│                              □ Conduit □ CableTray               │
│                              □ MechanicalEquipment               │
│                              □ ElectricalEquipment               │
├─────────────────────────────────────────────────────────────────┤
│ Wall Id  │ MEP Id │ Wall Name │ MEP Name │ Angle° │ Level │ Clash│
│ 12345    │ 67890  │ Tường BT  │ Pipe Ø50 │ 2.3°   │ T.01  │ ●   │
│ ...      │ ...    │ ...       │ ...      │ ...    │ ...   │     │
├─────────────────────────────────────────────────────────────────┤
│ [Status bar text]                                                │
│ [▶ Run Check] [👁 Show 3D] [🎨 Apply Color] [📄 Export HTML] [✖ Reset] │
└─────────────────────────────────────────────────────────────────┘
```

**Khác biệt so với CheckFloorElevation:**
- Thêm **checked combobox** cho categories (multi-select)
- Thêm **TextBox góc ngưỡng** (default: 10°)
- Show 3D → isolate **cả 2 elements** (wall host + MEP link instance)
- Kết quả row màu đỏ = clash song song

---

## Task List

### Phase 1: Project Setup & Models

---

#### Task 1 — Tạo project `Antigravity.WallMepClash.csproj`
**Copy từ:** `Antigravity.CheckFloorElevation.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AssemblyName>Antigravity.WallMepClash</AssemblyName>
    <RootNamespace>Antigravity.WallMepClash</RootNamespace>
    <Nullable>disable</Nullable>
    <LangVersion>9.0</LangVersion>
    <PlatformTarget>x64</PlatformTarget>
    <OutputPath>bin\$(Configuration)\</OutputPath>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <!-- References: giống CheckFloorElevation (RevitAPI, RevitAPIUI, Core, WPF) -->
</Project>
```

**AC:**
- [ ] Project xuất hiện trong solution
- [ ] Build không lỗi (empty project)

**Dependencies:** None  
**Size:** XS | Files: 1 (+ sln update)

---

#### Task 2 — Tạo Models: `ClashResult.cs` + `ClashCheckSettings.cs`

**`ClashCheckSettings.cs`:**
```csharp
public class ClashCheckSettings
{
    public RevitLinkInstance SelectedLink { get; set; }
    public List<BuiltInCategory> CategoriesToCheck { get; set; }
    public double ParallelThresholdDeg { get; set; } = 10.0;
}
```

**`ClashResult.cs`:**
```csharp
public class ClashResult
{
    public int HostWallId { get; set; }
    public int LinkMepId { get; set; }
    public string WallTypeName { get; set; }
    public string MepName { get; set; }
    public string LevelName { get; set; }
    public double AngleDeg { get; set; }
    public bool IsClash => true; // luôn là clash (đã filter)
    public XYZ ClashMidpoint { get; set; }
}
```

**AC:**
- [ ] Cả 2 class build được, properties đủ để bind WPF DataGrid

**Dependencies:** Task 1  
**Size:** XS | Files: 2

---

### Phase 2: Core Services

---

#### Task 3 — `MepLinkCollectorService.cs`

Service lấy thông tin từ linked model:

```csharp
public class MepLinkCollectorService
{
    private readonly Document _hostDoc;

    // Lấy tất cả loaded links trong host
    public IList<RevitLinkInstance> GetLoadedLinks();

    // Lấy categories có element trong link document
    // Trả List<BuiltInCategory> đã filter: chỉ MEP categories
    public IList<BuiltInCategory> GetAvailableMepCategories(Document linkDoc);

    // Collect MEP elements theo categories được chọn, trong link document
    public IList<Element> GetMepElements(Document linkDoc, IList<BuiltInCategory> categories);
}
```

**Danh sách MEP categories mặc định để show trong UI:**
```
Pipe, PipeFitting, PipeAccessory,
Duct, DuctFitting, DuctAccessory,
Conduit, ConduitFitting,
CableTray, CableTrayFitting,
MechanicalEquipment,
ElectricalEquipment, ElectricalFixtures,
LightingFixtures, LightingDevices,
FireAlarmDevices, DataDevices, CommunicationDevices
```

**AC:**
- [ ] `GetLoadedLinks()` trả đúng danh sách link instances loaded
- [ ] `GetAvailableMepCategories()` chỉ trả categories thực sự có element trong link
- [ ] `GetMepElements()` collect đúng theo categories được chọn

**Dependencies:** Task 1  
**Size:** S | Files: 1

---

#### Task 4 — `WallCollectorService.cs`

Clone từ `FloorCollectorService` nhưng collect Wall thay vì Floor:

```csharp
public class WallCollectorService
{
    public IList<Wall> GetHostWalls(View activeView, bool activeViewOnly);
    public IList<Level> GetHostLevels();
    public View3D FindSuitable3DView(View activeView = null); // copy giống hệt
}
```

**AC:**
- [ ] `GetHostWalls()` trả tất cả Wall không phải ElementType
- [ ] `FindSuitable3DView()` trả 3D view phù hợp (copy logic từ FloorCollectorService)

**Dependencies:** Task 1  
**Size:** XS | Files: 1

---

#### Task 5 — `BoundingBoxHelper.cs`

> [!IMPORTANT]
> **Key challenge:** MEP element bounding box nằm trong **tọa độ link**, cần transform sang **host coordinates** trước khi so sánh với wall BB.

```csharp
public static class BoundingBoxHelper
{
    // Transform BB từ link coords → host coords
    public static BoundingBoxXYZ TransformToHost(
        BoundingBoxXYZ linkBB, Transform linkTransform);

    // Kiểm tra 2 BB có giao nhau không (3D AABB intersection)
    public static bool Intersects(BoundingBoxXYZ a, BoundingBoxXYZ b);

    // Lấy direction vector của MEP element
    // → LocationCurve.Curve.Direction cho Pipe/Duct/Conduit/CableTray
    // → null cho point-based (FamilyInstance không có LocationCurve)
    public static XYZ GetElementDirection(Element mepElement);

    // Lấy direction vector của Wall (trục dài, không phải normal)
    // Wall.Orientation = normal → cần rotate 90° để lấy trục dài
    public static XYZ GetWallDirection(Wall wall);
}
```

**Transform logic:**
```csharp
// linkInstance.GetTotalTransform() → Transform từ link space → host space
Transform t = linkInstance.GetTotalTransform();
XYZ hostMin = t.OfPoint(linkBB.Min);
XYZ hostMax = t.OfPoint(linkBB.Max);
// Cẩn thận: sau transform Min/Max có thể đảo → normalize
```

**AC:**
- [ ] `Intersects()` đúng cho overlapping/non-overlapping/touching/contained
- [ ] `TransformToHost()` tính đúng khi link bị rotate/translate
- [ ] `GetWallDirection()` trả vector song song tường (không phải normal)
- [ ] `GetElementDirection()` trả `null` cho point-based elements

**Dependencies:** Task 1  
**Size:** M | Files: 1

---

#### Task 6 — `AngleClassifier.cs`

```csharp
public static class AngleClassifier
{
    public enum AngleClass { Parallel, Perpendicular, Skew, Undetermined }

    public static AngleClass Classify(
        XYZ wallDir, XYZ mepDir,
        double parallelThresholdDeg = 10.0)
    {
        if (wallDir == null || mepDir == null) return AngleClass.Undetermined;

        // Normalize vectors
        XYZ w = wallDir.Normalize();
        XYZ m = mepDir.Normalize();

        // Tính góc (0–90°), normalize vì đường thẳng không có hướng
        double angleRad = w.AngleTo(m);
        double angleDeg = angleRad * (180.0 / Math.PI);
        if (angleDeg > 90.0) angleDeg = 180.0 - angleDeg;

        if (angleDeg <= parallelThresholdDeg) return AngleClass.Parallel;
        if (angleDeg >= 90.0 - parallelThresholdDeg) return AngleClass.Perpendicular;
        return AngleClass.Skew;
    }

    // Trả góc thực (degrees, 0–90) để hiển thị trong báo cáo
    public static double GetAngleDeg(XYZ wallDir, XYZ mepDir);
}
```

**AC:**
- [ ] 0° → Parallel; 90° → Perpendicular; 45° → Skew
- [ ] 8° → Parallel (trong threshold 10°); 85° → Perpendicular
- [ ] Null input → Undetermined (không crash)

**Dependencies:** Task 1  
**Size:** S | Files: 1

---

#### Task 7 — `WallMepClashDetector.cs`

Service chính kết hợp tất cả:

```csharp
public class WallMepClashDetector
{
    public List<ClashResult> RunCheck(
        Document hostDoc,
        IList<Wall> walls,
        RevitLinkInstance linkInstance,
        IList<Element> mepElements,
        ClashCheckSettings settings,
        Action<int, int> progressCallback = null)
    {
        var results = new List<ClashResult>();
        Transform linkTransform = linkInstance.GetTotalTransform();
        Document linkDoc = linkInstance.GetLinkDocument();

        for (int i = 0; i < walls.Count; i++)
        {
            Wall wall = walls[i];
            progressCallback?.Invoke(i + 1, walls.Count);

            BoundingBoxXYZ wallBB = wall.get_BoundingBox(null);
            if (wallBB == null) continue;

            XYZ wallDir = BoundingBoxHelper.GetWallDirection(wall);

            foreach (Element mep in mepElements)
            {
                // 1. Get MEP BB in host coords
                BoundingBoxXYZ mepBBLink = mep.get_BoundingBox(null);
                if (mepBBLink == null) continue;

                BoundingBoxXYZ mepBBHost = BoundingBoxHelper.TransformToHost(mepBBLink, linkTransform);

                // 2. BB intersection check
                if (!BoundingBoxHelper.Intersects(wallBB, mepBBHost)) continue;

                // 3. Angle classification
                XYZ mepDir = BoundingBoxHelper.GetElementDirection(mep);
                AngleClassifier.AngleClass angleClass =
                    AngleClassifier.Classify(wallDir, mepDir, settings.ParallelThresholdDeg);

                if (angleClass != AngleClassifier.AngleClass.Parallel) continue;

                // 4. Build result
                results.Add(BuildResult(wall, mep, mepDir, wallDir, hostDoc, linkDoc));
            }
        }
        return results;
    }
}
```

**AC:**
- [ ] Song song → vào kết quả; Vuông góc/Chéo → bỏ qua
- [ ] Progress callback được gọi đúng
- [ ] Không crash khi MEP point-based (null direction → skip angle, không report)
- [ ] Performance: 1000 walls × 5000 MEP elements < 30 giây

**Dependencies:** Tasks 2, 3, 4, 5, 6  
**Size:** M | Files: 1

---

### ✅ Checkpoint 1: Core Logic
- [ ] Build project không lỗi
- [ ] Logic thủ công verify với paper test cases:
  - Pipe song song tường → detect
  - Pipe vuông góc → không detect
  - Pipe chéo 45° → không detect

---

### Phase 3: Services phụ

---

#### Task 8 — `ClashViewService.cs` (Show 3D)

Clone + mở rộng từ `HostFloorViewService`:

```csharp
public class ClashViewService
{
    // Isolate wall (host) + MEP link element trong 3D view
    // Section box = union BB của 2 elements + margin
    public void ShowClashIn3D(int hostWallId, int linkMepId, RevitLinkInstance linkInstance);
}
```

**Show 3D logic:**
1. Get wall element từ host doc
2. Get MEP element từ link doc
3. Transform MEP BB sang host coords
4. Tính **union BB** của wall + MEP
5. Set Section Box = union BB + 3ft margin
6. `IsolateElementsTemporary([wallId])` (chỉ isolate được host elements)
7. `uiDoc.Selection.SetElementIds([wallId])` + `ShowElements`

> [!NOTE]
> MEP element từ link **không thể isolate trực tiếp** bằng `IsolateElementsTemporary` trong host view. Workaround: chỉ isolate wall, nhưng section box sẽ crop vừa vị trí clash, MEP vẫn thấy trong view.

**AC:**
- [ ] Click "Show 3D" → Revit chuyển sang 3D view, zoom/crop vào vị trí clash
- [ ] Wall host được select/highlight
- [ ] Không crash khi 3D view không tồn tại

**Dependencies:** Task 4  
**Size:** S | Files: 1

---

#### Task 9 — `ColorOverrideService.cs` + `ReportService.cs`

**ColorOverrideService** — copy từ CheckFloorElevation, adapt cho Wall:
- Override màu đỏ cho tất cả wall có clash
- Reset color override

**ReportService** — copy từ CheckFloorElevation, adapt columns:
- HTML table: WallId, MepId, WallType, MepName, Angle, Level
- Mở browser sau khi export

**AC:**
- [ ] Apply Color: walls clash đổi màu đỏ trong 3D view
- [ ] Export HTML: file .html mở được trong browser với bảng đầy đủ
- [ ] Reset Color: khôi phục màu gốc

**Dependencies:** Task 2  
**Size:** S | Files: 2

---

### Phase 4: UI & Command

---

#### Task 10 — `SimpleEventHandler.cs`

**Copy nguyên từ** `Antigravity.CheckFloorElevation/UI/SimpleEventHandler.cs`:
```csharp
// Enqueue Action, execute on Revit thread via ExternalEvent
```

**AC:** Build được, không lỗi  
**Dependencies:** Task 1  
**Size:** XS | Files: 1

---

#### Task 11 — `WallMepClashDialog.xaml` + `WallMepClashDialog.xaml.cs`

**XAML — Header controls (thêm so với CheckFloorElevation):**
```xml
<!-- Row 1: Link selection (copy từ CheckFloorElevation) -->
<ComboBox x:Name="CmbLinks" ... />  <!-- Chọn file link -->

<!-- Row 2: Categories (NEW - multi-select ListBox in Popup) -->
<ComboBox x:Name="CmbCategories" ...>
    <!-- Template: checked listbox với checkbox cho mỗi category -->
</ComboBox>

<!-- Row 3: Angle threshold -->
<TextBlock Text="Parallel ≤ °"/>
<TextBox x:Name="TxtThreshold" Text="10" Width="60"/>
```

**Code-behind pattern (theo CheckFloorElevation):**
```csharp
private void LoadLinks()           // GetLoadedLinks() → CmbLinks
private void CmbLinks_SelectionChanged() // → LoadCategoriesForLink()
private void LoadCategoriesForLink()     // GetAvailableMepCategories() → CmbCategories
private void BtnRun_Click()        // ExecuteOnRevitThread → WallMepClashDetector.RunCheck()
private void BtnShow3D_Click()     // ExecuteOnRevitThread → ClashViewService.ShowClashIn3D()
private void BtnApplyColor_Click() // ExecuteOnRevitThread → ColorOverrideService
private void BtnExportHtml_Click() // ReportService.Export()
private void BtnReset_Click()      // ExecuteOnRevitThread → ColorOverrideService.Reset()
```

**DataGrid columns:**
| Column | Binding |
|--------|---------|
| Wall Id | HostWallId |
| MEP Id | LinkMepId |
| Wall Name | WallTypeName |
| MEP Name | MepName |
| Angle (°) | AngleDeg |
| Level | LevelName |
| Status | "Clash" (always red) |

**AC:**
- [ ] Dialog mở, link combobox populate đúng
- [ ] Chọn link → categories load đúng từ link đó
- [ ] Run Check: progress bar cập nhật, grid fill kết quả
- [ ] Show 3D: isolate clash trong 3D view
- [ ] Apply Color: tô màu đỏ walls clash
- [ ] Export HTML: mở browser
- [ ] Reset: xóa màu

**Dependencies:** Tasks 7, 8, 9, 10  
**Size:** L | Files: 2

---

#### Task 12 — `WallMepClashCommand.cs`

**Clone từ** `CheckFloorElevationCommand.cs`:
```csharp
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class WallMepClashCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uiDoc = commandData.Application.ActiveUIDocument;
        var dialog = new WallMepClashDialog(uiDoc);
        IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
        new WindowInteropHelper(dialog).Owner = handle;
        dialog.Show();
        return Result.Succeeded;
    }
}
```

**AC:** Command chạy từ Revit, mở dialog  
**Dependencies:** Task 11  
**Size:** XS | Files: 1

---

#### Task 13 — Đăng ký ribbon button trong `App.cs` + cập nhật `Antigravity.sln`

**Trong `App.cs`** — thêm vào panel "KIỂM SOÁT XUNG ĐỘT" (sau `btnCheckFloorElevation`):
```csharp
PushButtonData btnWallMepClash = new PushButtonData(
    "btnWallMepClash",
    "Tường\nMEP Clash",
    assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.WallMepClash.dll"),
    "Antigravity.WallMepClash.WallMepClashCommand");
btnWallMepClash.ToolTip = "Phát hiện va chạm song song giữa Tường host và đường ống/thiết bị MEP từ file link.";
btnWallMepClash.LargeImage = logoImage;
clashPanel.AddItem(btnWallMepClash);
```

**Trong `Antigravity.sln`** — thêm project entry:
```
Project("{...}") = "Antigravity.WallMepClash",
    "src\Antigravity.WallMepClash\Antigravity.WallMepClash.csproj", "{NEW-GUID}"
EndProject
```

**AC:** Button xuất hiện trên ribbon Revit tab VILAIVIET  
**Dependencies:** Task 12  
**Size:** XS | Files: 2

---

### ✅ Checkpoint 2: End-to-End Integration
- [ ] Build toàn solution không lỗi
- [ ] Deploy: chạy `scripts/deploy/Deploy-ToRevit.ps1` hoặc build → copy DLL
- [ ] Revit: button xuất hiện trên ribbon
- [ ] Chọn file link điện → categories hiển thị đúng
- [ ] Run Check: kết quả grid fill đúng
- [ ] Show 3D: zoom vào clash
- [ ] Export HTML: file báo cáo đúng format

---

## Dependency Graph

```
Task 1 (Project setup)
├── Task 2  (Models)
├── Task 3  (MepLinkCollectorService)
├── Task 4  (WallCollectorService)
├── Task 5  (BoundingBoxHelper)         ← Critical: transform coords
├── Task 6  (AngleClassifier)
├── Task 10 (SimpleEventHandler)
│
Task 2 + 3 + 4 + 5 + 6
└── Task 7  (WallMepClashDetector)      ← Core logic
        └── Task 8  (ClashViewService)
        └── Task 9  (ColorOverrideService + ReportService)
                └── Task 10 (SimpleEventHandler)
                └── Task 11 (Dialog XAML + Code-behind)
                        └── Task 12 (Command)
                                └── Task 13 (App.cs + sln registration)
```

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| BB transform sai (link có rotation) | High | Test với link model bị rotate, verify `GetTotalTransform()` |
| Wall curved không có direction rõ | Med | Dùng `(wall.Location as LocationCurve)?.Curve` → lấy tangent tại midpoint |
| Point-based MEP (không có LocationCurve) | Med | `null` direction → skip, không báo cáo (documented) |
| Model lớn: 10k walls × 5k MEP = 50M pairs | High | Pre-filter bằng BB trước angle check; level-based filter option |
| Show3D: MEP link không isolate được | Low | Section box + wall highlight = acceptable; document trong UI |
| Categories load chậm (link lớn) | Low | Cache categories list sau lần load đầu |

---

## Files Tạo Mới

| File | Size | Task |
|------|------|------|
| `Antigravity.WallMepClash.csproj` | XS | 1 |
| `Models/ClashCheckSettings.cs` | XS | 2 |
| `Models/ClashResult.cs` | XS | 2 |
| `Services/MepLinkCollectorService.cs` | S | 3 |
| `Services/WallCollectorService.cs` | XS | 4 |
| `Services/BoundingBoxHelper.cs` | M | 5 |
| `Services/AngleClassifier.cs` | S | 6 |
| `Services/WallMepClashDetector.cs` | M | 7 |
| `Services/ClashViewService.cs` | S | 8 |
| `Services/ColorOverrideService.cs` | S | 9 |
| `Services/ReportService.cs` | S | 9 |
| `UI/SimpleEventHandler.cs` | XS | 10 |
| `UI/WallMepClashDialog.xaml` | M | 11 |
| `UI/WallMepClashDialog.xaml.cs` | L | 11 |
| `WallMepClashCommand.cs` | XS | 12 |

## Files Modify

| File | Thay đổi | Task |
|------|----------|------|
| `src/Antigravity.Main/App.cs` | Thêm button ribbon | 13 |
| `Antigravity.sln` | Thêm project entry | 13 |

---

## Verification Plan

### Build Test
```powershell
# Từ solution root
dotnet build Antigravity.sln -c Debug
```

### Manual Revit Test
1. Open model có tường, load 1 file link MEP
2. Click **Tường MEP Clash** button
3. Chọn link file → kiểm tra categories load đúng
4. Chọn categories, set threshold 10°, click **Run Check**
5. Verify kết quả đúng (cặp song song có, vuông góc không có)
6. Click 1 row → **Show 3D** → verify zoom đúng vị trí
7. **Apply Color** → walls đỏ trong 3D
8. **Export HTML** → file mở trong browser

### Edge Cases
- Link file không có MEP element → "0 clashes found" message
- Không có link nào loaded → warning message
- Curved wall → test behavior
- Wall thickness 0 (abstract wall) → skip gracefully
