# 📦 HANDOVER PLAN: Antigravity.FloorElevationChecker v1.0

> **Dành cho:** IDE Agent (Cursor / Windsurf)  
> **Ngày tạo:** 2026-06-04  
> **Trạng thái:** ✅ Đã được approve — sẵn sàng triển khai  
> **Version:** v1.0

---

## 🎯 MỤC TIÊU

Tạo module Revit Add-in mới **`Antigravity.FloorElevationChecker`** kiểm tra chênh lệch cao độ Z giữa:
- **Sàn Kiến Trúc (KT):** nằm trong **host model** (file đang mở)
- **Sàn Kết Cấu (KC):** nằm trong **Linked Model** (file kết cấu được link vào)

Nếu `|ZTop_KT - ZTop_KC| > Tolerance` → flag là LỖI, đổi màu đỏ trên View và xuất HTML report có ElementID.

---

## 📐 KIẾN TRÚC TỔNG QUAN

```
Solution: Antigravity.sln
└── src/
    └── Antigravity.FloorElevationChecker/          ← [NEW PROJECT]
        ├── Antigravity.FloorElevationChecker.csproj
        ├── FloorElevationCheckerCommand.cs          ← IExternalCommand entry
        ├── Models/
        │   ├── FloorCheckResult.cs                  ← DTO kết quả
        │   └── CheckSettings.cs                     ← Cài đặt tolerance + link
        ├── Services/
        │   ├── FloorCollectorService.cs             ← Thu thập Floor từ host + link
        │   ├── ElevationService.cs                  ← GetZTop bằng HostObjectUtils
        │   ├── ElevationComparisonService.cs        ← Raycast + so sánh
        │   ├── ColorOverrideService.cs              ← Áp màu View override
        │   └── ReportService.cs                     ← Xuất HTML report
        └── UI/
            ├── FloorCheckerDialog.xaml              ← WPF Dialog chính
            └── FloorCheckerDialog.xaml.cs

src/Antigravity.Main/App.cs                          ← [MODIFY] đăng ký button ribbon
```

---

## ⚙️ THÔNG SỐ KỸ THUẬT

### Framework & References
```xml
<!-- Antigravity.FloorElevationChecker.csproj — copy pattern từ Antigravity.DrawFloors.csproj -->
<TargetFramework>net48</TargetFramework>
<References>
  - RevitAPI.dll (Autodesk.Revit.DB)
  - RevitAPIUI.dll (Autodesk.Revit.UI)
  - PresentationFramework (WPF)
  - PresentationCore (WPF)
  - WindowsBase (WPF)
  - Antigravity.Core (project reference)
</References>
```

### Namespace
```
Antigravity.FloorElevationChecker
Antigravity.FloorElevationChecker.Models
Antigravity.FloorElevationChecker.Services
Antigravity.FloorElevationChecker.UI
```

---

## 📋 PHASE 01 — Project Setup

**Mục tiêu:** Tạo project skeleton, kết nối solution, đăng ký ribbon button.

### 1.1. Tạo project file

Tạo `src/Antigravity.FloorElevationChecker/Antigravity.FloorElevationChecker.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AssemblyName>Antigravity.FloorElevationChecker</AssemblyName>
    <RootNamespace>Antigravity.FloorElevationChecker</RootNamespace>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="RevitAPI">
      <HintPath>..\..\lib\RevitAPI.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="RevitAPIUI">
      <HintPath>..\..\lib\RevitAPIUI.dll</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Antigravity.Core\Antigravity.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Page Include="UI\FloorCheckerDialog.xaml">
      <Generator>MSBuild:Compile</Generator>
      <SubType>Designer</SubType>
    </Page>
  </ItemGroup>
</Project>
```

### 1.2. Thêm vào Solution

Mở `Antigravity.sln`, thêm project mới bằng lệnh hoặc chỉnh sửa tay:
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Antigravity.FloorElevationChecker", "src\Antigravity.FloorElevationChecker\Antigravity.FloorElevationChecker.csproj", "{NEW-GUID-HERE}"
EndProject
```
*(Dùng `dotnet sln add` hoặc Visual Studio Add Existing Project)*

### 1.3. Entry Command — FloorElevationCheckerCommand.cs

```csharp
using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.FloorElevationChecker.UI;

namespace Antigravity.FloorElevationChecker
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FloorElevationCheckerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiDoc = commandData.Application.ActiveUIDocument;
                if (uiDoc == null)
                {
                    message = "Không có Document nào đang mở.";
                    return Result.Failed;
                }

                var dialog = new FloorCheckerDialog(uiDoc);
                dialog.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
```

### 1.4. Đăng ký Ribbon Button trong App.cs

**File:** `src/Antigravity.Main/App.cs`  
**Tìm đoạn** (khoảng line 218):
```csharp
clashPanel.AddItem(btnIssueManager);
clashPanel.AddItem(btnClashControl);
clashPanel.AddItem(btnDoorClearance);
```

**Thêm TRƯỚC dòng `clashPanel.AddItem(btnIssueManager)`:**
```csharp
PushButtonData btnFloorElevCheck = new PushButtonData(
    "btnFloorElevCheck",
    "Kiểm Tra\nCao Độ Sàn",
    assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.FloorElevationChecker.dll"),
    "Antigravity.FloorElevationChecker.FloorElevationCheckerCommand");

btnFloorElevCheck.ToolTip = "Kiểm tra chênh lệch cao độ Z giữa sàn Kiến Trúc (host) và sàn Kết Cấu (linked model).";
btnFloorElevCheck.LargeImage = logoImage;

clashPanel.AddItem(btnFloorElevCheck);
```

---

## 📋 PHASE 02 — Models

### 2.1. CheckSettings.cs

```csharp
using Autodesk.Revit.DB;

namespace Antigravity.FloorElevationChecker.Models
{
    /// <summary>
    /// Cài đặt cho một lần chạy kiểm tra cao độ sàn.
    /// </summary>
    public class CheckSettings
    {
        /// <summary>Ngưỡng sai lệch cho phép, đơn vị mm.</summary>
        public double ToleranceMm { get; set; } = 20.0;

        /// <summary>Linked model instance được chọn để kiểm tra sàn KC.</summary>
        public RevitLinkInstance SelectedLinkInstance { get; set; }

        /// <summary>Nếu true, chỉ kiểm tra sàn visible trong active view.</summary>
        public bool CheckActiveViewOnly { get; set; } = false;
    }
}
```

### 2.2. FloorCheckResult.cs

```csharp
namespace Antigravity.FloorElevationChecker.Models
{
    /// <summary>
    /// Kết quả kiểm tra cho một cặp sàn KT (host) — KC (link).
    /// Tất cả elevation đã quy về đơn vị mm.
    /// </summary>
    public class FloorCheckResult
    {
        /// <summary>ElementId của sàn KT trong host model.</summary>
        public int HostFloorId { get; set; }

