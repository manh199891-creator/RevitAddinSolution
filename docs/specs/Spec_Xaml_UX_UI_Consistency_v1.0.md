# Spec: Đồng bộ UX/UI và chống chồng/cắt chữ cho toàn bộ XAML trong `src`

**Phiên bản:** 1.0  
**Ngày khảo sát:** 2026-07-16  
**Vai trò:** Chief Architect  
**Trạng thái:** Draft — chờ phê duyệt trước khi chuyển sang Phase 2 (`/2.plan`)  
**Phạm vi khảo sát:** 24 file XAML, 17 module UI dưới `src`

## 1. Executive summary

### Goal

Chuẩn hóa vị trí button, kích thước cửa sổ/khung nội dung và hành vi sử dụng trên toàn bộ WPF UI của Revit add-in, đồng thời loại bỏ tình trạng chữ đè nhau, bị cắt hoặc không hiển thị hết ở các mức DPI và kích thước cửa sổ được hỗ trợ mà không làm thay đổi nghiệp vụ hiện có.

### Kết luận kiến trúc

Không nên tiếp tục copy-paste style vào từng `Window.Resources`. Source hiện có ít nhất 24 bản triển khai UI cục bộ, cùng tên style nhưng khác template, kích thước và trạng thái tương tác. Cách tiếp cận đề xuất là:

1. Dùng `Antigravity.Core` làm nơi sở hữu design tokens, shared control styles và hai component shell dùng chung (`BrandHeader`, `DialogFooter`).
2. Mỗi window chỉ giữ resource đặc thù nghiệp vụ (DataGrid, canvas markup, overlay, converter), còn palette/typography/button/input lấy từ merged dictionaries trong Core.
3. Chuẩn hóa dialog thành bốn profile kích thước thay vì ép mọi cửa sổ về cùng một `Width`/`Height`.
4. Dùng layout co giãn theo `Grid`, `Auto`, `*`, `MinWidth/MinHeight`, `ScrollViewer`, `TextWrapping` và `TextTrimming` có chủ đích; không chữa clipping bằng cách tăng cứng toàn bộ kích thước.
5. Bảo toàn toàn bộ `x:Name`, event handler, binding và luồng Revit `ExternalEvent`; thay đổi UI không được đồng nghĩa với refactor nghiệp vụ.
6. Thêm kiểm thử contract tĩnh cho XAML và ma trận visual QA trong Revit ở 100–200% DPI.

### Phát hiện quan trọng

- 24/24 XAML parse XML thành công; solution hiện build thành công với **0 error, 2 warning**. Hai warning `NU1701` thuộc `HatchPatterns`, không do UI.
- 23 cửa sổ/dialog thông thường và 1 overlay trong suốt đặc thù (`PenOverlayWindow`). Overlay phải là ngoại lệ kiến trúc, không áp shell/header thông thường.
- 20/24 file có brand header; 4 file không có: `MarkupEditorWindow`, `FolderNameDialog`, `ExportIssueSelectionDialog`, `PenOverlayWindow`. Ba dialog đầu cần shell rút gọn; overlay không cần brand header.
- 24/24 file có watermark `@manhns`; vị trí/margin hiện chưa đồng nhất.
- Chỉ 29/270 `TextBlock` đang khai báo `TextWrapping="Wrap"`; không có `TextBlock` nào khai báo `TextTrimming`.
- Có 7 mức `FontSize` đang dùng (10, 11, 12, 13, 14, 15, 16), nhiều khai báo rải trực tiếp trong XAML.
- Button height thực tế dao động từ 24 đến 46 DIP; nhiều button không có `MinWidth`, dẫn đến action bar đổi kích thước theo label.
- Window rộng từ 340 đến 1050 DIP, cao từ 160 đến 750 DIP. Nhiều cửa sổ `CanResize` không khai báo `MinWidth/MinHeight`.
- Theme thực tế chủ yếu là light (`#FFFFFF`, `#F5F6F8`), nhưng tài liệu cũ `Task_Apply_UI_Guidelines_v1.0.md` lại yêu cầu dark theme. Đây là mâu thuẫn governance cần giải quyết trước khi code.
- Icon đang trộn Unicode symbol và emoji (`▶`, `✖`, `🎯`, `📁`, `👁`, `🖊`...). Emoji phụ thuộc font fallback và có thể thay đổi width/color giữa Windows, là một nguyên nhân tiềm ẩn làm label button bị ép hoặc lệch.

## 2. Scope

### In scope

- Tất cả file `.xaml` dưới `src`, gồm `Window`, dialog, editor và overlay.
- Vị trí và thứ tự action button; phân cấp primary/secondary/destructive/utility.
- Kích thước mặc định, min/max, resize behavior và scrolling.
- Typography, wrapping, trimming, font fallback và khoảng cách.
- Shared resources, header/footer shell, trạng thái hover/pressed/disabled/focus.
- Keyboard navigation, default/cancel action và automation metadata cơ bản.
- Giữ nguyên chức năng hiện có thông qua event/binding contract.
- Automated XAML contract tests và visual QA checklist.

