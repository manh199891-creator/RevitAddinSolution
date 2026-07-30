# SETUP_LOG.md (Phase 4A)

## Kết quả Research Spike (Task A1 + A2)

### 1. Test Skeleton (A1)
- Tạo thành công project `Antigravity.TagArranger.RevitTests.csproj` (Target `net48`, `x64`).
- Tạo thành công `SmokeTest.cs` sử dụng `ricaun.RevitTest`.
- Lệnh `dotnet build` chạy thành công không có lỗi.

### 2. Runtime Execution (A2)
- **Lệnh thực thi:** `dotnet test ... --arch x64`
- **Kết quả:** `FAIL` (INFRA_FAIL)
- **Chi tiết lỗi:**
  ```text
  System.IO.FileNotFoundException: Could not load file or assembly 'RevitAPIUI.dll'
  Installed Revit with version 2024 or greater not found.
  ```

### 3. Phân tích Root Cause (INFRA_FAIL)
- `ricaun.RevitTest.TestAdapter` tìm đường dẫn Revit.exe bằng cách đọc Registry: `HKLM\SOFTWARE\Autodesk\Revit\Autodesk Revit 2024` -> `InstallationLocation`.
- Tuy nhiên, trên máy hiện tại, key `Autodesk Revit 2024` tồn tại nhưng **không có property `InstallationLocation`**.
- Thay vào đó, đường dẫn cài đặt lại nằm ở `HKLM\SOFTWARE\Autodesk\Revit\2024\REVIT-05` -> `InstallLocation`.
- Agent đã thử tạo Registry key ở `HKCU` (thành công nhưng framework không đọc HKCU) và thử ghi vào `HKLM` (thất bại do `PermissionDenied` - cần quyền Administrator).
- Do framework không tìm thấy thư mục cài đặt Revit, nó không thể load thư viện `RevitAPIUI.dll` (cần cho test discovery của NUnit) và cũng không thể khởi động Revit để chạy test.

### 4. Đề xuất Hướng giải quyết
Để vượt qua blocker này, cần **1 trong 3** giải pháp từ phía User (do có quyền Admin):
1. **Sửa Registry bằng quyền Admin:** Chạy lệnh PowerShell (Run as Administrator):
   ```powershell
   Set-ItemProperty -Path "HKLM:\Software\Autodesk\Revit\Autodesk Revit 2024" -Name "InstallationLocation" -Value "C:\Program Files\Autodesk\Revit 2024\"
   ```
2. **Sử dụng CI Runner chuẩn:** Bỏ qua lỗi trên máy dev hiện tại, tiếp tục code Phase 4C/4D (Harness Integration) và chạy test trên máy tính/CI có cài đặt Revit chuẩn.
3. **Liên hệ tác giả `ricaun.RevitTest`:** Mở issue yêu cầu support tìm đường dẫn từ key `REVIT-05` hoặc cho phép cấu hình đường dẫn thủ công qua file `.runsettings`.
