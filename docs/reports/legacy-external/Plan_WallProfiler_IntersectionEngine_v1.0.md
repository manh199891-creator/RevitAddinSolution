# 📦 HANDOVER PLAN: WallProfiler — Nâng cấp Intersection Engine v1.0

> **Dự án:** `E:\Antigravity\Join file link\`  
> **Namespace:** `WallProfiler`  
> **Ngày tạo:** 2026-06-08  
> **Trạng thái:** ✅ Đã được approve — sẵn sàng triển khai  
> **Version:** v1.0

---

## 🎯 MỤC TIÊU TỔNG THỂ

Nâng cấp toàn diện **thuật toán xử lý giao cắt hình học (Intersection Engine)** của WallProfiler, bổ sung 2 tính năng mới (Clearance + Auto-Update DMU), và tối ưu logic phân loại VoidCut cho dầm nghiêng / sàn vát.

**3 nhóm thay đổi chính:**
1. **Geometry Engine Upgrade** — Cắt chính xác hơn: chỉ cắt phần giao cắt (Intersect Solid), không cắt toàn bộ dầm dài 10m. Bổ sung Clearance bằng Multi-Transform Union.
2. **New Feature: Clearance (Khoảng hở)** — Cho phép nhập X mm để void cut rộng hơn tiết diện thực.
3. **New Feature: Auto-Update DMU** — Tự động cập nhật void khi tường di chuyển, có bật/tắt.

---

## 🗂️ DANH SÁCH FILE CẦN THAY ĐỔI

```
E:\Antigravity\Join file link\
├── Models\
│   └── ProfileResult.cs            [MODIFY] — thêm ClearanceMm, EnableAutoUpdate vào ProfileSettings
├── Services\
│   ├── SolidGeometryHelper.cs      [NEW]    — helper tính Clearance Solid (Multi-Transform Union)
│   ├── IntersectionClassifier.cs   [MODIFY] — dùng Exact Intersect Solid, xử lý dầm nghiêng
│   ├── WallDirectShapeCutter.cs    [MODIFY] — nhận clearanceMm, tạo void từ Intersect Solid
│   └── WallChangeUpdater.cs        [NEW]    — IUpdater (DMU) tự động re-cut khi tường dịch chuyển
└── UI\
    ├── MainWindow.xaml              [MODIFY] — thêm txtClearance, chkAutoUpdate
    └── MainWindow.xaml.cs           [MODIFY] — đọc txtClearance, chkAutoUpdate vào BuildSettings()
WallProfilerCommand.cs              [MODIFY] — truyền clearanceMm xuống CutOpenings, đăng ký DMU
```

---

## 📋 PHASE 01 — Bổ sung `ProfileSettings` (Model)

### 📄 [MODIFY] `Models/ProfileResult.cs`

**Tìm class `ProfileSettings`, thêm 2 property mới:**

```csharp
/// <summary>
/// Khoảng hở (mm) mở rộng khối Void ra quanh cấu kiện kết cấu.
/// 0 = cắt khít 100% hình học. Khuyến nghị: 10-20mm.
/// </summary>
public double ClearanceMm { get; set; } = 0;