### Out of scope

- Thay đổi thuật toán Revit, model/service/handler hoặc kết quả nghiệp vụ.
- Viết lại toàn bộ code-behind sang MVVM.
- Chuyển WPF sang WinUI, Avalonia hoặc framework UI khác.
- Đổi toàn solution từ .NET Framework 4.8 sang .NET 8/10.
- Thiết kế lại ribbon Revit, icon ribbon hoặc dockable pane mới.
- Dark mode runtime hoàn chỉnh; v1 chỉ chọn một theme chuẩn duy nhất.
- Localization đầy đủ qua satellite assembly; v1 chỉ làm UI sẵn sàng cho text Việt/Anh dài hơn.
- Thay đổi branding, nội dung watermark hoặc tên sản phẩm nếu chưa có quyết định riêng.

## 3. Hiện trạng công nghệ

### 3.1 Stack và compatibility

| Hạng mục | Hiện trạng | Đánh giá |
|---|---|---|
| UI framework | WPF/XAML | Phù hợp Revit desktop và không cần migration framework |
| Runtime chính | .NET Framework 4.8 (`net48`) | Đúng với Revit 2024 |
| Revit target | Mặc định 2024 trong `Directory.Build.props` | Cần test trực tiếp trong process Revit vì DPI awareness thuộc host process |
| Kiến trúc UI | Code-behind + binding cục bộ; một số modeless/external-event workflow | Không refactor nghiệp vụ trong thay đổi này |
| Shared UI | Style copy trong từng `Window.Resources` | Nguồn drift chính |
| Theme | Phần lớn light; vài resource/label còn tên `Dark*` và màu hard-code | Design system chưa có single source of truth |
| Build baseline | Thành công, 0 error / 2 warning | Phải giữ baseline sau từng nhóm thay đổi |

Autodesk xác nhận Revit 2024 API yêu cầu .NET Framework 4.8, nên giữ WPF/net48 là lựa chọn ít rủi ro nhất cho phạm vi này. WPF dùng device-independent units và tự scale theo DPI; tuy nhiên layout vẫn có thể mờ/lệch ở pixel lẻ, vì vậy nên bật `UseLayoutRounding` tại root. Microsoft cũng khuyến nghị dùng automatic/relative layout, tránh fixed size, thêm khoảng trống và bật wrapping để tránh clipping.

### 3.2 Inventory XAML

| Nhóm | Số file | Đặc trưng |
|---|---:|---|
| CAD/Model creation | 8 | Form nhập liệu, ComboBox, button action cuối cửa sổ |
| QA/Clash/Dimension | 7 | DataGrid/ListView, filter bar, nhiều utility actions |
| Issue Manager | 5 | Workflow nhiều dialog, ảnh/canvas, toolbar động |
| Core/Auth | 1 | Dialog nhỏ, no-resize |
| Tag/Finish | 2 | Form dài, mật độ control cao |
| Overlay | 1 | Transparent topmost canvas, ngoại lệ shell |

## 4. Root-cause analysis

### 4.1 Style bị nhân bản và drift

`PrimaryBtn`, `SecondaryBtn`, `ModernTextBox`, `ComboBoxItem` được định nghĩa lặp lại ở nhiều file. Cùng một semantic name nhưng khác `Height`, `Padding`, `FontSize`, template và trigger. Một số custom `ControlTemplate` thay hẳn template mặc định nhưng không tái tạo đầy đủ `IsMouseOver`, `IsPressed`, `IsEnabled` và keyboard focus. Hậu quả là cùng một tính năng nhưng affordance và accessibility khác nhau giữa module.

### 4.2 Fixed dimensions không có containment strategy

Các window đang dùng kích thước mặc định cứng, nhưng nhiều cửa sổ `CanResize` không có minimum dimensions. Khi user thu nhỏ hoặc Windows scale 125/150/175/200%, content có thể bị ép. Ngược lại, các window cao 680–750 DIP không có chiến lược giới hạn theo `SystemParameters.WorkArea`, dễ vượt màn hình laptop hoặc màn hình có taskbar/display scaling lớn.

### 4.3 Text không có overflow policy

Hiện tại phần lớn `TextBlock` không khai báo wrapping hoặc trimming. WPF có thể đo theo desired size, nhưng text vẫn bị cắt khi parent Grid/StackPanel, fixed row/column hoặc button template hạn chế chiều rộng. Đặc biệt rủi ro ở:

- subtitle/header dài;
- status text sinh động từ model;
- label song ngữ;
- button chứa emoji + label;
- DataGrid headers/cells;
- các window hẹp như `TagArranger` và `PasswordWindow`.

### 4.4 Action hierarchy và button placement không nhất quán

Primary action lúc ở trái, lúc ở phải, lúc chiếm toàn chiều rộng, lúc nằm cùng utility actions. Cancel có nơi bên trái primary, có nơi bên phải; destructive action có nơi dùng secondary style. Điều này làm tăng cognitive load và nguy cơ click nhầm.