        /// <summary>ElementId của sàn KC trong linked document. -1 nếu không tìm thấy.</summary>
        public int LinkFloorId { get; set; }

        /// <summary>Tên level của sàn KT.</summary>
        public string LevelName { get; set; }

        /// <summary>Cao độ ZTop của sàn KT (mm, tính từ Project Base Point).</summary>
        public double ZTopHost_mm { get; set; }

        /// <summary>Cao độ ZTop của sàn KC sau khi transform về Host space (mm).</summary>
        public double ZTopLink_mm { get; set; }

        /// <summary>Chênh lệch tuyệt đối ZTop_KT - ZTop_KC (mm).</summary>
        public double DeltaZ_mm => ZTopHost_mm - ZTopLink_mm;

        /// <summary>True nếu |DeltaZ_mm| > tolerance.</summary>
        public bool IsError { get; set; }

        /// <summary>True nếu không tìm thấy sàn KC tương ứng trong link.</summary>
        public bool IsNoMatch { get; set; }

        /// <summary>Thông điệp lỗi nếu có exception trong quá trình kiểm tra.</summary>
        public string ErrorMessage { get; set; }
    }
}
```

---

## 📋 PHASE 03 — Services

### 3.1. FloorCollectorService.cs

```csharp
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.FloorElevationChecker.Services
{
    /// <summary>
    /// Thu thập danh sách Floor từ host model và từ Linked Document.
    /// </summary>
    public class FloorCollectorService
    {
        private readonly Document _hostDoc;

        public FloorCollectorService(Document hostDoc)
        {
            _hostDoc = hostDoc;
        }

        /// <summary>Lấy tất cả Floor trong host model.</summary>
        public IList<Floor> GetHostFloors()
        {
            return new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(Floor))
                .WhereElementIsNotElementType()
                .Cast<Floor>()
                .ToList();
        }

        /// <summary>
        /// Lấy tất cả Floor trong linked document.
        /// </summary>
        public IList<Floor> GetLinkFloors(Document linkedDoc)
        {
            return new FilteredElementCollector(linkedDoc)
                .OfClass(typeof(Floor))
                .WhereElementIsNotElementType()
                .Cast<Floor>()
                .ToList();
        }

        /// <summary>
        /// Lấy tất cả RevitLinkInstance đã load trong host.
        /// </summary>
        public IList<RevitLinkInstance> GetLoadedLinks()
        {
            return new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(link => link.GetLinkDocument() != null)
                .ToList();
        }

        /// <summary>Tìm View3D đầu tiên phù hợp để dùng với ReferenceIntersector.</summary>
        public View3D FindSuitable3DView()
        {
            return new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate && v.CanBePrinted);
        }
    }
}
```

### 3.2. ElevationService.cs

**⚠️ QUAN TRỌNG:** Dùng `HostObjectUtils.GetTopFaces()` thay vì `BoundingBox.Max.Z`  
Lý do: `BoundingBox` sai khi sàn nghiêng hoặc đã dùng Slab Shape Editor.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.FloorElevationChecker.Services
{
    /// <summary>
    /// Tính ZTop chính xác của Floor bằng HostObjectUtils.GetTopFaces().
    /// Đơn vị trả về: feet (Revit internal). Caller tự quy đổi sang mm nếu cần.
    /// </summary>
    public static class ElevationService
    {
        private const double FeetToMm = 304.8;

        /// <summary>
        /// Lấy ZTop (feet) của một sàn trong document chỉ định.
        /// Dùng HostObjectUtils.GetTopFaces → face.Evaluate(UV.Zero).
        /// </summary>
        /// <param name="floor">Floor element.</param>
        /// <param name="doc">Document chứa floor này (có thể là host hoặc linked doc).</param>
        /// <param name="transform">
        /// Transform để chuyển từ linked doc space về host space.
        /// Truyền Transform.Identity nếu floor nằm trong host.
        /// </param>
        /// <returns>ZTop tính từ project internal origin, đơn vị feet. null nếu không lấy được.</returns>
        public static double? GetZTopFeet(Floor floor, Document doc, Transform transform = null)
        {
            try
            {
                // Lấy references của top faces từ HostObjectUtils
                IList<Reference> topFaceRefs = HostObjectUtils.GetTopFaces(floor);
                if (topFaceRefs == null || topFaceRefs.Count == 0)
                    return FallbackZTop(floor, doc, transform);

                double maxZ = double.MinValue;
                foreach (var faceRef in topFaceRefs)
                {
                    Face face = floor.GetGeometryObjectFromReference(faceRef) as Face;
                    if (face == null) continue;

                    // Lấy UV center của face để evaluate
                    BoundingBoxUV uvBounds = face.GetBoundingBox();
                    UV uvCenter = new UV(
                        (uvBounds.Min.U + uvBounds.Max.U) / 2.0,
                        (uvBounds.Min.V + uvBounds.Max.V) / 2.0);

                    XYZ point = face.Evaluate(uvCenter);

                    // Apply transform nếu cần (linked doc → host space)
                    if (transform != null && !transform.IsIdentity)
                        point = transform.OfPoint(point);

                    if (point.Z > maxZ)
                        maxZ = point.Z;
                }

                return maxZ == double.MinValue ? (double?)null : maxZ;
            }
            catch
            {
                return FallbackZTop(floor, doc, transform);
            }
        }

        /// <summary>Fallback: dùng BoundingBox.Max.Z nếu GetTopFaces thất bại.</summary>
        private static double? FallbackZTop(Floor floor, Document doc, Transform transform)
        {
            try
            {
                var bbox = floor.get_BoundingBox(null);
                if (bbox == null) return null;
                XYZ maxPt = bbox.Max;
                if (transform != null && !transform.IsIdentity)
                    maxPt = transform.OfPoint(maxPt);
                return maxPt.Z;
            }
            catch { return null; }
        }

        /// <summary>Tính midpoint (X,Y) từ BoundingBox của sàn. Z = Max.Z + 1ft offset để bắn tia xuống.</summary>
        public static XYZ GetMidpointAbove(Floor floor, double offsetFeet = 1.0)
        {
            var bbox = floor.get_BoundingBox(null);
            if (bbox == null) return null;

            double x = (bbox.Min.X + bbox.Max.X) / 2.0;
            double y = (bbox.Min.Y + bbox.Max.Y) / 2.0;
            double z = bbox.Max.Z + offsetFeet;
            return new XYZ(x, y, z);
        }

        /// <summary>Chuyển feet → mm.</summary>
        public static double FeetToMillimeters(double feet) => feet * FeetToMm;

        /// <summary>Chuyển mm → feet.</summary>
        public static double MillimetersToFeet(double mm) => mm / FeetToMm;
    }
}
```

