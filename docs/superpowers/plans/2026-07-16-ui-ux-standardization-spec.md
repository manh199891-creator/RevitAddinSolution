# Spec: Chuẩn hóa UX/UI toàn bộ Revit add-in

Ngày: 2026-07-16  
Trạng thái: Chờ duyệt Phase 1

## Goal

Xây dựng lại hệ thống giao diện WPF của toàn bộ add-in theo một design system nền trắng, dễ đọc, không cắt/che chữ ở các mức DPI phổ biến, có nhận diện Vilai Viet nhất quán và chữ ký `@manhns` ở góc dưới bên phải.

## Hiện trạng đã kiểm kê

- 24 file XAML giao diện trong 17 module có UI, gồm cửa sổ chính, dialog phụ, editor và overlay.
- Phần lớn đã chuyển sang nền trắng nhưng còn ngoại lệ không chủ đích: `PasswordWindow` vẫn dùng nền/header tối; `ZoneSplit` vẫn dùng header tối. `PenOverlayWindow` là ngoại lệ chức năng hợp lệ vì phải trong suốt trên viewport Revit.
- Header chưa thực sự đồng nhất: đa số là nền trắng + divider, một số còn nền tối, một số dialog/editor không có brand header đầy đủ.
- Tất cả màn hình đã có `@manhns`, nhưng `PasswordWindow` sai vị trí/kích thước và màu `#9BA3AF` với opacity 0.8 trên nền trắng không đủ rõ để coi là nội dung cần đọc.
- 8 cửa sổ chưa đặt `Segoe UI` ở root; còn font 10 px tại AutoJoin; nhiều control/row thấp hơn 24 px.
- 15 cửa sổ thiếu một hoặc cả hai giới hạn `MinWidth`/`MinHeight`; 4 cửa sổ resizable nhưng không có vùng cuộn; một số cửa sổ dùng kích thước cố định hoặc `SizeToContent` có nguy cơ vượt work area.
- Nhiều màu và style đang được copy trực tiếp trong từng XAML. Tài liệu `VilaiViet_UI_Guidelines.md` hiện còn mâu thuẫn: mô tả header tối trong khi phần lớn giao diện thực tế đã chuyển sang header trắng.

## Research baseline

- WPF dùng device-independent pixels và tự scale theo DPI, nhưng layout vẫn phải dùng Grid/Auto/star sizing và tránh kích thước cứng gây clipping.
- Mục tiêu accessibility áp dụng theo tinh thần WCAG 2.2 AA cho desktop UI: chữ thường tối thiểu 4.5:1; control/đường biên quan trọng 3:1; target tối thiểu 24x24, ưu tiên 32 px; nội dung và chức năng không mất khi phóng to.
- Thiết kế phải hoạt động bằng bàn phím, có focus rõ, tab order hợp lý, trạng thái không chỉ truyền đạt bằng màu.
- UI Revit phải giữ cảm giác native Windows/Revit: Segoe UI, bố cục gọn, hành động chính rõ, không dùng màu trang trí gây nhiễu.

## Design decisions

### Màu sắc

- Window/background: `#FFFFFF`.
- Card/surface: `#F5F6F8`; input: `#FFFFFF`; border/divider: `#D1D5DB`.
- Header: `#FFFFFF`, divider dưới `#D1D5DB`; logo navy/red; tên module `#111827`; subtitle `#4B5563`.
- Primary action: `#007ACC` + chữ trắng; destructive: `#D8262C` + chữ trắng.
- Text primary: `#111827`; text secondary: `#4B5563`; muted/decorative only: `#6B7280`.
- Không dùng `#9BA3AF` cho nội dung cần đọc trên nền trắng.

### Typography và control sizing

- Root font: Segoe UI 12 px; tiêu đề header 16 px SemiBold; section 14 px SemiBold; subtitle/help 11 px; không dùng text nội dung dưới 11 px.
- Button/input/select tối thiểu cao 32 px; icon-only target tối thiểu 28x28 và có tooltip/accessible name.
- Khoảng cách theo thang 4/8/12/16/24; content margin chuẩn 16 px.

### Window sizing

Không ép mọi cửa sổ cùng kích thước; dùng bốn tier:

- Compact dialog: khoảng 480x360, min 420x280.
- Standard form: khoảng 720x560, min 560x420.
- Wide/data dialog: khoảng 980x640, min 760x480.
- Editor/workspace: khoảng 1050x680, min 800x520.

Mọi cửa sổ thường phải `CanResize`, tự giới hạn trong work area của monitor chứa Revit, và có ScrollViewer ở vùng nội dung khi min size không đủ. Dialog rất ngắn có thể `NoResize` nếu đã chứng minh không clipping ở 200% text/DPI. Overlay/editor canvas là ngoại lệ có tài liệu rõ.

### Branding