### 4.5 Mâu thuẫn design governance

`VilaiViet_UI_Guidelines.md` mô tả light theme, trong khi `docs/superpowers/plans/Task_Apply_UI_Guidelines_v1.0.md` yêu cầu dark theme. Source hiện đang theo light theme phần lớn. Nếu không tuyên bố tài liệu nào là canonical, lần sửa sau sẽ tiếp tục đảo theme và copy style.

### 4.6 Emoji và font fallback

Root font chủ yếu là `Segoe UI`, nhưng emoji có thể fallback sang `Segoe UI Emoji`, có glyph màu và advance width khác nhau. Trên máy/Windows build khác, label button có thể rộng hơn thiết kế. Với icon chức năng quan trọng, geometry/vector 16 DIP hoặc glyph monochrome kiểm soát được sẽ ổn định hơn emoji.

## 5. Nguyên tắc UX/UI chuẩn đề xuất

### 5.1 Theme và taste direction

Chọn **light professional theme** làm canonical cho v1 vì:

- phù hợp đa số source hiện tại, giảm blast radius;
- giữ tương phản text đen trên nền trắng vốn đã được áp dụng rộng;
- không yêu cầu sửa toàn bộ DataGrid/Popup/selection state sang dark;
- tránh giao diện generic hoặc lạm dụng màu: màu chỉ mang semantic action/state.

Palette hiện có được giữ về ý nghĩa nhưng chuyển thành token: `Surface`, `SurfaceSubtle`, `TextPrimary`, `TextSecondary`, `Border`, `Accent`, `Danger`, `Focus`, `Disabled`. Màu hard-code trong window chỉ được phép cho canvas/markup/status nghiệp vụ có lý do cụ thể.

### 5.2 Spacing và typography

- Base unit: 4 DIP; spacing dùng dãy 4/8/12/16/24.
- Window content padding: 16 DIP; compact dialog có thể 12 DIP.
- Font family root: `Segoe UI`; khai báo `xml:lang="vi-VN"` cho shell tiếng Việt hoặc tài nguyên tương ứng.
- Body/input/button: 12 DIP; supporting text: 11 DIP; section title: 12 DIP semibold; window/header title: 14 DIP semibold/bold; status headline đặc thù tối đa 16 DIP.
- Không dùng font 10 DIP cho nội dung cần đọc thường xuyên.
- Text động hoặc câu mô tả: `TextWrapping="Wrap"`.
- Text định danh một dòng trong vùng hẹp: `TextTrimming="CharacterEllipsis"` + `ToolTip` chứa full text.
- Button không wrap label thành hai dòng trong v1; thay bằng `MinWidth`, icon chuẩn và action bar có khả năng wrap/overflow phù hợp.

### 5.3 Control dimensions

| Control | Chuẩn đề xuất |
|---|---|
| Standard button | `MinHeight=32`, horizontal padding 12, `MinWidth=88` nếu có text |
| Compact icon button | 32×32; tooltip + automation name bắt buộc |
| Primary command lớn | `MinHeight=40`, chỉ dùng cho single dominant workflow |
| TextBox/ComboBox | `MinHeight=30`; không khóa `Height` khi multiline |
| CheckBox/RadioButton | Hit area tối thiểu 24 DIP theo chiều cao |
| DataGrid row | `MinHeight=28`; header auto/wrap có kiểm soát |
| Separator/divider | 1 DIP, dùng token border |

### 5.4 Window size profiles

Không ép tất cả cửa sổ về một kích thước. Mỗi window chọn một profile:

| Profile | Kích thước mặc định tham chiếu | Minimum | Use case |
|---|---|---|---|
| Compact dialog | 400–520 × Auto/280–420 | 360×240 | Auth, folder name, confirm/select nhỏ |
| Standard form | 620–760 × 480–620 | 560×420 | Draw Walls/Floors/Beams/Columns, AutoDim |
| Tall tool | 480–600 × 640–720 | 440×520 | Sleeve/Void/Tag/Finish; content bắt buộc scroll |
| Workbench | 900–1100 × 600–720 | 800×520 | Issue Manager, clash checker, floor checker, markup |

Quy tắc chung:

- Resizable window phải có `MinWidth` và `MinHeight`.
- Nội dung chính của form dài nằm trong `ScrollViewer`; header và action footer luôn cố định.
- DataGrid/workbench dùng row `Height="*"`, không bọc DataGrid bằng vertical `ScrollViewer` ngoài.
- `SizeToContent="Height"` chỉ dùng khi nội dung ngắn có giới hạn; không kết hợp với form động dài mà thiếu max/work-area policy.
- Không dùng negative margin cho footer/watermark; negative margin của header shell chỉ được phép nếu component sở hữu toàn bộ padding contract.

### 5.5 Button placement và behavior

Action bar chuẩn nằm ở cuối window, cố định ngoài scrollable content:

