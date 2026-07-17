using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace Antigravity.IssueManager
{
    public class App : IExternalApplication
    {
        static App()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name;
            if (!assemblyName.StartsWith("Antigravity.", StringComparison.OrdinalIgnoreCase) && 
                !assemblyName.StartsWith("Serilog", StringComparison.OrdinalIgnoreCase))
                return null;

            string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string candidate = Path.Combine(baseDir, assemblyName + ".dll");
            if (File.Exists(candidate))
                return Assembly.LoadFrom(candidate);

            return null;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            // 1. Create Ribbon Tab
            string tabName = "VILAIVIET";
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                // Tab might already exist
            }

            // 2. Create Panel
            RibbonPanel clashPanel = null;
            foreach (var panel in application.GetRibbonPanels(tabName))
            {
                if (panel.Name == "KIỂM SOÁT XUNG ĐỘT")
                {
                    clashPanel = panel;
                    break;
                }
            }

            if (clashPanel == null)
            {
                clashPanel = application.CreateRibbonPanel(tabName, "KIỂM SOÁT XUNG ĐỘT");
            }

            // 3. Create Button
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            BitmapImage logoImage = GetEmbeddedImage("icon_logo_32.png");

            PushButtonData btnIssueManager = new PushButtonData(
                "btnIssueManager",
                "Quản Lý\nLỗi BIM",
                assemblyPath,
                "Antigravity.IssueManager.Commands.CmdOpenIssueManager");

            btnIssueManager.ToolTip = "Đồng bộ lỗi từ Navisworks thông qua file XML hoặc BCFzip.";
            if (logoImage != null)
            {
                btnIssueManager.LargeImage = logoImage;
            }

            try
            {
                clashPanel.AddItem(btnIssueManager);
            }
            catch (Exception ex)
            {
                // Button might already exist or panel is already populated by the main add-in
                System.Diagnostics.Debug.WriteLine($"Failed to add BIM Issue Manager button: {ex.Message}");
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private BitmapImage GetEmbeddedImage(string imageName)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = $"Antigravity.IssueManager.Resources.{imageName}";
                
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = stream;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    return image;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
