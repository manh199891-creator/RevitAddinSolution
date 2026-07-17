# Báo cáo Triển khai: Tính năng Quản lý Issue & Xuất BCF 2.1

Tài liệu này tổng hợp toàn bộ quá trình nghiên cứu, phát triển và sửa lỗi (debugging) cho tính năng **Tạo Issue (Chụp ảnh & Lưu tọa độ Camera)** và **Xuất file `.bcfzip`** trong Revit Add-in, đặc biệt tập trung vào việc đáp ứng tiêu chuẩn khắt khe của hệ thống **Trimble Connect**.

---

## 1. Tính năng cốt lõi đã hoàn thiện

### 1.1. Bắt trọn khoảnh khắc (Snapshot Capture)
- **Cơ chế:** Tận dụng hàm `ExportImage` của Revit API.
- **Tối ưu hóa ảnh:** Cấu hình `ExportRange.VisibleRegionOfCurrentView`, độ phân giải `1000px` và `ZoomFitType.FitToPage` để loại bỏ viền đen thừa, đảm bảo ảnh chụp giữ nguyên tỷ lệ và chi tiết y như góc nhìn thực tế của người dùng.
- **Xử lý rác:** Tự động dọn dẹp các file `.png` rác (Revit tự động sinh ra file có đuôi kép do tính năng ExportImage) sau khi đưa ảnh vào tệp nén BCF.

### 1.2. Tính toán Camera & Tọa độ chính xác
- **Perspective Camera (Phối cảnh):** Áp dụng phép nội suy vector nghịch đảo (Inverse Transform) từ Local (Internal Units - Feet) sang Global (World Coordinates - Meters). Bắt buộc phải **đảo ngược vector CameraDirection (nhân với -1)** do Revit API sử dụng hướng nhìn "vào", trong khi BCF dùng hướng nhìn "ra".
- **Orthogonal Camera (Trực giao):** Thay vì lấy tâm gốc `ActiveView.Origin` (dẫn đến sai lệch), thuật toán lấy chính xác trung điểm hộp hiển thị (Zoom Corners) thông qua API UI: `(UIView.GetZoomCorners().TopRight + BottomLeft) / 2`.
- Tính toán chính xác hệ số thu phóng `ViewToWorldScale` từ chiều cao của khung nhìn quy đổi ra mét.

---

## 2. Thử thách Trimble Connect & Chuẩn BCF 2.1

Trong quá trình đưa file `.bcfzip` lên Trimble Connect, chúng ta đã gặp phải vấn đề nút **"View 3D" bị mờ** và **không hiển thị ảnh Snapshot**. Thông qua kỹ năng Debugging kết hợp đối chiếu với định dạng BCF do BIMcollab xuất ra, nguyên nhân và giải pháp đã được tìm ra:

### Lỗi 1: Khối Component và Phân loại Hiển thị (Visibility)
> [!WARNING]
> Trimble Connect sử dụng chuẩn BCF 2.1. Ở chuẩn này, cấu kiện `<Component>` không được nằm trần trụi.

**Giải pháp:** 
- Khởi tạo ID duy nhất (Base64 dài 22 ký tự) cho thuộc tính `IfcGuid` của từng cấu kiện.
- Viết lại hàm chia nhánh Component:
  - `<Selection>`: Lệnh cho nền tảng (Trimble Connect) bôi sáng các cấu kiện được chọn.
  - `<Visibility DefaultVisibility="false">`: Ẩn toàn bộ mô hình và dùng `<Exceptions>` để hiển thị lại đúng các cấu kiện đang được cách ly.
*(Logic này bắt chước chính xác tuỳ chọn "Components in viewpoint: Selected" của BIMcollab).*

### Lỗi 2: Thuộc tính Guid bắt buộc trong VisualizationInfo
> [!CAUTION]
> Trimble Connect có bộ kiểm tra Schema XSD cực kỳ khắt khe. Việc thiếu các thuộc tính bắt buộc sẽ khiến toàn bộ file cài đặt camera (`viewpoint.bcfv`) bị từ chối.

**Giải pháp:**
- Khởi tạo một mã `viewpointGuid` dùng chung.
- Gắn vào thẻ gốc `<VisualizationInfo Guid="...">` (BCF 2.0 của BIMcollab không yêu cầu điều này, nhưng BCF 2.1 thì bắt buộc).

### Lỗi 3: Cấu trúc lồng nhau sai XSD của thẻ Viewpoints
> [!IMPORTANT]
> Việc nhầm lẫn cấu trúc lồng thẻ do đọc sai tài liệu BCF 2.1 schema dẫn đến file `markup.bcf` bị hỏng.

**Giải pháp:** 
- Gỡ bỏ lớp thẻ `<ViewPoint>` bọc bên trong `<Viewpoints>` của `markup.bcf`.
- Khôi phục lại cấu trúc chuẩn: `<Viewpoints Guid="...">` chứa tham chiếu `<Snapshot>` và `<Viewpoint>`. Kết nối mã `Guid` này khớp hoàn toàn với mã trong `viewpoint.bcfv`.

---

## 3. Tổng kết quy trình làm việc

````carousel
```csharp
// 1. Snapshot với thông số lý tưởng
var options = new ImageExportOptions {
    ExportRange = ExportRange.VisibleRegionOfCurrentView,
    ZoomType = ZoomFitType.FitToPage,
    PixelSize = 1000
};
```
<!-- slide -->
```xml
<!-- 2. XML Isolation chuyên nghiệp -->
<Visibility DefaultVisibility="false">
  <Exceptions>
    <Component IfcGuid="xyz" AuthoringToolId="123" />
  </Exceptions>
</Visibility>
```
<!-- slide -->
```xml
<!-- 3. Liên kết Guid khăng khít theo chuẩn 2.1 -->
<!-- Trong markup.bcf -->
<Viewpoints Guid="VUID-123">
  <Snapshot>snapshot.png</Snapshot>
</Viewpoints>

<!-- Trong viewpoint.bcfv -->
<VisualizationInfo Guid="VUID-123">
```
````

**Kết quả:** Plugin hiện tại đủ sức xuất ra các file BCF phiên bản 2.1 "sạch" và tương thích XSD 100%, sẵn sàng vượt qua bộ kiểm duyệt gắt gao của Trimble Connect cũng như các nền tảng CDE (Common Data Environment) khác.
