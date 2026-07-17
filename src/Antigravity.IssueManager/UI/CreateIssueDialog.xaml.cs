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
                if (!string.IsNullOrEmpty(issue.Viewpoint.SnapshotFilePath2) && System.IO.File.Exists(issue.Viewpoint.SnapshotFilePath2))
                {
                    LoadImage2DPreview(issue.Viewpoint.SnapshotFilePath2);
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
                Title = "Chọn hình ảnh 2D"
            };
            if (dlg.ShowDialog() == true)
            {
                LoadImage2DPreview(dlg.FileName);
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
                            foreach (string file in files)
                            {
                                if (IsSupportedImageFile(file))
                                {
                                    LoadImage2DPreview(file);
                                    return true;
                                }
                            }
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
                                LoadImage2DPreview(trimmed);
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
                                    LoadImage2DPreview(tempPath);
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
                                LoadImage2DPreview(tempPath);
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
                                LoadImage2DPreview(tempPath);
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
                else if (compression == 3) // BI_BITFIELDS
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
                BtnMarkup2D.IsEnabled = true;
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
            BtnMarkup2D.IsEnabled = false;
            Image2DPath = null;
        }

        private void BtnMarkup2D_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Image2DPath)) return;

            var editor = new MarkupEditorWindow(Image2DPath) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                LoadImage2DPreview(editor.ResultImagePath);
            }
        }
    }
}