- Một `BrandHeader` dùng chung, nền trắng, logo vector Vilai Viet, tên module và subtitle; không copy markup header giữa các module.
- Một `BrandSignature` dùng chung: `@manhns`, góc dưới phải, 12 px SemiBold, màu `#6B7280`, opacity 1, không chặn click.
- Overlay đặt chữ ký tại góc viewport nhưng vẫn không nhận hit test.

## Scope

### IN

- Toàn bộ 24 XAML hiện có trong `src`, kể cả dialog phụ của Issue Manager và Check Floor Elevation.
- Shared ResourceDictionary/tokens/styles, shared header/signature và hành vi sizing trong `Antigravity.Core`.
- Chuẩn hóa nền/header/font/control size/focus/spacing/resizing/scrolling.
- Sửa tài liệu design system và thêm kiểm tra tự động chống regression cho XAML.
- Build solution và kiểm tra tĩnh toàn bộ UI; smoke test những cửa sổ có thể khởi tạo không cần Revit API.

### OUT

- Thay đổi nghiệp vụ, thuật toán Revit, transaction hoặc dữ liệu người dùng.
- Thiết kế lại ribbon/icon trên Revit Add-Ins tab ngoài các lỗi accessibility rõ ràng.
- Theme tối; đợt này chốt light theme nền trắng.
- Thay đổi overlay canvas làm ảnh hưởng thao tác vẽ/markup.

## Architecture

1. Thêm `Antigravity.Core/UI/DesignSystem.xaml` chứa color brushes, typography, Button/TextBox/ComboBox/CheckBox/DataGrid/GroupBox styles và focus states.
2. Thêm `BrandHeader` và `BrandSignature` dạng WPF controls với dependency properties, bảo đảm một nguồn chuẩn cho branding.
3. Thêm attached behavior `WindowSizingAssist` để clamp kích thước theo monitor work area mà không chiếm `Loaded` handler hiện có.
4. Merge design system bằng pack URI ở từng Window, sau đó thay màu/style hardcode bằng resource keys; chỉ giữ màu semantic riêng cho trạng thái warning/error/markup.
5. Chuyển layout vùng nội dung sang Grid Auto/star + ScrollViewer; gán min size theo tier; sửa wrap/trimming/tooltip tại các trường dữ liệu dài.
6. Thêm script/test kiểm tra: nền trắng, root font, header/signature, min size, font tối thiểu, target tối thiểu, màu cấm và parse/build XAML.

## Acceptance criteria

- 100% cửa sổ thường có nền trắng, header trắng chuẩn và text palette chuẩn; ngoại lệ overlay được whitelist.
- 100% cửa sổ có `@manhns` đúng vị trí, không che control và không bắt mouse.
- Không còn font nội dung 10 px; không còn interactive target dưới 24 px nếu không có ngoại lệ có lý do.
- Không có nội dung/nhãn/nút bị cắt ở kích thước tối thiểu; nội dung dài wrap hoặc có tooltip/scroll phù hợp.
- Các cửa sổ mở trong work area ở DPI 100%, 125%, 150%, 200%; không vượt màn hình và không mất chức năng khi resize nhỏ.
- Keyboard focus nhìn thấy rõ; thao tác chính có access key/tab order hợp lý ở các form quan trọng.
- Solution build thành công trong môi trường có Revit references; kiểm tra UI tĩnh chạy xanh và không đổi logic nghiệp vụ.

## Open questions / assumptions

- Giả định header mục tiêu là nền trắng (theo yêu cầu và trạng thái đa số XAML hiện tại), không phải header tối trong guideline cũ.
- Giả định `@manhns` cần rõ nhưng không lấn át nội dung; vì vậy chuẩn mới là 12 px SemiBold thay vì 16 px Bold hiện tại.
- Không còn câu hỏi chặn kỹ thuật. Sau khi spec được duyệt, Phase 2 sẽ tạo plan theo từng wave và Phase 3 tự động triển khai, không hỏi lại trừ khi gặp thay đổi nghiệp vụ hoặc lỗi build ngoài phạm vi UI.

## Sources

- Microsoft Learn, WPF Layout: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/layout
- Microsoft Learn, Per-monitor DPI-aware WPF: https://learn.microsoft.com/en-us/windows/win32/hidpi/declaring-managed-apps-dpi-aware
- W3C WCAG 2.2: https://www.w3.org/TR/WCAG22/
- W3C Understanding Contrast Minimum: https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum
- W3C Understanding Resize Text: https://www.w3.org/WAI/WCAG22/Understanding/resize-text
- W3C Understanding Target Size Minimum: https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- Autodesk Revit API UI Guidelines: https://help.autodesk.com/cloudhelp/2025/ESP/Revit-API/files/Revit_API_Developers_Guide/Appendices/API_User_Interface_Guidelines/Revit_API_Revit_API_Developers_Guide_Appendices_API_User_Interface_Guidelines_Ribbon_Guidelines_html.html
