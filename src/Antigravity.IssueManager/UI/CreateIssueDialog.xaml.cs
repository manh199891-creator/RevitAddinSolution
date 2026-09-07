using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Antigravity.IssueManager.UI
{
    public partial class CreateIssueDialog : Window
    {
        public string IssueTitle { get; private set; }
        public string IssueLevel { get; private set; }
        public string IssueAssignedTo { get; private set; }
        public string IssueDescription { get; private set; }
        public string Image3DPath { get; private set; }
        public string Image2DPath { get; private set; }
        private string _preview2DPath;

        public Action OnCapture3DRequested { get; set; }
        public Action<CreateIssueDialog> OnCreateRequested { get; set; }
        public Action<CreateIssueDialog> OnSaveRequested { get; set; }
        public bool IsEditMode { get; private set; }

        public void LoadIssueData(Models.IssueModel issue)
        {
            if (issue == null) return;

            IsEditMode = true;
            Title = "Edit Issue";

            if (BtnOk != null)
            {
                BtnOk.Content = "💾 Save Changes";
            }

            TxtIssueTitle.Text = issue.Title;
            TxtIssueLevel.Text = issue.Level;
            TxtIssueAssignedTo.Text = issue.AssignedTo;
            TxtIssueDesc.Text = issue.Description;

            if (issue.Viewpoint != null)
            {
                if (!string.IsNullOrEmpty(issue.Viewpoint.SnapshotFilePath) && System.IO.File.Exists(issue.Viewpoint.SnapshotFilePath))
                {
                    LoadImage3DPreview(issue.Viewpoint.SnapshotFilePath);
                }
                if (!string.IsNullOrEmpty(issue.Viewpoint.SnapshotFilePath2))
                {
                    var paths = issue.Viewpoint.SnapshotFilePath2.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in paths)
                    {
                        if (System.IO.File.Exists(p))
                        {
                            LoadImage2DPreview(p, append: true);
                        }
                    }
                }
            }
        }

        public CreateIssueDialog()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPasteExecuted));
            PreviewKeyDown += CreateIssueDialog_PreviewKeyDown;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string title = TxtIssueTitle.Text?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Title is required.", IsEditMode ? "Edit Issue" : "Create Issue", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtIssueTitle.Focus();
                return;
            }

            IssueTitle = title;
            IssueLevel = TxtIssueLevel.Text?.Trim() ?? string.Empty;
            IssueAssignedTo = TxtIssueAssignedTo.Text?.Trim() ?? string.Empty;
            IssueDescription = TxtIssueDesc.Text?.Trim() ?? string.Empty;
            
            if (IsEditMode)
            {
                OnSaveRequested?.Invoke(this);
            }
            else
            {
                OnCreateRequested?.Invoke(this);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadImage3DPreview(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                ImgPreview3D.Source = bitmap;
                TxtHint3D.Visibility = Visibility.Collapsed;
                Image3DPath = path;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải ảnh 3D: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void HandleCaptured3DView(string snapshotPath, string levelName)
        {
            try
            {
                if (string.IsNullOrEmpty(snapshotPath) || !System.IO.File.Exists(snapshotPath))
                {
                    MessageBox.Show("Không tìm thấy file ảnh chụp 3D từ Revit.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var editor = new MarkupEditorWindow(snapshotPath) { Owner = this };
                if (editor.ShowDialog() == true)
                {
                    LoadImage3DPreview(editor.ResultImagePath);
                    if (!string.IsNullOrEmpty(levelName) && string.IsNullOrEmpty(TxtIssueLevel.Text))
                    {
                        TxtIssueLevel.Text = levelName;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi mở MarkupEditorWindow:\n{ex}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMarkup3D_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Image3DPath) && System.IO.File.Exists(Image3DPath))
            {
                MessageBoxResult result = MessageBox.Show(
                    "Bạn muốn chỉnh sửa ảnh 3D hiện tại (Yes) hay chụp góc nhìn mới từ Revit (No)?",
                    "Markup 3D",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var editor = new MarkupEditorWindow(Image3DPath) { Owner = this };
                    if (editor.ShowDialog() == true)
                    {
                        LoadImage3DPreview(editor.ResultImagePath);
                    }
                }
                else if (result == MessageBoxResult.No)
                {
                    OnCapture3DRequested?.Invoke();
                }
            }
            else
            {
                OnCapture3DRequested?.Invoke();
            }
        }

        private void BtnPickImage2D_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Chọn hình ảnh 2D",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames)
                {
                    LoadImage2DPreview(file, append: true);
                }
            }
        }

        private void OnPasteExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = TryLoadImage2DFromClipboard();
        }

        private void CreateIssueDialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isPasteShortcut = e.Key == Key.V &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (!isPasteShortcut)
            {
                return;
            }

            e.Handled = TryLoadImage2DFromClipboard();
        }

        private bool TryLoadImage2DFromClipboard()
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    IDataObject dataObject = Clipboard.GetDataObject();
                    if (dataObject == null) return false;

                    // 1. Check for Copied Files
                    if (dataObject.GetDataPresent(DataFormats.FileDrop))
                    {
                        string[] files = (string[])dataObject.GetData(DataFormats.FileDrop);
                        if (files != null)
                        {
                            bool loadedAny = false;
                            foreach (string file in files)
                            {
                                if (IsSupportedImageFile(file))
                                {
                                    LoadImage2DPreview(file, append: true);
                                    loadedAny = true;
                                }
                            }
                            if (loadedAny) return true;
                        }
                    }

                    // 2. Check for Text containing an Image Path
                    if (dataObject.GetDataPresent(DataFormats.Text))
                    {
                        string text = dataObject.GetData(DataFormats.Text) as string;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            string trimmed = text.Trim().Trim('"'); // remove potential quotes
                            if (IsSupportedImageFile(trimmed))
                            {
                                LoadImage2DPreview(trimmed, append: true);
                                return true;
                            }
                        }
                    }

                    // Check for DeviceIndependentBitmap (DIB)
                    if (dataObject.GetDataPresent("DeviceIndependentBitmap"))
                    {
                        var stream = dataObject.GetData("DeviceIndependentBitmap") as System.IO.MemoryStream;
                        if (stream != null)
                        {
                            var bitmapSource = ConvertDibToBitmapSource(stream.ToArray());
                            if (bitmapSource != null)
                            {
                                string tempPath = SaveBitmapSourceToTemp(bitmapSource);
                                if (tempPath != null)
                                {
                                    LoadImage2DPreview(tempPath, append: true);
                                    return true;
                                }
                            }
                        }
                    }

                    // 3. Check for Bitmap Images
                    if (dataObject.GetDataPresent(DataFormats.Bitmap))
                    {
                        BitmapSource bitmapSource = dataObject.GetData(DataFormats.Bitmap) as BitmapSource;
                        if (bitmapSource != null)
                        {
                            string tempPath = SaveBitmapSourceToTemp(bitmapSource);
                            if (tempPath != null)
                            {
                                LoadImage2DPreview(tempPath, append: true);
                                return true;
                            }
                        }
                    }

                    // 4. Fallback GetImage
                    if (Clipboard.ContainsImage())
                    {
                        BitmapSource bitmapSource = Clipboard.GetImage();
                        if (bitmapSource != null)
                        {
                            string tempPath = SaveBitmapSourceToTemp(bitmapSource);
                            if (tempPath != null)
                            {
                                LoadImage2DPreview(tempPath, append: true);
                                return true;
                            }
                        }
                    }

                    break;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    System.Threading.Thread.Sleep(50);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Clipboard error: " + ex.Message);
                    break;
                }
            }

            return false;
        }

        private static bool IsSupportedImageFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || !System.IO.File.Exists(file))
            {
                return false;
            }

            string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp";
        }

        private static string SaveBitmapSourceToTemp(BitmapSource source)
        {
            if (source == null) return null;

            try
            {
                string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AntigravityIssueManager");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "clipboard_" + Guid.NewGuid().ToString("N") + ".png");

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

        private static BitmapSource ConvertDibToBitmapSource(byte[] dibBytes)
        {
            if (dibBytes == null || dibBytes.Length < 40) return null;

            try
            {
                int headerSize = BitConverter.ToInt32(dibBytes, 0);
                int width = BitConverter.ToInt32(dibBytes, 4);
                int height = BitConverter.ToInt32(dibBytes, 8);
                ushort planes = BitConverter.ToUInt16(dibBytes, 12);
                ushort bitCount = BitConverter.ToUInt16(dibBytes, 14);
                int compression = BitConverter.ToInt32(dibBytes, 16);
                int clrUsed = BitConverter.ToInt32(dibBytes, 32);

                int colorTableSize = 0;
                if (bitCount <= 8)
                {
                    colorTableSize = (clrUsed > 0 ? clrUsed : (1 << bitCount)) * 4;
                }
                else if (compression == 3 && headerSize == 40) // BI_BITFIELDS with BITMAPINFOHEADER
                {
                    colorTableSize = 12;
                }

                int pixelOffset = 14 + headerSize + colorTableSize;
                int fileSize = 14 + dibBytes.Length;

                byte[] bmpBytes = new byte[fileSize];
                bmpBytes[0] = 0x42; // 'B'
                bmpBytes[1] = 0x4D; // 'M'

                byte[] sizeBytes = BitConverter.GetBytes(fileSize);
                Array.Copy(sizeBytes, 0, bmpBytes, 2, 4);

                bmpBytes[6] = 0;
                bmpBytes[7] = 0;
                bmpBytes[8] = 0;
                bmpBytes[9] = 0;

                byte[] offsetBytes = BitConverter.GetBytes(pixelOffset);
                Array.Copy(offsetBytes, 0, bmpBytes, 10, 4);

                Array.Copy(dibBytes, 0, bmpBytes, 14, dibBytes.Length);

                using (var ms = new System.IO.MemoryStream(bmpBytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        private void BorderImage2D_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BorderImage2D.Focus();
            e.Handled = true;
        }

        private string AppendImagesHorizontally(string img1Path, string img2Path)
        {
            try
            {
                var bmp1 = LoadBitmapSafely(img1Path);
                var bmp2 = LoadBitmapSafely(img2Path);

                if (bmp1 == null || bmp2 == null) return img2Path;

                int gap = 20;
                int width = bmp1.PixelWidth + bmp2.PixelWidth + gap;
                int height = Math.Max(bmp1.PixelHeight, bmp2.PixelHeight);

                var visual = new System.Windows.Media.DrawingVisual();
                using (var ctx = visual.RenderOpen())
                {
                    ctx.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, width, height));

                    double y1 = (height - bmp1.PixelHeight) / 2.0;
                    ctx.DrawImage(bmp1, new Rect(0, y1, bmp1.PixelWidth, bmp1.PixelHeight));

                    double y2 = (height - bmp2.PixelHeight) / 2.0;
                    ctx.DrawImage(bmp2, new Rect(bmp1.PixelWidth + gap, y2, bmp2.PixelWidth, bmp2.PixelHeight));
                }

                var rtb = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(visual);

                string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AntigravityIssueManager");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "clipboard_" + Guid.NewGuid().ToString("N") + ".png");

                using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    encoder.Save(stream);
                }

                return path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to append images: " + ex.Message);
                return img2Path;
            }
        }

        private BitmapImage LoadBitmapSafely(string path)
        {
            if (!System.IO.File.Exists(path)) return null;
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private void LoadImage2DPreview(string path, bool append = false)
        {
            try
            {
                string newRaw = path;
                if (append && !string.IsNullOrEmpty(_preview2DPath) && System.IO.File.Exists(_preview2DPath))
                {
                    path = AppendImagesHorizontally(_preview2DPath, path);
                    newRaw = (string.IsNullOrEmpty(Image2DPath) ? _preview2DPath : Image2DPath) + "|" + newRaw;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                ImgPreview2D.Source = bitmap;
                TxtHint2D.Visibility = Visibility.Collapsed;
                BtnClearImage2D.Visibility = Visibility.Visible;
                BtnMarkup2D.IsEnabled = true;
                _preview2DPath = path;
                Image2DPath = newRaw;
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
            BtnMarkup2D.IsEnabled = false;
            Image2DPath = null;
            _preview2DPath = null;
        }

        private void BtnMarkup2D_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_preview2DPath)) return;

            var editor = new MarkupEditorWindow(_preview2DPath) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                Image2DPath = null;
                _preview2DPath = null;
                LoadImage2DPreview(editor.ResultImagePath);
            }
        }
    }
}
