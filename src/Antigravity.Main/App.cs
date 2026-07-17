using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.Core.Services;

namespace Antigravity.Main
{
    public class App : IExternalApplication
    {
        private UIControlledApplication _uiControlledApplication;
        private DoorClearanceBox.Updaters.DoorChangeUpdater _doorClearanceUpdater;

        static App()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAntigravityAssembly;
        }

        private static Assembly ResolveAntigravityAssembly(object sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name;
            if (!assemblyName.StartsWith("Antigravity.", StringComparison.OrdinalIgnoreCase))
                return null;

            string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string candidate = Path.Combine(baseDir, assemblyName + ".dll");
            if (File.Exists(candidate))
                return Assembly.LoadFrom(candidate);

            return null;
        }

        private BitmapImage GetEmbeddedImage(string imageName)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = $"Antigravity.Main.Resources.{imageName}";
                
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

            // 2. Create Panels
            RibbonPanel structuralPanel = application.CreateRibbonPanel(tabName, "DỰNG HÌNH");

            // 3. Add Buttons
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            BitmapImage logoImage = GetEmbeddedImage("icon_logo_32.png");

            PushButtonData btnDrawColumns = new PushButtonData(
                "btnDrawColumns", 
                "Vẽ Cột", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.DrawColumns.dll"), 
                "Antigravity.DrawColumns.CreateColumnCommand");
            
            btnDrawColumns.ToolTip = "Tự động vẽ cột từ bản vẽ CAD";
            btnDrawColumns.LargeImage = logoImage;
            
            PushButtonData btnDrawBeams = new PushButtonData(
                "btnDrawBeams", 
                "Vẽ Dầm", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.DrawBeams.dll"), 
                "Antigravity.DrawBeams.CreateBeamCommand");

            btnDrawBeams.ToolTip = "Tự động vẽ dầm từ bản vẽ CAD";
            btnDrawBeams.LargeImage = logoImage;

            PushButtonData btnDrawWalls = new PushButtonData(
                "btnDrawWalls", 
                "Vẽ Vách", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.DrawWalls.dll"), 
                "Antigravity.DrawWalls.CreateWallCommand");

            btnDrawWalls.ToolTip = "Tự động vẽ vách từ bản vẽ CAD";
            btnDrawWalls.LargeImage = logoImage;

            PushButtonData btnDrawFloors = new PushButtonData(
                "btnDrawFloors", 
                "Vẽ Sàn", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.DrawFloors.dll"), 
                "Antigravity.DrawFloors.CreateFloorCommand");

            btnDrawFloors.ToolTip = "Tự động vẽ sàn từ bản vẽ CAD";
            btnDrawFloors.LargeImage = logoImage;

            PushButtonData btnCadSleevePlacer = new PushButtonData(
                "btnCadSleevePlacer",
                "Đặt Sleeve\nDầm Vách",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.CadSleevePlacer.dll"),
                "Antigravity.CadSleevePlacer.AppCommand");

            btnCadSleevePlacer.ToolTip = "Tự động đặt Family Sleeve Generic Model cho Dầm/Vách dựa trên dữ liệu AutoCAD (Leader, Text).";
            btnCadSleevePlacer.LargeImage = logoImage;

            PushButtonData btnDoorClearance = new PushButtonData(
                "btnDoorClearance",
                "Khoảng Mở\nCửa",
                assemblyPath.Replace("Antigravity.Main.dll", "DoorClearanceBox.dll"),
                "DoorClearanceBox.Commands.CreateClearanceBoxCommand");
            
            btnDoorClearance.ToolTip = "Tự động tạo khối không gian (Clearance Box) cho Cửa đi và Cửa sổ.";
            btnDoorClearance.LargeImage = logoImage;


            structuralPanel.AddItem(btnDrawColumns);
            structuralPanel.AddItem(btnDrawBeams);
            structuralPanel.AddItem(btnDrawWalls);
            structuralPanel.AddItem(btnDrawFloors);

            // --- KIẾN TRÚC ---
            PushButtonData btnArchWall = new PushButtonData(
                "btnArchWall", 
                "Vẽ Tường KT", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ArchModeling.dll"), 
                "Antigravity.ArchModeling.Commands.DrawWallFromCadCommand");
            btnArchWall.ToolTip = "Tự động vẽ tường kiến trúc từ layer AutoCAD";
            btnArchWall.LargeImage = logoImage;

