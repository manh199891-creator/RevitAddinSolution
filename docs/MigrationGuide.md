# Hướng dẫn Gom Dự án Revit Add-in vào RevitAddinSolution

Tài liệu này hướng dẫn cách di chuyển các công cụ Revit Add-in đơn lẻ (Vẽ dầm, Vẽ sàn, Vẽ vách,...) vào cấu trúc giải pháp modular chuyên nghiệp `RevitAddinSolution`.

## 1. Cấu trúc Mục tiêu
Mỗi công cụ cũ sẽ được chuyển thành một Project con trong thư mục `src/` với định danh `Antigravity.[TênModule]`.

Ví dụ:
- `Vẽ dầm` -> `Antigravity.DrawBeams`
- `Vẽ sàn` -> `Antigravity.DrawFloors`
- `Vẽ vách` -> `Antigravity.DrawWalls`

## 2. Các Bước Thực Hiện (Ví dụ cho "Vẽ dầm")

### Bước 1: Tạo Project mới
1. Tạo thư mục `src/Antigravity.DrawBeams`.
2. Tạo file `.csproj` mới (có thể copy từ `Antigravity.DrawColumns.csproj` và sửa tên).

### Bước 2: Chép mã nguồn
1. Chép các thư mục `Models`, `Services`, `UI` và các file `.cs` (ngoại trừ `App.cs` vì chúng ta dùng Ribbon chung) từ dự án cũ vào thư mục mới.

### Bước 3: Cập nhật Namespace
1. Đổi toàn bộ namespace từ cũ (ví dụ: `CreateBeamFromCAD`) sang `Antigravity.DrawBeams`.
2. Cập nhật các lệnh `using` để trỏ đúng vào `Antigravity.Core` nếu cần.

### Bước 4: Tích hợp vào Ribbon (Antigravity.Main)
1. Mở dự án `Antigravity.Main`.
2. Thêm reference đến project `Antigravity.DrawBeams`.
3. Trong code tạo Ribbon, thêm nút bấm (PushButton) mới trỏ đến Command của module Dầm.

### Bước 5: Kiểm tra và Build
1. Chạy lệnh Build toàn bộ solution.
2. Đảm bảo các file `.addin` chỉ còn 1 file duy nhất cho `Antigravity.Main`.
