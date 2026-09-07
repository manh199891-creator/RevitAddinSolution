# Kế hoạch lưu trữ Issue trực tiếp vào file Revit (RVT) thông qua Extensible Storage

## Context
Người dùng muốn lưu trữ các Issue ngay trong file `.rvt` hiện tại thay vì ổ cứng cục bộ (`%AppData%`) để phục vụ làm việc nhóm (Worksharing / Sync server).

## Các vấn đề gặp phải với thiết kế hiện tại:
1. File lưu trữ ở `%AppData%` chỉ có tác dụng cục bộ. Người khác mở file RVT không thấy các issue.
2. Hàm "Load XML / BCF" đang ghi đè danh sách `_currentIssues`, làm mất các Issue vừa khởi tạo.
3. Việc lưu tự động chỉ kích hoạt qua `AutoSaveIssues` trên click, đôi khi không an toàn nếu tắt phần mềm đột ngột.
4. Lỗi ẩn trong `System.IO.Compression.FileSystem` khi extract làm trắng danh sách Issue lúc khởi động.

## Proposed Changes

### Antigravity.IssueManager

#### [NEW] [IssueStorageService.cs](file:///E:/Antigravity/RevitAddinSolution/src/Antigravity.IssueManager/Services/IssueStorageService.cs)
- Lớp `IssueStorageService` quản lý `Autodesk.Revit.DB.ExtensibleStorage`.
- Tạo một `SchemaBuilder` với tên `Antigravity_IssueManager_Schema`.
- Chứa 1 trường kiểu chuỗi (String field) lớn để chứa nội dung JSON serialize toàn bộ danh sách `List<IssueModel>`.
- Có hàm `SaveIssuesToDocument(Document doc, List<IssueModel> issues)`: Serialize issues sang JSON và lưu vào `DataStorage` element (tạo mới hoặc cập nhật). Yêu cầu chạy trong `Transaction`.
- Có hàm `LoadIssuesFromDocument(Document doc)`: Đọc `DataStorage`, Deserialize JSON và trả về `List<IssueModel>`.

#### [MODIFY] [ViewpointModel.cs](file:///E:/Antigravity/RevitAddinSolution/src/Antigravity.IssueManager/Models/ViewpointModel.cs)
- Thêm trường `public string SnapshotBase64 { get; set; }` để lưu trữ ảnh dưới dạng mã hoá text (phục vụ nhúng ảnh thẳng vào JSON Extensible Storage).

#### [MODIFY] [IssueManagerWindow.xaml.cs](file:///E:/Antigravity/RevitAddinSolution/src/Antigravity.IssueManager/UI/IssueManagerWindow.xaml.cs)
- Sửa đổi hàm `AutoSaveIssues()`: Khi gọi lưu, chuyển ảnh sang Base64, sau đó kích hoạt 1 `ExternalEvent` (ví dụ `SaveIssueEvent.Raise()`) để chạy `IssueStorageService.SaveIssuesToDocument` (vì API bắt buộc phải lưu trong Transaction của Revit).
- Sửa đổi hàm `AutoLoadIssues()`: Chuyển sang đọc bằng `IssueStorageService.LoadIssuesFromDocument(_uiApp.ActiveUIDocument.Document)`. Đồng thời giải mã Base64 thành file ảnh tại `%TEMP%` để ListView load được ảnh mượt mà.
- Trong các sự kiện `BtnLoadXml_Click` / `BtnLoadBcf_Click`: Sử dụng `_currentIssues.AddRange(...)` thay vì `= parser...` để tránh bị mất dữ liệu.

## Verification Plan
- Chạy Add-in, tạo 1 Issue, có kèm ảnh.
- Load XML Navisworks, thêm 4 Issues. Tổng = 5.
- Đóng cửa sổ quản lý, mở lại, kiểm tra danh sách = 5 và ảnh hiển thị.
- Đóng Revit, mở lại RVT. Bật addin, kiểm tra danh sách có đúng 5 issues.
