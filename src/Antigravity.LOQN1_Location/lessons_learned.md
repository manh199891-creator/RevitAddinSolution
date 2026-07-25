# Bài học Kinh nghiệm (Lessons Learned) - Revit 2024 API

1. **⚠️ QUAN TRỌNG NHẤT - Target Framework Revit 2024**:
   - **Revit 2024 chạy trên `.NET Framework 4.8`**, KHÔNG PHẢI `.NET 7` hay `.NET Core`.
   - File `.csproj` PHẢI dùng `<TargetFramework>net48</TargetFramework>`.
   - Chỉ từ **Revit 2025** trở đi mới chuyển sang `.NET 8 (Core)`.
   - Build bằng `net7.0-windows` sẽ gây lỗi `File Load Is Invalid` trong Add-In Manager.

2. **Revit API Units**:
   - Từ Revit 2021+, Revit chuyển đổi hệ thống Unit API từ DisplayUnitType sang `ForgeTypeId` (`UnitTypeId`).
   - Đơn vị chiều dài tiêu chuẩn nội bộ của Revit luôn là **Feet**. Để đổi sang Mét (Meters): dùng `UnitUtils.ConvertFromInternalUnits(valFeet, UnitTypeId.Meters)`.

3. **Xử lý In-Place Families & Null Level Parameters**:
   - Nhiều Model-In-Place hoặc DirectShape không có Parameter Level tiêu chuẩn.
   - Thuật toán dự phòng (Fallback): Lấy Z_center từ BoundingBoxXYZ, sau đó tìm Level có Elevation cao nhất mà vẫn `<= Z`.

4. **Tra cứu Grid X & Grid Y**:
   - Kiểm tra vector hướng của Grid để phân loại phương X và phương Y.
   - Đối với Grid cong/chéo, lấy BoundingBox Mid Point để tìm khoảng cách gần nhất.

5. **An toàn Parameter IsReadOnly & Transaction**:
   - Kiểm tra `param != null && !param.IsReadOnly` trước khi gọi `param.Set(...)`.

6. **Add-In Manager chỉ hỗ trợ Type="Command"**:
   - Không nạp được file `.addin` có `Type="Application"` qua Add-In Manager.
   - Để load Ribbon UI (IExternalApplication), phải copy `.addin` vào thư mục `%APPDATA%\Autodesk\Revit\Addins\2024\` và khởi động lại Revit.

7. **File DLL bị khóa khi Revit đang chạy**:
   - Khi Revit đã load DLL, file sẽ bị khóa (locked by process).
   - Giải pháp: Đổi `<AssemblyName>` sang tên mới trong `.csproj` để build ra file DLL khác không bị khóa.
