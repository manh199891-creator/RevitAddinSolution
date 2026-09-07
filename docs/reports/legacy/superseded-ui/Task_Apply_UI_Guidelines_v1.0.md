# TASK: Apply Vilai Viet UI Guidelines (v1.0)

## Objective
The Tech Lead (Antigravity 2.0) has approved and finalized the new **Vilai Viet UI Design System**. Bạn (IDE Assistant) được giao nhiệm vụ áp dụng các quy chuẩn giao diện (UI Guidelines) này vào các file XAML trong dự án.

## 1. UI Guidelines Reference
Tài liệu UI Guidelines chính thức (và mã nguồn XAML chuẩn) nằm tại: 
`E:\Antigravity\RevitAddinSolution\VilaiViet_UI_Guidelines.md`

## 2. Các yêu cầu cốt lõi cần thực hiện (Key Actions)
- **Color Palette & Theme**: Áp dụng giao diện tối Dark Slate Charcoal (`#202226` cho nền Window, `#1E1F22` cho nền Input).
- **Header Component**: Thay thế toàn bộ Header cũ bằng chuẩn mới (Logo Vector chữ V + Tên Add-in). **TUYỆT ĐỐI KHÔNG** dùng text "VILAI VIET" cạnh logo để tránh trùng lặp.
- **ComboBox & TextBoxes (Sửa lỗi chữ trắng/nền trắng)**: Đảm bảo áp dụng `Style` cho `ComboBoxItem` để ép nền danh sách thả xuống thành màu tối (`#1E1F22`), giúp chữ màu trắng (`#F5F6F8`) có thể đọc được (Tránh lỗi Popup mặc định của Windows).
- **Window Title**: Xóa từ khóa `Antigravity` khỏi thuộc tính `Title` của các Window.
- **Watermark**: Thêm chữ ký tác giả `@manhns` ở góc phải dưới cùng với thông số to, rõ ràng: `FontSize="16"`, `FontWeight="Bold"`, `Opacity="0.8"`.

## 3. Implementation Plan cho IDE
1. Quét dự án để tìm các file `.xaml` chứa giao diện người dùng.
2. Cập nhật `<Window.Resources>` để khai báo các Style chuẩn cho `PrimaryBtn`, `ModernTextBox`, và `ComboBoxItem` theo đúng Guideline.
3. Cập nhật `Background` của Window, cấu trúc `Grid` và chèn mã XAML của Header Component chuẩn.
4. Kiểm tra lại code để đảm bảo các ComboBox không bị lỗi trùng màu.

## Status
- **Approved by User**: Yes.
- **Action**: Mời bạn (IDE Agent) thực hiện plan này thông qua lệnh `/code` workflow.