### 3.3. ElevationComparisonService.cs

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.FloorElevationChecker.Models;

namespace Antigravity.FloorElevationChecker.Services
{
    /// <summary>
    /// So sánh cao độ ZTop giữa sàn KT (host) và sàn KC (linked model)
    /// bằng ReferenceIntersector raycast.
    /// </summary>
    public class ElevationComparisonService
    {
        private readonly Document _hostDoc;
        private readonly RevitLinkInstance _linkInstance;
        private readonly Document _linkedDoc;
        private readonly Transform _linkTransform;
        private readonly View3D _view3D;

        public ElevationComparisonService(
            Document hostDoc,
            RevitLinkInstance linkInstance,
            View3D view3D)
        {
            _hostDoc = hostDoc;
            _linkInstance = linkInstance;
            _linkedDoc = linkInstance.GetLinkDocument();
            _linkTransform = linkInstance.GetTotalTransform();
            _view3D = view3D;
        }

        /// <summary>
        /// Kiểm tra danh sách sàn KT, trả về kết quả so sánh với sàn KC.
        /// </summary>
        /// <param name="hostFloors">Danh sách sàn KT từ host.</param>
        /// <param name="toleranceMm">Ngưỡng sai lệch tính bằng mm.</param>
        /// <param name="progressCallback">Callback báo tiến độ (currentIndex, total).</param>
        public List<FloorCheckResult> RunCheck(
            IList<Floor> hostFloors,
            double toleranceMm,
            Action<int, int> progressCallback = null)
        {
            var results = new List<FloorCheckResult>();

            // Cấu hình ReferenceIntersector — chỉ tìm Floor trong Linked Model
            var floorFilter = new ElementClassFilter(typeof(Floor));
            var intersector = new ReferenceIntersector(floorFilter, FindReferenceTarget.Face, _view3D)
            {
                FindReferencesInRevitLinks = true
            };

            double toleranceFeet = ElevationService.MillimetersToFeet(toleranceMm);

            for (int i = 0; i < hostFloors.Count; i++)
            {
                progressCallback?.Invoke(i + 1, hostFloors.Count);

                var hostFloor = hostFloors[i];
                var result = new FloorCheckResult
                {
                    HostFloorId = hostFloor.Id.IntegerValue,
                    LinkFloorId = -1,
                    LevelName = GetLevelName(hostFloor)
                };

                try
                {
                    // Lấy ZTop của sàn KT trong host
                    double? zTopHost = ElevationService.GetZTopFeet(hostFloor, _hostDoc, Transform.Identity);
                    if (!zTopHost.HasValue)
                    {
                        result.ErrorMessage = "Không lấy được ZTop của sàn KT.";
                        results.Add(result);
                        continue;
                    }
                    result.ZTopHost_mm = ElevationService.FeetToMillimeters(zTopHost.Value);

                    // Tính midpoint để bắn tia xuống
                    XYZ originAbove = ElevationService.GetMidpointAbove(hostFloor, offsetFeet: 2.0);
                    if (originAbove == null)
                    {
                        result.ErrorMessage = "Không tính được midpoint của sàn KT.";
                        results.Add(result);
                        continue;
                    }

                    // Raycast xuống tìm sàn KC trong Linked Model
                    XYZ rayDirection = new XYZ(0, 0, -1);
                    ReferenceWithContext refWithCtx = intersector.FindNearest(originAbove, rayDirection);

                    if (refWithCtx == null)
                    {
                        result.IsNoMatch = true;
                        result.ErrorMessage = "Không tìm thấy sàn KC tương ứng trong linked model.";
                        results.Add(result);
                        continue;
                    }

                    // Lấy Floor KC từ linked document
                    Reference linkRef = refWithCtx.GetReference();
                    ElementId linkFloorId = linkRef.LinkedElementId;

                    if (linkFloorId == ElementId.InvalidElementId)
                    {
                        result.IsNoMatch = true;
                        result.ErrorMessage = "Reference không trỏ đến element hợp lệ trong link.";
                        results.Add(result);
                        continue;
                    }

                    Floor linkFloor = _linkedDoc.GetElement(linkFloorId) as Floor;
                    if (linkFloor == null)
                    {
                        result.IsNoMatch = true;
                        results.Add(result);
                        continue;
                    }

                    result.LinkFloorId = linkFloorId.IntegerValue;

                    // Lấy ZTop của sàn KC — apply linkTransform để về Host space
                    double? zTopLink = ElevationService.GetZTopFeet(linkFloor, _linkedDoc, _linkTransform);
                    if (!zTopLink.HasValue)
                    {
                        result.ErrorMessage = "Không lấy được ZTop của sàn KC.";
                        results.Add(result);
                        continue;
                    }
                    result.ZTopLink_mm = ElevationService.FeetToMillimeters(zTopLink.Value);

                    // So sánh
                    result.IsError = Math.Abs(result.DeltaZ_mm) > toleranceMm;
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = ex.Message;
                }

                results.Add(result);
            }

            return results;
        }

        private string GetLevelName(Floor floor)
        {
            try
            {
                var levelParam = floor.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                if (levelParam != null)
                {
                    var level = _hostDoc.GetElement(levelParam.AsElementId()) as Level;
                    return level?.Name ?? "Unknown";
                }
            }
            catch { }
            return "Unknown";
        }
    }
}
```

### 3.4. ColorOverrideService.cs

```csharp
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Antigravity.FloorElevationChecker.Models;

namespace Antigravity.FloorElevationChecker.Services
{
    /// <summary>
    /// Áp màu lên sàn KT lỗi trong active view.
    /// Đỏ = lỗi vượt tolerance, Vàng = cảnh báo (50%-100% tolerance).
    /// </summary>
    public class ColorOverrideService
    {
        private readonly Document _hostDoc;
        private readonly View _activeView;

        // Màu sắc
        private static readonly Color ErrorColor   = new Color(220, 53, 69);   // Đỏ Bootstrap
        private static readonly Color WarningColor = new Color(255, 193,  7);  // Vàng Bootstrap
        private static readonly Color OkColor      = new Color( 25, 135, 84);  // Xanh Bootstrap

        public ColorOverrideService(Document hostDoc, View activeView)
        {
            _hostDoc = hostDoc;
            _activeView = activeView;
        }