/// <summary>
/// Bật/tắt Dynamic Model Update: tự động re-cut khi Tường bị di chuyển.
/// </summary>
public bool EnableAutoUpdate { get; set; } = false;
```

---

## 📋 PHASE 02 — Tạo `SolidGeometryHelper.cs` (Service mới)

### 📄 [NEW] `Services/SolidGeometryHelper.cs`

**Chức năng:** Tạo khối Void "béo hơn" từ một Solid gốc bằng thuật toán Multi-Transform Union (vì Revit API không có `Solid.Offset()`).

**Thuật toán Multi-Transform Union:**
1. Lấy Solid gốc (chính xác hình dạng của cấu kiện đã được transform về host coords).
2. Tạo 6 bản sao của solid đó, mỗi bản được tịnh tiến theo 1 trục:
   - `+X`, `-X`, `+Y`, `-Y`, `+Z`, `-Z` — mỗi bản dịch một khoảng = `clearanceFt`.
3. Union tất cả 7 khối (gốc + 6 bản sao) lại bằng `BooleanOperationsUtils.ExecuteBooleanOperation(Union)`.
4. Kết quả là một khối "phồng" đều quanh solid gốc đúng = clearance theo mọi hướng.

> ⚠️ **Lưu ý quan trọng:** Phép Union phải thực hiện từng bước một (a Union b, rồi result Union c, ...). KHÔNG union tất cả cùng lúc.
> ⚠️ Nếu BooleanOperation fail (geometry quá phức tạp), fallback về solid gốc và log cảnh báo.

```csharp
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace WallProfiler.Services
{
    /// <summary>
    /// Các tiện ích hình học cho Solid trong không gian 3D.
    /// </summary>
    public static class SolidGeometryHelper
    {
        /// <summary>
        /// Tạo khối Solid "phồng" hơn solid gốc bằng Multi-Transform Union.
        /// Dùng khi cần tạo Clearance (khoảng hở) quanh cấu kiện kết cấu.
        /// Nếu clearanceFt <= 0 hoặc Boolean fail, trả về solid gốc.
        /// </summary>
        /// <param name="baseSolid">Solid gốc (đã ở host coords).</param>
        /// <param name="clearanceFt">Khoảng hở, đơn vị feet (internal unit Revit).</param>
        /// <returns>Solid mới đã được mở rộng, hoặc solid gốc nếu fail.</returns>
        public static Solid CreateClearanceSolid(Solid baseSolid, double clearanceFt)
        {
            if (baseSolid == null) return null;
            if (clearanceFt <= 1e-6) return baseSolid; // 0 clearance → trả về gốc

            // Danh sách 6 hướng tịnh tiến
            var offsets = new[]
            {
                new XYZ( clearanceFt,  0,             0),
                new XYZ(-clearanceFt,  0,             0),
                new XYZ( 0,            clearanceFt,   0),
                new XYZ( 0,           -clearanceFt,   0),
                new XYZ( 0,            0,             clearanceFt),
                new XYZ( 0,            0,            -clearanceFt),
            };

            Solid result = baseSolid;

            foreach (var offset in offsets)
            {
                try
                {
                    var tf = Transform.CreateTranslation(offset);
                    Solid shifted = SolidUtils.CreateTransformed(baseSolid, tf);

                    if (shifted == null || shifted.Volume <= 1e-9) continue;

                    Solid union = BooleanOperationsUtils.ExecuteBooleanOperation(
                        result, shifted, BooleanOperationsType.Union);

                    if (union != null && union.Volume > result.Volume)
                        result = union;
                }
                catch
                {
                    // Union fail cho hướng này → bỏ qua, tiếp tục hướng khác
                }
            }

            return result;
        }

        /// <summary>
        /// Tính Solid giao cắt (Intersect) giữa tường và cấu kiện kết cấu.
        /// Trả về null nếu không có giao cắt hoặc BooleanOp fail.
        /// </summary>
        public static Solid GetIntersectSolid(Solid wallSolid, Solid structureSolid)
        {
            if (wallSolid == null || structureSolid == null) return null;
            try
            {
                Solid intersect = BooleanOperationsUtils.ExecuteBooleanOperation(
                    wallSolid, structureSolid, BooleanOperationsType.Intersect);

                return (intersect != null && intersect.Volume > 1e-9) ? intersect : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Lấy Solid lớn nhất từ GeometryElement của một Element (host hoặc linked đã transform).
        /// </summary>
        public static Solid GetLargestSolid(Element elem)
        {
            Solid largest = null;
            try
            {
                var opts = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
                var geo = elem.get_Geometry(opts);
                if (geo == null) return null;

                foreach (var obj in geo)
                {
                    if (obj is Solid s && s.Volume > 1e-9)
                    {
                        if (largest == null || s.Volume > largest.Volume) largest = s;
                    }
                    else if (obj is GeometryInstance gi)
                    {
                        foreach (var sub in gi.GetInstanceGeometry())
                        {
                            if (sub is Solid s2 && s2.Volume > 1e-9)
                            {
                                if (largest == null || s2.Volume > largest.Volume) largest = s2;
                            }
                        }
                    }
                }
            }
            catch { }
            return largest;
        }
    }
}
```

---

## 📋 PHASE 03 — Nâng cấp `IntersectionClassifier.cs`

### 📄 [MODIFY] `Services/IntersectionClassifier.cs`

**Mục tiêu:**
1. Bổ sung tham số `clearanceMm` vào signature của `Classify()`.
2. Khi phân loại **VoidCut**: thay vì lưu `vc.Geometry = info.Geometry` (toàn bộ dầm), lưu `vc.IntersectSolid = phần giao cắt thực sự` với tường.
3. Xử lý dầm nghiêng: dùng `GetBottomZByFaceNormal()` (đã có) để lấy Z chính xác nhất.

**Thay đổi cụ thể:**

#### 3.1. Thay đổi signature của `Classify()`

```csharp
// CŨ:
public static (List<FlatElementInfo> flatTop, List<VoidCutInfo> voidCuts) Classify(
    Document doc, Wall wall, IList<TransformedSolidInfo> intersectingInfos,
    double manualZOffset = 0.0)

// MỚI: thêm clearanceMm
public static (List<FlatElementInfo> flatTop, List<VoidCutInfo> voidCuts) Classify(
    Document doc, Wall wall, IList<TransformedSolidInfo> intersectingInfos,
    double manualZOffset = 0.0,
    double clearanceMm = 0.0)
```

#### 3.2. Khi tạo `VoidCutInfo`, ưu tiên dùng Intersect Solid

Trong phần `else` (tạo VoidCutInfo), thay đổi logic lấy `IntersectSolid`:

```csharp
// Tính Solid giao cắt chính xác giữa tường và cấu kiện
// → Chỉ cắt đúng phần thân dầm đâm vào tường, không cắt toàn bộ dầm dài
Solid exactIntersect = null;
if (wallSolid != null)
{
    exactIntersect = SolidGeometryHelper.GetIntersectSolid(wallSolid, info.Geometry);
}

// Solid dùng để cắt = intersect solid (nếu có) hoặc fallback toàn bộ solid
Solid solidForCut = exactIntersect ?? info.Geometry;

// Áp dụng Clearance (Multi-Transform Union) nếu clearanceMm > 0
if (clearanceMm > 1e-6 && solidForCut != null)
{
    double clearanceFt = clearanceMm / 304.8;
    Solid enlarged = SolidGeometryHelper.CreateClearanceSolid(solidForCut, clearanceFt);
    if (enlarged != null) solidForCut = enlarged;
}

voidCuts.Add(new VoidCutInfo
{
    Geometry       = solidForCut,      // ← Solid đã được clip + clearance
    IntersectSolid = exactIntersect,   // ← Solid giao cắt gốc (để tham chiếu)
    BottomZ        = compPreciseBottomZ,
    TopZ           = compMaxZ,
    Center         = center,
    WidthAlongWall = width,
    CategoryName   = info.CategoryName
});
```

#### 3.3. Tương tự cho FlatTop khi tái phân loại thành VoidCut (phần `flatTop.Count > 1`)

Áp dụng cùng pattern: tính `exactIntersect`, tạo `clearanceSolid`, gán `Geometry = solidForCut`.

#### 3.4. Xử lý TopOffset cho sàn/dầm nghiêng

Trong phần tính `FlatElementInfo`, bổ sung logic lấy Z chính xác tại footprint tường:

```csharp
// ĐÃ CÓ: GetBottomZByFaceNormal tìm face hướng xuống → lấy Z thấp nhất của mặt đáy
// BỔ SUNG: Nếu dầm nghiêng, Z tại vị trí tường có thể khác Z thấp nhất tổng thể
// → Tính Z trung bình của các vertex face hướng xuống nằm trong footprint tường
double precisBottomZ = GetBottomZAtWallFootprint(info.Geometry, wall);
// Nếu không tính được → fallback về GetBottomZByFaceNormal
if (double.IsNaN(precisBottomZ) || precisBottomZ == double.MaxValue)
    precisBottomZ = GetBottomZByFaceNormal(info.Geometry);

// Trừ Clearance khỏi TopOffset (hạ thấp đỉnh tường thêm 1 khoảng = clearance)
double bottomZWithClearance = precisBottomZ - (clearanceMm / 304.8);

flatTop.Add(new FlatElementInfo
{
    Geometry     = info.Geometry,
    BottomZ      = bottomZWithClearance,   // ← Đã bao gồm clearance
    CategoryName = catName
});
```

**Hàm mới cần thêm vào `IntersectionClassifier.cs`:**

```csharp
/// <summary>
/// Lấy Z đáy của mặt hướng xuống TẠI vị trí footprint của tường.
/// Chính xác hơn GetBottomZByFaceNormal() khi dầm/sàn nghiêng.
/// </summary>
private static double GetBottomZAtWallFootprint(Solid structureSolid, Wall wall)
{
    try
    {
        if (!(wall.Location is LocationCurve lc)) return double.NaN;

        // Lấy BoundingBox của tường để xác định vùng footprint
        var wallBB = wall.get_BoundingBox(null);
        if (wallBB == null) return double.NaN;

        double wallMinX = wallBB.Min.X - 0.5; // mở rộng 150mm mỗi phía
        double wallMaxX = wallBB.Max.X + 0.5;
        double wallMinY = wallBB.Min.Y - 0.5;
        double wallMaxY = wallBB.Max.Y + 0.5;

        double bottomZ = double.MaxValue;

        foreach (Face face in structureSolid.Faces)
        {
            if (face == null) continue;

            BoundingBoxUV uvBB = face.GetBoundingBox();
            if (uvBB == null) continue;

            UV midUV = new UV(
                (uvBB.Min.U + uvBB.Max.U) / 2.0,
                (uvBB.Min.V + uvBB.Max.V) / 2.0);

            XYZ normal;
            try { normal = face.ComputeNormal(midUV); }
            catch { continue; }

            if (normal == null || normal.Z >= -0.95) continue; // Chỉ lấy face hướng xuống

            var mesh = face.Triangulate();
            if (mesh == null) continue;

            foreach (XYZ vertex in mesh.Vertices)
            {
                // Chỉ lấy vertex nằm trong vùng footprint tường (XY)
                if (vertex.X < wallMinX || vertex.X > wallMaxX) continue;
                if (vertex.Y < wallMinY || vertex.Y > wallMaxY) continue;

                if (vertex.Z < bottomZ) bottomZ = vertex.Z;
            }
        }

        return bottomZ;
    }
    catch { return double.NaN; }
}
```

---

## 📋 PHASE 04 — Nâng cấp `WallDirectShapeCutter.cs`

### 📄 [MODIFY] `Services/WallDirectShapeCutter.cs`

**Mục tiêu:** Nhận Solid đã được clip+clearance từ `IntersectionClassifier`, không cần tự tính clearance nữa.

#### 4.1. Thay đổi signature `CutOpenings()` — thêm `clearanceMm`

```csharp
// CŨ:
public static List<CutResult> CutOpenings(
    Document doc, Wall wall, List<VoidCutInfo> voidCuts, double zOffsetMm)

// MỚI:
public static List<CutResult> CutOpenings(
    Document doc, Wall wall, List<VoidCutInfo> voidCuts, 
    double zOffsetMm, double clearanceMm = 0)
```

#### 4.2. Trong `CutOpenings()` — Dùng `vc.Geometry` trực tiếp

Vì `vc.Geometry` từ Phase 03 đã là Solid đã clip + clearance, `CutOpenings()` chỉ cần dùng nó trực tiếp:

```csharp
foreach (var vc in voidCuts)
{
    // vc.Geometry đã được IntersectionClassifier xử lý:
    //   - Clip thành Intersect Solid (chỉ phần đâm vào tường)
    //   - Áp Clearance nếu clearanceMm > 0
    // WallDirectShapeCutter chỉ việc đưa vào DirectShape + SolidSolidCut
    Solid cutterSolid = vc.Geometry;

    if (cutterSolid == null || cutterSolid.Volume <= 1e-6)
    {
        results.Add(new CutResult { Success = false, Note = "Khối cắt rỗng hoặc quá nhỏ" });
        continue;
    }

    // ... (phần còn lại giữ nguyên: TrySolidSolidCut → TryNewOpening fallback)
}
```

> ℹ️ Phần `BuildOrientedWallBox` và `TrySolidSolidCut` giữ nguyên, không thay đổi.

---

## 📋 PHASE 05 — Nâng cấp UI `MainWindow.xaml` + `MainWindow.xaml.cs`

### 📄 [MODIFY] `UI/MainWindow.xaml`

**Tìm phần chứa `txtZOffset` (textbox Manual Z Offset), thêm 2 control mới VÀO PHÍA DƯỚI nó:**

```xml
<!-- Clearance -->
<TextBlock Text="Khoảng hở (mm):" Margin="0,8,0,2" FontSize="12"
           ToolTip="Mở rộng vùng cắt Void ra quanh dầm/cột. 0 = cắt khít. Khuyến nghị: 10-20mm."/>
<TextBox x:Name="txtClearance" Text="0" Width="80" HorizontalAlignment="Left"
         ToolTip="Nhập số mm khoảng hở (VD: 10 = mở rộng 10mm quanh dầm/cột)"/>

<!-- Auto-Update DMU -->
<CheckBox x:Name="chkAutoUpdate" Content="Tự động cắt lại khi tường di chuyển (DMU)"
          IsChecked="False" Margin="0,8,0,0" FontSize="12"
          ToolTip="Kích hoạt Dynamic Model Update. Khi tường bị dịch chuyển, void sẽ tự cập nhật. Có thể làm Revit hơi chậm."/>
```

### 📄 [MODIFY] `UI/MainWindow.xaml.cs`

**Trong `BuildSettings()`, thêm đọc 2 giá trị mới:**

```csharp
// Đọc Clearance
if (!double.TryParse(txtClearance.Text, out double clearanceMm) || clearanceMm < 0)
    clearanceMm = 0;

// Đọc Auto-Update
bool enableAutoUpdate = chkAutoUpdate.IsChecked == true;

return new ProfileSettings
{
    // ... (giữ các dòng cũ) ...
    ClearanceMm     = clearanceMm,
    EnableAutoUpdate = enableAutoUpdate
};
```

**Trong `BtnAnalyze_Click()`, truyền `clearanceMm` vào `Classify()`:**

```csharp
// CŨ:
var (flatTop, voidCuts) = IntersectionClassifier.Classify(_doc, wall, intersectingInfos, manualZOffsetFt);

// MỚI:
double clearanceFt = settings.ClearanceMm / 304.8;
var (flatTop, voidCuts) = IntersectionClassifier.Classify(
    _doc, wall, intersectingInfos, manualZOffsetFt, settings.ClearanceMm);
```

---

## 📋 PHASE 06 — Tạo `WallChangeUpdater.cs` (Auto-Update DMU)

### 📄 [NEW] `Services/WallChangeUpdater.cs`

**Chức năng:** Lắng nghe sự kiện khi Tường bị di chuyển/thay đổi hình học, tự động trigger lại lệnh cắt Void Cut cho tường đó.

> ⚠️ **Hạn chế của IUpdater trong Revit:** IUpdater chạy TRONG transaction của Revit. Ta KHÔNG được tạo transaction mới bên trong `Execute()` của IUpdater. Mọi thay đổi phải thực hiện qua `UpdaterData` hoặc phải defer lại sau khi transaction của Revit kết thúc bằng `ExternalEvent`.

**Strategy đúng:** Khi `IUpdater.Execute()` được gọi:
1. Thu thập danh sách tường bị thay đổi.
2. Đưa danh sách vào một Queue (`static ConcurrentQueue<ElementId>`).
3. Raise một `ExternalEvent` — ExternalEvent sẽ được Revit xử lý ở vòng lặp kế tiếp (sau khi transaction hiện tại của Revit kết thúc), đảm bảo an toàn để tạo Transaction mới.

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using WallProfiler.Models;

namespace WallProfiler.Services
{
    /// <summary>
    /// Dynamic Model Updater — tự động re-cut Void khi tường di chuyển.
    /// Đăng ký/hủy đăng ký từ WallProfilerCommand hoặc App.cs khi user bật/tắt.
    /// </summary>
    public class WallChangeUpdater : IUpdater
    {
        // ── Singleton-like registry ─────────────────────────────────────────
        private static WallChangeUpdater _instance;
        private static ExternalEvent      _externalEvent;
        private static ReVoidHandler      _handler;

        public static readonly ConcurrentQueue<ElementId> PendingWalls = new ConcurrentQueue<ElementId>();

        // ── IUpdater identity ───────────────────────────────────────────────
        private readonly UpdaterId _updaterId;
        private readonly AddInId   _addInId;

        public WallChangeUpdater(AddInId addInId)
        {
            _addInId   = addInId;
            _updaterId = new UpdaterId(addInId, new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890"));
        }

        // ── Register / Unregister ───────────────────────────────────────────
        public static void Register(UIApplication uiApp, AddInId addInId)
        {
            if (_instance != null) return; // Đã đăng ký rồi

            _instance = new WallChangeUpdater(addInId);
            UpdaterRegistry.RegisterUpdater(_instance, uiApp.ActiveUIDocument.Document);

            // Lắng nghe thay đổi Geometry của Wall
            var filter = new ElementClassFilter(typeof(Wall));
            UpdaterRegistry.AddTrigger(
                _instance._updaterId,
                uiApp.ActiveUIDocument.Document,
                filter,
                Element.GetChangeTypeGeometry());

            // Khởi tạo ExternalEvent để thực hiện cắt sau khi transaction kết thúc
            _handler       = new ReVoidHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public static void Unregister(Document doc)
        {
            if (_instance == null) return;
            try { UpdaterRegistry.UnregisterUpdater(_instance._updaterId, doc); } catch { }
            _instance = null;
        }

        // ── IUpdater implementation ─────────────────────────────────────────
        public void Execute(UpdaterData data)
        {
            // Thu thập tường bị thay đổi vào queue
            foreach (var id in data.GetModifiedElementIds())
                PendingWalls.Enqueue(id);

            // Raise ExternalEvent (sẽ được Revit xử lý sau khi transaction kết thúc)
            _externalEvent?.Raise();
        }

        public string GetAdditionalInformation() => "WallProfiler Auto-Update: Re-cuts voids when walls move.";
        public ChangePriority GetChangePriority()  => ChangePriority.MEPFixtures;
        public UpdaterId GetUpdaterId()            => _updaterId;
        public string GetUpdaterName()             => "WallProfiler.WallChangeUpdater";
    }

    // ── ExternalEvent Handler ────────────────────────────────────────────────
    /// <summary>
    /// Được Revit gọi sau khi transaction kết thúc. Thực hiện re-cut void an toàn.
    /// </summary>
    internal class ReVoidHandler : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null) return;

            // Lấy settings từ ProfileSettings lưu trữ (cần implement lưu trữ đơn giản)
            var settings = WallProfilerSettings.Last; // ← Xem Phase 07
            if (settings == null) return;

            // Xử lý từng tường trong queue
            var toProcess = new List<ElementId>();
            while (WallChangeUpdater.PendingWalls.TryDequeue(out var id))
                toProcess.Add(id);

            if (!toProcess.Any()) return;

            // Rebuild link cache (chỉ cần lấy lại solids, không collect Wall)
            var linkCache = StructureDetector.BuildLinkCache(doc);

            using (var txGroup = new TransactionGroup(doc, "WallProfiler: Auto Re-Cut Voids"))
            {
                txGroup.Start();

                foreach (var wallId in toProcess.Distinct())
                {
                    try
                    {
                        var wall = doc.GetElement(wallId) as Wall;
                        if (wall == null || wall.IsValidObject == false) continue;

                        // Tìm lại giao cắt
                        var intersecting = StructureDetector.FindIntersecting(
                            wall, linkCache, settings.SearchRangeMm,
                            settings.IncludeFloors, settings.IncludeBeams,
                            settings.IncludeColumns, settings.IncludeStructuralWalls);

                        if (!intersecting.Any()) continue;

                        double manualZFt = settings.ManualZOffsetMm / 304.8;
                        var (_, voidCuts) = IntersectionClassifier.Classify(
                            doc, wall, intersecting, manualZFt, settings.ClearanceMm);

                        if (!voidCuts.Any()) continue;

                        using (var tx = new Transaction(doc, $"Auto Re-Cut: Wall {wallId.Value}"))
                        {
                            tx.Start();
                            doc.Regenerate();

                            var opts = tx.GetFailureHandlingOptions();
                            opts.SetFailuresPreprocessor(new SilentPreprocessor());
                            tx.SetFailureHandlingOptions(opts);

                            WallDirectShapeCutter.CutOpenings(doc, wall, voidCuts,
                                settings.ManualZOffsetMm, settings.ClearanceMm);

                            tx.Commit();
                        }
                    }
                    catch { /* Lỗi cho 1 tường không được phép dừng toàn bộ batch */ }
                }

                txGroup.Assimilate();
            }
        }

        public string GetName() => "WallProfiler.ReVoidHandler";
    }

    // ── SilentPreprocessor (copy từ WallProfilerCommand) ───────────────────
    internal class SilentPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor f)
        {
            foreach (var m in f.GetFailureMessages().ToList())
            {
                if (m.GetSeverity() == FailureSeverity.Warning)
                    f.DeleteWarning(m);
                else if (m.GetSeverity() == FailureSeverity.Error && m.HasResolutions())
                    f.ResolveFailure(m);
            }
            return FailureProcessingResult.Continue;
        }
    }
}
```

---

## 📋 PHASE 07 — Tạo `WallProfilerSettings.cs` (Settings Registry đơn giản)

### 📄 [NEW] `Services/WallProfilerSettings.cs`

DMU cần biết settings đã dùng lần cuối (Clearance, SearchRange...) để tái sử dụng khi auto re-cut.

```csharp
using WallProfiler.Models;

namespace WallProfiler.Services
{
    /// <summary>
    /// Lưu ProfileSettings cuối cùng được user Apply.
    /// Dùng bởi WallChangeUpdater (DMU) để auto re-cut với cùng settings.
    /// </summary>
    public static class WallProfilerSettings
    {
        /// <summary>Settings cuối cùng được Apply. null nếu chưa chạy lần nào.</summary>
        public static ProfileSettings Last { get; set; }
    }
}
```

---

## 📋 PHASE 08 — Nâng cấp `WallProfilerCommand.cs`

### 📄 [MODIFY] `WallProfilerCommand.cs`

**Mục tiêu:** Truyền `clearanceMm` vào `CutOpenings()`, đăng ký / hủy DMU theo `settings.EnableAutoUpdate`.

#### 8.1. Lưu settings vào `WallProfilerSettings.Last` ngay sau khi lấy được settings

```csharp
var settings = dialog.Settings;
WallProfilerSettings.Last = settings; // ← THÊM DÒNG NÀY
```

#### 8.2. Truyền `clearanceMm` vào `ProcessWall()`

```csharp
// CŨ:
var result = ProcessWall(doc, wall, item, settings);

// MỚI (signature ProcessWall đã có settings, chỉ cần truyền clearanceMm xuống CutOpenings):
// Trong nội thân ProcessWall(), tìm dòng gọi WallDirectShapeCutter.CutOpenings:
cutResults = WallDirectShapeCutter.CutOpenings(
    doc, wall, item.VoidCutElements, 
    settings.ManualZOffsetMm,
    settings.ClearanceMm);   // ← THÊM THAM SỐ NÀY
```

#### 8.3. Đăng ký / hủy DMU sau khi Apply

```csharp
// Sau khi Apply xong (sau txGroup.Assimilate()):
if (settings.EnableAutoUpdate)
{
    WallChangeUpdater.Register(commandData.Application, commandData.Application.ActiveAddInId);
}
else
{
    WallChangeUpdater.Unregister(doc);
}
```

---

## ✅ CHECKLIST TRIỂN KHAI

### Phase 01 — ProfileSettings
- [ ] Thêm `ClearanceMm` (double, default=0) vào `ProfileSettings`
- [ ] Thêm `EnableAutoUpdate` (bool, default=false) vào `ProfileSettings`
- [ ] Build thử — không lỗi compile

### Phase 02 — SolidGeometryHelper.cs (File mới)
- [ ] Tạo file `Services/SolidGeometryHelper.cs`
- [ ] Implement `CreateClearanceSolid()` với Multi-Transform Union (6 hướng)
- [ ] Implement `GetIntersectSolid()` wrapper
- [ ] Implement `GetLargestSolid()` helper (refactor từ duplicate code ở nhiều chỗ)
- [ ] Build thử

### Phase 03 — IntersectionClassifier.cs
- [ ] Thêm param `clearanceMm` vào `Classify()`
- [ ] Thêm hàm `GetBottomZAtWallFootprint()` để lấy Z chính xác tại footprint tường (dầm nghiêng)
- [ ] Cập nhật logic VoidCutInfo: dùng `GetIntersectSolid` → `CreateClearanceSolid`
- [ ] Cập nhật logic FlatElementInfo: `BottomZ -= clearanceMm/304.8`
- [ ] Test logic phân loại không bị sai (kiểm tra vẫn phân FlatTop/VoidCut đúng)

### Phase 04 — WallDirectShapeCutter.cs
- [ ] Thêm param `clearanceMm` vào `CutOpenings()` (nhận thêm, không cần xử lý nữa vì Classifier đã lo)
- [ ] Đảm bảo không có code tự expand solid bên trong Cutter (để tránh nhân đôi clearance)

### Phase 05 — UI
- [ ] Thêm `txtClearance` TextBox vào `MainWindow.xaml`
- [ ] Thêm `chkAutoUpdate` CheckBox vào `MainWindow.xaml`
- [ ] Cập nhật `BuildSettings()` trong `MainWindow.xaml.cs`
- [ ] Cập nhật `BtnAnalyze_Click()` — truyền `clearanceMm` vào `Classify()`
- [ ] Test UI hiển thị đúng, giá trị đọc đúng

### Phase 06 — WallChangeUpdater.cs (File mới)
- [ ] Tạo file `Services/WallChangeUpdater.cs`
- [ ] Implement `WallChangeUpdater : IUpdater` — chỉ queue wallId và raise ExternalEvent
- [ ] Implement `ReVoidHandler : IExternalEventHandler` — thực hiện cắt thực tế
- [ ] **KHÔNG** tạo Transaction bên trong `IUpdater.Execute()`

### Phase 07 — WallProfilerSettings.cs (File mới)
- [ ] Tạo file `Services/WallProfilerSettings.cs`
- [ ] Implement static property `Last`

### Phase 08 — WallProfilerCommand.cs
- [ ] Gán `WallProfilerSettings.Last = settings`
- [ ] Truyền `clearanceMm` vào `CutOpenings()`
- [ ] Đăng ký hoặc hủy DMU theo `settings.EnableAutoUpdate`
- [ ] Build toàn bộ solution không lỗi

### Verification
- [ ] Build release: `dotnet build WallProfiler.csproj -c Release`
- [ ] Deploy: copy DLL + addin vào `C:\ProgramData\Autodesk\Revit\Addins\2024\`
- [ ] Test: Dầm thẳng đứng + cột → VoidCut đúng tiết diện
- [ ] Test: Clearance 10mm → Void rộng hơn 10mm quanh tiết diện
- [ ] Test: Sàn nghiêng đè lên tường → TopOffset đúng tại vị trí tường, không phải Z thấp nhất toàn bộ sàn
- [ ] Test: Bật DMU → dời tường → Void tự cập nhật
- [ ] Test: Tắt DMU → dời tường → không có gì xảy ra tự động

---

## 🔑 GHI CHÚ QUAN TRỌNG CHO IDE

1. **Thứ tự Build phụ thuộc:** Phải xây Phase 01 → 02 trước, vì Phase 03-08 phụ thuộc vào `ProfileSettings.ClearanceMm` và `SolidGeometryHelper`.
2. **Duplicate code cần xóa:** `GetLargestSolid()` hiện có ở cả `IntersectionClassifier.cs`, `StructureDetector.cs`, `WallDirectShapeCutter.cs`. Sau khi tạo `SolidGeometryHelper.GetLargestSolid()`, hãy refactor để dùng chung, tránh duy trì 3 bản sao.
3. **Guid cho IUpdater:** Guid `"A1B2C3D4-E5F6-7890-ABCD-EF1234567890"` trong `WallChangeUpdater` PHẢI là duy nhất và cố định. **Không thay đổi sau khi deploy**, nếu không Revit sẽ không nhận ra updater cũ và tạo duplicate.
4. **Unit tuyệt đối là feet:** `clearanceMm` ở UI (mm), nhưng khi truyền vào Revit API geometry phải chia cho 304.8 thành feet.
5. **Pattern tham khảo:** File `WallProfilerCommand.cs` có `SilentPreprocessor` — hãy tái sử dụng bản sao trong `WallChangeUpdater.cs` để tránh trùng class name. Đổi thành `private class SilentPreprocessor` thay vì `internal`.

---

*Tài liệu này được tạo bởi Antigravity Planning Agent — 2026-06-08*  
*Dự án: `E:\Antigravity\Join file link\` — WallProfiler v2.3.0*
