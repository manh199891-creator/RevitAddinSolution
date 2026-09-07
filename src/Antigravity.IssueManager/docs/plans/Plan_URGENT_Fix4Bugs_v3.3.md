# 🚨 KHẨN CẤP: Fix 4 lỗi BCF Import/Export
**Version:** v3.3
**Ưu tiên:** Tối cao - Cần deploy ngay

---

## Bug 1 (🔴): Ma trận tọa độ - Bỏ hoàn toàn logic thử 2 chiều Transform
**File:** `RevitCameraSync.cs`
**Vấn đề:** Tọa độ BCF từ Trimble là geo-referenced (X=597,000m, Y=2,304,000m). Hàm `SectionBoxIntersectsContext` thử cả 2 chiều Transform nhưng cả 2 đều fail vì tọa độ quá lớn.

**Cách sửa - Viết lại hàm `TransformCoordinates`:**
```csharp
private static XYZ TransformCoordinates(Document doc, double x, double y, double z)
{
    // BCF → Meters → Feet
    double xFeet = UnitUtils.ConvertToInternalUnits(x, UnitTypeId.Meters);
    double yFeet = UnitUtils.ConvertToInternalUnits(y, UnitTypeId.Meters);
    double zFeet = UnitUtils.ConvertToInternalUnits(z, UnitTypeId.Meters);
    XYZ sharedXyz = new XYZ(xFeet, yFeet, zFeet);

    // GetTransform() = Internal → Shared
    // Vậy Shared → Internal = Inverse
    Transform transform = doc.ActiveProjectLocation.GetTransform();
    return transform.Inverse.OfPoint(sharedXyz);
}
```
- **XÓA** overload `TransformCoordinates(doc, x, y, z, bool useInverseTransform)`. Chỉ giữ 1 phiên bản duy nhất dùng `.Inverse`.
- **XÓA** overload `TransformVector(doc, x, y, z, bool useInverseTransform)`. Chỉ giữ 1 phiên bản:
```csharp
private static XYZ TransformVector(Document doc, double x, double y, double z)
{
    if (x == 0 && y == 0 && z == 0) return XYZ.Zero;
    Transform transform = doc.ActiveProjectLocation.GetTransform();
    return transform.Inverse.OfVector(new XYZ(x, y, z)).Normalize();
}
```
- **XÓA** tham số `out bool useInverseTransform` khỏi hàm `ApplySectionBoxFromClippingPlanes`.
- **XÓA** toàn bộ logic "thử chiều thứ 2" (`alternateEyePosition`, `alternateForwardDirection`, `alternateSectionBox`) trong hàm `ApplySectionBoxFromClippingPlanes`.
- **XÓA** tham số `bool useInverseTransform` khỏi các hàm: `ToClipPlaneData`, `GetSectionBoxFallbackCenter`, `CreateSectionBoxFromClippingPlanes`, `CreateFallbackSectionBoxFromPartialPlanes`.

---

## Bug 2 (🔴): Hướng mặt cắt Export bị NGƯỢC
**File:** `RevitIssueCreator.cs`, hàm `CreateSectionBoxClipPlanes` (dòng 180-185)
**Vấn đề:** Direction đang hướng Outward. Chuẩn BCF 2.1 yêu cầu Inward.

**Cách sửa - Đảo ngược 6 vector:**
```csharp
// Cũ (SAI - Outward):
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(min.X, mid.Y, mid.Z), XYZ.BasisX.Negate());
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(max.X, mid.Y, mid.Z), XYZ.BasisX);
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, min.Y, mid.Z), XYZ.BasisY.Negate());
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, max.Y, mid.Z), XYZ.BasisY);
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, mid.Y, min.Z), XYZ.BasisZ.Negate());
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, mid.Y, max.Z), XYZ.BasisZ);

// Mới (ĐÚNG - Inward):
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(min.X, mid.Y, mid.Z), XYZ.BasisX);          // min X → hướng vào = +X
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(max.X, mid.Y, mid.Z), XYZ.BasisX.Negate()); // max X → hướng vào = -X
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, min.Y, mid.Z), XYZ.BasisY);          // min Y → hướng vào = +Y
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, max.Y, mid.Z), XYZ.BasisY.Negate()); // max Y → hướng vào = -Y
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, mid.Y, min.Z), XYZ.BasisZ);          // min Z → hướng vào = +Z
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, mid.Y, max.Z), XYZ.BasisZ.Negate()); // max Z → hướng vào = -Z
```

Đồng thời đổi tên tham số cho rõ nghĩa:
```csharp
private static void AddClipPlane(
    List<ClippingPlaneModel> planes,
    Transform boxTransform,
    ProjectPosition projectPosition,
    XYZ localLocation,
    XYZ localInwardDirection)   // Đổi tên từ localOutwardDirection → localInwardDirection
```

---

## Bug 3 (🟡): Fallback Center khi thiếu mặt cắt
**File:** `RevitCameraSync.cs`, hàm `ApplySectionBoxFromClippingPlanes` và `GetSectionBoxFallbackCenter`

**Vấn đề:** Khi BCF chỉ có 1 mặt cắt, Center được tính từ Camera (geo-referenced, bị lệch hàng triệu Feet). Section Box fallback đặt sai chỗ.

