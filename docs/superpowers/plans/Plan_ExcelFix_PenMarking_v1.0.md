# 📦 HANDOVER: Excel Bug Fix + Pen Marking 3D
> **Version:** v1.0 | **Date:** 2026-06-19 | **Project:** Antigravity.IssueManager

---

## ⚡ PHẦN 1 — BUG FIX: Lỗi xuất Excel (Ưu tiên cao)

### Root Cause

**File:** `src/Antigravity.IssueManager/Services/ExcelIssueExporter.cs` — method `CreateXml()` (line 511–527)

`XmlWriter.Create(StringBuilder, settings)` **bỏ qua `Encoding` setting**, luôn ghi `<?xml version="1.0" encoding="utf-16"?>` vào header. Khi `WriteTextEntry` write string ra ZIP bằng `StreamWriter(UTF-8)`, nội dung thực là UTF-8 nhưng header khai báo `utf-16` → Excel validate fail → báo lỗi "file format or file extension is not valid".

### Fix — Task 1 [XS] — 1 dòng duy nhất

**File:** `src/Antigravity.IssueManager/Services/ExcelIssueExporter.cs`  
**Dòng 517** — thay `false` → `true`:

```csharp
// BEFORE ❌
OmitXmlDeclaration = false

// AFTER ✅
OmitXmlDeclaration = true
```

> OOXML spec (ECMA-376) không bắt buộc XML declaration trong part files. Excel, LibreOffice, Google Sheets đều accept khi không có declaration.

**Acceptance:** Xuất file `.xlsx` → Excel mở được, không còn lỗi.

---

## 🖊️ PHẦN 2 — TÍNH NĂNG MỚI: Pen Marking (Markup Editor)

### Mô tả

Cho phép user **vẽ annotation** lên ảnh issue trực tiếp trong ứng dụng, tương tự Snipping Tool markup. Tích hợp tại 2 điểm:

1. **`CreateIssueDialog`** → markup ảnh 2D trước khi tạo issue
2. **`IssueManagerWindow`** → markup lại ảnh 3D hoặc 2D của issue đã tạo

### Tools hỗ trợ

| Tool | Icon | Mô tả |
|------|------|--------|
| Pen | 🖊️ | Vẽ tay tự do freehand |
| Rectangle | ⬜ | Khoanh vùng highlight |
| Arrow | ➡️ | Mũi tên chỉ vị trí |
| Text | 🔤 | Thêm text label |
| Color | 🎨 | Chọn màu (mặc định đỏ) |
| Undo | ↩️ | Hoàn tác stroke cuối |
| Clear | 🗑️ | Xóa toàn bộ |
| Save | 💾 | Flatten → PNG mới |

### Kiến trúc flow

```
[CreateIssueDialog]
  └── Btn "✏️ Markup" (enable khi có ảnh 2D)
        ↓ mở
[MarkupEditorWindow]
  ├── Grid chứa Image (background ảnh gốc)
  └── InkCanvas (overlay, vẽ lên trên)
  └── Toolbar: Pen / Rect / Arrow / Text / Color / Undo / Clear / Save
        ↓ BtnSave_Click
  RenderTargetBitmap.Render(panel chứa cả Image + InkCanvas)
  → encode PNG → lưu temp
  → DialogResult = true, ResultImagePath = path mới
        ↓
[CreateIssueDialog] nhận ResultImagePath → cập nhật Image2DPath + reload preview

[IssueManagerWindow]
  └── Btn "✏️ Edit Markup 3D" / "✏️ Edit Markup 2D"
        ↓ mở MarkupEditorWindow với ảnh tương ứng
        ↓ Save → cập nhật issue.Viewpoint.SnapshotFilePath / SnapshotFilePath2
        ↓ AutoSaveIssues()
```

### ⚠️ Constraint quan trọng

> Snapshot 3D chụp bởi `RevitIssueCreator` trong **ExternalEvent** — chạy SAU khi user bấm Create. Do đó **không thể markup ảnh 3D trong CreateIssueDialog** (ảnh chưa tồn tại). Chỉ markup được ảnh 3D từ `IssueManagerWindow` sau khi issue đã tạo xong.

---

## 📁 Files cần tạo / sửa