- Trái: status/progress hoặc utility actions ít quan trọng.
- Phải: `Secondary` → `Destructive` (nếu có) → `Primary` theo thứ tự đọc trái sang phải; primary là button ngoài cùng bên phải.
- `Cancel/Close` dùng `IsCancel="True"` khi phù hợp; primary form submit dùng `IsDefault="True"` khi không gây thao tác phá hủy.
- Các action song song như Run/Show/Export đặt theo nhóm: Run là primary; Show/Export/Reset là secondary/ghost; Delete/Unjoin/Reset destructive phải được phân loại theo hậu quả thực tế.
- Button chỉ có icon phải có `ToolTip`, `AutomationProperties.Name` và keyboard focus visible.
- Không đổi event handler hoặc command trong đợt chuẩn hóa.

### 5.6 Input, focus và validation

- Mọi custom template phải có visual states/triggers cho hover, pressed, disabled và focus.
- Giữ tab order theo thứ tự thao tác; các decorative `Path`, watermark, image overlay đặt `IsHitTestVisible="False"` và không nhận focus.
- Error/validation không chỉ biểu diễn bằng màu; phải có text hoặc icon + automation help text.
- ComboBox popup phải dùng cùng foreground/background token và không che text selection.
- Tất cả control tương tác custom cần giữ khả năng invoke bằng keyboard.

## 6. Kiến trúc đề xuất

### 6.1 Component model

```text
Antigravity.Core
└─ UI
   ├─ Themes
   │  ├─ DesignTokens.xaml
   │  ├─ Typography.xaml
   │  ├─ Controls.xaml
   │  └─ DataControls.xaml
   ├─ Controls
   │  ├─ BrandHeader.xaml(.cs)
   │  └─ DialogFooter.xaml(.cs)
   └─ UiThemeBootstrapper.cs (chỉ khi pack URI merge tại runtime thực sự cần)

Feature assembly
└─ UI
   └─ FeatureWindow.xaml
      ├─ merge shared dictionaries
      ├─ dùng BrandHeader/DialogFooter
      ├─ giữ resource đặc thù feature
      └─ giữ nguyên binding/event contract
```

### 6.2 Vì sao dùng `Antigravity.Core`

Phần lớn module UI đã tham chiếu `Antigravity.Core`, và Core đang có `PasswordWindow.xaml`, nên đây là nơi tích hợp ít xáo trộn nhất. Tạo assembly `Antigravity.UI` riêng sẽ sạch dependency hơn về lý thuyết nhưng làm tăng deployment artifact, project reference và load-order risk trong một thay đổi chủ yếu là consistency.

Bốn feature UI hiện chưa tham chiếu Core và cần thêm reference nếu chọn kiến trúc này:

- `Antigravity.AutoDimWalls`
- `Antigravity.CadVoidPlacer`
- `Antigravity.DoorClearance`
- `Antigravity.TagArranger`

`Antigravity.Core` không tự merge resource vào `Application.Current.Resources` một cách mù quáng, vì add-in chạy trong process Revit và không sở hữu `Application`. Mỗi window nên merge dictionary bằng WPF pack URI tới referenced assembly; đây là cơ chế WPF chính thức cho resource trong assembly được tham chiếu.

### 6.3 Resource ownership

| Resource | Shared | Feature-local |
|---|---:|---:|
| Color/brush/spacing/font tokens | Có | Không |
| Button/TextBox/ComboBox/CheckBox/Radio styles | Có | Chỉ override semantic hiếm |
| DataGrid baseline | Có | Columns, row triggers nghiệp vụ |
| Brand header/footer | Có | Title, subtitle, status/actions được truyền vào |
| Markup canvas/tool palette | Không | `MarkupEditorWindow` |
| Pen overlay chrome | Không | `PenOverlayWindow` |
| Converters | Không | Feature sở hữu |
| Status/error colors nghiệp vụ | Token chung | Trigger/logic feature |

### 6.4 Functional preservation contract

Mỗi XAML migration phải giữ nguyên:

- `x:Class` và constructor;
- toàn bộ `x:Name` được code-behind truy cập;
- event attributes (`Click`, `Loaded`, `SelectionChanged`, `Checked`, `Unchecked`, ...);
- binding path, mode, update trigger và element-name relation;
- dialog result/default/cancel behavior hiện có, trừ khi spec được phê duyệt sửa rõ;
- Revit owner window, modal/modeless semantics và `ExternalEvent` lifecycle;
- DataGrid column binding và user data flow.

## 7. Đánh giá từng file và thay đổi dự kiến

### 7.1 Core, small dialogs và special surfaces

