Đã hoàn thành tài liệu research/spec chi tiết tại:

[Spec_Xaml_UX_UI_Consistency_v1.0.md](E:/Antigravity/RevitAddinSolution/docs/specs/Spec_Xaml_UX_UI_Consistency_v1.0.md:1)

Kết quả chính:

- Kiểm kê và đánh giá đủ **24 XAML thuộc 17 module UI**.
- Phân tích nguyên nhân font chồng/cắt, fixed dimensions, DPI, emoji fallback, duplicated styles và button placement.
- Đề xuất kiến trúc shared design system trong `Antigravity.Core`.
- Phân loại bốn window-size profile thay vì ép mọi cửa sổ cùng kích thước.
- Chỉ rõ thay đổi dự kiến cho từng XAML.
- Liệt kê **13 file cần tạo** và các project/guideline/XAML cần sửa.
- Xây dựng acceptance criteria, automated contract tests, visual QA từ 100–200% DPI và bảng rủi ro.
- Giữ `PenOverlayWindow` như ngoại lệ kiến trúc để không phá transparent overlay.
- Xác nhận lựa chọn WPF/.NET Framework 4.8 phù hợp Revit 2024 theo [Autodesk Revit API requirements](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Introduction/Getting_Started/Welcome_to_the_Revit_Platform_API/Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Welcome_to_the_Revit_Platform_API_Development_Requirements_html.html).
- Baseline build: **0 error, 2 warning NuGet không liên quan UI**.

Khuyến nghị kiến trúc là chọn light theme làm canonical, dùng shared ResourceDictionary qua WPF pack URI, component hóa header/footer và bảo vệ `x:Name`, event, binding bằng contract tests. Các nguyên tắc chống clipping dựa trên automatic layout, wrapping và DPI-independent layout chính thức của [Microsoft WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-globalization-and-localization-overview).

Theo hard gate của `/1.spec`, chưa sửa implementation. Nếu phê duyệt spec cùng các mặc định tại mục 13, bước tiếp theo là `/2.plan`.