```
src/Antigravity.IssueManager/
├── UI/
│   ├── MarkupEditorWindow.xaml          [NEW]
│   ├── MarkupEditorWindow.xaml.cs       [NEW]
│   ├── CreateIssueDialog.xaml           [MODIFY]
│   ├── CreateIssueDialog.xaml.cs        [MODIFY]
│   ├── IssueManagerWindow.xaml          [MODIFY]
│   └── IssueManagerWindow.xaml.cs       [MODIFY]
└── Services/
    └── ExcelIssueExporter.cs            [MODIFY — bug fix]
```

---

## 📋 Task List (theo thứ tự)

### ✅ Task 1 [XS] — Bug fix Excel

**File:** `Services/ExcelIssueExporter.cs` line 517
```csharp
OmitXmlDeclaration = true   // thay false → true
```

---

### ✅ Task 2 [M] — Tạo `MarkupEditorWindow.xaml`

**File:** `UI/MarkupEditorWindow.xaml` — **[NEW]**

Layout tổng thể:
```xml
<Window Title="Markup Editor" Width="900" Height="650"
        Background="#1A1A5E" WindowStartupLocation="CenterOwner">
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>   <!-- Toolbar -->
      <RowDefinition Height="*"/>      <!-- Canvas area -->
      <RowDefinition Height="Auto"/>   <!-- Bottom buttons -->
    </Grid.RowDefinitions>

    <!-- TOOLBAR -->
    <ToolBar Grid.Row="0" Background="#0D0D4A">
      <ToggleButton x:Name="BtnPen"       Content="🖊️ Pen"       IsChecked="True"/>
      <ToggleButton x:Name="BtnRect"      Content="⬜ Rect"/>
      <ToggleButton x:Name="BtnArrow"     Content="➡️ Arrow"/>
      <ToggleButton x:Name="BtnText"      Content="🔤 Text"/>
      <Separator/>
      <ComboBox x:Name="CmbColor" Width="90" SelectionChanged="CmbColor_Changed">
        <ComboBoxItem Tag="#FF0000" Content="🔴 Đỏ"     IsSelected="True"/>
        <ComboBoxItem Tag="#FFD700" Content="🟡 Vàng"/>
        <ComboBoxItem Tag="#00CC00" Content="🟢 Xanh lá"/>
        <ComboBoxItem Tag="#00AAFF" Content="🔵 Xanh dương"/>
        <ComboBoxItem Tag="#FFFFFF" Content="⬜ Trắng"/>
      </ComboBox>
      <Separator/>
      <Button Content="↩️ Undo"  Click="BtnUndo_Click"/>
      <Button Content="🗑️ Clear" Click="BtnClear_Click"/>
    </ToolBar>

    <!-- CANVAS AREA: Image + InkCanvas overlay trong cùng 1 Grid -->
    <Grid Grid.Row="1" x:Name="MarkupPanel" Background="#0A0A3A">
      <Image x:Name="ImgBase" Stretch="Uniform" HorizontalAlignment="Center" VerticalAlignment="Center"/>
      <InkCanvas x:Name="InkLayer"
                 Background="Transparent"
                 EditingMode="Ink"/>
    </Grid>

    <!-- BOTTOM BUTTONS -->
    <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="8">
      <Button Content="💾 Lưu markup" Click="BtnSave_Click"
              Background="#CC0000" Foreground="White" Padding="14,7" Margin="0,0,8,0"/>
      <Button Content="✖ Huỷ"         Click="BtnCancel_Click"
              Background="#3A3A8A" Foreground="White" Padding="14,7"/>
    </StackPanel>
  </Grid>
</Window>
```

> **Lưu ý:** `InkCanvas` đặt **sau** `Image` trong cùng Grid cell → tự động overlay lên trên. Background="Transparent" để ảnh nền hiển thị xuyên qua.

---

### ✅ Task 3 [M] — Code-behind `MarkupEditorWindow.xaml.cs`