| File | Hiện trạng/rủi ro | Thay đổi dự kiến |
|---|---|---|
| `src/Antigravity.Core/UI/PasswordWindow.xaml` | 400×280, no-resize; warning text dài không wrap; button width hard-code | Dùng compact shell; warning wrap; action bar phải, Cancel/Unlock chuẩn; giữ no-resize nếu content pass DPI matrix |
| `src/Antigravity.IssueManager/UI/FolderNameDialog.xaml` | 360×160 quá thấp cho footer/watermark; thiếu brand shell | Compact header hoặc title strip; `SizeToContent=Height`; input auto; OK default/Cancel cancel |
| `src/Antigravity.IssueManager/UI/ExportIssueSelectionDialog.xaml` | Work list tốt nhưng thiếu shell; action/footer cạnh watermark | Compact header; list row wrapping/trimming; footer chuẩn; giữ resizable + min size |
| `src/Antigravity.IssueManager/UI/MarkupEditorWindow.xaml` | Toolbar WrapPanel, canvas workbench; không header; emoji toolbar | Dùng workbench shell tối giản; chuẩn hóa toggle/icon size, tooltip/automation; không đặt content canvas trong ScrollViewer |
| `src/Antigravity.CheckFloorElevation/UI/PenOverlayWindow.xaml` | Transparent overlay đặc thù, không Width/Height | **Exemption:** không thêm header/theme/window sizing; chỉ chuẩn hóa toolbar hit area, focus, contrast, tooltip và safe-area watermark |

### 7.2 Standard CAD/modeling forms

| File | Hiện trạng/rủi ro | Thay đổi dự kiến |
|---|---|---|
| `src/Antigravity.DrawWalls/UI/MainWindow.xaml` | 700×480 resizable nhưng không min size; duplicated style | Standard form profile; min size; footer primary-right; input rows auto; subtitle wrap |
| `src/Antigravity.DrawFloors/UI/MainWindow.xaml` | Tương tự DrawWalls; 3 primary actions cạnh Cancel | Phân nhóm draw modes thành action group, Cancel/Close riêng; standard form profile |
| `src/Antigravity.DrawColumns/UI/MainWindow.xaml` | 700×580; nhiều input và 4 primary actions; fixed columns | Standard form rộng; responsive two-column sections; action group có wrap/overflow policy |
| `src/Antigravity.DrawBeams/UI/MainWindow.xaml` | Custom ComboBox template lớn; fixed picker width 35 | Dùng shared combo state/focus; picker thành compact icon button 32; min size và footer chuẩn |
| `src/Antigravity.CadVoidPlacer/UI/MainWindow.xaml` | 460×620; form dài có scroll; nhiều style implicit/hard-code | Tall profile; shell/footer cố định; thay implicit blanket styles bằng keyed/shared styles; long status wrap |
| `src/Antigravity.CadSleevePlacer/UI/SleevePlacerWindow.xaml` | 480×700; button 38/46; footer negative watermark margin | Tall profile; standard/large command tokens; bỏ negative margin; scroll content, footer fixed |
| `src/Antigravity.ArchModeling/UI/ArchModelingWindow.xaml` | 750×550, DataGrid dense, không ScrollViewer; styles tên `Dark*` trên light UI | Workbench/large form; shared DataGrid baseline; mapping panes star-sized; footer chuẩn; đổi semantic resource name |
| `src/Antigravity.DoorClearance/UI/ClearanceBoxWindow.xaml` | 500×750, custom dark-named palette, form rất cao | Tall profile; scroll content; header/footer fixed; harmonize action placement; giữ selection logic |

### 7.3 QA, clash và utility windows

| File | Hiện trạng/rủi ro | Thay đổi dự kiến |
|---|---|---|
| `src/Antigravity.ZoneSplit/UI/MainWindow.xaml` | 450×400 resizable không min; subtitle không wrap | Compact/standard profile; min size; checklist scroll; primary-right |
| `src/Antigravity.Autojoin/UI/MainWindow.xaml` | 950×550; nhiều utility/action; icon buttons 24×24 dưới target chuẩn | Workbench profile; compact icon 32; group config vs execute; Unjoin semantic destructive tùy xác nhận |
| `src/Antigravity.AutoDimWalls/UI/AutoDimWindow.xaml` | 620×520 có min; action rows khá rõ | Giữ profile; chuyển shared resources; Run primary, Delete destructive, utility secondary; consistent widths |
| `src/Antigravity.TagArranger/UI/ArrangerWindow.xaml` | 340×680, 18 button, nhiều fixed row/width; rủi ro clipping cao nhất | Ưu tiên P0; tăng minimum practical width, dùng section layout co giãn, icon 32, action labels wrap via panel chứ không wrap text, scroll body/fixed footer |
| `src/Antigravity.WallMepClash/UI/WallMepClashDialog.xaml` | 980×640 có min; filter grid nhiều fixed columns; button row bên phải | Workbench; filter bar responsive/wrap; DataGrid star row; action hierarchy Run → utilities → reset |
| `src/Antigravity.CheckFloorElevation/UI/FloorCheckerDialog.xaml` | 980×640 có min; 6 button ngang; fixed filter columns | Workbench; responsive filter/action wrap; shared DataGrid; status/footer không chồng watermark |
| `src/Antigravity.DoorClearance/UI/ClashControlWindow.xaml` | 850×550 có min; mô tả dài không wrap; action labels dài | Workbench; description wrap; action bar responsive; CSV export utility, Scan primary |

