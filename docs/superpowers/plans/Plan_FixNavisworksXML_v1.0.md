# Kế hoạch sửa lỗi hiển thị Issue từ file XML Navisworks

## Context
Khi load file báo cáo xung đột dạng XML từ Navisworks vào Issue Manager:
1. **Màn hình đen**: Nút "Show in Model" dẫn tới màn hình đen do XML Navisworks chỉ cung cấp `clashpoint` mà `NavisworksXmlParser` chưa trích xuất thông tin `<viewpoint><camera>`. Điều này khiến toạ độ camera mặc định là `(0,0,0)` và hướng nhìn sai, bị Section Box che khuất hoặc nhìn ra khoảng trống.
2. **Không hiển thị ảnh Snapshot**: UI không tải được ảnh do đường dẫn `href` ghi trong XML có thể không khớp chính xác với tên file do Navisworks sinh ra (ví dụ trong XML ghi `clash28.jpg` nhưng thực tế tạo ra `clash28_image0.jpg`), và cách xử lý URI trong WPF đôi khi lỗi đối với các file path nội bộ.

## Proposed Changes

### 1. Cập nhật `NavisworksXmlParser.cs`
- Chỉnh sửa hàm `ParseReport(string xmlFilePath)` để chủ động tìm và trích xuất dữ liệu từ thẻ `<camera>`.
- Trích xuất:
  - `CameraX`, `CameraY`, `CameraZ` từ thẻ `<pos3f>` bên trong `<camera>`.
  - `CameraDirectionX`, `CameraDirectionY`, `CameraDirectionZ` từ thẻ `<dir3f>` bên trong `<camera>`.
  - `CameraUpX`, `CameraUpY`, `CameraUpZ` từ thẻ `<up3f>` bên trong `<camera>`.
- Cải thiện hàm `ResolveImagePath` hoặc logic tìm ảnh (đoạn line 71-100) để thêm cơ chế "thử nghiệm/dò tìm" (probing) các hậu tố như `_image0.jpg` và `_image1.jpg` khi không tìm thấy chính xác đường dẫn gốc.

### 2. Cập nhật `IssueManagerWindow.xaml.cs`
- Trong hàm `LoadSnapshot(Image image, string path)`, thay thế việc gọi `new Uri(path)` bằng cách gọi rõ định dạng an toàn cho đường dẫn tuyệt đối (ví dụ `new Uri(path, UriKind.Absolute)`).

---
*Vui lòng căn cứ theo kế hoạch này để viết code sửa lỗi cho Issue Manager.*