**Cách sửa - Thay đổi thứ tự ưu tiên tìm Center:**
```csharp
private static bool ApplySectionBoxFromClippingPlanes(
    Document doc, View3D view3d, ViewpointModel viewpoint,
    XYZ eyePosition, XYZ forwardDirection, IList<ElementId> idsToSelect)
{
    if (viewpoint?.ClippingPlanes == null || viewpoint.ClippingPlanes.Count == 0)
        return false;

    BoundingBoxXYZ sectionBox = CreateSectionBoxFromClippingPlanes(
        doc, viewpoint, eyePosition, forwardDirection);

    // Kiểm tra section box có nằm trong model không
    if (!SectionBoxIntersectsContext(doc, sectionBox, idsToSelect))
    {
        // Section box nằm ngoài model → bỏ qua tọa độ BCF hoàn toàn
        // Thử tạo section box xung quanh các Element đã match được
        return false;
    }

    using (Transaction t = new Transaction(doc, "Apply BCF Section Box"))
    {
        t.Start();
        view3d.SetSectionBox(sectionBox);
        view3d.IsSectionBoxActive = true;
        t.Commit();
    }
    return true;
}
```

Sửa `GetSectionBoxFallbackCenter` - Ưu tiên Element trước, Camera sau:
```csharp
private static XYZ GetSectionBoxFallbackCenter(
    Document doc, ViewpointModel viewpoint,
    XYZ eyePosition, XYZ forwardDirection)
{
    // Ưu tiên 1: ClashPoint (từ Navisworks XML)
    if (viewpoint.ClashPointX != 0 || viewpoint.ClashPointY != 0 || viewpoint.ClashPointZ != 0)
    {
        return TransformCoordinates(doc, viewpoint.ClashPointX, viewpoint.ClashPointY, viewpoint.ClashPointZ);
    }

    // Ưu tiên 2: Camera direction (chỉ dùng nếu eyePosition nằm trong phạm vi model)
    if (eyePosition != null && forwardDirection != null && !forwardDirection.IsZeroLength())
    {
        XYZ target = eyePosition + forwardDirection.Normalize().Multiply(DefaultCameraTargetDistanceFeet);
        // Kiểm tra xem target có nằm trong model extents không
        BoundingBoxXYZ modelExtents = GetModelExtents(doc);
        if (modelExtents != null && IsInsideExtents(target, modelExtents, 1000.0))
        {
            return target;
        }
    }

    // Ưu tiên 3: Tâm model
    BoundingBoxXYZ fallbackExtents = GetModelExtents(doc);
    if (fallbackExtents != null)
    {
        return (fallbackExtents.Min + fallbackExtents.Max) * 0.5;
    }

    return XYZ.Zero;
}

private static BoundingBoxXYZ GetModelExtents(Document doc)
{
    // Tạo merged bounding box từ tất cả phần tử
    List<ElementId> allIds = new FilteredElementCollector(doc)
        .WhereElementIsNotElementType()
        .ToElementIds().ToList();
    return CreateMergedElementBoundingBox(doc, allIds);
}

private static bool IsInsideExtents(XYZ point, BoundingBoxXYZ extents, double tolerance)
{
    return point.X >= extents.Min.X - tolerance && point.X <= extents.Max.X + tolerance
        && point.Y >= extents.Min.Y - tolerance && point.Y <= extents.Max.Y + tolerance
        && point.Z >= extents.Min.Z - tolerance && point.Z <= extents.Max.Z + tolerance;
}
```

---

## Bug 4 (🟡): Bỏ SetOrientation - Chỉ dùng ZoomToSectionBox
**File:** `RevitCameraSync.cs`, hàm `SyncCamera` (block "3. Apply Section Box") và `OrientViewFromBcfCamera`

**Cách sửa:**
1. **XÓA** toàn bộ hàm `OrientViewFromBcfCamera` (dòng 187-225).
2. **XÓA** 2 lệnh gọi `OrientViewFromBcfCamera(...)` trong hàm `SyncCamera` (dòng 103 và 114).
3. Luồng mới của block "3" trong `SyncCamera`:
```csharp
// 3. Apply Section Box and zoom to it.
if (uidoc.ActiveView is View3D view3d)
{
    try
    {
        XYZ eyePosition = TransformCoordinates(doc, viewpoint.CameraX, viewpoint.CameraY, viewpoint.CameraZ);
        XYZ forwardDirection = TransformVector(doc, viewpoint.CameraDirectionX, viewpoint.CameraDirectionY, viewpoint.CameraDirectionZ);

        bool sectionBoxApplied = ApplySectionBoxFromClippingPlanes(
            doc, view3d, viewpoint, eyePosition, forwardDirection, idsToSelect);

        if (sectionBoxApplied && ZoomToSectionBox(uidoc, view3d))
        {
            return;  // Thành công! Zoom vào section box.
        }

        // Fallback: Section box từ BCF không hợp lệ → Tạo box quanh Element
        if (idsToSelect.Count > 0)
        {
            if (ApplySectionBoxAroundElements(doc, view3d, idsToSelect)
                && ZoomToSectionBox(uidoc, view3d))
            {
                return;
            }
            uidoc.ShowElements(idsToSelect);
        }
        else
        {
            DisableSectionBox(doc, view3d);
            TaskDialog.Show("Revit Sync",
                "Không thể hiển thị vị trí BCF. Tọa độ BCF không khớp với mô hình Revit " +
                "và không tìm thấy phần tử tương ứng.");
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine("Camera Sync error: " + ex.Message);
    }
}
```

---

## 🛠 Checklist cho IDE
- [ ] Sửa Bug 2 trước (đảo 6 hướng trong RevitIssueCreator.cs) - Nhanh nhất
- [ ] Sửa Bug 4 (xóa OrientViewFromBcfCamera, xóa SetOrientation)
- [ ] Sửa Bug 1 (xóa logic thử 2 chiều, chỉ dùng Inverse)
- [ ] Sửa Bug 3 (thêm hàm GetModelExtents, IsInsideExtents, sửa fallback)
- [ ] Build lại: `dotnet build src\Antigravity.IssueManager\Antigravity.IssueManager.csproj`
- [ ] Gửi link DLL cho user test