### 7.4 Issue Manager và finishing

| File | Hiện trạng/rủi ro | Thay đổi dự kiến |
|---|---|---|
| `src/Antigravity.IssueManager/UI/IssueManagerWindow.xaml` | 1050×680, 16 button, toolbar có WrapPanel; nhiều panes | Ưu tiên P0; workbench shell; toolbar phân nhóm Load/Create/Export; action pane có overflow/wrap; giữ list/tree bindings |
| `src/Antigravity.IssueManager/UI/CreateIssueDialog.xaml` | 860×560 có min; form + 2 preview; action hierarchy tương đối tốt | Dùng shared resources; cân min preview size; text area multiline không khóa template height; default/cancel semantics |
| `src/Antigravity.HoanThien/UI/HoanThienWindow.xaml` | Width 550 + `SizeToContent=Height`; content động/tab có thể vượt màn hình; text placeholder dài không wrap | Tall/standard hybrid; bỏ unbounded size-to-content hoặc thêm scroll/max work-area policy; fixed footer; dynamic labels wrap |

## 8. File cần tạo

Các file sau là đề xuất kiến trúc; chỉ tạo sau khi spec được phê duyệt:

| File mới | Mục đích |
|---|---|
| `src/Antigravity.Core/UI/Themes/DesignTokens.xaml` | Color, brush, spacing, radius, size tokens; canonical theme |
| `src/Antigravity.Core/UI/Themes/Typography.xaml` | Font family/size/weight và text semantic styles |
| `src/Antigravity.Core/UI/Themes/Controls.xaml` | Button, input, ComboBox, CheckBox, RadioButton, focus/disabled states |
| `src/Antigravity.Core/UI/Themes/DataControls.xaml` | DataGrid/ListView/TreeView baseline styles |
| `src/Antigravity.Core/UI/Controls/BrandHeader.xaml` | Header reusable, nhận title/subtitle và optional status content |
| `src/Antigravity.Core/UI/Controls/BrandHeader.xaml.cs` | Dependency properties tối thiểu cho header; không chứa nghiệp vụ |
| `src/Antigravity.Core/UI/Controls/DialogFooter.xaml` | Footer reusable, content slots cho status/actions/watermark |
| `src/Antigravity.Core/UI/Controls/DialogFooter.xaml.cs` | Dependency properties/content slots tối thiểu; không chứa nghiệp vụ |
| `tests/Antigravity.Ui.Tests/Antigravity.Ui.Tests.csproj` | Test project contract tĩnh cho XAML |
| `tests/Antigravity.Ui.Tests/XamlInventoryTests.cs` | Bảo đảm mọi XAML thuộc inventory và parse được |
| `tests/Antigravity.Ui.Tests/XamlLayoutContractTests.cs` | Kiểm tra shell/resource, min size, overflow policy, forbidden hard-code với allowlist |
| `tests/Antigravity.Ui.Tests/XamlInteractionContractTests.cs` | Snapshot `x:Name`, event và binding surface để ngăn mất chức năng |
| `tests/Antigravity.Ui.Tests/XamlExceptionRegistry.cs` | Khai báo ngoại lệ có lý do, gồm PenOverlay/canvas/markup |
| `docs/qa/Xaml_Visual_QA_Matrix.md` | Checklist Revit/DPI/window size/keyboard/high-contrast và sign-off |

`UiThemeBootstrapper.cs` **không** nên tạo mặc định. Chỉ thêm nếu thử nghiệm pack URI cho thấy một số module cũ không load được merged dictionary trực tiếp; tránh side effect vào `Application.Current.Resources` của Revit.

## 9. File cần sửa

### 9.1 Project/solution và governance

| File | Thay đổi |
|---|---|
| `Antigravity.sln` | Thêm `Antigravity.Ui.Tests` |
| `src/Antigravity.Core/Antigravity.Core.csproj` | Compile Page/UserControl resources nếu SDK default item không đủ rõ; không đổi target framework |
| `src/Antigravity.AutoDimWalls/Antigravity.AutoDimWalls.csproj` | Thêm project reference tới Core; với explicit item mode, include XAML/component mới theo yêu cầu build |
| `src/Antigravity.CadVoidPlacer/Antigravity.CadVoidPlacer.csproj` | Thêm project reference tới Core |
| `src/Antigravity.DoorClearance/Antigravity.DoorClearance.csproj` | Thêm project reference tới Core |
| `src/Antigravity.TagArranger/Antigravity.TagArranger.csproj` | Thêm project reference tới Core; giữ explicit Page/Compile items hợp lệ |
| `VilaiViet_UI_Guidelines.md` | Sửa encoding UTF-8, tuyên bố light theme canonical, trỏ tới tokens/component thay vì copy XAML |
| `docs/superpowers/plans/Task_Apply_UI_Guidelines_v1.0.md` | Đánh dấu superseded/archived vì yêu cầu dark theme mâu thuẫn; không xóa lịch sử |
| `docs/specs/Spec_Xaml_UX_UI_Consistency_v1.0.md` | Cập nhật trạng thái Approved và các quyết định sau review |

