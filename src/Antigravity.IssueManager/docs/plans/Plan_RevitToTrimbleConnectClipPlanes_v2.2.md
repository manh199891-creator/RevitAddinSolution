# Kế hoạch triển khai (Detailed Implementation Plan)
**Tên tính năng:** Chuyển đổi Revit Section Box thành Trimble Connect Clip Planes (Thông qua xuất file BCF)
**Version:** v2.2 (Cập nhật chuẩn hóa ma trận Toán học Transform)
**Môi trường:** Visual Studio, C# .NET, Revit API, System.IO.Compression
**Hệ tọa độ:** Shared Coordinates

---

## 🎯 Mục tiêu tổng thể
Tích hợp trực tiếp tính năng trích xuất Section Box vào **Add-in Antigravity Issue Manager**.
Khi người dùng tạo Issue mới hoặc xuất file BCF từ giao diện công cụ này, hệ thống sẽ:
1. Lấy kích thước Section Box, Camera và các ma trận Transform.
2. Chuyển đổi hệ tọa độ sang Shared Coordinates một cách chuẩn xác (ngay cả khi Section Box bị xoay).
3. Chèn 6 Clip Planes vào file `viewpoint.bcfv` bên trong gói `.bcfzip`.

---

## 📋 Chi tiết các Giai đoạn (Phases)

### Phase 1: Khảo sát mã nguồn hiện hành (Antigravity Issue Manager)
**Nhiệm vụ:**
1. Mở Project `Antigravity Issue Manager`.
2. Tìm đến các Class chịu trách nhiệm xử lý nút **"Create Issue"** hoặc **"Export BCF"**.
3. Phân tích cấu trúc hàm ghi file XML (BCF) hiện tại để chuẩn bị "bơm" thêm dữ liệu Clipping Planes.

### Phase 2: Xử lý Revit API & Hình học (Toán học Transform)
**Nhiệm vụ:**
Viết một hàm tiện ích (Utility method) thực hiện tính toán hình học:
1. Lấy `bbox = view3D.GetSectionBox()`.
2. Lấy ma trận của chính hộp cắt (Do hộp cắt có thể bị xoay): `boxTransform = bbox.Transform`.
3. Lấy ma trận Shared Coordinates: `sharedTransform = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero).Transform`.
4. Lấy thông số Camera: `ViewOrientation3D orientation = view3D.GetOrientation();`.
5. **Tính toán & Chuyển đổi tọa độ:**
   * Từ `bbox.Min` và `bbox.Max` nội bộ, tạo ra 6 điểm gốc (Location) và 6 Vector pháp tuyến (Direction) tương ứng cho 6 mặt.
   * Để có tọa độ thế giới thực sự của 6 mặt cắt, ta phải nhân tọa độ/vector với `boxTransform` trước, sau đó nhân tiếp với `sharedTransform`.
   * Đối với Camera, chỉ cần nhân với `sharedTransform` (vì nó không phụ thuộc hệ trục local của Section Box).
   * Quy đổi toàn bộ tọa độ từ hệ đơn vị nội bộ của Revit (Feet) sang đơn vị Mét (Meters) theo chuẩn BCF.

### Phase 3: Bơm dữ liệu vào quy trình sinh file BCF hiện có
**Nhiệm vụ:**
1. Chỉnh sửa logic sinh file `viewpoint.bcfv` trong mã nguồn.
2. Thêm thẻ `<PerspectiveCamera>` bằng dữ liệu Camera lấy từ Phase 2.
3. Thêm thẻ `<ClippingPlanes>`, dùng vòng lặp ghi 6 thẻ `<ClippingPlane>` (với `<Location>` và `<Direction>`).
4. Đảm bảo file ảnh `snapshot.png` của Add-in hiện tại vẫn hoạt động bình thường, kẹp chung vào thư mục `.bcfzip`.

### Phase 4: Nâng cấp Giao diện (UI/UX) - Tuỳ chọn
**Nhiệm vụ:**
1. Thêm một Checkbox nhỏ trên giao diện WPF với nhãn: `[x] Include Section Box (Clip Planes)`.
2. Chỉ thực thi luồng Phase 2 & 3 nếu Checkbox này được tick khi Export BCF.

---

## 🛠 Hướng dẫn cho IDE Agent
- Chú ý quan trọng: Phép nhân ma trận trong Revit không có tính giao hoán. Trình tự áp dụng biến đổi cho Section Box phải là: Điểm/Vector nội bộ -> áp dụng `boxTransform` -> áp dụng `sharedTransform`.
- Khi ghi XML, bắt buộc sử dụng `CultureInfo.InvariantCulture` để ghi số thập phân (dấu chấm `.`).
- Vector hướng (`Direction`) của chuẩn BCF là hướng chỉ VÀO TRONG vùng giữ lại của mô hình.
