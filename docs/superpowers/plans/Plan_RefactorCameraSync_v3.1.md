# Kế hoạch Refactor: Đơn giản hóa "Show in Model"
**Version:** v3.1
**File mục tiêu:** `RevitCameraSync.cs`

---

## 🎯 Triết lý mới
**Bỏ hoàn toàn việc đặt góc Camera (Perspective).** Thay vào đó:
1. Nhảy ra View 3D mặc định (Isometric - luôn có sẵn).
2. Đặt Section Box từ tọa độ BCF lên View đó.
3. Gọi `ZoomAndCenterRectangle()` để Revit tự zoom vào hộp cắt.

Cách này **nhanh tức thì** (không animation xoay), luôn chính xác, và không phụ thuộc vào việc camera BCF có hợp lệ hay không.

---

## 📋 Chi tiết (Phases)

### Phase 1: Sửa hàm `GetOrCreateBcfView` - Luôn dùng Isometric
**Thay đổi:**
1. Bỏ hoàn toàn nhánh `View3D.CreatePerspective(...)`. 
2. Luôn luôn tạo hoặc tìm View kiểu `View3D.CreateIsometric(...)` với tên cố định `"BCF Issue View"`.
3. Bỏ tham số `bool isOrthogonal` vì giờ luôn là Isometric.

### Phase 2: Sửa hàm `SyncCamera` - Bỏ SetOrientation, chỉ dùng Section Box + Zoom
**Thay đổi trong block `// 3. Set Camera View`:**
1. **Xóa toàn bộ** đoạn code gọi `SetOrientation()`.
2. **Giữ nguyên** hàm `ApplySectionBoxFromClippingPlanes(...)` để đặt Section Box.
3. **Thay thế** phần camera bằng logic đơn giản:
   ```csharp
   // Sau khi đặt Section Box xong, zoom view vào hộp cắt
   UIView uiView = uidoc.GetOpenUIViews()
       .FirstOrDefault(v => v.ViewId == view3d.Id);
   if (uiView != null)
   {
       BoundingBoxXYZ sbox = view3d.GetSectionBox();
       if (sbox != null)
       {
           XYZ worldMin = sbox.Transform.OfPoint(sbox.Min);
           XYZ worldMax = sbox.Transform.OfPoint(sbox.Max);
           uiView.ZoomAndCenterRectangle(worldMin, worldMax);
       }
   }
   ```
4. **Giữ nguyên** fallback: Nếu không có ClippingPlanes VÀ không có Section Box, dùng `uidoc.ShowElements(idsToSelect)`.

### Phase 3: Kiểm tra hàm `TransformCoordinates`
Code hiện tại dùng `doc.ActiveProjectLocation.GetTransform()`. Theo tài liệu Revit API:
- `GetTransform()` trả về ma trận biến đổi từ **Internal → Shared**.
- Để biến ngược (BCF/Shared → Internal), ta cần dùng `.Inverse`.

Code hiện tại đã bỏ `.Inverse` nhưng Eye Elevation vẫn `20229.1`. Cần kiểm tra:
1. Thêm Debug Log tạm ở đầu hàm `TransformCoordinates` để in giá trị Transform Origin.
2. Tuy nhiên, nếu ta **bỏ hẳn Camera** (Phase 2), lỗi Camera không còn quan trọng. Chỉ cần **tọa độ Section Box đúng**.
3. Nếu Section Box vẫn lệch, thử đảo lại `.Inverse` cho hàm `TransformCoordinates`.

---

## 🛠 Hướng dẫn cho IDE Agent
- Ưu tiên tuyệt đối: **Bỏ Perspective, dùng Isometric + ZoomToFit.**
- Không cần lo Camera Direction hay Camera Up nữa.
- Giữ nguyên logic Isolate Elements (nếu có ElementIds) và logic tạo Section Box.
- Mục tiêu: Khi user bấm "Show in Model", Revit phải nhảy tới đúng vị trí **trong vòng 1 giây**, không xoay, không animation.