        /// <summary>Áp màu cho tất cả sàn KT theo kết quả kiểm tra.</summary>
        public void ApplyOverrides(IList<FloorCheckResult> results, double toleranceMm)
        {
            using (var tx = new Transaction(_hostDoc, "Floor Elevation Check — Apply Colors"))
            {
                tx.Start();
                foreach (var r in results)
                {
                    var floorId = new ElementId(r.HostFloorId);
                    if (r.IsError)
                        SetColor(floorId, ErrorColor);
                    else if (!r.IsNoMatch && System.Math.Abs(r.DeltaZ_mm) > toleranceMm * 0.5)
                        SetColor(floorId, WarningColor);
                    else if (!r.IsNoMatch && !r.IsError)
                        SetColor(floorId, OkColor);
                }
                tx.Commit();
            }
        }

        /// <summary>Reset tất cả override về mặc định.</summary>
        public void ResetOverrides(IList<FloorCheckResult> results)
        {
            using (var tx = new Transaction(_hostDoc, "Floor Elevation Check — Reset Colors"))
            {
                tx.Start();
                foreach (var r in results)
                {
                    var floorId = new ElementId(r.HostFloorId);
                    _activeView.SetElementOverrides(floorId, new OverrideGraphicSettings());
                }
                tx.Commit();
            }
        }

        private void SetColor(ElementId elementId, Color color)
        {
            var ogs = new OverrideGraphicSettings();
            var fillPattern = GetSolidFillPattern();

            ogs.SetSurfaceForegroundPatternColor(color);
            ogs.SetSurfaceForegroundPatternVisible(true);
            if (fillPattern != ElementId.InvalidElementId)
                ogs.SetSurfaceForegroundPatternId(fillPattern);

            ogs.SetProjectionLineColor(color);
            ogs.SetCutLineColor(color);

            _activeView.SetElementOverrides(elementId, ogs);
        }