### 9.2 Toàn bộ XAML thuộc phạm vi

Sửa cả 24 file được liệt kê ở mục 7. Mẫu thay đổi chung:

- merge shared dictionaries;
- thay style local trùng lặp bằng shared semantic style;
- dùng header/footer component theo profile hoặc exemption;
- bổ sung min size/scroll/wrap/trimming theo contract;
- chuẩn hóa action placement và control state;
- giữ nguyên resource đặc thù, bindings, events và `x:Name`.

Không dự kiến sửa 24 file `.xaml.cs` chỉ để đổi layout. Chỉ sửa code-behind khi component hóa cần truyền property rõ ràng hoặc khi thêm `IsDefault/IsCancel` làm lộ conflict hành vi; mọi sửa như vậy phải được liệt kê cụ thể ở Phase 2.

## 10. Verification strategy và acceptance criteria

### 10.1 Automated gates

1. Tất cả XAML parse và compile BAML thành công.
2. `dotnet build Antigravity.sln --no-restore -p:DeployToRevitAddins=false` không phát sinh error/warning UI mới.
3. Inventory test khẳng định không bỏ sót XAML mới dưới `src`.
4. Interaction contract khẳng định không mất/đổi ngoài ý muốn `x:Name`, event attribute và binding path.
5. Layout contract khẳng định:
   - resizable normal window có min size;
   - long/dynamic text có wrap hoặc trim policy;
   - standard window dùng shared resources;
   - icon-only button có tooltip và automation name;
   - không xuất hiện local style trùng semantic key nếu không nằm trong allowlist;
   - special overlay/canvas được ghi trong exception registry.

### 10.2 Visual QA matrix

Mỗi cửa sổ phải kiểm tra trong Revit 2024 trên:

- DPI: 100%, 125%, 150%, 175%, 200%;
- kích thước: default, minimum supported, maximize/large monitor;
- text: dữ liệu ngắn, dữ liệu dài thực tế, rỗng/null, tiếng Việt có dấu và tiếng Anh;
- trạng thái control: normal, hover, pressed, focus, disabled, validation error;
- keyboard: Tab/Shift+Tab, Enter, Escape, Space, arrow keys trong combo/list;
- DataGrid/ListView: empty, 1 row, nhiều row, cell/header dài;
- multi-monitor: mở và di chuyển giữa màn hình có DPI khác nhau nếu môi trường cho phép.

### 10.3 Definition of done

- Không có text đè control/text khác ở ma trận hỗ trợ.
- Không có text bị cắt mà không có wrapping/trimming + tooltip có chủ đích.
- Header/content/footer không chồng nhau; primary action luôn dễ nhận biết.
- Window không vượt work area ở cấu hình laptop mục tiêu và vẫn thao tác được ở minimum size.
- Tất cả chức năng/button/event/binding hiện có hoạt động như baseline.
- Màu, typography, spacing và interaction state lấy từ single source of truth.
- `PenOverlayWindow` vẫn transparent/topmost và không bị biến thành dialog thường.
- Visual QA có người sign-off trong Revit, không chỉ dựa vào build/test ngoài host.

## 11. Rủi ro và biện pháp giảm thiểu

| Rủi ro | Xác suất | Tác động | Giảm thiểu |
|---|---:|---:|---|
| Pack URI/resource assembly load lỗi trong Revit | Trung bình | Cao | Prototype trên 1 dialog nhỏ; dùng URI đầy đủ; build + smoke test trong Revit trước rollout |
| Shared implicit style làm đổi control đặc thù | Cao | Cao | Ưu tiên keyed semantic styles; implicit style chỉ cho typography an toàn; exception registry |
| Component hóa làm mất `x:Name`/event/binding | Trung bình | Cao | Interaction contract snapshot; không di chuyển control nghiệp vụ vào component generic |
| Custom template làm mất keyboard/focus/disabled state | Cao | Trung bình/Cao | Visual state contract + Accessibility Insights/manual keyboard QA |
| DPI trong add-in phụ thuộc host process Revit | Trung bình | Cao | Không tự thay process DPI awareness; test thật trong Revit/multi-monitor; dùng WPF auto layout và layout rounding |
| Window dài vượt work area | Cao | Trung bình | Fixed header/footer + scroll body; min/default profile; kiểm thử 150–200% DPI |
| Emoji render khác máy và làm label rộng | Cao | Trung bình | Thay icon quan trọng bằng vector/glyph monochrome; giữ text label; tooltip |
| Regression nghiệp vụ do sửa XAML diện rộng | Trung bình | Cao | Chia migration theo archetype ở Phase 2; giữ event/binding contract; smoke test từng module |
| Thêm Core reference làm thay đổi deployment | Trung bình | Trung bình | Kiểm tra output/install manifest của 4 module; Core đã là shared assembly trong solution |
| Tài liệu theme tiếp tục mâu thuẫn | Cao | Trung bình | Một canonical guideline, version/status rõ; tài liệu cũ marked superseded |
| Watermark chiếm vùng action ở dialog nhỏ | Cao | Thấp/Trung bình | Footer sở hữu layout watermark; không overlay/negative margin; compact variant |
| High contrast/Windows theme phá màu hard-code | Trung bình | Trung bình | Token hóa brush, không dùng màu làm tín hiệu duy nhất; kiểm tra high contrast tối thiểu |

