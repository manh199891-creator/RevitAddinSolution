# Vilai Viet Revit Add-in UI Design System v2

Ngày hiệu lực: 2026-07-17  
Theme canonical: Light, nền trắng

## Palette

- Window/header/input: `#FFFFFF`.
- Surface phụ: `#F5F6F8`; hover/selection nhẹ: `#E5F1FB`.
- Border/divider: `#D1D5DB`.
- Text chính: `#111827`; text phụ: `#4B5563`; muted/signature: `#6B7280`.
- Primary: `#007ACC`; danger: `#D8262C`; chữ trên accent: `#FFFFFF`.
- Brand: navy `#1B3679`, red `#D8262C`.
- Không dùng `#9BA3AF` cho nội dung cần đọc trên nền trắng.

## Typography

- Root: Segoe UI 12 DIP.
- Header title: 16 DIP SemiBold; section title: 14 DIP SemiBold.
- Supporting text: 11 DIP, có `TextWrapping="Wrap"` khi là câu mô tả.
- Không dùng text nội dung dưới 11 DIP.

## Control sizing

- Button có chữ: min-height 32, min-width 88, padding ngang 12.
- Icon-only button: 32x32, bắt buộc tooltip và automation name.
- TextBox/ComboBox: min-height 30.
- CheckBox/RadioButton: hit area tối thiểu 24 DIP.
- DataGrid row: 28; column header: 32.

## Window profiles

- Compact: 400–520 x 280–420; dialog ngắn.
- Standard: 620–760 x 480–620; form nhập liệu.
- Tall: 480–600 x 640–720; body phải scroll.
- Workbench: 900–1100 x 600–720; DataGrid dùng star row và scroll nội tại.
- Window `CanResize` bắt buộc có `MinWidth` và `MinHeight`.
- Root bật `UseLayoutRounding` và `SnapsToDevicePixels`.
- Header/footer nằm ngoài vùng scroll nếu form dài.

## Branding

- Header nền trắng, divider dưới `#D1D5DB`, logo vector navy/red.
- Module title dùng `#111827`; subtitle dùng `#4B5563` hoặc `#6B7280`.
- Mỗi window có đúng một `@manhns`, góc dưới phải, 12 DIP SemiBold,
  `#6B7280`, opacity 1, `IsHitTestVisible="False"`.
- `PenOverlayWindow` là ngoại lệ: giữ transparency/topmost và safe-area signature.

## Shared resources

Các window thường merge đúng một lần bốn dictionary sau từ `Antigravity.Core`:

- `UI/Themes/DesignTokens.xaml`
- `UI/Themes/Typography.xaml`
- `UI/Themes/Controls.xaml`
- `UI/Themes/DataControls.xaml`

Key shared dùng prefix `Vv` để không đè resource nghiệp vụ cục bộ. Header/signature
tái sử dụng được cung cấp bởi `Antigravity.Core.UI.Controls.BrandHeader` và
`BrandSignature`.

## Interaction contract

- Không đổi `x:Name`, event handler, binding hoặc ExternalEvent chỉ để sửa layout.
- Mỗi button phải có `Click`, `Command`, `IsDefault` hoặc `IsCancel` hợp lệ.
- Custom control phải có keyboard focus nhìn thấy được.
- Trạng thái lỗi không chỉ truyền đạt bằng màu.
- Chạy `powershell -ExecutionPolicy Bypass -File tests/ui/Assert-XamlContracts.ps1`
  và full build trước khi nghiệm thu.
