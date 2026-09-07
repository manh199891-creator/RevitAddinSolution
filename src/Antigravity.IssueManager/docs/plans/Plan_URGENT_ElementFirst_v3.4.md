# 🚨 FIX TRIỆT ĐỂ: Ưu tiên Element, bỏ qua tọa độ geo-referenced
**Version:** v3.4
**Mục tiêu:** Khi bấm "Show in Model", Revit PHẢI zoom vào đúng phần tử (lỗ mở vách 300x300) giống hệt ảnh Trimble.

---

## Phân tích nguyên nhân thực sự

BCF từ Trimble chứa:
- `IfcGuid="1l0o6ehCP0ouDtA2Q74nTs"` → Đây là CHỈ DẪN đến đúng phần tử cần xem
- Camera X=597,026m, Y=2,304,647m → Tọa độ địa lý (VN-2000/UTM), KHÔNG DÙNG ĐƯỢC trong Revit

Code hiện tại đang cố dùng tọa độ 597,000m → Section Box bị đặt sai → View trống.

**Giải pháp:** ĐẢO NGƯỢC thứ tự ưu tiên. Tìm Element bằng IFC GUID TRƯỚC, tạo Section Box quanh Element, bỏ qua hoàn toàn tọa độ BCF.

---

## Viết lại hàm `SyncCamera` (Thay thế TOÀN BỘ block "3.")

```csharp
// 3. Apply Section Box and zoom
if (uidoc.ActiveView is View3D view3d)
{
    try
    {
        // === CHIẾN LƯỢC MỚI: Element-first ===

        // Bước 1: Nếu tìm được Element → Tạo Section Box quanh Element
        if (idsToSelect.Count > 0)
        {
            if (ApplySectionBoxAroundElements(doc, view3d, idsToSelect))
            {
                ZoomToSectionBox(uidoc, view3d);
                return;
            }
            // Fallback: Zoom thẳng đến Element
            uidoc.ShowElements(idsToSelect);
            return;
        }

        // Bước 2: Không tìm được Element → Thử dùng tọa độ BCF (cho trường hợp Shared Coordinates đã đúng)
        XYZ eyePosition = TransformCoordinates(doc, viewpoint.CameraX, viewpoint.CameraY, viewpoint.CameraZ);
        XYZ forwardDirection = TransformVector(doc, viewpoint.CameraDirectionX, viewpoint.CameraDirectionY, viewpoint.CameraDirectionZ);

        bool sectionBoxApplied = ApplySectionBoxFromClippingPlanes(
            doc, view3d, viewpoint, eyePosition, forwardDirection, idsToSelect);

        if (sectionBoxApplied && ZoomToSectionBox(uidoc, view3d))
        {
            return;
        }

        // Bước 3: Không được gì cả → Thông báo
        DisableSectionBox(doc, view3d);
        TaskDialog.Show("Revit Sync",
            "Không thể hiển thị vị trí BCF.\n" +
            "• Không tìm thấy phần tử tương ứng với IFC GUID trong BCF.\n" +
            "• Tọa độ BCF (geo-referenced) không khớp với mô hình Revit.");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine("Camera Sync error: " + ex.Message);
    }
}
```

---

## Tăng kích thước Section Box quanh Element

Hàm `ApplySectionBoxAroundElements` hiện có `paddingFeet = 5.0` (quá nhỏ, chỉ ~1.5m).
Tăng lên `paddingFeet = 15.0` (~4.5m) để nhìn rõ ngữ cảnh xung quanh phần tử:

```csharp
private static BoundingBoxXYZ CreateSectionBoxAroundElements(Document doc, IList<ElementId> idsToSelect)
{
    BoundingBoxXYZ merged = CreateMergedElementBoundingBox(doc, idsToSelect);
    if (merged == null) return null;

    const double paddingFeet = 15.0;  // ~4.5 mét padding
    return new BoundingBoxXYZ
    {
        Transform = Transform.Identity,
        Min = new XYZ(merged.Min.X - paddingFeet, merged.Min.Y - paddingFeet, merged.Min.Z - paddingFeet),
        Max = new XYZ(merged.Max.X + paddingFeet, merged.Max.Y + paddingFeet, merged.Max.Z + paddingFeet)
    };
}
```

---

## Kiểm tra hàm tìm Element bằng IFC GUID

Hàm `FindElementByStringIdentifier` đang quét qua TẤT CẢ Element trong Revit model để tìm parameter IFC GUID. Điều này có thể CHẬM nhưng CHÍNH XÁC.

**Cần kiểm tra:** Trong file Revit đang test, mở một phần tử bất kỳ và kiểm tra xem nó có parameter tên `IfcGUID` hoặc `IFC GUID` không. Nếu không có, thì code sẽ không tìm được Element và rơi vào fallback.

**Nếu model KHÔNG có parameter IFC GUID:** Cần thêm phương án match bằng cách dùng `ExportUtils.GetExportId()` để tính ngược IFC GUID từ mỗi Revit Element và so sánh:

```csharp
// Thêm vào cuối hàm FindElementByStringIdentifier, trước return null:

// Fallback: Tính IFC GUID từ ExportUtils và so sánh
string targetIfcGuid = value;
foreach (Element element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
{
    try
    {
        Guid exportGuid = ExportUtils.GetExportId(doc, element.Id);
        string computedIfcGuid = BcfExporter.ToIfcGuid(exportGuid);
        if (string.Equals(computedIfcGuid, targetIfcGuid, StringComparison.Ordinal))
        {
            return element;
        }
    }
    catch { }
}
```

> **LƯU Ý:** Vòng lặp này quét toàn bộ model nên có thể mất 5-10 giây với model lớn.
> Nên thêm ProgressBar hoặc chạy 1 lần rồi cache kết quả trong biến static.

---

## Tóm tắt thay đổi

| File | Thay đổi |
|------|----------|
| `RevitCameraSync.cs` | Đảo thứ tự: Element-first → BCF coordinates-second |
| `RevitCameraSync.cs` | Tăng `paddingFeet` từ 5 lên 15 |
| `RevitCameraSync.cs` | Thêm fallback match IFC GUID bằng `ExportUtils.GetExportId()` |
| `RevitIssueCreator.cs` | Đảo 6 hướng Direction (Outward → Inward) nếu chưa sửa |

## Build & Test
```powershell
dotnet build src\Antigravity.IssueManager\Antigravity.IssueManager.csproj
```
DLL: `src\Antigravity.IssueManager\bin\Debug\Antigravity.IssueManager.dll`