        /// <summary>Tìm Solid Fill pattern trong document.</summary>
        private ElementId GetSolidFillPattern()
        {
            var collector = new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>();

            foreach (var fp in collector)
            {
                if (fp.GetFillPattern().IsSolidFill)
                    return fp.Id;
            }
            return ElementId.InvalidElementId;
        }
    }
}
```

### 3.5. ReportService.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Antigravity.FloorElevationChecker.Models;

namespace Antigravity.FloorElevationChecker.Services
{
    /// <summary>
    /// Xuất báo cáo HTML cho kết quả kiểm tra cao độ sàn.
    /// </summary>
    public class ReportService
    {
        /// <summary>
        /// Tạo file HTML report và mở trong browser mặc định.
        /// </summary>
        /// <param name="results">Kết quả kiểm tra.</param>
        /// <param name="settings">Cài đặt đã dùng để kiểm tra.</param>
        /// <param name="projectName">Tên dự án (từ Document.Title).</param>
        /// <param name="linkedModelName">Tên linked model.</param>
        /// <returns>Đường dẫn file HTML đã tạo.</returns>
        public string ExportHtml(
            IList<FloorCheckResult> results,
            CheckSettings settings,
            string projectName,
            string linkedModelName)
        {
            string outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Antigravity", "Reports");
            Directory.CreateDirectory(outputDir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(outputDir, $"FloorElevCheck_{timestamp}.html");

            int errorCount   = results.Count(r => r.IsError);
            int noMatchCount = results.Count(r => r.IsNoMatch);
            int okCount      = results.Count(r => !r.IsError && !r.IsNoMatch);

            var html = BuildHtml(results, settings, projectName, linkedModelName, errorCount, noMatchCount, okCount);
            File.WriteAllText(filePath, html, System.Text.Encoding.UTF8);

            // Mở trong browser
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });

            return filePath;
        }

        private string BuildHtml(
            IList<FloorCheckResult> results,
            CheckSettings settings,
            string projectName,
            string linkedModelName,
            int errorCount, int noMatchCount, int okCount)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine(@"<!DOCTYPE html>
<html lang='vi'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>Báo Cáo Kiểm Tra Cao Độ Sàn</title>
<style>
  :root {
    --bg: #0f1117; --surface: #1a1d2e; --surface2: #252840;
    --border: #2d3156; --text: #e2e8f0; --text-muted: #94a3b8;
    --red: #ef4444; --yellow: #f59e0b; --green: #10b981; --blue: #3b82f6;
    --red-bg: rgba(239,68,68,0.12); --yellow-bg: rgba(245,158,11,0.12);
    --green-bg: rgba(16,185,129,0.12);
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { background: var(--bg); color: var(--text); font-family: 'Segoe UI', system-ui, sans-serif; padding: 24px; }
  .header { background: var(--surface); border: 1px solid var(--border); border-radius: 12px; padding: 24px 28px; margin-bottom: 20px; }
  .header h1 { font-size: 22px; font-weight: 700; color: #fff; margin-bottom: 8px; }
  .meta { display: flex; flex-wrap: wrap; gap: 20px; margin-top: 14px; }
  .meta-item { font-size: 13px; color: var(--text-muted); }
  .meta-item span { color: var(--text); font-weight: 500; }
  .stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 14px; margin-bottom: 20px; }
  .stat-card { background: var(--surface); border: 1px solid var(--border); border-radius: 10px; padding: 16px 20px; }
  .stat-label { font-size: 12px; color: var(--text-muted); text-transform: uppercase; letter-spacing: .05em; }
  .stat-value { font-size: 28px; font-weight: 700; margin-top: 4px; }
  .stat-card.total .stat-value { color: var(--blue); }
  .stat-card.error .stat-value { color: var(--red); }
  .stat-card.warning .stat-value { color: var(--yellow); }
  .stat-card.ok .stat-value { color: var(--green); }
  .table-wrap { background: var(--surface); border: 1px solid var(--border); border-radius: 12px; overflow: hidden; }
  .table-header { padding: 16px 20px; border-bottom: 1px solid var(--border); font-weight: 600; font-size: 15px; }
  table { width: 100%; border-collapse: collapse; font-size: 13px; }
  thead th { background: var(--surface2); padding: 10px 14px; text-align: left; color: var(--text-muted);
             font-weight: 600; font-size: 11px; text-transform: uppercase; letter-spacing: .06em;
             border-bottom: 1px solid var(--border); position: sticky; top: 0; z-index: 10; }
  tbody tr { border-bottom: 1px solid var(--border); transition: background .15s; }
  tbody tr:hover { background: var(--surface2); }
  tbody td { padding: 10px 14px; vertical-align: middle; }
  .badge { display: inline-flex; align-items: center; gap: 5px; padding: 3px 10px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
  .badge-error   { background: var(--red-bg);    color: var(--red);    border: 1px solid rgba(239,68,68,.3); }
  .badge-warning { background: var(--yellow-bg); color: var(--yellow); border: 1px solid rgba(245,158,11,.3); }
  .badge-ok      { background: var(--green-bg);  color: var(--green);  border: 1px solid rgba(16,185,129,.3); }
  .badge-nomatch { background: rgba(100,116,139,.12); color: #94a3b8; border: 1px solid rgba(100,116,139,.3); }
  .row-error   { background: var(--red-bg); }
  .row-warning { background: var(--yellow-bg); }
  .id-chip { font-family: 'Consolas', monospace; font-size: 11px; background: var(--surface2);
             padding: 2px 8px; border-radius: 6px; border: 1px solid var(--border); color: #93c5fd; }
  .delta-error   { color: var(--red);    font-weight: 700; }
  .delta-warning { color: var(--yellow); font-weight: 600; }
  .delta-ok      { color: var(--green); }
  .footer { text-align: center; color: var(--text-muted); font-size: 12px; margin-top: 20px; }
</style>
</head>
<body>
");

            // Header
            sb.AppendLine($@"<div class='header'>
  <h1>📊 Báo Cáo Kiểm Tra Cao Độ Sàn</h1>
  <div class='meta'>
    <div class='meta-item'>🏗️ Dự án: <span>{EscapeHtml(projectName)}</span></div>
    <div class='meta-item'>🔗 Linked Model (KC): <span>{EscapeHtml(linkedModelName)}</span></div>
    <div class='meta-item'>📏 Tolerance: <span>{settings.ToleranceMm:0.#} mm</span></div>
    <div class='meta-item'>📅 Ngày kiểm tra: <span>{DateTime.Now:dd/MM/yyyy HH:mm}</span></div>
  </div>
</div>");

            // Stats
            sb.AppendLine($@"<div class='stats'>
  <div class='stat-card total'><div class='stat-label'>Tổng sàn KT</div><div class='stat-value'>{results.Count}</div></div>
  <div class='stat-card error'><div class='stat-label'>❌ Lỗi</div><div class='stat-value'>{errorCount}</div></div>
  <div class='stat-card warning'><div class='stat-label'>⚠️ Không tìm thấy KC</div><div class='stat-value'>{noMatchCount}</div></div>
  <div class='stat-card ok'><div class='stat-label'>✅ Đạt</div><div class='stat-value'>{okCount}</div></div>
</div>");

            // Table
            sb.AppendLine(@"<div class='table-wrap'>
<div class='table-header'>📋 Chi Tiết Kết Quả</div>
<table>
<thead>
  <tr>
    <th>#</th>
    <th>Floor KT (ElementID)</th>
    <th>Floor KC (ElementID)</th>
    <th>Level</th>
    <th>ZTop KT (mm)</th>
    <th>ZTop KC (mm)</th>
    <th>ΔZ (mm)</th>
    <th>Trạng Thái</th>
  </tr>
</thead>
<tbody>");

            int idx = 1;
            foreach (var r in results.OrderByDescending(x => x.IsError).ThenByDescending(x => x.IsNoMatch))
            {
                string rowClass = r.IsError ? "row-error" : (r.IsNoMatch ? "" : "");
                string badge, deltaClass;
                if (r.IsNoMatch)
                {
                    badge = "<span class='badge badge-nomatch'>⚠ Không tìm thấy KC</span>";
                    deltaClass = "";
                }
                else if (r.IsError)
                {
                    badge = "<span class='badge badge-error'>❌ Lỗi</span>";
                    deltaClass = "delta-error";
                }
                else if (!string.IsNullOrEmpty(r.ErrorMessage))
                {
                    badge = "<span class='badge badge-nomatch'>⚠ Error</span>";
                    deltaClass = "";
                }
                else
                {
                    badge = "<span class='badge badge-ok'>✅ OK</span>";
                    deltaClass = "delta-ok";
                }

                string linkIdCell = r.LinkFloorId >= 0
                    ? $"<span class='id-chip'>{r.LinkFloorId}</span>"
                    : "<span style='color:#64748b'>—</span>";

                string deltaCell = r.IsNoMatch
                    ? "—"
                    : $"<span class='{deltaClass}'>{r.DeltaZ_mm:+0.#;-0.#;0}</span>";

                string errorNote = !string.IsNullOrEmpty(r.ErrorMessage)
                    ? $"<br><small style='color:#94a3b8'>{EscapeHtml(r.ErrorMessage)}</small>"
                    : "";

                sb.AppendLine($@"<tr class='{rowClass}'>
  <td>{idx++}</td>
  <td><span class='id-chip'>{r.HostFloorId}</span></td>
  <td>{linkIdCell}</td>
  <td>{EscapeHtml(r.LevelName)}</td>
  <td>{(r.IsNoMatch ? "—" : r.ZTopHost_mm.ToString("0.#"))}</td>
  <td>{(r.IsNoMatch ? "—" : r.ZTopLink_mm.ToString("0.#"))}</td>
  <td>{deltaCell}</td>
  <td>{badge}{errorNote}</td>
</tr>");
            }

            sb.AppendLine(@"</tbody></table></div>");
            sb.AppendLine($"<div class='footer'>Tạo bởi Antigravity.FloorElevationChecker — {DateTime.Now:yyyy}</div>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        private static string EscapeHtml(string s)
            => s?.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;") ?? "";
    }
}
```

---

## 📋 PHASE 04 — UI: WPF Dialog

### 4.1. FloorCheckerDialog.xaml

