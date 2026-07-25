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
        private Autodesk.Revit.DB.IUpdater _doorClearanceUpdater;

        static App()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAntigravityAssembly;
        }

        private static Assembly ResolveAntigravityAssembly(object sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name;
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
            try
            {
                return OnStartupInternal(application);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("VilaiViet Startup Error", ex.ToString());
                return Result.Failed;
            }
        }

        private Result OnStartupInternal(UIControlledApplication application)
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
            RibbonPanel structuralPanel = application.CreateRibbonPanel(tabName, "MODELING");

            // 3. Add Buttons
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            BitmapImage logoImage = GetEmbeddedImage("icon_logo_32.png");

            PushButtonData btnDrawColumns = new PushButtonData(
                "btnDrawColumns",
                "Draw\nColumns",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.DrawColumns.dll"),
                "Antigravity.DrawColumns.CreateColumnCommand");

            btnDrawColumns.ToolTip = "Create structural columns automatically from CAD geometry.";
            btnDrawColumns.LargeImage = logoImage;

            PushButtonData btnDrawBeams = new PushButtonData(
                "btnDrawBeams",
                "Draw\nBeams",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.DrawBeams.dll"),
                "Antigravity.DrawBeams.CreateBeamCommand");

            btnDrawBeams.ToolTip = "Create structural framing automatically from CAD geometry.";
            btnDrawBeams.LargeImage = logoImage;

            PushButtonData btnDrawWalls = new PushButtonData(
                "btnDrawWalls",
                "Draw\nWalls",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.DrawWalls.dll"),
                "Antigravity.DrawWalls.CreateWallCommand");

            btnDrawWalls.ToolTip = "Create structural walls automatically from CAD geometry.";
            btnDrawWalls.LargeImage = logoImage;

            PushButtonData btnDrawFloors = new PushButtonData(
                "btnDrawFloors",
                "Draw\nFloors",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.DrawFloors.dll"),
                "Antigravity.DrawFloors.CreateFloorCommand");

            btnDrawFloors.ToolTip = "Create floors automatically from CAD geometry.";
            btnDrawFloors.LargeImage = logoImage;

            PushButtonData btnCadSleevePlacer = new PushButtonData(
                "btnCadSleevePlacer",
                "Place CAD\nSleeves",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.CadSleevePlacer.dll"),
                "Antigravity.CadSleevePlacer.AppCommand");

            btnCadSleevePlacer.ToolTip = "Place Generic Model sleeve families in beams and walls from AutoCAD leaders and text.";
            btnCadSleevePlacer.LargeImage = logoImage;

            PushButtonData btnDoorClearance = new PushButtonData(
                "btnDoorClearance",
                "Door\nClearance",
                assemblyPath.Replace("Antigravity.Main.dll", "DoorClearanceBox.dll"),
                "DoorClearanceBox.Commands.CreateClearanceBoxCommand");

            btnDoorClearance.ToolTip = "Create clearance boxes automatically for doors and windows.";
            btnDoorClearance.LargeImage = logoImage;


            PushButtonData btnAutoFoundation = new PushButtonData(
                "btnAutoFoundation",
                "Draw\nFoundations",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.AutoFoundation.dll"),
                "Antigravity.AutoFoundation.Commands.AutoFoundationCommand");

            btnAutoFoundation.ToolTip = "Create structural foundations automatically from CAD geometry.";
            btnAutoFoundation.LargeImage = logoImage;

            structuralPanel.AddItem(btnAutoFoundation);
            structuralPanel.AddItem(btnDrawColumns);
            structuralPanel.AddItem(btnDrawBeams);
            structuralPanel.AddItem(btnDrawWalls);
            structuralPanel.AddItem(btnDrawFloors);

            // --- KIẾN TRÚC ---
            PushButtonData btnArchWall = new PushButtonData(
                "btnArchWall",
                "Architectural\nWalls",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ArchModeling.dll"),
                "Antigravity.ArchModeling.Commands.DrawWallFromCadCommand");
            btnArchWall.ToolTip = "Create architectural walls automatically from AutoCAD layers.";
            btnArchWall.LargeImage = logoImage;

            PushButtonData btnArchWallDoor = new PushButtonData(
                "btnArchWallDoor",
                "Walls &\nDoors",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ArchModeling.dll"),
                "Antigravity.ArchModeling.Commands.DrawWallsAndDoorsFromCadCommand");
            btnArchWallDoor.ToolTip = "Create walls and place doors together from AutoCAD.";
            btnArchWallDoor.LargeImage = logoImage;

            PushButtonData btnArchFloor = new PushButtonData(
                "btnArchFloor",
                "Finish\nFloors",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ArchModeling.dll"),
                "Antigravity.ArchModeling.Commands.DrawFloorCeilFromCadCommand");
            btnArchFloor.ToolTip = "Create finish floors and ceilings from AutoCAD hatches.";
            btnArchFloor.LargeImage = logoImage;

            PushButtonData btnArchDoor = new PushButtonData(
                "btnArchDoor",
                "Place\nOpenings",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ArchModeling.dll"),
                "Antigravity.ArchModeling.Commands.PlaceDoorFromCadCommand");
            btnArchDoor.ToolTip = "Place doors and windows automatically from AutoCAD blocks.";
            btnArchDoor.LargeImage = logoImage;

            structuralPanel.AddItem(btnArchWall);
            structuralPanel.AddItem(btnArchWallDoor);
            structuralPanel.AddItem(btnArchFloor);
            structuralPanel.AddItem(btnArchDoor);

            // --- Panel Kiểm soát khối lượng ---
            RibbonPanel utilityPanel = application.CreateRibbonPanel(tabName, "QUANTITY CONTROL");

