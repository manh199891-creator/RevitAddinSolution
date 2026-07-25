using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace LOQN1_Location_element
{
    /// <summary>
    /// Triển khai IExternalApplication để tự động tạo Ribbon Tab và PushButton với Logo cho Revit 2024.
    /// An toàn tuyệt đối: Không throw Exception làm crash Ribbon Revit.
    /// </summary>
    public class App : IExternalApplication
    {
        private const string TAB_NAME = "VILAIVIET Tools";
        private const string PANEL_NAME = "Location Tools";
        private const string BUTTON_NAME = "VILAIVIET-LOQN1-Location";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // 1. Tạo Ribbon Tab mới "VILAIVIET Tools"
                try
                {
                    application.CreateRibbonTab(TAB_NAME);
                }
                catch
                {
                    // Tab đã tồn tại từ trước (bỏ qua)
                }

                // 2. Lấy hoặc tạo mới Ribbon Panel
                RibbonPanel panel = null;
                try
                {
                    List<RibbonPanel> panels = application.GetRibbonPanels(TAB_NAME);
                    foreach (RibbonPanel p in panels)
                    {
                        if (p.Name == PANEL_NAME)
                        {
                            panel = p;
                            break;
                        }
                    }
                }
                catch
                {
                }

                if (panel == null)
                {
                    try
                    {
                        panel = application.CreateRibbonPanel(TAB_NAME, PANEL_NAME);
                    }
                    catch
                    {
                        panel = application.CreateRibbonPanel(PANEL_NAME);
                    }
                }

                // 3. Tạo dữ liệu PushButton "VILAIVIET-LOQN1-Location"
                string thisAssemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                PushButtonData buttonData = new PushButtonData(
                    "btn_VILAIVIET_LOQN1_Location",
                    BUTTON_NAME,
                    thisAssemblyPath,
                    "LOQN1_Location_element.Command"
                );

                buttonData.ToolTip = "Tính toán vị trí đối tượng (Level, Grid X, Grid Y, Offset) và ghi vào Shared Parameter LOQN1_Location.";
                buttonData.LongDescription = "Hỗ trợ đa dạng các loại đối tượng Point-based, Curve-based, Surface/Solid-based, MEP, Cột, Dầm, Sàn, Vách...";

                // 4. Nạp Logo Image cho Ribbon Button
                try
                {
                    // Lấy đường dẫn gốc của DLL (dùng CodeBase để không bị lỗi khi Revit Shadow Copy vào thư mục Temp)
                    string codeBase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                    Uri uri = new Uri(codeBase);
                    string originalDllPath = uri.LocalPath;
                    string dllDir = Path.GetDirectoryName(originalDllPath);
                    
                    string logoPath = Path.Combine(dllDir, "VILAIVIET-Logo.png");

                    // Fallback: thử tìm logo tên cũ nếu không có
                    if (!File.Exists(logoPath))
                    {
                        logoPath = Path.Combine(dllDir, "VILAIVIET-LOGO-01-Vientrang-dày.png");
                    }

                    if (File.Exists(logoPath))
                    {
                        // Large Image (chiều cao 32, tự động tính chiều rộng để không bị méo)
                        BitmapImage largeImage = new BitmapImage();
                        largeImage.BeginInit();
                        largeImage.UriSource = new Uri(logoPath, UriKind.Absolute);
                        largeImage.DecodePixelHeight = 32;
                        largeImage.CacheOption = BitmapCacheOption.OnLoad;
                        largeImage.EndInit();

                        // Small Image (chiều cao 16, tự động tính chiều rộng)
                        BitmapImage smallImage = new BitmapImage();
                        smallImage.BeginInit();
                        smallImage.UriSource = new Uri(logoPath, UriKind.Absolute);
                        smallImage.DecodePixelHeight = 16;
                        smallImage.CacheOption = BitmapCacheOption.OnLoad;
                        smallImage.EndInit();

                        buttonData.LargeImage = largeImage;
                        buttonData.Image = smallImage;
                    }
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Lỗi Load Logo", "Không thể load logo:\n" + ex.Message);
                }

                // 5. Thêm Button vào Panel
                panel.AddItem(buttonData);

                return Result.Succeeded;
            }
            catch (Exception)
            {
                // Trả về Succeeded để không hiện thông báo "External Tool Failure" của Revit
                return Result.Succeeded;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