```xml
<Window x:Class="Antigravity.FloorElevationChecker.UI.FloorCheckerDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Kiểm Tra Cao Độ Sàn — Antigravity"
        Width="760" MinHeight="520"
        WindowStartupLocation="CenterScreen"
        ResizeMode="CanResizeWithGrip"
        Background="#0F1117">

    <Window.Resources>
        <!-- Colors -->
        <SolidColorBrush x:Key="BgBrush"        Color="#0F1117"/>
        <SolidColorBrush x:Key="SurfaceBrush"   Color="#1A1D2E"/>
        <SolidColorBrush x:Key="Surface2Brush"  Color="#252840"/>
        <SolidColorBrush x:Key="BorderBrush"    Color="#2D3156"/>
        <SolidColorBrush x:Key="TextBrush"      Color="#E2E8F0"/>
        <SolidColorBrush x:Key="MutedBrush"     Color="#94A3B8"/>
        <SolidColorBrush x:Key="AccentBrush"    Color="#3B82F6"/>
        <SolidColorBrush x:Key="ErrorBrush"     Color="#EF4444"/>
        <SolidColorBrush x:Key="SuccessBrush"   Color="#10B981"/>
        <SolidColorBrush x:Key="WarningBrush"   Color="#F59E0B"/>

        <!-- ComboBox style -->
        <Style TargetType="ComboBox" x:Key="DarkCombo">
            <Setter Property="Background" Value="#252840"/>
            <Setter Property="Foreground" Value="#E2E8F0"/>
            <Setter Property="BorderBrush" Value="#2D3156"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="10,6"/>
            <Setter Property="Height" Value="34"/>
        </Style>

        <!-- TextBox style -->
        <Style TargetType="TextBox" x:Key="DarkInput">
            <Setter Property="Background" Value="#252840"/>
            <Setter Property="Foreground" Value="#E2E8F0"/>
            <Setter Property="BorderBrush" Value="#2D3156"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="10,6"/>
            <Setter Property="Height" Value="34"/>
            <Setter Property="CaretBrush" Value="#E2E8F0"/>
        </Style>

        <!-- Button styles -->
        <Style TargetType="Button" x:Key="BtnPrimary">
            <Setter Property="Background" Value="#3B82F6"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="16,8"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}" CornerRadius="7" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Background" Value="#2563EB"/>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter Property="Background" Value="#374151"/>
                                <Setter Property="Foreground" Value="#6B7280"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType="Button" x:Key="BtnDanger" BasedOn="{StaticResource BtnPrimary}">
            <Setter Property="Background" Value="#EF4444"/>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#DC2626"/>
                </Trigger>
            </Style.Triggers>
        </Style>

        <Style TargetType="Button" x:Key="BtnSecondary" BasedOn="{StaticResource BtnPrimary}">
            <Setter Property="Background" Value="#252840"/>
            <Setter Property="BorderBrush" Value="#2D3156"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#2D3156"/>
                </Trigger>
            </Style.Triggers>
        </Style>

        <Style TargetType="Button" x:Key="BtnSuccess" BasedOn="{StaticResource BtnPrimary}">
            <Setter Property="Background" Value="#10B981"/>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#059669"/>
                </Trigger>
            </Style.Triggers>
        </Style>

        <!-- DataGrid style -->
        <Style TargetType="DataGrid" x:Key="DarkGrid">
            <Setter Property="Background" Value="#1A1D2E"/>
            <Setter Property="Foreground" Value="#E2E8F0"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="GridLinesVisibility" Value="Horizontal"/>
            <Setter Property="HorizontalGridLinesBrush" Value="#2D3156"/>
            <Setter Property="RowBackground" Value="Transparent"/>
            <Setter Property="AlternatingRowBackground" Value="#20252840"/>
            <Setter Property="SelectionMode" Value="Single"/>
            <Setter Property="AutoGenerateColumns" Value="False"/>
            <Setter Property="IsReadOnly" Value="True"/>
            <Setter Property="CanUserAddRows" Value="False"/>
            <Setter Property="HeadersVisibility" Value="Column"/>
            <Setter Property="FontSize" Value="12"/>
        </Style>
    </Window.Resources>

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- Header -->
            <RowDefinition Height="Auto"/>  <!-- Settings -->
            <RowDefinition Height="Auto"/>  <!-- Progress -->
            <RowDefinition Height="*"/>     <!-- Results grid -->
            <RowDefinition Height="Auto"/>  <!-- Buttons -->
        </Grid.RowDefinitions>

        <!-- Header -->
        <StackPanel Grid.Row="0" Margin="0,0,0,16">
            <TextBlock Text="🏗️ Kiểm Tra Cao Độ Sàn" FontSize="20" FontWeight="Bold"
                       Foreground="#FFFFFF"/>
            <TextBlock Foreground="#94A3B8" FontSize="12" Margin="0,4,0,0">
                So sánh ZTop của sàn Kiến Trúc (host) với sàn Kết Cấu (linked model)
            </TextBlock>
        </StackPanel>

        <!-- Settings Panel -->
        <Border Grid.Row="1" Background="#1A1D2E" BorderBrush="#2D3156" BorderThickness="1"
                CornerRadius="10" Padding="16" Margin="0,0,0,12">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="16"/>
                    <ColumnDefinition Width="160"/>
                    <ColumnDefinition Width="16"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Link selector -->
                <StackPanel Grid.Column="0">
                    <TextBlock Text="Linked Model (Kết Cấu)" FontSize="11" Foreground="#94A3B8"
                               Margin="0,0,0,6" FontWeight="SemiBold"/>
                    <ComboBox x:Name="CmbLinks" Style="{StaticResource DarkCombo}"
                              DisplayMemberPath="Name"/>
                </StackPanel>

                <!-- Tolerance -->
                <StackPanel Grid.Column="2">
                    <TextBlock Text="Tolerance (mm)" FontSize="11" Foreground="#94A3B8"
                               Margin="0,0,0,6" FontWeight="SemiBold"/>
                    <TextBox x:Name="TxtTolerance" Style="{StaticResource DarkInput}" Text="20"/>
                </StackPanel>

                <!-- Check only active view -->
                <StackPanel Grid.Column="4" VerticalAlignment="Bottom">
                    <CheckBox x:Name="ChkActiveViewOnly" Content="View hiện tại"
                              Foreground="#94A3B8" FontSize="11" VerticalAlignment="Center"
                              Margin="0,0,0,8"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- Progress Bar -->
        <Border Grid.Row="2" Background="#1A1D2E" BorderBrush="#2D3156" BorderThickness="1"
                CornerRadius="8" Padding="12,10" Margin="0,0,0,12"
                x:Name="PanelProgress" Visibility="Collapsed">
            <StackPanel>
                <TextBlock x:Name="TxtProgress" Foreground="#94A3B8" FontSize="12" Margin="0,0,0,6"/>
                <ProgressBar x:Name="PbProgress" Height="6" Background="#252840"
                             Foreground="#3B82F6" BorderThickness="0" Minimum="0" Maximum="100"/>
            </StackPanel>
        </Border>

        <!-- Results DataGrid -->
        <Border Grid.Row="3" Background="#1A1D2E" BorderBrush="#2D3156" BorderThickness="1"
                CornerRadius="10" Margin="0,0,0,12" ClipToBounds="True">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>

                <Border Grid.Row="0" Background="#252840" Padding="14,10" BorderBrush="#2D3156" BorderThickness="0,0,0,1">
                    <Grid>
                        <TextBlock Text="📋 Kết Quả" FontWeight="SemiBold" Foreground="#E2E8F0" FontSize="13"/>
                        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                            <TextBlock x:Name="TxtSummary" Foreground="#94A3B8" FontSize="12" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Grid>
                </Border>

                <DataGrid Grid.Row="1" x:Name="GridResults" Style="{StaticResource DarkGrid}" MinHeight="200">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Floor KT ID"     Binding="{Binding HostFloorId}"            Width="100"/>
                        <DataGridTextColumn Header="Floor KC ID"     Binding="{Binding LinkFloorIdDisplay}"     Width="100"/>
                        <DataGridTextColumn Header="Level"           Binding="{Binding LevelName}"              Width="*"/>
                        <DataGridTextColumn Header="ZTop KT (mm)"   Binding="{Binding ZTopHost_mmDisplay}"     Width="110"/>
                        <DataGridTextColumn Header="ZTop KC (mm)"   Binding="{Binding ZTopLink_mmDisplay}"     Width="110"/>
                        <DataGridTextColumn Header="ΔZ (mm)"        Binding="{Binding DeltaZDisplay}"          Width="90"/>
                        <DataGridTextColumn Header="Trạng Thái"     Binding="{Binding StatusDisplay}"          Width="130"/>
                    </DataGrid.Columns>

                    <DataGrid.RowStyle>
                        <Style TargetType="DataGridRow">
                            <Setter Property="Foreground" Value="#E2E8F0"/>
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IsError}" Value="True">
                                    <Setter Property="Background" Value="#1F0A0A"/>
                                    <Setter Property="Foreground" Value="#FCA5A5"/>
                                </DataTrigger>
                                <DataTrigger Binding="{Binding IsNoMatch}" Value="True">
                                    <Setter Property="Foreground" Value="#94A3B8"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </DataGrid.RowStyle>
                </DataGrid>
            </Grid>
        </Border>

        <!-- Action Buttons -->
        <Grid Grid.Row="4">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column="0" Orientation="Horizontal" HorizontalAlignment="Left">
                <TextBlock x:Name="TxtStatusBar" Foreground="#94A3B8" FontSize="11" VerticalAlignment="Center"/>
            </StackPanel>

            <StackPanel Grid.Column="1" Orientation="Horizontal" HorizontalAlignment="Right">
                <Button x:Name="BtnRun" Content="▶  Chạy Kiểm Tra"
                        Style="{StaticResource BtnPrimary}" Margin="0,0,8,0"
                        Click="BtnRun_Click" Width="140"/>
                <Button x:Name="BtnApplyColor" Content="🎨  Đổi Màu Lỗi"
                        Style="{StaticResource BtnDanger}" Margin="0,0,8,0"
                        Click="BtnApplyColor_Click" IsEnabled="False" Width="130"/>
                <Button x:Name="BtnExportHtml" Content="📄  Xuất HTML"
                        Style="{StaticResource BtnSuccess}" Margin="0,0,8,0"
                        Click="BtnExportHtml_Click" IsEnabled="False" Width="120"/>
                <Button x:Name="BtnResetColor" Content="↩  Reset Màu"
                        Style="{StaticResource BtnSecondary}"
                        Click="BtnResetColor_Click" IsEnabled="False" Width="110"/>
            </StackPanel>
        </Grid>
    </Grid>
</Window>
```