#if DEBUG
            SplitButtonData sbAutoJoinData = new SplitButtonData("splitAutoJoin", "Auto Join");
            SplitButton sbAutoJoin = utilityPanel.AddItem(sbAutoJoinData) as SplitButton;

            PushButtonData btnAutojoin = new PushButtonData(
                "btnAutojoin",
                "Auto Join",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.Autojoin.dll"),
                "Antigravity.Autojoin.AutoJoinCommand");
            btnAutojoin.ToolTip = "Join structural elements automatically using configurable rules.";
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

            btnAutojoin.ToolTip = "Join structural elements automatically using configurable rules.";
            btnAutojoin.LargeImage = logoImage;
            utilityPanel.AddItem(btnAutojoin);
#endif

            PushButtonData btnZoneSplit = new PushButtonData(
                "btnZoneSplit",
                "Split\nZones",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ZoneSplit.dll"),
                "Antigravity.ZoneSplit.Commands.ZoneProcessCommand");

            btnZoneSplit.ToolTip = "Classify elements by Generic Model zone volumes and generate a Markdown report.";
            btnZoneSplit.LargeImage = logoImage;
            utilityPanel.AddItem(btnZoneSplit);

            PushButtonData btnZoneExport = new PushButtonData(
                "btnZoneExport",
                "Export\nNavis",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.ZoneSplit.dll"),
                "Antigravity.ZoneSplit.Commands.ExportCommand");

            btnZoneExport.ToolTip = "Export ZoneID selection sets to Navisworks XML.";
            btnZoneExport.LargeImage = logoImage;
            utilityPanel.AddItem(btnZoneExport);

            // --- Panel Kiểm soát xung đột ---
            RibbonPanel clashPanel = application.CreateRibbonPanel(tabName, "CLASH CONTROL");

            PushButtonData btnIssueManager = new PushButtonData(
                "btnIssueManager",
                "Issue\nManager",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.IssueManager.dll"),
                "Antigravity.IssueManager.Commands.CmdOpenIssueManager");

            btnIssueManager.ToolTip = "Synchronize Navisworks issues through XML or BCFZIP files.";
            btnIssueManager.LargeImage = logoImage;

            PushButtonData btnClashControl = new PushButtonData(
                "btnClashControl",
                "Door Clash\nControl",
                assemblyPath.Replace("Antigravity.Main.dll", "DoorClearanceBox.dll"),
                "DoorClearanceBox.Commands.ClashControlCommand");

            btnClashControl.ToolTip = "Detect structural elements that clash with door clearance boxes.";
            btnClashControl.LargeImage = logoImage;

            PushButtonData btnCheckFloorElevation = new PushButtonData(
                "btnCheckFloorElevation",
                "Check Floor\nElevation",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.CheckFloorElevation.dll"),
                "Antigravity.CheckFloorElevation.CheckFloorElevationCommand");

            btnCheckFloorElevation.ToolTip = "Check floor top elevation delta between host architectural floors and linked structural floors.";
            btnCheckFloorElevation.LargeImage = logoImage;

            PushButtonData btnWallMepClash = new PushButtonData(
                "btnWallMepClash",
                "Wall MEP\nClash",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.WallMepClash.dll"),
                "Antigravity.WallMepClash.WallMepClashCommand");
            btnWallMepClash.ToolTip = "Detect parallel clashes between host walls and linked MEP pipes or equipment.";
            btnWallMepClash.LargeImage = logoImage;

            clashPanel.AddItem(btnCadSleevePlacer);
            clashPanel.AddItem(btnCheckFloorElevation);
            clashPanel.AddItem(btnWallMepClash);
            clashPanel.AddItem(btnIssueManager);
            clashPanel.AddItem(btnClashControl);
            clashPanel.AddItem(btnDoorClearance);

            // --- Panel Trình bày ---
            RibbonPanel docPanel = application.CreateRibbonPanel(tabName, "PRESENTATION");

            PushButtonData btnAutoDimWalls = new PushButtonData(
                "btnAutoDimWalls",
                "AutoDim\nWalls",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.AutoDimWalls.dll"),
                "Antigravity.AutoDimWalls.AutoDimCommand");

            btnAutoDimWalls.ToolTip = "Dimension straight architectural walls automatically in the active view.";
            btnAutoDimWalls.LargeImage = logoImage;
            docPanel.AddItem(btnAutoDimWalls);

            PushButtonData btnTagArranger = new PushButtonData(
                "btnTagArranger",
                "Arrange\nTags",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.TagArranger.dll"),
                "Antigravity.TagArranger.TagArrangeCommand");

            btnTagArranger.ToolTip = "Align, untangle, and distribute tags and dimensions automatically.";
            btnTagArranger.LargeImage = logoImage;
            docPanel.AddItem(btnTagArranger);

            PushButtonData btnHoanThien = new PushButtonData(
                "btnHoanThien",
                "Room\nFinishes",
                assemblyPath.Replace("Antigravity.Main.dll", "Antigravity.HoanThien.dll"),
                "Antigravity.HoanThien.Commands.HoanThienCommand");

            btnHoanThien.ToolTip = "Create finish walls and floors automatically from room boundaries.";
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
