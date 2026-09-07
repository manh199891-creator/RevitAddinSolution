# 📦 HANDOVER: Redesign Create Issue Dialog
> **Version:** v1.0 | **Date:** 2026-06-19 | **Project:** Antigravity.IssueManager

---

## 🎯 Mục tiêu

Redesign cửa sổ `CreateIssueDialog` từ dạng đơn giản (chỉ Title + Description) thành layout đầy đủ có **2 vùng ảnh side-by-side** theo mẫu form RFI:

| Vùng | Mô tả |
|------|--------|
| **Hình ảnh 3D** (trái) | Snapshot tự động chụp từ Revit view hiện tại khi tạo issue. Trong dialog chỉ hiển thị placeholder vì ảnh thật chụp sau khi ExternalEvent chạy. |
| **Hình ảnh 2D** (phải) | User chủ động thêm ảnh annotation/chú thích bằng 2 cách: (1) browse chọn file PNG/JPG, (2) **Ctrl+V** dán từ clipboard (Snipping Tool, Print Screen, v.v.) |

---

## 📁 Files cần sửa

```
src/Antigravity.IssueManager/
├── UI/
│   ├── CreateIssueDialog.xaml          ← Task 1: Redesign layout
│   ├── CreateIssueDialog.xaml.cs       ← Task 2: Logic ảnh 2D + Ctrl+V
│   └── IssueManagerWindow.xaml.cs      ← Task 5: Truyền Image2DPath sang handler
├── Handlers/
│   └── CreateIssueHandler.cs           ← Task 3: Thêm property Image2DPath
└── Services/
    └── RevitIssueCreator.cs            ← Task 4: Gán SnapshotFilePath2
```

---

## 📋 Task chi tiết

### ✅ Task 1 [S] — Redesign `CreateIssueDialog.xaml`

**File:** `src/Antigravity.IssueManager/UI/CreateIssueDialog.xaml`

**Thay đổi:**
- Tăng `Width` từ `480` → `860`
- Tăng `Height` từ `340` → `560`
- Thêm `MinWidth="720"`, `MinHeight="480"`
- Giữ nguyên: Header (➕ AUTO ISSUE CREATOR @Vilai Viet), Title, Description, 2 nút Create/Cancel

**Thêm row mới chứa 2 cột ảnh side-by-side (Grid 2 columns 1*:1*):**

**Cột trái — Hình ảnh 3D:**
```xml
<Border BorderBrush="#3333AA" BorderThickness="1.5" CornerRadius="4"
        Background="#0D0D4A" Padding="6">
    <!-- Label -->
    <TextBlock Text="Hình ảnh 3D" Foreground="White" FontWeight="SemiBold" .../>
    <!-- Image placeholder -->
    <Image x:Name="ImgPreview3D" Stretch="Uniform" MaxHeight="200"/>
    <!-- Hint text khi chưa có ảnh -->
    <TextBlock x:Name="TxtHint3D"
               Text="(Chụp tự động từ Revit khi bấm Create Issue)"
               Foreground="#8888CC" FontStyle="Italic" TextAlignment="Center"/>
</Border>
```

**Cột phải — Hình ảnh 2D:**
```xml
<Border BorderBrush="#3333AA" BorderThickness="1.5" BorderDashArray="4,2"
        CornerRadius="4" Background="#0D0D4A" Padding="6">
    <!-- Label -->
    <TextBlock Text="Hình ảnh 2D" Foreground="White" FontWeight="SemiBold" .../>
    <!-- Image preview -->
    <Image x:Name="ImgPreview2D" Stretch="Uniform" MaxHeight="200"/>
    <!-- Hint text -->
    <TextBlock x:Name="TxtHint2D"
               Text="Ctrl+V để dán ảnh, hoặc bấm 📁 để chọn file"
               Foreground="#8888CC" FontStyle="Italic" TextAlignment="Center"/>
    <!-- Buttons row -->
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
        <Button x:Name="BtnPickImage2D" Content="📁 Chọn ảnh..."
                Click="BtnPickImage2D_Click" .../>
        <Button x:Name="BtnClearImage2D" Content="✖ Xóa ảnh"
                Click="BtnClearImage2D_Click" Visibility="Collapsed" .../>
    </StackPanel>
</Border>
```

