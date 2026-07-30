# UI/UX Strict v3 — Recovery and Full Synchronization Spec

Date: 2026-07-17
Status: Awaiting approval

## Goal

Loại bỏ toàn bộ lỗi runtime/readability/layout đang thấy trong Revit 2024 và chuyển 24 cửa sổ add-in sang một design system light-theme có kiểm soát tự động, để sai lệch tương tự không thể vượt qua bước build/QA.

## Evidence and root causes

- Ảnh thực tế cho thấy lỗi `StaticResourceExtension` tại các dòng 139, 141 và 152.
- Static resource audit hiện phát hiện 29 tham chiếu key tiềm năng không tồn tại trong 5 window: AutoDim, AutoJoin, Tag Arranger, Wall MEP Clash và ZoneSplit.
- DoorClearance vẫn dùng các style dark (`Foreground="White"`) trên shell nền trắng, làm mất nội dung.
- HoanThien đặt content/action trong các hàng `Auto` trong khi hàng `*` không có nội dung, tạo vùng trắng lớn và làm footer/signature sai vị trí.
- ArchModeling dùng bố cục cột cố định, nên bảng kết quả không giãn để chiếm vùng khả dụng.
- `@manhns` đang là TextBlock chèn trực tiếp và đôi khi nằm cùng Grid cell với nút/action, dẫn tới chồng lấn hoặc bị cắt.
- Contract test v2 mới kiểm tra sự tồn tại theme/handler, chưa kiểm tra resource resolution, grid ownership, contrast pair hoặc responsive layout.

## Scope — IN

1. P0 runtime recovery
   - Sửa toàn bộ missing/forward `StaticResource` và khóa resource order.
   - Mọi normal Window phải load được bốn shared dictionaries; không còn lỗi XAML load khi mở lệnh.

2. Full visual synchronization
   - Chuẩn hóa cả 24 Window theo light theme nền trắng.
   - Chuyển form nghiệp vụ sang landscape responsive; form dài được chia 2 cột hoặc nhóm/tab, không kéo thành một cột dọc quá dài.
   - Duy trì 4 profile duy nhất: Compact, Standard Landscape, Tall Exception và Workbench; Tall phải có lý do/exception khai báo.
   - Header cố định, body co giãn/scroll, footer cố định; không phần nào dùng khoảng trắng giả để đẩy vị trí.
   - DataGrid/ListView/TreeView chiếm toàn bộ vùng `*` hợp lệ.

3. Readability and accessibility
   - Text thường đạt tối thiểu WCAG 2.2 AA 4.5:1; component/focus boundary đạt 3:1.
   - Text không nhỏ hơn 11 DIP; target tương tác tối thiểu 24x24 DIP, button nghiệp vụ tối thiểu cao 32 DIP.
   - Kiểm tra 100%, 125%, 150%, 200% DPI/text scaling; không mất chữ, che nút hoặc mất chức năng.

4. Branding
   - Dùng `BrandHeader` và `BrandSignature` dùng chung, không copy logo/header thủ công.
   - Đúng một `@manhns`, nằm trong footer cell riêng ở góc dưới phải; cấm margin âm và cấm overlay lên action.

5. Strict guideline v3 and enforcement
   - Nâng `VilaiViet_UI_Guidelines_v2.md` thành v3 canonical.
   - Shared keys bắt buộc prefix `Vv`; local key bắt buộc prefix theo module.
   - Cấm hard-coded palette ngoài logo/exception được allowlist.
   - Thêm static-resource graph audit: key phải tồn tại và được khai báo trước nếu dùng `StaticResource`.
   - Thêm grid/layout audit: signature/footer cell riêng, body phải có star row, long body phải có ScrollViewer, cấm negative margin dùng để định vị.
   - Thêm contrast/token audit và kiểm tra foreground trắng trên surface sáng.
   - Contract test phải fail nếu bất kỳ window nào vi phạm.

6. Verification and delivery
   - Build solution, unit tests, strict XAML contract, dependency/security scan.
   - Deploy lại Addins Revit 2024 và checksum toàn bộ DLL.
   - Manual QA matrix trong Revit cho các lệnh đại diện và toàn bộ window load.

## Scope — OUT

- Không thay đổi thuật toán BIM, Revit Transaction hoặc business behavior trừ khi cần để window khởi tạo an toàn.
- Không thiết kế lại ribbon/icon trong đợt này, ngoài việc bảo đảm command mở đúng window.
- Không bổ sung dark mode.
- `PenOverlayWindow` giữ transparency/topmost và được quản lý bằng exception riêng.

## Architecture

- `Antigravity.Core/UI/Themes`: nguồn duy nhất cho token, typography, controls và data controls.
- `Antigravity.Core/UI/Controls`: canonical `BrandHeader`, `BrandSignature`, và shell primitives nếu cần.
- Mỗi Window dùng cấu trúc `Header / Body(*) / Footer`, không nhân bản brand markup.
- Resource lookup dùng shared `Vv*` keys; `DynamicResource` cho theme token có phụ thuộc runtime, `StaticResource` chỉ cho key đã định nghĩa chắc chắn trước thời điểm XAML load.
- `tests/ui/Assert-XamlContracts.ps1` trở thành release gate, kết hợp parse XML, resource graph, semantic layout và handler validation.

## Acceptance criteria

- 0 missing/unresolved StaticResource trong source Window XAML.
- 0 chữ trắng trên nền trắng/surface sáng; 0 text nghiệp vụ dưới 11 DIP.
- 0 signature chồng action, 0 footer bị cắt, 0 negative positioning hack.
- 24/24 normal/exception windows qua strict contract; 131 button vẫn có action hợp lệ.
- DoorClearance đọc được toàn bộ label; HoanThien không còn vùng trắng sai; ArchModeling result grid giãn đúng; các command từng báo dòng 139/141/152 mở không lỗi.
- Build 0 error; tất cả unit test pass; DLL deploy checksum khớp.

## Standards baseline

- W3C WCAG 2.2: contrast 4.5:1, resize/reflow và target-size minimum.
- Microsoft WPF resource lookup: `StaticResource` được resolve khi XAML load và không hỗ trợ forward reference; `DynamicResource` dùng khi lookup cần trì hoãn.
- Autodesk Revit UI guidance: command/ribbon grouping rõ ràng và dialog controls nhất quán với Revit.

## Open question / approval decision

Đề xuất mặc định: tất cả form nghiệp vụ chuyển sang **landscape responsive**; chỉ overlay và dialog thật sự ngắn được exception. Xác nhận spec này để chuyển sang `/2.plan` và tự động triển khai `/3.code`.
