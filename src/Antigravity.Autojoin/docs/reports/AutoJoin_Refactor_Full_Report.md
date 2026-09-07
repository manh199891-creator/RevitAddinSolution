# Implementation Plan: AutoJoin Refactor (Final Phase)

## 🎯 Trạng thái hiện tại
- [x] **Phase 1: An toàn & Logging** (Transaction safety, Debug logs)
- [x] **Phase 2: Tương thích & NuGet** (SDK-style, NuGet Revit API)
- [x] **Phase 3: UI/UX Branding** (Vilai Viet Standards - Background #1A1A5E, Red Buttons)
- [x] **Logic Fix**: Bổ sung BoundingBox fallback cho giao cắt Dầm-Sàn trong `AutoJoinService.cs`.

## 🛠 Thống kê chi tiết Code đã sửa:
1. **AutoJoinService.cs**:
   - Thêm `TryParseBic` thông minh hơn (tự động thêm prefix OST_).
   - `ExecuteJoin` báo lỗi chi tiết nếu không tìm thấy phần tử nào.
   - `ProcessJoin` sử dụng thêm `BoundingBoxIntersectsFilter` nếu Solid filter trả về 0 kết quả (tăng độ nhạy khi dầm chỉ chạm mặt sàn).
   - Thêm TODO cho Revit 2025.
2. **MainWindow.xaml**:
   - Refactor toàn bộ theo Vilai Viet Design Guidelines.
   - Header có logo `@Vilai Viet` màu vàng.
3. **Antigravity.Autojoin.csproj**:
   - Chuyển sang dùng NuGet `Autodesk.Revit.SDK`.
   - Thêm ProjectReference tới `Antigravity.Core`.

## 📋 Danh sách Task tiếp theo (Phase 4: Testing & Hardening)

### Task 4.1: Kiểm thử tích hợp (Integration Test)
**Mô tả:** Viết script kiểm tra tự động xem Dầm có thực sự cắt Sàn sau khi chạy lệnh không.
**Acceptance criteria:**
- [ ] Hàm `JoinGeometryUtils.AreElementsJoined` trả về true cho cặp test.
- [ ] Kết quả Join thành công được báo cáo chính xác trên Status Bar.

### Task 4.2: Đồng bộ hóa Monorepo (Beams/Columns)
**Mô tả:** Áp dụng chuẩn NuGet và UI Branding này cho module `DrawBeams` và `DrawColumns`.
**Acceptance criteria:**
- [ ] Xóa bỏ hardcode path trong .csproj của Beams/Columns.
- [ ] Chuyển màu nút hành động của Beams sang Đỏ chuẩn (#CC0000).

### Task 4.3: Triển khai Serilog
**Mô tả:** Thay thế toàn bộ `Debug.WriteLine` bằng hệ thống log Serilog ghi vào file sau khi cấu hình Core hoàn thiện.

## ⚠️ Rủi ro & Giải pháp
| Rủi ro | Tác động | Giải pháp |
|:---:|:---:|:---|
| Lỗi tọa độ | Cao | Luôn dùng CoordinateService từ Antigravity.Core |
| Revit treo khi DMU | Trung | Giữ nguyên actionCache 2s để tránh loop |

---
**Ghi chú cho Codex:** Hãy đọc kỹ file `src/Antigravity.Autojoin/docs/design/AutoJoin_Refactor_Spec.md` và `src/Antigravity.Autojoin/docs/reports/AutoJoin_Refactor_Full_Report.md` trước khi thực hiện các Task trên.