**File:** `UI/MarkupEditorWindow.xaml.cs` — **[NEW]**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Antigravity.IssueManager.UI
{
    public partial class MarkupEditorWindow : Window
    {
        public string ResultImagePath { get; private set; }

        private Color _currentColor = Colors.Red;
        private double _penThickness = 3.0;
        private readonly Stack<Stroke> _undoStack = new Stack<Stroke>();
        // Shapes (Rect/Arrow) được add vào InkLayer.Children
        private readonly Stack<UIElement> _shapeUndoStack = new Stack<UIElement>();

        private bool _isDrawingShape;
        private Point _shapeStart;
        private UIElement _currentShape;

        public MarkupEditorWindow(string imagePath)
        {
            InitializeComponent();
            LoadImage(imagePath);
            SetupInkCanvas();
            InkLayer.StrokeCollected += (s, e) => _undoStack.Push(e.Stroke);
        }

        private void LoadImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(imagePath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            ImgBase.Source = bmp;
        }

        private void SetupInkCanvas()
        {
            UpdateDrawingAttributes();
            // Pen mode active by default
            BtnPen.IsChecked = true;
            InkLayer.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void UpdateDrawingAttributes()
        {
            InkLayer.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = _currentColor,
                Width = _penThickness,
                Height = _penThickness,
                FitToCurve = true
            };
        }

        // ── Tool toggle ──────────────────────────────────────────
        private void BtnPen_Checked(object sender, RoutedEventArgs e)
        {
            if (InkLayer == null) return;
            UncheckOthers(BtnPen);
            InkLayer.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void BtnRect_Checked(object sender, RoutedEventArgs e)
        {
            UncheckOthers(BtnRect);
            InkLayer.EditingMode = InkCanvasEditingMode.None; // handle manually
        }

        private void BtnArrow_Checked(object sender, RoutedEventArgs e)
        {
            UncheckOthers(BtnArrow);
            InkLayer.EditingMode = InkCanvasEditingMode.None;
        }

        private void BtnText_Checked(object sender, RoutedEventArgs e)
        {
            UncheckOthers(BtnText);
            InkLayer.EditingMode = InkCanvasEditingMode.None;
        }

        private void UncheckOthers(System.Windows.Controls.Primitives.ToggleButton active)
        {
            foreach (var btn in new[] { BtnPen, BtnRect, BtnArrow, BtnText })
                if (btn != active) btn.IsChecked = false;
        }

        // ── Mouse events for Rect / Arrow drawing ─────────────────
        private void InkLayer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (BtnRect.IsChecked == true)
            {
                _isDrawingShape = true;
                _shapeStart = e.GetPosition(InkLayer);
                var rect = new Rectangle
                {
                    Stroke = new SolidColorBrush(_currentColor),
                    StrokeThickness = _penThickness,
                    Fill = Brushes.Transparent
                };
                InkCanvas.SetLeft(rect, _shapeStart.X);
                InkCanvas.SetTop(rect, _shapeStart.Y);
                _currentShape = rect;
                InkLayer.Children.Add(rect);
            }
            else if (BtnArrow.IsChecked == true)
            {
                _isDrawingShape = true;
                _shapeStart = e.GetPosition(InkLayer);
            }
        }

        private void InkLayer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawingShape || e.LeftButton != MouseButtonState.Pressed) return;
            Point cur = e.GetPosition(InkLayer);

            if (BtnRect.IsChecked == true && _currentShape is Rectangle r)
            {
                double x = Math.Min(_shapeStart.X, cur.X);
                double y = Math.Min(_shapeStart.Y, cur.Y);
                InkCanvas.SetLeft(r, x);
                InkCanvas.SetTop(r, y);
                r.Width  = Math.Abs(cur.X - _shapeStart.X);
                r.Height = Math.Abs(cur.Y - _shapeStart.Y);
            }
        }

        private void InkLayer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawingShape) return;
            _isDrawingShape = false;
            if (_currentShape != null)
            {
                _shapeUndoStack.Push(_currentShape);
                _currentShape = null;
            }
            else if (BtnArrow.IsChecked == true)
            {
                // Vẽ arrow bằng Polyline (line + arrowhead)
                Point end = e.GetPosition(InkLayer);
                var arrow = BuildArrow(_shapeStart, end, _currentColor, _penThickness);
                foreach (var el in arrow)
                {
                    InkLayer.Children.Add(el);
                    _shapeUndoStack.Push(el);
                }
            }
        }

        private static List<UIElement> BuildArrow(Point from, Point to, Color color, double thickness)
        {
            var brush = new SolidColorBrush(color);
            var line = new Line
            {
                X1 = from.X, Y1 = from.Y,
                X2 = to.X,   Y2 = to.Y,
                Stroke = brush, StrokeThickness = thickness
            };
            // Arrowhead: 2 short lines at tip
            double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
            double headLen = 14;
            double a1 = angle + 2.5, a2 = angle - 2.5;
            var h1 = new Line
            {
                X1 = to.X, Y1 = to.Y,
                X2 = to.X - headLen * Math.Cos(a1),
                Y2 = to.Y - headLen * Math.Sin(a1),
                Stroke = brush, StrokeThickness = thickness
            };
            var h2 = new Line
            {
                X1 = to.X, Y1 = to.Y,
                X2 = to.X - headLen * Math.Cos(a2),
                Y2 = to.Y - headLen * Math.Sin(a2),
                Stroke = brush, StrokeThickness = thickness
            };
            return new List<UIElement> { line, h1, h2 };
        }

        // ── Color ────────────────────────────────────────────────
        private void CmbColor_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbColor.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                _currentColor = (Color)ColorConverter.ConvertFromString(item.Tag.ToString());
                UpdateDrawingAttributes();
            }
        }

        // ── Undo / Clear ─────────────────────────────────────────
        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            // Undo Ink stroke
            if (InkLayer.EditingMode == InkCanvasEditingMode.Ink && _undoStack.Count > 0)
            {
                InkLayer.Strokes.Remove(_undoStack.Pop());
                return;
            }
            // Undo shape
            if (_shapeUndoStack.Count > 0)
                InkLayer.Children.Remove(_shapeUndoStack.Pop());
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            InkLayer.Strokes.Clear();
            InkLayer.Children.Clear();
            _undoStack.Clear();
            _shapeUndoStack.Clear();
        }

        // ── Save: flatten → PNG ──────────────────────────────────
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Render MarkupPanel (Image + InkCanvas together)
                var rtb = new RenderTargetBitmap(
                    (int)MarkupPanel.ActualWidth,
                    (int)MarkupPanel.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);
                rtb.Render(MarkupPanel);

                string dir  = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "AntigravityIssueManager", "Markups");
                Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(
                    dir, $"markup_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                using (var stream = File.Create(path))
                {
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(rtb));
                    enc.Save(stream);
                }

                ResultImagePath = path;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lưu markup thất bại: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
