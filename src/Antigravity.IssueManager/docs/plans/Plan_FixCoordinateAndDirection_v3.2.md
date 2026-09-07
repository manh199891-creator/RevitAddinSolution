# Kế hoạch Fix: BCF Coordinate Mismatch & ClipPlane Direction
**Version:** v3.2
**Mục tiêu:** Sửa triệt để 3 lỗi gốc rễ khiến "Show in Model" không hoạt động.

---

## 🔴 Bug 1: Tọa độ Geo-referenced bị lệch hàng trăm km

### Nguyên nhân gốc
File BCF từ Trimble Connect chứa tọa độ địa lý thực (UTM/VN-2000):
- `CameraViewPoint.X = 597,026 m` (≈ 1,958,000 Feet)
- `CameraViewPoint.Y = 2,304,647 m` (≈ 7,560,000 Feet)

Trong khi Revit model nội bộ thường có gốc gần `(0, 0, 0)`.

Hàm `TransformCoordinates` hiện tại đổi BCF Meters → Feet rồi dùng `GetTransform()` để biến đổi. Nhưng ma trận này KHÔNG đủ lớn để kéo `597,000m` về gần `(0, 0)`. Kết quả: Section Box bị đặt cách mô hình hàng triệu Feet.

### Cách sửa
Hàm `TransformCoordinates` cần kiểm tra kết quả sau khi transform:
1. Sau khi transform xong, so sánh kết quả với Model Extents (BoundingBox tổng thể của toàn bộ mô hình Revit).
2. Nếu kết quả nằm **ngoài** Model Extents (khoảng cách > 1000 Feet), thì thử dùng `.Inverse`:
   ```csharp
   XYZ result = transform.OfPoint(sharedXyz);
   BoundingBoxXYZ modelExtents = GetModelExtents(doc);
   if (!IsInsideExtents(result, modelExtents, toleranceFeet: 1000))
   {
       // Thử chiều ngược lại
       result = transform.Inverse.OfPoint(sharedXyz);
   }
   ```
3. Nếu cả 2 chiều đều nằm ngoài Model Extents → **Bỏ qua tọa độ BCF hoàn toàn**. Dùng fallback: Tìm Element bằng IFC GUID, lấy BoundingBox của Element đó làm tâm Section Box.

### Hàm phụ trợ cần tạo mới
```csharp
private static BoundingBoxXYZ GetModelExtents(Document doc)
{
    // Lấy BoundingBox tổng thể của tất cả phần tử trong mô hình
    FilteredElementCollector collector = new FilteredElementCollector(doc)
        .WhereElementIsNotElementType();
    // Gom Min/Max từ tất cả BoundingBox
    ...
}

private static bool IsInsideExtents(XYZ point, BoundingBoxXYZ extents, double tolerance)
{
    return point.X >= extents.Min.X - tolerance 
        && point.X <= extents.Max.X + tolerance
        && point.Y >= extents.Min.Y - tolerance 
        && point.Y <= extents.Max.Y + tolerance
        && point.Z >= extents.Min.Z - tolerance 
        && point.Z <= extents.Max.Z + tolerance;
}
```

---

## 🔴 Bug 2: Hướng Direction mặt cắt bị NGƯỢC (Export)

### Nguyên nhân gốc
File `RevitIssueCreator.cs` hiện tại ghi Direction mặt cắt hướng **OUTWARD** (ra ngoài).
Nhưng chuẩn BCF 2.1 (buildingSMART) quy định Direction phải hướng **INWARD** (vào trong vùng giữ lại).

### Cách sửa
Mở file `RevitIssueCreator.cs`, hàm `CreateSectionBoxClipPlanes`.
Đảo ngược (Negate) toàn bộ 6 vector Direction:

**Code cũ (SAI):**
```csharp
// min X direction -X (outward)
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(min.X, mid.Y, mid.Z), XYZ.BasisX.Negate());
// max X direction +X (outward) 
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(max.X, mid.Y, mid.Z), XYZ.BasisX);
```

**Code mới (ĐÚNG - Inward):**
```csharp
// min X: mặt bên trái → hướng vào trong là +X
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(min.X, mid.Y, mid.Z), XYZ.BasisX);
// max X: mặt bên phải → hướng vào trong là -X
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(max.X, mid.Y, mid.Z), XYZ.BasisX.Negate());
// min Y → hướng vào trong là +Y
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, min.Y, mid.Z), XYZ.BasisY);
// max Y → hướng vào trong là -Y
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, max.Y, mid.Z), XYZ.BasisY.Negate());
// min Z → hướng vào trong là +Z
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, mid.Y, min.Z), XYZ.BasisZ);
// max Z → hướng vào trong là -Z
AddClipPlane(planes, boxTransform, projectPosition, new XYZ(mid.X, mid.Y, max.Z), XYZ.BasisZ.Negate());
```

---

## 🟡 Bug 3: BCF chỉ có 1 Clipping Plane (Import)

### Nguyên nhân gốc
Khi Trimble/Navis chỉ gửi 1 mặt cắt, code hiện tại không đủ dữ liệu tạo Section Box đầy đủ.

### Cách sửa
Ở hàm `ApplySectionBoxFromClippingPlanes`, khi số mặt cắt `< 6`:
1. Tìm tâm dựa trên IFC GUID element hoặc Camera position (fallback).
2. Tạo hộp mặc định 20x20x20 Feet quanh tâm.
3. Chỉ ghi đè các giới hạn min/max mà BCF cung cấp (1 mặt cắt Z = chỉ ghi đè maxZ hoặc minZ).
4. Giữ nguyên 5 mặt ảo còn lại.

---

## 🛠 Thứ tự ưu tiên cho IDE
1. **Bug 2 (Direction ngược)** → Sửa nhanh nhất, chỉ cần đảo 6 dấu. Ảnh hưởng đến Export.
2. **Bug 1 (Tọa độ lệch)** → Cần thêm hàm kiểm tra Model Extents. Ảnh hưởng đến Import.
3. **Bug 3 (Thiếu mặt cắt)** → Nâng cấp thuật toán fallback. Ảnh hưởng đến Import.