### 4.2. FloorCheckerDialog.xaml.cs

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.FloorElevationChecker.Models;
using Antigravity.FloorElevationChecker.Services;

namespace Antigravity.FloorElevationChecker.UI
{
    public partial class FloorCheckerDialog : Window
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly FloorCollectorService _collector;
        private List<FloorCheckResult> _lastResults;
        private ObservableCollection<FloorResultViewModel> _viewModels;

        public FloorCheckerDialog(UIDocument uiDoc)
        {
            InitializeComponent();
            _uiDoc = uiDoc;
            _doc = uiDoc.Document;
            _collector = new FloorCollectorService(_doc);
            _viewModels = new ObservableCollection<FloorResultViewModel>();
            GridResults.ItemsSource = _viewModels;

            LoadLinks();
        }

        private void LoadLinks()
        {
            var links = _collector.GetLoadedLinks();
            CmbLinks.ItemsSource = links.Select(l => new LinkItem
            {
                Name = l.Name,
                Instance = l
            }).ToList();

            if (CmbLinks.Items.Count > 0)
                CmbLinks.SelectedIndex = 0;
        }

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (CmbLinks.SelectedItem is not LinkItem selectedLink)
            {
                MessageBox.Show("Vui lòng chọn Linked Model trước.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtTolerance.Text, out double tol) || tol <= 0)
            {
                MessageBox.Show("Tolerance phải là số dương (mm).", "Giá trị không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var view3D = _collector.FindSuitable3DView();
            if (view3D == null)
            {
                MessageBox.Show("Không tìm thấy View3D nào trong project. Vui lòng tạo một View 3D.", "Cần View 3D", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            BtnRun.IsEnabled = false;
            BtnApplyColor.IsEnabled = false;
            BtnExportHtml.IsEnabled = false;
            BtnResetColor.IsEnabled = false;
            PanelProgress.Visibility = Visibility.Visible;
            _viewModels.Clear();
            TxtSummary.Text = "";

            try
            {
                var hostFloors = _collector.GetHostFloors();
                var service = new ElevationComparisonService(_doc, selectedLink.Instance, view3D);
                var settings = new CheckSettings
                {
                    ToleranceMm = tol,
                    SelectedLinkInstance = selectedLink.Instance
                };

                TxtProgress.Text = $"Đang kiểm tra 0 / {hostFloors.Count} sàn...";
                PbProgress.Maximum = hostFloors.Count;
                PbProgress.Value = 0;

                // Chạy trong thread UI (Revit API yêu cầu)
                _lastResults = service.RunCheck(hostFloors, tol, (cur, total) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtProgress.Text = $"Đang kiểm tra {cur} / {total} sàn...";
                        PbProgress.Value = cur;
                    });
                });

                // Cập nhật UI
                foreach (var r in _lastResults)
                    _viewModels.Add(new FloorResultViewModel(r, tol));

                int errorCount = _lastResults.Count(r => r.IsError);
                int okCount    = _lastResults.Count(r => !r.IsError && !r.IsNoMatch);
                int noMatch    = _lastResults.Count(r => r.IsNoMatch);
                TxtSummary.Text = $"✅ {okCount} OK  |  ❌ {errorCount} Lỗi  |  ⚠ {noMatch} Không tìm thấy KC";
                TxtStatusBar.Text = $"Hoàn tất — {_lastResults.Count} sàn KT đã kiểm tra.";

                BtnApplyColor.IsEnabled = true;
                BtnExportHtml.IsEnabled = true;
                BtnResetColor.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi chạy kiểm tra:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnRun.IsEnabled = true;
                PanelProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnApplyColor_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0) return;
            if (!double.TryParse(TxtTolerance.Text, out double tol)) tol = 20;
            try
            {
                var overrideService = new ColorOverrideService(_doc, _uiDoc.ActiveView);
                overrideService.ApplyOverrides(_lastResults, tol);
                TxtStatusBar.Text = "✅ Đã áp màu lên view.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đổi màu:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportHtml_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0) return;
            if (!double.TryParse(TxtTolerance.Text, out double tol)) tol = 20;
            if (CmbLinks.SelectedItem is not LinkItem selectedLink) return;

            try
            {
                var settings = new CheckSettings { ToleranceMm = tol, SelectedLinkInstance = selectedLink.Instance };
                var reportService = new ReportService();
                string path = reportService.ExportHtml(_lastResults, settings, _doc.Title, selectedLink.Name);
                TxtStatusBar.Text = $"📄 Báo cáo đã xuất: {System.IO.Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất báo cáo:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnResetColor_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0) return;
            try
            {
                var overrideService = new ColorOverrideService(_doc, _uiDoc.ActiveView);
                overrideService.ResetOverrides(_lastResults);
                TxtStatusBar.Text = "↩ Đã reset màu về mặc định.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi reset màu:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ── Helper classes ──────────────────────────────────────────────────────

    internal class LinkItem
    {
        public string Name { get; set; }
        public RevitLinkInstance Instance { get; set; }
        public override string ToString() => Name;
    }

    /// <summary>ViewModel bọc FloorCheckResult để hiển thị trong DataGrid.</summary>
    internal class FloorResultViewModel
    {
        private readonly FloorCheckResult _r;
        private readonly double _tol;

        public FloorResultViewModel(FloorCheckResult r, double tol) { _r = r; _tol = tol; }

        public int HostFloorId => _r.HostFloorId;
        public bool IsError    => _r.IsError;
        public bool IsNoMatch  => _r.IsNoMatch;
        public string LevelName => _r.LevelName;

        public string LinkFloorIdDisplay => _r.LinkFloorId >= 0 ? _r.LinkFloorId.ToString() : "—";
        public string ZTopHost_mmDisplay => _r.IsNoMatch ? "—" : _r.ZTopHost_mm.ToString("0.#");
        public string ZTopLink_mmDisplay => _r.IsNoMatch ? "—" : _r.ZTopLink_mm.ToString("0.#");

        public string DeltaZDisplay => _r.IsNoMatch
            ? "—"
            : $"{_r.DeltaZ_mm:+0.#;-0.#;0}";

        public string StatusDisplay => _r.IsNoMatch
            ? "⚠ Không tìm thấy KC"
            : (_r.IsError ? "❌ LỖI" : "✅ OK");
    }
}
```

---

## 📋 PHASE 05 — Integration & Polish

### 5.1. Kiểm tra sau khi build

```bash
# Build solution
dotnet build Antigravity.sln -c Debug

# Chạy deploy script
.\DeployToRevit.ps1
```

### 5.2. Edge Cases cần xử lý trong code

| Tình huống | Xử lý |
|------------|--------|
| Không có View3D | Hiển thị MessageBox hướng dẫn tạo View3D |
| Linked Model chưa load (unloaded) | `GetLoadedLinks()` đã filter `GetLinkDocument() != null` |
| Sàn KT không có BoundingBox | Skip, ghi vào `ErrorMessage` |
| `HostObjectUtils.GetTopFaces()` trả về rỗng | Fallback sang `BoundingBox.Max.Z` |
| Raycast không tìm thấy sàn KC | `IsNoMatch = true`, hiển thị ⚠ |
| Transaction fail (view locked) | Wrap trong try/catch, thông báo user |

### 5.3. Logging

Dùng `Antigravity.Core.Services.AppLogger` cho mọi exception:
```csharp
AppLogger.Error(ex, "[FloorElevationChecker] ElevationComparisonService.RunCheck failed");
```

---

## ✅ CHECKLIST TRIỂN KHAI

### Phase 01 — Setup
- [ ] Tạo `Antigravity.FloorElevationChecker.csproj`
- [ ] Thêm vào `Antigravity.sln`
- [ ] Tạo folder structure: Models/, Services/, UI/
- [ ] Tạo `FloorElevationCheckerCommand.cs`
- [ ] Chỉnh sửa `App.cs` — thêm button vào `clashPanel`
- [ ] Build thử (không lỗi compile)

### Phase 02 — Models
- [ ] `CheckSettings.cs`
- [ ] `FloorCheckResult.cs`

### Phase 03 — Services
- [ ] `FloorCollectorService.cs`
- [ ] `ElevationService.cs` (với HostObjectUtils + fallback)
- [ ] `ElevationComparisonService.cs` (ReferenceIntersector)
- [ ] `ColorOverrideService.cs` (SolidFill override)
- [ ] `ReportService.cs` (HTML dark theme)

### Phase 04 — UI
- [ ] `FloorCheckerDialog.xaml` (WPF dark theme)
- [ ] `FloorCheckerDialog.xaml.cs` (code-behind)
- [ ] Test dialog hiển thị đúng

### Phase 05 — Integration
- [ ] Build toàn bộ solution không lỗi
- [ ] Deploy và test trong Revit
- [ ] Kiểm tra: button xuất hiện trên ribbon
- [ ] Kiểm tra: chọn link → chạy → màu đỏ đúng
- [ ] Kiểm tra: HTML report mở được trong browser

---

## 🔑 GHI CHÚ QUAN TRỌNG CHO IDE

1. **`HostObjectUtils.GetTopFaces()`** cần `using Autodesk.Revit.DB;` — không cần thêm package nào
2. **WPF** cần thêm reference `PresentationFramework`, `PresentationCore`, `WindowsBase` trong csproj
3. **ReferenceIntersector** chỉ hoạt động khi được gọi trong **Revit API Context** (trong `Execute()` của IExternalCommand)
4. **Transaction** chỉ dùng trong `ColorOverrideService` — không cần Transaction cho việc đọc dữ liệu
5. **Pattern tham khảo:** Xem `src/Antigravity.DrawFloors/` để hiểu cách project khác được cấu trúc
6. Nếu `GetLinkDocument()` trả về `null` → link đã unload, bỏ qua
7. **View3D tìm tự động:** `FindSuitable3DView()` lấy view đầu tiên `!IsTemplate && CanBePrinted`

---

*Tài liệu này được tạo bởi Antigravity Planning Agent — 2026-06-04*