```

**Namespaces cần có** (thêm vào XAML):
```xml
xmlns:local="clr-namespace:Antigravity.IssueManager.UI"
```

---

### ✅ Task 4 [S] — Tích hợp vào `CreateIssueDialog`

**File:** `UI/CreateIssueDialog.xaml` — **[MODIFY]**

Trong panel Hình ảnh 2D, thêm nút Markup bên cạnh nút Chọn ảnh:
```xml
<Button x:Name="BtnMarkup2D" Content="✏️ Markup"
        Click="BtnMarkup2D_Click"
        IsEnabled="{Binding ElementName=BtnClearImage2D, Path=Visibility,
                    Converter={...}}"
        Padding="10,5" Margin="4,0,0,0"
        Background="#FF8800" Foreground="White" BorderThickness="0"/>
```

> Đơn giản hơn: `IsEnabled` bind vào `Image2DPath != null` bằng code-behind.

**File:** `UI/CreateIssueDialog.xaml.cs` — **[MODIFY]**

```csharp
private void BtnMarkup2D_Click(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrEmpty(Image2DPath)) return;

    var editor = new MarkupEditorWindow(Image2DPath) { Owner = this };
    if (editor.ShowDialog() == true)
    {
        LoadImage2DPreview(editor.ResultImagePath);  // cập nhật Image2DPath + preview
    }
}
```

Cập nhật `BtnMarkup2D.IsEnabled` trong `LoadImage2DPreview` và `BtnClearImage2D_Click`:
```csharp
// Trong LoadImage2DPreview():
BtnMarkup2D.IsEnabled = true;

// Trong BtnClearImage2D_Click():
BtnMarkup2D.IsEnabled = false;
```

---

### ✅ Task 5 [S] — Thêm vào `IssueManagerWindow`

**File:** `UI/IssueManagerWindow.xaml` — **[MODIFY]**

Thêm 2 nút dưới mỗi panel ảnh:
```xml
<!-- Dưới ImgSnapshot (ảnh 3D) -->
<Button x:Name="BtnMarkupSnapshot3D" Content="✏️ Markup 3D"
        Click="BtnMarkupSnapshot3D_Click"
        Style="{StaticResource SecondaryBtn}" Margin="0,4,0,0"/>

