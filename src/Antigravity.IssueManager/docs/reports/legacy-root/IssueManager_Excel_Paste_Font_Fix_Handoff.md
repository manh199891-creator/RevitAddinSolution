# Tài liệu bàn giao: Tổng hợp các thay đổi (Từ 11:55 AM đến nay)

Tài liệu này tổng hợp toàn bộ lịch sử chỉnh sửa và giải pháp kỹ thuật liên quan đến phân hệ **Issue Manager** trong Revit Add-in, bắt đầu từ phiên cập nhật lúc **11:55 AM**.

---

## 1. Sửa lỗi tương thích định dạng file Excel (.xlsx)

### Nguyên nhân
Khi mở file Excel xuất ra từ Add-in bằng Microsoft Excel, hệ thống báo lỗi định dạng không hợp lệ hoặc không tương thích. Lỗi do trình tạo XML mặc định thêm thẻ khai báo `<?xml version="1.0" encoding="utf-8"?>` ở đầu các file XML con bên trong gói ZIP (.xlsx), khiến Excel không chấp nhận.

### Giải pháp kỹ thuật
Cập nhật [ExcelIssueExporter.cs](file:///e:/Antigravity/RevitAddinSolution/src/Antigravity.IssueManager/Services/ExcelIssueExporter.cs):
- Cấu hình `OmitXmlDeclaration = true` trong `XmlWriterSettings` tại phương thức trợ giúp `CreateXml`.
- Việc này giúp loại bỏ dòng khai báo XML thừa ở đầu mỗi file con, đảm bảo cấu trúc OOXML (.xlsx) chuẩn hóa hoàn toàn, mở được trên mọi phiên bản Excel/WPS Office mà không gặp cảnh báo lỗi.

---

## 2. Tạo mới cửa sổ biên tập ảnh chú thích (Markup Editor)

### Yêu cầu
Cần có công cụ cho phép vẽ ghi chú, khoanh đỏ, vẽ mũi tên trực tiếp lên ảnh chụp 3D từ Revit hoặc ảnh 2D tải lên để người dùng chú thích rõ ràng vấn đề (clash/RFI) trước khi tạo RFI.

### Giải pháp kỹ thuật
- **Tạo mới giao diện vẽ (`MarkupEditorWindow.xaml` / `.xaml.cs`)**:
  - Giao diện tối giản hiện đại (Dark Theme) với thanh công cụ phía trên và vùng vẽ (InkCanvas) chiếm trọn bên dưới.
  - Hỗ trợ công cụ: Vẽ tự do (Pen), Vẽ hình chữ nhật (Rectangle), Vẽ mũi tên (Arrow).
  - Hỗ trợ chức năng chọn màu sắc, độ dày nét vẽ, Hoàn tác (Undo/Redo) và Lưu (Save/Flatten) ảnh.
  - Khi lưu, hệ thống sẽ gộp (flatten) tất cả các nét vẽ vector trên InkCanvas đè lên ảnh nền bitmap và xuất ra một file ảnh PNG mới hoàn chỉnh.
- **Tích hợp vào các hộp thoại**:
  - **Tạo mới Issue (`CreateIssueDialog`)**: Thêm nút `✏️ Markup` bên cạnh ảnh preview 2D để mở trình biên tập trực tiếp.
  - **Quản lý Issue (`IssueManagerWindow`)**: Thêm nút `✏️ Markup 3D` và `✏️ Markup 2D` động dưới panel hiển thị ảnh của issue hiện tại để người dùng cập nhật ghi chú bất kỳ lúc nào.

---

## 3. Nhúng ảnh trực tiếp vào cột Excel (Cột T) thay vì in đường dẫn text

### Nguyên nhân
Trước đây, cột "Hình ảnh" (Cột T) chỉ in ra đường dẫn dạng text của ảnh 3D và 2D trên máy cục bộ. File Excel xuất ra không chứa dữ liệu hình ảnh nhúng.

### Giải pháp kỹ thuật (OpenXML bằng ZIP/XML thủ công)
Cập nhật [ExcelIssueExporter.cs](file:///e:/Antigravity/RevitAddinSolution/src/Antigravity.IssueManager/Services/ExcelIssueExporter.cs):
- **Nhúng tệp hình ảnh vật lý**: Đọc các ảnh từ `SnapshotFilePath` và `SnapshotFilePath2`, ghi trực tiếp vào tệp lưu trữ dưới dạng `xl/media/image{N}.png`.
- **Cấu hình Content Types**: Đăng ký phần mở rộng ảnh (`.png`, `.jpg`, `.jpeg`) và override phân hệ vẽ tranh (`/xl/drawings/drawing1.xml`) trong `[Content_Types].xml`.
- **Thiết lập Vẽ tranh (Drawing XML)**:
  - Tạo file `xl/drawings/drawing1.xml` định vị ảnh trong cột T (Index 19).
  - Nếu chỉ có 1 ảnh: Ảnh chiếm trọn bề rộng cột T.
  - Nếu có 2 ảnh (3D & 2D): Tự động chia đôi ô cột T bằng tọa độ EMU offset để đặt 2 ảnh nằm song song (side-by-side) gọn gàng.
  - Sử dụng khóa tỉ lệ `<a:picLocks noChangeAspect="1"/>` để Excel giữ nguyên tỉ lệ gốc của ảnh chụp khi người dùng co giãn dòng/cột.
- **Tạo mối quan hệ (Relationships)**:
  - Tạo `xl/worksheets/_rels/sheet1.xml.rels` liên kết worksheet với bản vẽ (`drawing1.xml`).
  - Tạo `xl/drawings/_rels/drawing1.xml.rels` liên kết bản vẽ với các tài nguyên ảnh nhúng trong `xl/media/`.
- **Định dạng bảng**:
  - Hàng nào có ảnh sẽ tự động tăng chiều cao lên `120.0 pt` (tương đương ~160 pixel) để ảnh hiển thị rõ nét.
  - Xóa chuỗi đường dẫn text trong ô cột T nếu ô đó có vẽ hình ảnh đè lên để tránh rối mắt.

---

## 4. Sửa lỗi gõ tiếng Việt bị chuyển thành dấu hỏi chấm `?`

### Nguyên nhân
Cửa sổ `CreateIssueDialog` trước đây được mở bằng lệnh `.Show()` dưới dạng **Modeless**. Khi đó, các luồng bắt phím của Revit chiếm quyền ưu tiên và chặn quá trình xử lý ký tự tổ hợp của bộ gõ (UniKey/EVKey), khiến ký tự Unicode tiếng Việt bị hỏng thành dấu `?`.

### Giải pháp kỹ thuật
Cập nhật [IssueManagerWindow.xaml.cs](file:///e:/Antigravity/RevitAddinSolution/src/Antigravity.IssueManager/UI/IssueManagerWindow.xaml.cs):
- Chuyển đổi lệnh gọi mở cửa sổ `CreateIssueDialog` từ `.Show()` thành `.ShowDialog()` (**Modal**).
- Phương pháp này tạo vòng lặp thông điệp (Message Loop) riêng cho hộp thoại WPF, cô lập hoàn toàn khỏi tiến trình bắt phím của Revit, giúp gõ tiếng Việt có dấu chuẩn xác 100%.

---

## 5. Sửa lỗi dán ảnh 2D từ Clipboard (Ctrl+V) không hoạt động

### Nguyên nhân
- Clipboard mặc định của WPF chỉ kiểm tra định dạng `Bitmap`, trong khi các công cụ chụp ảnh màn hình của Windows (Snipping Tool, Snip & Sketch) lưu ảnh tạm dưới dạng **DIB (Device Independent Bitmap)**.
- Vùng chứa ảnh 2D (`Border`) trong XAML không hỗ trợ Focus mặc định, dẫn đến phím tắt Ctrl+V không được gửi đến đúng vùng nhận diện khi người dùng click vào vùng ảnh.

### Giải pháp kỹ thuật
- **Hỗ trợ định dạng DIB**: Cập nhật [CreateIssueDialog.xaml.cs](file:///e:/Antigravity/RevitAddinSolution/src/Antigravity.IssueManager/UI/CreateIssueDialog.xaml.cs) thêm hàm `ConvertDibToBitmapSource` để giải mã mảng byte DIB bằng cách dựng lại phần Header 14-byte của tệp BMP tiêu chuẩn, giúp dán trực tiếp ảnh từ Snipping Tool mượt mà.
- **Bổ sung Focus & Chỉ báo viền**:
  - Cập nhật [CreateIssueDialog.xaml](file:///e:/Antigravity/RevitAddinSolution/src/Antigravity.IssueManager/UI/CreateIssueDialog.xaml) thiết lập `Focusable="True"` cho `BorderImage2D`.
  - Tạo Style Trigger `BorderFocusStyle`: Khi click chọn vùng ảnh 2D, viền Border sẽ tự động chuyển sang màu **Vàng Gold (`#FFD700`)** báo hiệu sẵn sàng nhận lệnh Paste.
  - Bắt sự kiện `MouseLeftButtonDown` để kích hoạt focus vào Border này.