            PushButtonData btnArchWallDoor = new PushButtonData(
                "btnArchWallDoor", 
                "Tường & Cửa", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ArchModeling.dll"), 
                "Antigravity.ArchModeling.Commands.DrawWallsAndDoorsFromCadCommand");
            btnArchWallDoor.ToolTip = "Tự động vẽ tường và đặt cửa cùng lúc từ AutoCAD";
            btnArchWallDoor.LargeImage = logoImage;

            PushButtonData btnArchFloor = new PushButtonData(
                "btnArchFloor", 
                "Vẽ Sàn HT", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ArchModeling.dll"), 
                "Antigravity.ArchModeling.Commands.DrawFloorCeilFromCadCommand");
            btnArchFloor.ToolTip = "Tự động vẽ sàn/trần hoàn thiện từ Hatch AutoCAD";
            btnArchFloor.LargeImage = logoImage;

            PushButtonData btnArchDoor = new PushButtonData(
                "btnArchDoor", 
                "Đặt Cửa", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ArchModeling.dll"), 
                "Antigravity.ArchModeling.Commands.PlaceDoorFromCadCommand");
            btnArchDoor.ToolTip = "Tự động đặt cửa đi/cửa sổ từ Block AutoCAD";
            btnArchDoor.LargeImage = logoImage;

            structuralPanel.AddItem(btnArchWall);
            structuralPanel.AddItem(btnArchWallDoor);
            structuralPanel.AddItem(btnArchFloor);
            structuralPanel.AddItem(btnArchDoor);

            // --- Panel Kiểm soát khối lượng ---
            RibbonPanel utilityPanel = application.CreateRibbonPanel(tabName, "KIỂM SOÁT KHỐI LƯỢNG");

#if DEBUG
            SplitButtonData sbAutoJoinData = new SplitButtonData("splitAutoJoin", "Auto Join");
            SplitButton sbAutoJoin = utilityPanel.AddItem(sbAutoJoinData) as SplitButton;

            PushButtonData btnAutojoin = new PushButtonData(
                "btnAutojoin", 
                "Auto Join", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.Autojoin.dll"), 
                "Antigravity.Autojoin.AutoJoinCommand");
            btnAutojoin.ToolTip = "Tự động kết nối (Join) các phần tử kết cấu theo quy tắc";
            btnAutojoin.LargeImage = logoImage;
            sbAutoJoin.AddPushButton(btnAutojoin);

            PushButtonData btnAutojoinTest = new PushButtonData(
                "btnAutojoinTest",
                "AJ Test",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.Autojoin.dll"),
                "Antigravity.Autojoin.AutoJoinIntegrationTestCommand");
            btnAutojoinTest.ToolTip = "Run AutoJoin integration test on one selected beam and one selected floor";
            btnAutojoinTest.LargeImage = logoImage;
            sbAutoJoin.AddPushButton(btnAutojoinTest);
#else
            PushButtonData btnAutojoin = new PushButtonData(
                "btnAutojoin", 
                "Auto Join", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.Autojoin.dll"), 
                "Antigravity.Autojoin.AutoJoinCommand");

            btnAutojoin.ToolTip = "Tự động kết nối (Join) các phần tử kết cấu theo quy tắc";
            btnAutojoin.LargeImage = logoImage;
            utilityPanel.AddItem(btnAutojoin);
#endif

            PushButtonData btnZoneSplit = new PushButtonData(
                "btnZoneSplit", 
                "Chia Zone", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ZoneSplit.dll"), 
                "Antigravity.ZoneSplit.Commands.ZoneProcessCommand");

            btnZoneSplit.ToolTip = "Phân loại cấu kiện theo Generic Model zone volume và xuất báo cáo Markdown.";
            btnZoneSplit.LargeImage = logoImage;
            utilityPanel.AddItem(btnZoneSplit);

            PushButtonData btnZoneExport = new PushButtonData(
                "btnZoneExport", 
                "Xuất Navis", 
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ZoneSplit.dll"), 
                "Antigravity.ZoneSplit.Commands.ExportCommand");

            btnZoneExport.ToolTip = "Xuất ZoneID selection sets sang file XML cho Navisworks.";
            btnZoneExport.LargeImage = logoImage;
            utilityPanel.AddItem(btnZoneExport);

            // --- Panel Kiểm soát xung đột ---
            RibbonPanel clashPanel = application.CreateRibbonPanel(tabName, "KIỂM SOÁT XUNG ĐỘT");

            PushButtonData btnIssueManager = new PushButtonData(
                "btnIssueManager",
                "Quản Lý\nLỗi BIM",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.IssueManager.dll"),
                "Antigravity.IssueManager.Commands.CmdOpenIssueManager");