<!-- Dưới ImgSnapshot2 (ảnh 2D) -->
<Button x:Name="BtnMarkupSnapshot2D" Content="✏️ Markup 2D"
        Click="BtnMarkupSnapshot2D_Click"
        Style="{StaticResource SecondaryBtn}" Margin="0,4,0,0"/>
```

**File:** `UI/IssueManagerWindow.xaml.cs` — **[MODIFY]**

```csharp
private void BtnMarkupSnapshot3D_Click(object sender, RoutedEventArgs e)
{
    OpenMarkupEditor(_selectedIssue?.Viewpoint?.SnapshotFilePath, isSnapshot3D: true);
}

private void BtnMarkupSnapshot2D_Click(object sender, RoutedEventArgs e)
{
    OpenMarkupEditor(_selectedIssue?.Viewpoint?.SnapshotFilePath2, isSnapshot3D: false);
}

private void OpenMarkupEditor(string imagePath, bool isSnapshot3D)
{
    if (_selectedIssue == null || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
    {
        MessageBox.Show("Không có ảnh để markup.", "Markup Editor",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }

    var editor = new MarkupEditorWindow(imagePath) { Owner = this };
    if (editor.ShowDialog() != true) return;

    if (isSnapshot3D)
        _selectedIssue.Viewpoint.SnapshotFilePath  = editor.ResultImagePath;
    else
        _selectedIssue.Viewpoint.SnapshotFilePath2 = editor.ResultImagePath;

    // Reload preview
    LoadSnapshot(isSnapshot3D ? ImgSnapshot : ImgSnapshot2, editor.ResultImagePath);
    AutoSaveIssues();
}
```

---

## ⚙️ XAML events cần wire trong MarkupEditorWindow.xaml

```xml
<!-- Thêm vào InkCanvas -->
<InkCanvas x:Name="InkLayer"
           Background="Transparent"
           EditingMode="Ink"
           MouseDown="InkLayer_MouseDown"
           MouseMove="InkLayer_MouseMove"
           MouseUp="InkLayer_MouseUp"/>

<!-- Thêm vào ToggleButtons -->
<ToggleButton x:Name="BtnPen"   Checked="BtnPen_Checked"   .../>
<ToggleButton x:Name="BtnRect"  Checked="BtnRect_Checked"  .../>
<ToggleButton x:Name="BtnArrow" Checked="BtnArrow_Checked" .../>
<ToggleButton x:Name="BtnText"  Checked="BtnText_Checked"  .../>
```

---

## 🧪 Verification Checklist

- [ ] **T1**: Xuất Excel → mở được, không còn lỗi format
- [ ] **T2+T3**: Bấm "✏️ Markup" trong CreateIssueDialog → MarkupEditorWindow mở, hiển thị ảnh nền
- [ ] Vẽ Pen → nét xuất hiện màu đỏ
- [ ] Vẽ Rectangle → hình chữ nhật bao quanh vùng kéo
- [ ] Vẽ Arrow → mũi tên từ điểm start → end
- [ ] Undo → xóa stroke/shape cuối
- [ ] Clear → xóa toàn bộ
- [ ] Lưu → file PNG mới xuất hiện trong `%TEMP%\AntigravityIssueManager\Markups\`
- [ ] **T4**: Ảnh preview trong CreateIssueDialog cập nhật với annotation
- [ ] **T5**: Bấm "✏️ Markup 3D" trong IssueManagerWindow → markup ảnh → lưu → ảnh panel cập nhật

---

## ⚠️ Risks & Notes

| Risk | Mitigation |
|------|------------|
| `InkCanvas` Rect/Arrow không native | Handle `MouseDown/Move/Up` thủ công, add `UIElement` vào `InkCanvas.Children` |
| `RenderTargetBitmap` sai kích thước nếu ảnh chưa render xong | Gọi `UpdateLayout()` trước `rtb.Render()` |
| MarkupPanel size = 0 nếu window chưa show | Dùng `Window.ContentRendered` event để đảm bảo layout hoàn tất trước khi Render |
| Text tool cần `TextBox` popup | Phase 2 future: Hiện tại Text = todo, có thể dùng `InkCanvasEditingMode.Ink` với nét chữ tay |
