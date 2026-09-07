# Kế hoạch Refactor (Sửa lỗi) BCF Camera Sync
**Tên task:** Sửa lỗi Camera bị văng và vô hiệu hóa Section Box khi đọc file BCF.
**Version:** v3.0 (Update for Antigravity Issue Manager)
**File mục tiêu:** `RevitCameraSync.cs`

---

## 🎯 Mục tiêu tổng thể
Tái cấu trúc lại luồng xử lý tọa độ và thuật toán hộp cắt trong hàm "Show in Model". 
Đảm bảo rằng dù file BCF chỉ có 1 mặt cắt (chẳng hạn lưu Viewpoint bằng cách cắt trần) và xuất từ môi trường Shared Coordinates, Revit vẫn nhận diện và zoom lại đúng vị trí va chạm với hộp cắt bao quanh hợp lý.

---

## 📋 Chi tiết các Giai đoạn (Phases)

### Phase 1: Fix lỗi Ma trận Tọa độ (Văng Camera)
**Phân tích lỗi:** 
Trong Revit API, hàm `doc.ActiveProjectLocation.GetTransform()` trả về một ma trận có tác dụng biến đổi một điểm từ **Shared Coordinates** (Hệ tọa độ chung) về **Internal Coordinates** (Hệ tọa độ nội bộ của Revit).
Tuy nhiên, code cũ lại gọi `transform.Inverse.OfPoint()`. Việc gọi `Inverse` (nghịch đảo) đã biến nó thành ma trận chuyển từ Internal ra Shared. Áp dụng ma trận này lên một điểm BCF vốn dĩ đã là Shared, khiến điểm đó bị "nhân đôi độ lệch" và bay ra xa hàng ngàn Feet.

**Nhiệm vụ cho IDE:**
1. Mở file `RevitCameraSync.cs`.
2. Tìm hàm `TransformCoordinates` và `TransformVector`.
3. Sửa `transform.Inverse.OfPoint(sharedXyz)` thành `transform.OfPoint(sharedXyz)`. (Bỏ Inverse).
4. Tương tự cho `OfVector`. Đảm bảo code chạy xuôi chiều ma trận.

### Phase 2: Nới lỏng thuật toán tạo Section Box
**Phân tích lỗi:**
Hàm `CreateSectionBoxFromClippingPlanes` đang ép buộc phải gom đủ 3 cặp trục tạo thành khối hộp 6 mặt (Orthogonal Box). Nếu thiếu mặt, code trả về `null`.

**Nhiệm vụ cho IDE:**
1. Viết lại hàm `CreateSectionBoxFromClippingPlanes`.
2. Xác định **Điểm tâm (Center Point)**:
   * Nếu BCF có lưu tọa độ va chạm (`viewpoint.ClashPointX`), dùng làm tâm.
   * Nếu không, lấy `CameraViewPoint` + một đoạn tịnh tiến theo `CameraDirection`.
3. **Tạo khối cắt mặc định (Default Box):** Khởi tạo một hộp cắt ảo kích thước khoảng 20x20x20 Feet bao quanh Tâm. Định nghĩa sẵn 6 giới hạn Min-Max cơ bản theo trục XYZ thế giới (BasisX, BasisY, BasisZ).
4. **Cắt gọt bằng BCF:**
   * Lặp qua mảng `clippingPlanes` có trong BCF (có thể chỉ có 1-2 mặt).
   * Dùng góc vector pháp tuyến (`Normal`) để xác định mặt này thuộc trục nào (VD: Hướng lên là trục Z).
   * Thay thế giới hạn mặc định bằng khoảng cách thực tế của mặt cắt đó.
5. Cuối cùng, trả về `BoundingBoxXYZ` từ 6 mặt giới hạn mới này. Bằng cách này, dù BCF gửi về 1 mặt phẳng, nó vẫn hiện ra một hộp 6 mặt (5 mặt ảo + 1 mặt thật cắt đúng chuẩn BCF).