            btnIssueManager.ToolTip = "Đồng bộ lỗi từ Navisworks thông qua file XML hoặc BCFzip.";
            btnIssueManager.LargeImage = logoImage;

            PushButtonData btnClashControl = new PushButtonData(
                "btnClashControl",
                "Kiểm Soát\nXung Đột",
                assemblyPath.Replace("Antigravity.Main.dll", "DoorClearanceBox.dll"),
                "DoorClearanceBox.Commands.ClashControlCommand");

            btnClashControl.ToolTip = "Phát hiện và hiển thị các cấu kiện kết cấu (Cột, Dầm, Sàn, Tường) xung đột với khoảng mở cửa.";
            btnClashControl.LargeImage = logoImage;

            PushButtonData btnCheckFloorElevation = new PushButtonData(
                "btnCheckFloorElevation",
                "Kiem Tra\nCao Do San",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.CheckFloorElevation.dll"),
                "Antigravity.CheckFloorElevation.CheckFloorElevationCommand");

            btnCheckFloorElevation.ToolTip = "Check floor top elevation delta between host architectural floors and linked structural floors.";
            btnCheckFloorElevation.LargeImage = logoImage;

            PushButtonData btnWallMepClash = new PushButtonData(
                "btnWallMepClash",
                "Tường\nMEP Clash",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.WallMepClash.dll"),
                "Antigravity.WallMepClash.WallMepClashCommand");
            btnWallMepClash.ToolTip = "Phát hiện va chạm song song giữa Tường host và đường ống/thiết bị MEP từ file link.";
            btnWallMepClash.LargeImage = logoImage;

            clashPanel.AddItem(btnCadSleevePlacer);
            clashPanel.AddItem(btnCheckFloorElevation);
            clashPanel.AddItem(btnWallMepClash);
            clashPanel.AddItem(btnIssueManager);
            clashPanel.AddItem(btnClashControl);
            clashPanel.AddItem(btnDoorClearance);

            // --- Panel Trình bày ---
            RibbonPanel docPanel = application.CreateRibbonPanel(tabName, "TRÌNH BÀY");

            PushButtonData btnAutoDimWalls = new PushButtonData(
                "btnAutoDimWalls",
                "AutoDim\nWalls",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.AutoDimWalls.dll"),
                "Antigravity.AutoDimWalls.AutoDimCommand");
            
            btnAutoDimWalls.ToolTip = "Tự động đo kích thước cho các bức tường kiến trúc thẳng trong View hiện tại.";
            btnAutoDimWalls.LargeImage = logoImage;
            docPanel.AddItem(btnAutoDimWalls);

            PushButtonData btnTagArranger = new PushButtonData(
                "btnTagArranger",
                "Sắp Xếp\nTag",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.TagArranger.dll"),
                "Antigravity.TagArranger.TagArrangeCommand");
            
            btnTagArranger.ToolTip = "Tự động căn thẳng hàng, chống đè chữ và giãn đều Tag/Dimension.";
            btnTagArranger.LargeImage = logoImage;
            docPanel.AddItem(btnTagArranger);

            PushButtonData btnHoanThien = new PushButtonData(
                "btnHoanThien",
                "Hoàn Thiện",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.HoanThien.dll"),
                "Antigravity.HoanThien.Commands.HoanThienCommand");
            
            btnHoanThien.ToolTip = "Tự động tạo tường và sàn hoàn thiện dựa trên Room Boundaries.";
            btnHoanThien.LargeImage = logoImage;
            docPanel.AddItem(btnHoanThien);



            // --- Đăng ký Updater cho Autojoin (Realtime) ---
            // Tạm thời vô hiệu hóa theo yêu cầu
            /*
            try { Antigravity.Autojoin.Services.AutoJoinUpdater.Register(application.ActiveAddInId); }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "[Antigravity] AutoJoin DMU register failed");
            }
            */
            // --- Đăng ký Automation Hook ---
            try { RegisterDoorClearanceUpdater(application); }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "[Antigravity] DoorClearance DMU register failed");
            }

#if DEBUG
            _uiControlledApplication = application;
            application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
            application.ViewActivated += OnViewActivated;
