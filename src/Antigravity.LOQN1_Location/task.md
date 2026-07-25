# Danh sách Tác vụ (Task List) - VILAIVIET-LOQN1-Location

- [x] Khởi tạo tài liệu bối cảnh dự án `PROJECT_CONTEXT.md`
- [x] Xây dựng các Helper Class và Command chính:
  - [x] `RevitUnitUtils.cs`: Chuyển đổi Feet -> Meters và Format Elevation String (+0,000)
  - [x] `ElementPositionHelper.cs`: Thuật toán xác định Center Point, BoundingBoxXYZ, Level tham chiếu và Offset
  - [x] `GridLocationHelper.cs`: Thuật toán phân loại Grid X, Y và tìm dải Grid phủ bởi BoundingBox
  - [x] `Command.cs`: Triển khai `IExternalCommand`, lọc đối tượng, mở Transaction và cập nhật Shared Parameter "LOQN1_Location"
  - [x] `App.cs`: Triển khai `IExternalApplication` tạo Ribbon Tab "VILAIVIET Tools" & PushButton "VILAIVIET-LOQN1-Location" với Logo VILAIVIET.
  - [x] `VILAIVIET-LOQN1-Location.addin`: File Manifest điền sẵn đường dẫn tuyệt đối cho Add-In Manager
  - [x] `Copy_Addin_To_Revit2024.bat`: Script tự động cài Add-in vào Revit 2024
- [x] Tự kiểm định (Self-QA) cho mã nguồn thành công
