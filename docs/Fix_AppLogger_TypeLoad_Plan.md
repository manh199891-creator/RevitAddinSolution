# Fix Plan: AppLogger TypeLoadException

## 🔍 Nguyên nhân gốc (Root Cause Analysis)

Lỗi: `Could not load type 'Antigravity.Core.Services.AppLogger' from assembly 'Antigravity.Core, Version=0.0.0.0'`

### Phân tích so sánh DLL:

| Vị trí | File | Kích thước | Thời gian | Trạng thái |
|:-------|:-----|:-----------|:----------|:-----------|
| Build output (`src\Core\bin\Debug\`) | `Antigravity.Core.dll` | **13,312 bytes** | 3:26 PM | ✅ MỚI (có AppLogger) |
| Deployed (`Addins\2024\Antigravity\`) | `Antigravity.Core.dll` | **11,264 bytes** | 11:53 AM | ❌ CŨ (không có AppLogger) |
| Build output | `Serilog.dll` | 136,704 bytes | Có | ✅ Đã build |
| Deployed | `Serilog.dll` | — | — | ❌ THIẾU HOÀN TOÀN |
| Build output | `Serilog.Sinks.File.dll` | 33,280 bytes | Có | ✅ Đã build |
| Deployed | `Serilog.Sinks.File.dll` | — | — | ❌ THIẾU HOÀN TOÀN |

### Kết luận: 3 vấn đề cần sửa

1. **DLL cũ**: `Antigravity.Core.dll` trong thư mục Addins là bản cũ (11:53 AM), chưa có class `AppLogger`.
2. **Thiếu dependency**: `Serilog.dll` và `Serilog.Sinks.File.dll` chưa bao giờ được copy vào thư mục Addins.
3. **Script cũ**: `DeployToRevit.ps1` trỏ sai đường dẫn (`E:\ANTIGRAVITY\Antigravity\Revit\RevitAddinSolution`) và chỉ copy 7 DLL module chính, KHÔNG copy dependency NuGet.

---

## 📋 Kế hoạch sửa (Tasks)

### Task 1: Cập nhật `DeployToRevit.ps1` [Size: S - 1 file]

**Mô tả:** Sửa script deployment để trỏ đúng đường dẫn dự án và bổ sung copy các DLL dependency (Serilog).

**Acceptance criteria:**
- [ ] `$solutionRoot` trỏ đúng tới `E:\Antigravity\RevitAddinSolution\RevitAddinSolution`
- [ ] `$revitVersion` = `"2024"` (máy user đang dùng Revit 2024)
- [ ] Script copy thêm `Serilog.dll` và `Serilog.Sinks.File.dll` từ `src\Antigravity.Core\bin\Debug\`

**Files touched:**
- `DeployToRevit.ps1`

---

### Task 2: Build toàn bộ Solution [Size: XS]

**Mô tả:** Build lại tất cả các module để đảm bảo DLL mới nhất.

**Verification:**
- [ ] `dotnet build` cho Core, AutoJoin, DrawBeams, DrawColumns, DrawFloors, Main đều 0 Error.

**Dependencies:** None (có thể chạy ngay)

---

### Task 3: Tắt Revit + Chạy Deploy [Size: XS]

**Mô tả:** Tắt Revit để giải phóng file lock, sau đó chạy script deploy.

**Acceptance criteria:**
- [ ] `Antigravity.Core.dll` mới (13,312 bytes) nằm trong `Addins\2024\Antigravity\`
- [ ] `Serilog.dll` (136,704 bytes) nằm trong `Addins\2024\Antigravity\`
- [ ] `Serilog.Sinks.File.dll` (33,280 bytes) nằm trong `Addins\2024\Antigravity\`

**Dependencies:** Task 1, Task 2

---

### Task 4: Khởi động Revit + Verify [Size: XS]

**Mô tả:** Mở Revit, chạy AutoJoin, xác nhận không còn lỗi TypeLoadException.

**Acceptance criteria:**
- [ ] Nhấn JOIN GEOMETRY → Không hiện lỗi `Could not load type 'AppLogger'`
- [ ] Status bar hiển thị kết quả join (VD: "Mới join: 5 — Đã có: 2 — Bỏ qua: 0")
- [ ] File log xuất hiện tại `%APPDATA%\Antigravity\Logs\antigravity-20260514.log`

**Dependencies:** Task 3

---

## ⚠️ Rủi ro & Giải pháp

| Rủi ro | Tác động | Giải pháp |
|:-------|:--------:|:----------|
| Revit lock DLL | Cao | Phải TẮT Revit trước khi deploy |
| NuGet chưa restore | Trung | Chạy `dotnet restore` trước build |
| Serilog version mismatch | Thấp | Đã pin version 2.12.0 trong .csproj |

## ❓ Câu hỏi cần User xác nhận

1. Bạn có đang dùng **Revit 2024** phải không? (Script sẽ deploy vào thư mục 2024)
2. Bạn có sẵn sàng **TẮT Revit** để tôi chạy deploy không?