**Border style dashed:** Dùng `Border` + custom `BorderDashArray` hoặc vẽ dashed bằng `Rectangle` với `StrokeDashArray`.

---

### ✅ Task 2 [S] — Cập nhật `CreateIssueDialog.xaml.cs`

**File:** `src/Antigravity.IssueManager/UI/CreateIssueDialog.xaml.cs`

**Thêm property:**
```csharp
public string Image2DPath { get; private set; }
```

**Handler chọn file:**
```csharp
private void BtnPickImage2D_Click(object sender, RoutedEventArgs e)
{
    var dlg = new Microsoft.Win32.OpenFileDialog
    {
        Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
        Title = "Chọn hình ảnh 2D"
    };
    if (dlg.ShowDialog() == true)
    {
        LoadImage2DPreview(dlg.FileName);
    }
}
```

**Handler Ctrl+V (clipboard paste):**
```csharp
// Trong constructor: thêm CommandBinding cho ApplicationCommands.Paste
this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPasteExecuted));

private void OnPasteExecuted(object sender, ExecutedRoutedEventArgs e)
{
    if (Clipboard.ContainsImage())
    {
        BitmapSource bitmapSource = Clipboard.GetImage();
        string tempPath = SaveBitmapSourceToTemp(bitmapSource);
        if (tempPath != null)
            LoadImage2DPreview(tempPath);
    }
    else if (Clipboard.ContainsFileDropList())
    {
        // Xử lý trường hợp copy file ảnh từ Explorer rồi Ctrl+V
        var files = Clipboard.GetFileDropList();
        foreach (string file in files)
        {
            string ext = System.IO.Path.GetExtension(file).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
            {
                LoadImage2DPreview(file);
                break;
            }
        }
    }
}

private static string SaveBitmapSourceToTemp(BitmapSource source)
{
    try
    {
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AntigravityIssueManager");
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(stream);
        }
        return path;
    }
    catch { return null; }
}

private void LoadImage2DPreview(string path)
{
    try
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();

        ImgPreview2D.Source = bitmap;
        TxtHint2D.Visibility = Visibility.Collapsed;
        BtnClearImage2D.Visibility = Visibility.Visible;
        Image2DPath = path;
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Không thể tải ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

private void BtnClearImage2D_Click(object sender, RoutedEventArgs e)
{
    ImgPreview2D.Source = null;
    TxtHint2D.Visibility = Visibility.Visible;
    BtnClearImage2D.Visibility = Visibility.Collapsed;
    Image2DPath = null;
}
```

**Namespaces cần thêm:**
```csharp
using System.Windows.Input;
using System.Windows.Media.Imaging;
```

---

### ✅ Task 3 [XS] — Thêm `Image2DPath` vào `CreateIssueHandler.cs`

**File:** `src/Antigravity.IssueManager/Handlers/CreateIssueHandler.cs`

**Thêm property:**
```csharp
public string Image2DPath { get; set; }
```

**Sửa lời gọi `Execute`:**
```csharp
IssueModel newIssue = RevitIssueCreator.CreateIssueFromCurrentView(
    app,
    Title,
    Description,
    IncludeSectionBoxClipPlanes,
    UseSharedCoordinates,
    Image2DPath);   // ← thêm dòng này
```

---

### ✅ Task 4 [XS] — Cập nhật `RevitIssueCreator.cs`

**File:** `src/Antigravity.IssueManager/Services/RevitIssueCreator.cs`

**Sửa signature overload chính (line 28-33):**
```csharp
public static IssueModel CreateIssueFromCurrentView(
    UIApplication uiApp,
    string title,
    string description,
    bool includeSectionBoxClipPlanes,
    bool useSharedCoordinates,
    string image2DPath = null)   // ← thêm param optional
```