## 12. Phương án đã cân nhắc

### A. Chỉ sửa trực tiếp 24 XAML

Nhanh ban đầu nhưng tiếp tục duplicate style, không ngăn regression và không có single source of truth. **Không khuyến nghị.**

### B. Shared ResourceDictionary trong `Antigravity.Core` + reusable shell components

Cân bằng tốt nhất giữa kiến trúc, số dependency mới và khả năng rollout. Phù hợp net48/Revit 2024. **Khuyến nghị.**

### C. Tạo assembly `Antigravity.UI` riêng

Separation of concerns tốt hơn, không phụ thuộc Revit API, nhưng tăng project/deployment/load-order scope cho một đợt consistency. Chỉ nên chọn nếu roadmap xác nhận design system sẽ được dùng ngoài Revit hoặc Core sắp được tách mạnh. **Không chọn cho v1.**

### D. Migrate framework UI

WinUI/Avalonia không giải quyết trực tiếp regression hiện tại và tạo risk lớn với Revit host. **Out of scope.**

## 13. Open questions cần phê duyệt

1. Xác nhận **light theme** là canonical và tài liệu dark theme cũ được đánh dấu superseded?
2. Có giữ brand header đầy đủ trên ba auxiliary dialog (`FolderName`, `ExportIssueSelection`, `MarkupEditor`) hay dùng compact title strip? Đề xuất: compact title strip để tiết kiệm diện tích.
3. Watermark `@manhns` có bắt buộc trên mọi dialog/overlay không? Đề xuất: giữ theo guideline hiện hành, nhưng footer sở hữu layout; overlay chỉ dùng safe-area.
4. Có chấp nhận thay emoji bằng vector icon tối giản trong phạm vi này không? Đề xuất: có, ưu tiên button/toolbar; không đổi semantic label.
5. Mức DPI cam kết chính thức là đến 200% hay thấp hơn? Đề xuất: 200% để bao phủ laptop/4K phổ biến.
6. Ngôn ngữ canonical là Việt, Anh hay song ngữ? Đề xuất: không dịch trong v1, nhưng layout phải chịu được chuỗi Việt/Anh dài hơn 30%.
7. Có cho phép thêm project reference tới `Antigravity.Core` ở bốn module độc lập không? Đề xuất: có; nếu không, phải chọn assembly UI riêng hoặc duplicate resource, trong đó duplicate không được khuyến nghị.

## 14. Nguồn tham khảo chính thức

- Microsoft, [WPF Layout](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/layout): device-independent units, DPI scaling và `UseLayoutRounding`.
- Microsoft, [WPF Globalization and Localization Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-globalization-and-localization-overview): automatic layout, tránh fixed size, dùng Grid/Auto, thêm spacing, `TextWrapping` và `xml:lang`.
- Microsoft, [Pack URIs in WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/pack-uris-in-wpf?view=netframeworkdesktop-4.8): tải resource từ referenced assembly.
- Microsoft, [Styling for Focus in Controls](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/styling-for-focus-in-controls-and-focusvisualstyle): focus visual nhất quán và accessibility cho custom templates.
- Microsoft Accessibility Insights, [WPF Button IsKeyboardFocusable](https://learn.microsoft.com/en-us/accessibility-tools-docs/items/wpf/button_iskeyboardfocusable): button/invokable control cần keyboard focus và tab order.
- Microsoft, [Developing a Per-Monitor DPI-Aware WPF Application](https://learn.microsoft.com/en-us/windows/win32/hidpi/declaring-managed-apps-dpi-aware): WPF/system DPI, multi-monitor và rủi ro scaling bởi host process.
- Autodesk, [Revit 2024 API Development Requirements](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Introduction/Getting_Started/Welcome_to_the_Revit_Platform_API/Revit_API_Revit_API_Developers_Guide_Introduction_Getting_Started_Welcome_to_the_Revit_Platform_API_Development_Requirements_html.html): Revit 2024 yêu cầu .NET Framework 4.8.
- Autodesk, [Revit 2024 Dockable Dialog Panes](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Advanced_Topics/Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Dockable_Dialog_Panes_html.html): WPF dialog/modeless behavior trong Revit và External Events context.

## 15. Approval gate

Tài liệu này là Phase 1 spec/research, chưa cho phép sửa implementation. Sau khi các câu hỏi ở mục 13 được chốt và spec được approve, công việc mới chuyển sang `/2.plan` để chia batch, thứ tự migration, test gates và rollback points; sau đó mới tới `/3.code`.