#endif

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            if (_uiControlledApplication != null)
            {
                _uiControlledApplication.ControlledApplication.ApplicationInitialized -= OnApplicationInitialized;
                _uiControlledApplication.Idling -= OnIdling;
                _uiControlledApplication.ViewActivated -= OnViewActivated;
            }

            if (_doorClearanceUpdater != null)
            {
                try
                {
                    UpdaterRegistry.UnregisterUpdater(_doorClearanceUpdater.GetUpdaterId());
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"[Antigravity] DoorClearance DMU unregister failed: {ex.Message}");
                }
            }
            return Result.Succeeded;
        }

        private void RegisterDoorClearanceUpdater(UIControlledApplication application)
        {
            _doorClearanceUpdater = new DoorClearanceBox.Updaters.DoorChangeUpdater(application.ActiveAddInId);
            var updaterId = _doorClearanceUpdater.GetUpdaterId();

            if (!UpdaterRegistry.IsUpdaterRegistered(updaterId))
                UpdaterRegistry.RegisterUpdater(_doorClearanceUpdater, true);

            var categories = new List<ElementId>
            {
                new ElementId(BuiltInCategory.OST_Doors),
                new ElementId(BuiltInCategory.OST_Windows),
                new ElementId(BuiltInCategory.OST_CurtainWallPanels)
            };
            var elementFilter = new ElementMulticategoryFilter(categories);

            UpdaterRegistry.AddTrigger(
                updaterId,
                elementFilter,
                Element.GetChangeTypeParameter(new ElementId(BuiltInParameter.FAMILY_WIDTH_PARAM)));

            UpdaterRegistry.AddTrigger(
                updaterId,
                elementFilter,
                Element.GetChangeTypeParameter(new ElementId(BuiltInParameter.FAMILY_HEIGHT_PARAM)));
        }

        private bool _isIdlingHooked = false;

        private void OnApplicationInitialized(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            HookIdlingOnce();
        }

        private void OnViewActivated(object sender, Autodesk.Revit.UI.Events.ViewActivatedEventArgs e)
        {
            HookIdlingOnce();
        }

        private void HookIdlingOnce()
        {
            if (_uiControlledApplication != null && !_isIdlingHooked)
            {
                _isIdlingHooked = true;
                _uiControlledApplication.Idling += OnIdling;
                AutomationLogger.Write("AutomationHook", "Subscribed to Idling.");
            }
        }

        private void OnIdling(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            UIApplication uiapp = sender as UIApplication;
            if (uiapp == null || uiapp.ActiveUIDocument == null || uiapp.ActiveUIDocument.Document == null)
            {
                // Chưa có document được mở, tiếp tục chờ
                return;
            }

            // Đã có document, hủy đăng ký Idling để tránh vòng lặp vô hạn
            _uiControlledApplication.Idling -= OnIdling;

            string triggerFile = @"C:\temp\revit_auto_run.txt";
            if (File.Exists(triggerFile))
            {
                AutomationLogger.Write("AutomationHook", $"Phát hiện file kích hoạt: {triggerFile}. Bắt đầu chạy automation...");
                
                bool shouldExit = false;
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(triggerFile);
                    bool commandFound = false;

                    foreach (string line in lines)
                    {
                        string tLine = line.Trim();
                        if (tLine.Equals("COMMAND: TagArrangerTestCommand", StringComparison.OrdinalIgnoreCase))
                        {
                            Antigravity.Main.Commands.TagArrangerTestCommand.Execute2(uiapp.ActiveUIDocument.Document);
                            commandFound = true;
                        }
                        else if (tLine.Equals("COMMAND: AutonomousTestCommand", StringComparison.OrdinalIgnoreCase))
                        {
                            Antigravity.Main.Commands.AutonomousTestCommand.Execute2(uiapp.ActiveUIDocument.Document);
                            commandFound = true;
                        }
                        else if (tLine.Equals("EXIT: TRUE", StringComparison.OrdinalIgnoreCase))
                        {
                            shouldExit = true;
                        }
                    }

                    // Tương thích ngược: Nếu file rỗng hoặc chỉ ghi linh tinh, chạy lệnh default
                    if (!commandFound && lines.Length > 0 && !lines[0].StartsWith("COMMAND:", StringComparison.OrdinalIgnoreCase))
                    {
                        Antigravity.Main.Commands.AutonomousTestCommand.Execute2(uiapp.ActiveUIDocument.Document);
                    }
                }
                catch (Exception ex)
                {
                    AutomationLogger.WriteError("AutomationHook", ex);
                }
                finally
                {
                    // Xóa file kích hoạt sau khi chạy xong để không lặp lại ở lần mở Revit sau
                    try { System.IO.File.Delete(triggerFile); } catch { }

                    if (shouldExit)
                    {
                        AutomationLogger.Write("AutomationHook", "Gửi lệnh đóng Revit (ExitRevit)...");
                        var exitCommandId = RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit);
                        if (exitCommandId != null && uiapp.CanPostCommand(exitCommandId))
                        {
                            uiapp.PostCommand(exitCommandId);
                        }
                    }
                }
            }
        }
    }
}