**Trong block khởi tạo `ViewpointModel` (khoảng line 88-106), thêm:**
```csharp
Viewpoint = new ViewpointModel
{
    // ... các field hiện có giữ nguyên ...
    SnapshotFilePath = snapshotPath,
    SnapshotFilePath2 = image2DPath   // ← thêm dòng này
}
```

> **Lưu ý:** Các overload ngắn hơn (`CreateIssueFromCurrentView(uiApp, title, description)` và `CreateIssueFromCurrentView(uiApp, title, description, includeSectionBoxClipPlanes)`) không cần sửa vì `image2DPath` là optional với default `null`.

---

### ✅ Task 5 [XS] — Cập nhật `BtnCreateIssue_Click` trong `IssueManagerWindow.xaml.cs`

**File:** `src/Antigravity.IssueManager/UI/IssueManagerWindow.xaml.cs`

**Tìm method `BtnCreateIssue_Click` (khoảng line 635-646), sửa thành:**
```csharp
private void BtnCreateIssue_Click(object sender, RoutedEventArgs e)
{
    CreateIssueDialog dialog = new CreateIssueDialog();
    dialog.Owner = this;
    if (dialog.ShowDialog() != true) return;

    _createIssueHandler.Title = dialog.IssueTitle;
    _createIssueHandler.Description = dialog.IssueDescription;
    _createIssueHandler.Image2DPath = dialog.Image2DPath;   // ← thêm dòng này
    _createIssueHandler.IncludeSectionBoxClipPlanes = ChkIncludeSectionBox.IsChecked == true;
    _createIssueHandler.UseSharedCoordinates = ChkUseSharedCoordinates.IsChecked == true;
    _createIssueEvent.Raise();
}
```

---

## ⚠️ Lưu ý khi implement

1. **WPF dashed border**: `Border` chuẩn không hỗ trợ `StrokeDashArray`. Dùng `Rectangle` với `Stroke` + `StrokeDashArray` đặt bên trong panel để tạo viền nét đứt, hoặc dùng `Border` thường với style khác biệt (màu nhạt hơn).

2. **Clipboard paste scope**: `CommandBinding` cho `ApplicationCommands.Paste` đặt trên Window sẽ bắt Ctrl+V toàn bộ dialog. Đảm bảo không conflict với TextBox (TextBox tự handle Ctrl+V nội bộ — không cần lo, `CommandBinding` trên Window chỉ trigger khi focus không nằm trong TextBox).

3. **Temp file ảnh clipboard**: File PNG tạm lưu vào `%TEMP%\AntigravityIssueManager\` — cùng thư mục với snapshots khác của project. Không cần dọn dẹp thủ công vì OS sẽ tự xóa temp.

4. **`RestoreSnapshotsFromStorage`** (line 424 trong `IssueManagerWindow.xaml.cs`): Method này gọi `RestoreSnapshotFile(viewpoint.SnapshotBase642, ...)` — nếu `SnapshotBase642` là null/empty thì trả về `existingPath` (đã có guard). Ảnh 2D do user chọn file local sẽ không có Base64 nên không bị overwrite. ✅

---

## 🧪 Verification

1. Build project — không có compile error
2. Deploy vào Revit, mở Issue Manager
3. Bấm **Create Issue** → cửa sổ mới hiện ra với layout 2 cột ảnh
4. Panel **Hình ảnh 3D**: hiện placeholder text "(Chụp tự động từ Revit khi bấm Create Issue)"
5. Panel **Hình ảnh 2D**:
   - Bấm **📁 Chọn ảnh...** → browse file → ảnh hiển thị preview ✅
   - Ctrl+V sau khi chụp Snipping Tool → ảnh hiển thị preview ✅
   - Bấm **✖ Xóa ảnh** → panel reset về hint text ✅
6. Bấm **Create Issue** → issue tạo thành công, `SnapshotFilePath2` có giá trị
7. Chọn issue trong list → panel Hình ảnh 2D bên phải `IssueManagerWindow` hiển thị ảnh 2D ✅
