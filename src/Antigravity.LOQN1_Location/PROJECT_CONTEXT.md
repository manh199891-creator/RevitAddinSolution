# PROJECT CONTEXT: Revit 2024 Add-in - LOQN1 Location Element Generator

## 1. Mục tiêu Dự án
Nâng cấp mã nguồn External Command (IExternalCommand) cho Revit 2024 Add-in (.NET 7 / C#) để tự động tính toán vị trí của đa dạng các loại đối tượng (Dầm, Cột, Sàn, Vách, MEP, Cửa, Mái...) và ghi kết quả vào Shared Parameter "LOQN1_Location" theo định dạng chuẩn:
`[LevelName], ([GridX_Start]-[GridX_End]), ([GridY_Start]-[GridY_End]), [Elevation_Offset]`

Ví dụ mẫu: `3FL, (3X2-3X10), (3Y1-3Y10), +0,000`

## 2. Kiến trúc & Công nghệ
- **Nền tảng**: Revit API 2024 (.NET 7, C# 11)
- **Cấu trúc mã nguồn**:
  - `Command.cs`: Lớp chính triển khai `IExternalCommand`, quản lý selection / collection đối tượng và Transaction.
  - `ElementPositionHelper.cs`: Thuật toán trích xuất vị trí Center Point, BoundingBoxXYZ, Level tham chiếu và Elevation Offset.
  - `GridLocationHelper.cs`: Thuật toán phân loại, chiếu và tra cứu dải Grid X, Grid Y cho đối tượng.
  - `RevitUnitUtils.cs`: Chuyển đổi đơn vị Feet sang Meters dùng API `UnitTypeId.Meters` của Revit 2024.

## 3. Quy chuẩn Định dạng Parameter
- Dấu phân cách thập phân Elevation Offset: Dấu phẩy `,` với 3 chữ số thập phân (Ví dụ: `+0,000`, `+1,200`, `-0,500`).
- Grid Format: `(GridX_Start-GridX_End)`, `(GridY_Start-GridY_End)`. Nếu 1 Grid duy nhất hoặc trùng nhau thì hiển thị tên Grid đó hoặc khoảng lân cận.
