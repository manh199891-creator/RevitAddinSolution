using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.ArchModeling.Models;
using Antigravity.ArchModeling.Services;
using Microsoft.Win32;
using System.IO;

namespace Antigravity.ArchModeling.UI
{
    public partial class ArchModelingWindow : Window, INotifyPropertyChanged
    {
        private UIApplication _uiApp;
        private Document _doc;
        private ArchModelingMode _mode;
        
        public ObservableCollection<MappingItem> MappingItems { get; set; } = new ObservableCollection<MappingItem>();
        public ObservableCollection<MappingItem> DoorMappingItems { get; set; } = new ObservableCollection<MappingItem>();
        public ObservableCollection<ElementTypeViewModel> AvailableTypes { get; set; } = new ObservableCollection<ElementTypeViewModel>();
        public ObservableCollection<string> AvailableGroups { get; set; } = new ObservableCollection<string>();
        
        private List<ElementTypeViewModel> _combinedWallTypes = new List<ElementTypeViewModel>();
        private List<ElementTypeViewModel> _combinedDoorTypes = new List<ElementTypeViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;

        public ArchModelingWindow(UIApplication uiApp, ArchModelingMode mode)
        {
            InitializeComponent();
            _uiApp = uiApp;
            _doc = uiApp.ActiveUIDocument.Document;
            _mode = mode;

            DataContext = this;

            LoadRevitData();
            SetupUIForMode();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void SetupUIForMode()
        {
            switch (_mode)
            {
                case ArchModelingMode.Wall:
                    TxtModeTitle.Text = " · DRAW WALLS";
                    DgMapping.Columns[0].Header = "CAD Name (Pattern/Layer)";
                    ColGroup.Visibility   = System.Windows.Visibility.Collapsed;
                    ColPreview.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case ArchModelingMode.DoorWindow:
                    TxtModeTitle.Text = " · PLACE DOORS/WINDOWS";
                    LblTopLevel.Visibility  = System.Windows.Visibility.Collapsed;
                    CboTopLevel.Visibility  = System.Windows.Visibility.Collapsed;
                    LblTopOffset.Visibility = System.Windows.Visibility.Collapsed;
                    TxtTopOffset.Visibility = System.Windows.Visibility.Collapsed;
                    ColThickness.Visibility = System.Windows.Visibility.Collapsed;
                    ColGroup.Visibility     = System.Windows.Visibility.Collapsed;
                    ColPreview.Visibility   = System.Windows.Visibility.Collapsed;
                    DgMapping.Columns[0].Header = "CAD Name (Block)";
                    ChkMirrorHinge.Visibility = System.Windows.Visibility.Visible;
                    break;
                case ArchModelingMode.FloorCeil:
                    TxtModeTitle.Text = " · DRAW FLOORS/CEILINGS";
                    LblTopLevel.Visibility  = System.Windows.Visibility.Collapsed;
                    CboTopLevel.Visibility  = System.Windows.Visibility.Collapsed;
                    LblTopOffset.Visibility = System.Windows.Visibility.Collapsed;
                    TxtTopOffset.Visibility = System.Windows.Visibility.Collapsed;
                    ColThickness.Visibility = System.Windows.Visibility.Collapsed;
                    ColGroup.Visibility     = System.Windows.Visibility.Visible;
                    ColPreview.Visibility   = System.Windows.Visibility.Visible;
                    DgMapping.RowHeight     = 86;   // accommodate image
                    DgMapping.Columns[0].Header = "CAD Name (Pattern)";
                    break;
                case ArchModelingMode.Combined:
                    TxtModeTitle.Text = " · WALLS & DOORS";
                    DgMapping.Columns[0].Header = "CAD Name (Hatch/Layer)";
                    TxtWallMappingTitle.Visibility = System.Windows.Visibility.Visible;
                    GridDoorsMapping.Visibility   = System.Windows.Visibility.Visible;
                    ChkMirrorHinge.Visibility     = System.Windows.Visibility.Visible;
                    ColGroup.Visibility   = System.Windows.Visibility.Collapsed;
                    ColPreview.Visibility = System.Windows.Visibility.Collapsed;
                    break;
            }
        }

        private void LoadRevitData()
        {
            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            CboBaseLevel.ItemsSource = levels;
            CboTopLevel.ItemsSource = levels;

            if (levels.Count > 0)
            {
                CboBaseLevel.SelectedIndex = 0;
                if (levels.Count > 1) CboTopLevel.SelectedIndex = 1;
                else CboTopLevel.SelectedIndex = 0;
            }

            IEnumerable<ElementType> types = null;

            switch (_mode)
            {
                case ArchModelingMode.Wall:
                    types = new FilteredElementCollector(_doc).OfClass(typeof(WallType)).Cast<ElementType>();
                    break;
                case ArchModelingMode.DoorWindow:
                    types = new FilteredElementCollector(_doc).OfClass(typeof(FamilySymbol))
                                .OfCategory(BuiltInCategory.OST_Doors)
                                .Cast<ElementType>()
                                .Concat(new FilteredElementCollector(_doc).OfClass(typeof(FamilySymbol))
                                .OfCategory(BuiltInCategory.OST_Windows).Cast<ElementType>());
                    break;
                case ArchModelingMode.FloorCeil:
                    types = new FilteredElementCollector(_doc).OfClass(typeof(FloorType)).Cast<ElementType>()
                                .Concat(new FilteredElementCollector(_doc).OfClass(typeof(CeilingType)).Cast<ElementType>());
                    break;
                case ArchModelingMode.Combined:
                    var wallTypes = new FilteredElementCollector(_doc).OfClass(typeof(WallType)).Cast<ElementType>();
                    var doorTypes = new FilteredElementCollector(_doc).OfClass(typeof(FamilySymbol))
                                .OfCategory(BuiltInCategory.OST_Doors).Cast<ElementType>()
                                .Concat(new FilteredElementCollector(_doc).OfClass(typeof(FamilySymbol))
                                .OfCategory(BuiltInCategory.OST_Windows).Cast<ElementType>());
                    
                    _combinedWallTypes.Clear();
                    foreach (var t in wallTypes.OrderBy(x => x.Name))
                        _combinedWallTypes.Add(new ElementTypeViewModel(t));
                        
                    _combinedDoorTypes.Clear();
                    foreach (var t in doorTypes.OrderBy(x => x.Name))
                        _combinedDoorTypes.Add(new ElementTypeViewModel(t));

                    types = wallTypes.Concat(doorTypes);
                    break;
            }

            if (types != null)
            {
                foreach (var t in types.OrderBy(x => x.Name))
                {
                    AvailableTypes.Add(new ElementTypeViewModel(t));
                }
            }
            
            DgMapping.ItemsSource = MappingItems;
        }

        private void BtnSetOrigin_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                var pt = _uiApp.ActiveUIDocument.Selection.PickPoint("Pick origin point in Revit");
                var acad = new ArchCadInteropService(); // Just to reuse the connection and prompt, or use DrawFloors service
                var dfCad = new Antigravity.DrawFloors.Services.CadInteropService();
                dfCad.SetOriginFromRevitPoint(pt);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.ShowDialog();
            }
        }

        private void BtnScanCAD_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MappingItems.Clear();
                AvailableGroups.Clear();
                if (_mode == ArchModelingMode.Wall)
                {
                    var cadService = new ArchCadInteropService();
                    var data = cadService.SelectAndParseByLayer("Select CAD walls (lines/polylines)");
                    foreach (var kvp in data)
                    {
                        string[] parts = kvp.Key.Split('|');
                        string cadName = parts[0];
                        string thickness = parts.Length > 1 ? parts[1] : "";
                        
                        var item = new MappingItem { CadName = cadName, Thickness = thickness, Count = kvp.Value.Count, Data = kvp.Value };
                        item.InitMasterTypes(AvailableTypes.ToList());
                        item.GroupChanged += MappingItem_GroupChanged;
                        MappingItems.Add(item);
                        AvailableGroups.Add(item.CadName);
                    }
                }
                else if (_mode == ArchModelingMode.DoorWindow)
                {
                    var cadService = new ArchCadInteropService();
                    var data = cadService.SelectAndParseBlocks("Select CAD door/window blocks");
                    foreach (var kvp in data)
                    {
                        var item = new MappingItem { CadName = kvp.Key, Count = kvp.Value.Count, Data = kvp.Value };
                        item.InitMasterTypes(AvailableTypes.ToList());
                        item.GroupChanged += MappingItem_GroupChanged;
                        MappingItems.Add(item);
                        AvailableGroups.Add(item.CadName);
                    }
                }
                else if (_mode == ArchModelingMode.FloorCeil)
                {
                    var cadService = new HatchBoundaryService();
                    var scanResult = cadService.GetHatchBoundaries();
                    foreach (var kvp in scanResult.Boundaries)
                    {
                        var item = new MappingItem { CadName = kvp.Key, Count = kvp.Value.Count, Data = kvp.Value };
                        if (scanResult.PatternInfos.TryGetValue(kvp.Key, out var info))
                            item.PatternInfo = info;
                        item.InitMasterTypes(AvailableTypes.ToList());
                        item.GroupChanged += MappingItem_GroupChanged;
                        MappingItems.Add(item);
                        AvailableGroups.Add(item.CadName);
                    }
                }
                else if (_mode == ArchModelingMode.Combined)
                {
                    DoorMappingItems.Clear();
                    var cadService = new ArchCadInteropService();
                    var data = cadService.SelectAndParseCombined("Select CAD walls (hatch) and doors (blocks)");
                    
                    foreach (var kvp in data.Walls)
                    {
                        string[] parts = kvp.Key.Split('|');
                        string cadName = parts[0];
                        string thickness = parts.Length > 1 ? parts[1] : "";
                        var item = new MappingItem { CadName = cadName, Thickness = thickness, Count = kvp.Value.Count, Data = kvp.Value };
                        item.InitMasterTypes(_combinedWallTypes);
                        item.GroupChanged += MappingItem_GroupChanged;
                        MappingItems.Add(item);
                        AvailableGroups.Add(item.CadName);
                    }

                    foreach (var kvp in data.Doors)
                    {
                        var item = new MappingItem { CadName = kvp.Key, Count = kvp.Value.Count, Data = kvp.Value };
                        item.InitMasterTypes(_combinedDoorTypes);
                        DoorMappingItems.Add(item);
                    }
                }
                
                if (_mode == ArchModelingMode.FloorCeil)
                {
                    TxtStatus.Text = $"Found {MappingItems.Count} hatch groups.";
                }
                else
                {
                    TxtStatus.Text = $"Found {MappingItems.Count} walls, {DoorMappingItems.Count} doors.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MappingItem_GroupChanged(object sender, EventArgs e)
        {
            var changedItem = sender as MappingItem;
            if (changedItem == null || string.IsNullOrEmpty(changedItem.GroupName)) return;
            
            var target = MappingItems.FirstOrDefault(m => m.CadName == changedItem.GroupName && m != changedItem);
            if (target != null)
            {
                target.Count += changedItem.Count;
                
                if (_mode == ArchModelingMode.Wall || _mode == ArchModelingMode.Combined)
                {
                    if (target.Data is List<WallData> tList && changedItem.Data is List<WallData> cList)
                        tList.AddRange(cList);
                }
                else if (_mode == ArchModelingMode.FloorCeil)
                {
                    if (target.Data is List<IList<CurveLoop>> tList && changedItem.Data is List<IList<CurveLoop>> cList)
                        tList.AddRange(cList);
                }
                
                Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    MappingItems.Remove(changedItem);
                    AvailableGroups.Remove(changedItem.CadName);
                    changedItem.GroupChanged -= MappingItem_GroupChanged;
                }));
            }
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            bool hasWallMapped = MappingItems.Any(m => m.SelectedType != null);
            bool hasDoorMapped = DoorMappingItems.Any(m => m.SelectedType != null);

            if (_mode == ArchModelingMode.Combined)
            {
                if (!hasWallMapped && !hasDoorMapped)
                {
                    MessageBox.Show("Please map at least one CAD item (Wall or Door) to a Revit Type.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                if (MappingItems.Count == 0 || !hasWallMapped)
                {
                    MessageBox.Show("Please map at least one CAD item to a Revit Type.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            Level baseLevel = CboBaseLevel.SelectedItem as Level;
            Level topLevel = CboTopLevel.SelectedItem as Level;
            double baseOffset = 0, topOffset = 0;
            double.TryParse(TxtBaseOffset.Text, out baseOffset);
            double.TryParse(TxtTopOffset.Text, out topOffset);

            using (Transaction t = new Transaction(_doc, "Arch Modeling Create"))
            {
                t.Start();
                int createdCount = 0;

                try
                {
                    if (_mode == ArchModelingMode.Wall)
                    {
                        var builder = new WallFromCadBuilder(_doc, baseLevel, topLevel, baseOffset, topOffset);

                        foreach (var item in MappingItems.Where(m => m.SelectedType != null))
                        {
                            WallType wallType = item.SelectedType.RevitType as WallType;
                            var list = item.Data as List<WallData>;
                            if (list == null || wallType == null) continue;
                            foreach (var data in list)
                            {
                                var wall = builder.BuildWall(data, wallType);
                                if (wall != null) createdCount++;
                            }
                        }

                    }
                    else if (_mode == ArchModelingMode.DoorWindow)
                    {
                        var map = MappingItems.Where(m => m.SelectedType != null).ToDictionary(m => m.CadName, m => m.SelectedType.RevitType as FamilySymbol);
                        var placer = new DoorWindowPlacer();
                        var ambiguousHandles = new List<string>();

                        foreach (var item in MappingItems.Where(m => m.SelectedType != null))
                        {
                            // Update Type Mark if provided
                            if (!string.IsNullOrEmpty(item.TypeMark))
                            {
                                var symbol = item.SelectedType.RevitType as FamilySymbol;
                                if (symbol != null)
                                {
                                    Parameter typeMarkParam = symbol.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                                    if (typeMarkParam != null && !typeMarkParam.IsReadOnly)
                                    {
                                        typeMarkParam.Set(item.TypeMark);
                                    }
                                }
                            }

                            var list = item.Data as List<BlockInfo>;
                            foreach (var data in list)
                            {
                                var result = placer.PlaceDoorOrWindow(_doc, data, map, baseLevel, ChkMirrorHinge.IsChecked == true);
                                if (result?.Instance != null)
                                {
                                    createdCount++;
                                    if (result.IsAmbiguous && !string.IsNullOrEmpty(result.EntityHandle))
                                        ambiguousHandles.Add(result.EntityHandle);
                                }
                            }
                        }

                        t.Commit();
                        
                        // ═══ NO MORE SLOW COM INTEROP ═══
                        // We removed MarkEntitiesRed because COM communication is extremely slow
                        // and the new Door Orientation logic is robust enough.
                        if (ambiguousHandles.Count > 0)
                        {
                            MessageBox.Show(
                                $"Created {createdCount} elements.\n\n" +
                                $"⚠ {ambiguousHandles.Count} block(s) might have an ambiguous orientation (e.g., perpendicular to wall).\n" +
                                "Please verify them manually in Revit.",
                                "Done with Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        else
                        {
                            MessageBox.Show($"Successfully created {createdCount} elements.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        this.Close();
                        return; // early exit — transaction already committed
                    }
                    else if (_mode == ArchModelingMode.FloorCeil)
                    {
                        int failedCount = 0;
                        SketchPlane errorSp = null;
                        
                        foreach (var item in MappingItems.Where(m => m.SelectedType != null))
                        {
                            var listProfiles = item.Data as List<IList<CurveLoop>>;
                            FloorType ft = item.SelectedType.RevitType as FloorType;
                            CeilingType ct = item.SelectedType.RevitType as CeilingType;
                            
                            if ((ft != null || ct != null) && listProfiles != null)
                            {
                                FillPatternElement fpe = null;

                                foreach (var profile in listProfiles)
                                {
                                    try
                                    {
                                        Element createdElem = null;
                                        if (ft != null)
                                        {
                                            Floor floor = Floor.Create(_doc, profile, ft.Id, baseLevel.Id);
                                            if (floor != null)
                                            {
                                                Parameter offsetParam = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                                                offsetParam?.Set(baseOffset / 304.8);
                                                createdElem = floor;
                                            }
                                        }
                                        else if (ct != null)
                                        {
                                            Ceiling ceiling = Ceiling.Create(_doc, profile, ct.Id, baseLevel.Id);
                                            if (ceiling != null)
                                            {
                                                Parameter offsetParam = ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM);
                                                offsetParam?.Set(baseOffset / 304.8);
                                                createdElem = ceiling;
                                            }
                                        }

                                        if (createdElem != null)
                                        {
                                            createdCount++;
                                            // Apply pattern match override
                                            if (item.PatternInfo != null)
                                            {
                                                if (fpe == null) fpe = GetOrCreateFillPatternElement(_doc, item.CadName, item.PatternInfo);
                                                if (fpe != null)
                                                {
                                                    var ogs = new OverrideGraphicSettings();
                                                    ogs.SetSurfaceForegroundPatternId(fpe.Id);
                                                    _doc.ActiveView.SetElementOverrides(createdElem.Id, ogs);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[ArchModeling] Failed to create floor for hatch: {ex.Message}");
                                        failedCount++;
                                        try
                                        {
                                            if (errorSp == null)
                                            {
                                                Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, baseLevel.Elevation + baseOffset / 304.8));
                                                errorSp = SketchPlane.Create(_doc, plane);
                                            }
                                            foreach (var loop in profile)
                                            {
                                                foreach (Curve curve in loop)
                                                {
                                                    _doc.Create.NewModelCurve(curve, errorSp);
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                        t.Commit();
                        if (failedCount > 0)
                        {
                            MessageBox.Show($"Successfully created {createdCount} floors.\nFailed to create {failedCount} floors due to invalid CAD geometry (self-intersecting or unclosed boundaries).", "Done with Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        else
                        {
                            MessageBox.Show($"Successfully created {createdCount} elements.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        this.Close();
                        return;
                    }
                    else if (_mode == ArchModelingMode.Combined)
                    {
                        var ambiguousHandles = new List<string>();
                        
                        // 1. Create Walls
                        var wallBuilder = new WallFromCadBuilder(_doc, baseLevel, topLevel, baseOffset, topOffset);
                        foreach (var item in MappingItems.Where(m => m.SelectedType != null))
                        {
                            WallType wallType = item.SelectedType.RevitType as WallType;
                            var list = item.Data as List<WallData>;
                            if (list == null || wallType == null) continue;
                            foreach (var data in list)
                            {
                                var wall = wallBuilder.BuildWall(data, wallType);
                                if (wall != null) createdCount++;
                            }
                        }

                        // Must regenerate so doors can find the newly created walls
                        _doc.Regenerate();

                        // 2. Create Doors
                        var doorMap = DoorMappingItems.Where(m => m.SelectedType != null).ToDictionary(m => m.CadName, m => m.SelectedType.RevitType as FamilySymbol);
                        var doorPlacer = new DoorWindowPlacer();
                        
                        foreach (var item in DoorMappingItems.Where(m => m.SelectedType != null))
                        {
                            if (!string.IsNullOrEmpty(item.TypeMark))
                            {
                                var symbol = item.SelectedType.RevitType as FamilySymbol;
                                if (symbol != null)
                                {
                                    Parameter typeMarkParam = symbol.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                                    if (typeMarkParam != null && !typeMarkParam.IsReadOnly)
                                    {
                                        typeMarkParam.Set(item.TypeMark);
                                    }
                                }
                            }

                            var list = item.Data as List<BlockInfo>;
                            foreach (var data in list)
                            {
                                var result = doorPlacer.PlaceDoorOrWindow(_doc, data, doorMap, baseLevel, ChkMirrorHinge.IsChecked == true);
                                if (result?.Instance != null)
                                {
                                    createdCount++;
                                    if (result.IsAmbiguous && !string.IsNullOrEmpty(result.EntityHandle))
                                        ambiguousHandles.Add(result.EntityHandle);
                                }
                            }
                        }

                        t.Commit();
                        
                        if (ambiguousHandles.Count > 0)
                        {
                            MessageBox.Show(
                                $"Created {createdCount} elements (Walls + Doors).\n\n" +
                                $"⚠ {ambiguousHandles.Count} block(s) might have an ambiguous orientation.\n" +
                                "Please verify them manually in Revit.",
                                "Done with Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        else
                        {
                            MessageBox.Show($"Successfully created {createdCount} elements.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        this.Close();
                        return; // early exit
                    }

                    t.Commit();
                    MessageBox.Show($"Successfully created {createdCount} elements.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnExportWallTemplate_Click(object sender, RoutedEventArgs e)
        {
            ExportTemplateHelper(MappingItems, "WallMapping.csv");
        }

        private void BtnExportDoorTemplate_Click(object sender, RoutedEventArgs e)
        {
            ExportTemplateHelper(DoorMappingItems, "DoorMapping.csv");
        }

        private void ExportTemplateHelper(IEnumerable<MappingItem> items, string defaultName)
        {
            if (!items.Any())
            {
                MessageBox.Show("No items to export.", "Empty", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title       = "Save Mapping Template",
                Filter      = "CSV File (*.csv)|*.csv",
                FileName    = defaultName
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                using (var writer = new StreamWriter(dlg.FileName, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("CAD Pattern (contains match),Revit Family,Revit Type,Type Mark");

                    foreach (var item in items)
                    {
                        string safeName = item.CadName?.Replace("\"", "\"\"") ?? "";
                        if (safeName.Contains(",")) safeName = $"\"{safeName}\"";
                        
                        string safeMark = item.TypeMark?.Replace("\"", "\"\"") ?? "";
                        if (safeMark.Contains(",")) safeMark = $"\"{safeMark}\"";

                        writer.WriteLine($"{safeName},,,{safeMark}");
                    }
                }
                TxtStatus.Text = $"Template exported: {System.IO.Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImportWallMapping_Click(object sender, RoutedEventArgs e)
        {
            ImportMappingHelper(MappingItems, "Walls");
        }

        private void BtnImportDoorMapping_Click(object sender, RoutedEventArgs e)
        {
            ImportMappingHelper(DoorMappingItems, "Doors");
        }

        private void ImportMappingHelper(IEnumerable<MappingItem> items, string typeName)
        {
            if (!items.Any())
            {
                MessageBox.Show($"Please scan CAD selection first before importing {typeName} mapping.",
                    "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Title  = $"Open {typeName} Mapping Excel File",
                Filter = "Excel File (*.xlsx)|*.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var svc  = new MappingExcelService();
                var rows = svc.ReadMappingFromFile(dlg.FileName);

                string CleanCadName(string input)
                {
                    if (string.IsNullOrEmpty(input)) return "";
                    var match = System.Text.RegularExpressions.Regex.Match(input, @"-\d{5,8}-");
                    if (match.Success) return input.Substring(0, match.Index).Trim();
                    return input.Trim();
                }

                int matched = 0;
                int total = 0;
                
                foreach (var item in items)
                {
                    total++;
                    string cleanItemName = CleanCadName(item.CadName);

                    var excelRow = rows.FirstOrDefault(r =>
                    {
                        if (string.IsNullOrEmpty(r.CadPattern)) return false;
                        string cleanPattern = CleanCadName(r.CadPattern);
                        if (string.IsNullOrEmpty(cleanPattern)) return false;
                        return cleanItemName.IndexOf(cleanPattern, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    });

                    if (excelRow == null) continue;

                    if (!string.IsNullOrEmpty(excelRow.RevitFamily))
                    {
                        var matchedFamily = item.AvailableFamilies
                            .FirstOrDefault(f => string.Equals(f, excelRow.RevitFamily, System.StringComparison.OrdinalIgnoreCase));
                        if (matchedFamily != null)
                            item.SelectedFamily = matchedFamily;
                    }

                    if (!string.IsNullOrEmpty(excelRow.RevitType))
                    {
                        var matchedType = item.FilteredTypes
                            .FirstOrDefault(t => string.Equals(t.Name, excelRow.RevitType, System.StringComparison.OrdinalIgnoreCase));
                        if (matchedType != null)
                        {
                            item.SelectedType = matchedType;
                            matched++;
                        }
                    }

                    if (!string.IsNullOrEmpty(excelRow.TypeMark))
                    {
                        item.TypeMark = excelRow.TypeMark;
                    }
                }

                TxtStatus.Text = $"Mapped {matched} / {total} {typeName} items from Excel.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private FillPatternElement GetOrCreateFillPatternElement(Document doc, string name, HatchPatternInfo info)
        {
            if (info == null || info.Lines.Count == 0) return null;
            
            // Check if it already exists
            var existingPattern = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(f => f.Name == name && f.GetFillPattern().Target == FillPatternTarget.Model);
                
            if (existingPattern != null)
                return existingPattern;
                
            try
            {
                FillPattern fillPattern = new FillPattern(name, FillPatternTarget.Model, FillPatternHostOrientation.ToView);
                var grids = new List<FillGrid>();
                foreach (var line in info.Lines)
                {
                    FillGrid grid = new FillGrid();
                    grid.Angle = line.AngleDegrees * Math.PI / 180.0;
                    grid.Origin = new UV(line.BaseXMm / 304.8, line.BaseYMm / 304.8);
                    
                    // Revit FillGrid requires Offset > 0. 
                    // AutoCAD OffsetY is the perpendicular distance between lines.
                    double offsetFt = Math.Abs(line.OffsetYMm) / 304.8;
                    if (offsetFt < 0.0001) offsetFt = 1.0; // Prevent invalid grid
                    grid.Offset = offsetFt;
                    
                    // Shift along the line
                    grid.Shift = line.OffsetXMm / 304.8;
                    
                    // Segments (Dashes)
                    if (line.DashLengthsMm != null && line.DashLengthsMm.Count > 0)
                    {
                        var segments = new List<double>();
                        foreach (var d in line.DashLengthsMm)
                        {
                            segments.Add(d / 304.8);
                        }
                        // Validate segments (Revit requires pairs of solid/space, even number, non-zero sum, max 8 segments)
                        if (segments.Count <= 8 && segments.Count % 2 == 0)
                        {
                            bool valid = true;
                            foreach(var s in segments) if (Math.Abs(s) < 0.0001) valid = false;
                            if (valid) grid.SetSegments(segments);
                        }
                    }
                    grids.Add(grid);
                }
                fillPattern.SetFillGrids(grids);
                return FillPatternElement.Create(doc, fillPattern);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create FillPatternElement {name}: {ex.Message}");
                return null;
            }
        }
    }

    public class MappingItem : INotifyPropertyChanged
    {
        public HatchPatternInfo PatternInfo { get; set; }
        public string CadName { get; set; }
        public string Thickness { get; set; }
        public int Count { get; set; }
        
        private string _typeMark;
        public string TypeMark
        {
            get => _typeMark;
            set { _typeMark = value; OnPropertyChanged(nameof(TypeMark)); }
        }

        private string _groupName;
        public string GroupName
        {
            get => _groupName;
            set 
            {
                if (_groupName != value)
                {
                    _groupName = value;
                    OnPropertyChanged(nameof(GroupName));
                    GroupChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler GroupChanged;

        private ElementTypeViewModel _selectedType;
        public ElementTypeViewModel SelectedType 
        { 
            get => _selectedType; 
            set { _selectedType = value; OnPropertyChanged(nameof(SelectedType)); }
        }

        private List<ElementTypeViewModel> _masterTypes;
        public ObservableCollection<string> AvailableFamilies { get; set; }
        public ObservableCollection<ElementTypeViewModel> FilteredTypes { get; set; }

        private string _selectedFamily;
        public string SelectedFamily
        {
            get => _selectedFamily;
            set
            {
                if (_selectedFamily != value)
                {
                    _selectedFamily = value;
                    OnPropertyChanged(nameof(SelectedFamily));
                    FilterTypes();
                }
            }
        }
        
        public object Data { get; set; }

        public void InitMasterTypes(List<ElementTypeViewModel> masterTypes)
        {
            _masterTypes = masterTypes;
            var families = masterTypes.Select(t => t.FamilyName).Distinct().OrderBy(f => f).ToList();
            AvailableFamilies = new ObservableCollection<string>(families);
            FilteredTypes = new ObservableCollection<ElementTypeViewModel>(masterTypes);
        }

        private void FilterTypes()
        {
            if (_masterTypes == null) return;
            
            if (string.IsNullOrEmpty(SelectedFamily))
            {
                FilteredTypes = new ObservableCollection<ElementTypeViewModel>(_masterTypes);
            }
            else
            {
                FilteredTypes = new ObservableCollection<ElementTypeViewModel>(
                    _masterTypes.Where(t => t.FamilyName == SelectedFamily));
            }
            OnPropertyChanged(nameof(FilteredTypes));
            
            // Clear selected type if it doesn't match the new family
            if (SelectedType != null && SelectedType.FamilyName != SelectedFamily)
            {
                SelectedType = null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Converts HatchPatternInfo → DrawingImage for tooltip preview.
    /// </summary>
    public class HatchPreviewConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var info = value as HatchPatternInfo;
            if (info == null || info.Lines == null || info.Lines.Count == 0)
                return null;

            const double size = 90;
            var dg = new DrawingGroup();
            using (var dc = dg.Open())
            {
                // White background
                dc.DrawRectangle(Brushes.White,
                    new Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)), 0.5),
                    new Rect(0, 0, size, size));

                var colors = new[]
                {
                    System.Windows.Media.Color.FromRgb(30,  80,  200),
                    System.Windows.Media.Color.FromRgb(200, 60,  30),
                    System.Windows.Media.Color.FromRgb(30,  150, 60)
                };

                for (int li = 0; li < info.Lines.Count; li++)
                {
                    var lineDef = info.Lines[li];
                    var pen = new Pen(new SolidColorBrush(colors[li % colors.Length]), 0.9);
                    pen.Freeze();

                    double angleRad = lineDef.AngleDegrees * Math.PI / 180.0;
                    double cos = Math.Cos(angleRad);
                    double sin = Math.Sin(angleRad);

                    // Normalize spacing to 5–18 px
                    double raw     = Math.Abs(lineDef.OffsetYMm) > 0.001 ? Math.Abs(lineDef.OffsetYMm) : 100;
                    double spacing = Math.Max(5, Math.Min(18, raw / 8.0));

                    for (double offset = -size * 2; offset <= size * 2; offset += spacing)
                    {
                        double cx = size / 2 + (-sin) * offset;
                        double cy = size / 2 + cos   * offset;
                        dc.DrawLine(pen,
                            new System.Windows.Point(cx - cos * size * 1.5, cy - sin * size * 1.5),
                            new System.Windows.Point(cx + cos * size * 1.5, cy + sin * size * 1.5));
                    }
                }
            }
            dg.ClipGeometry = new RectangleGeometry(new Rect(0, 0, size, size));
            return new DrawingImage(dg);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class ElementTypeViewModel
    {
        public ElementType RevitType { get; set; }
        public string Name { get; set; }
        public string FamilyName { get; set; }
        public string DisplayName { get; set; }

        public ElementTypeViewModel(ElementType type)
        {
            RevitType = type;
            Name = type.Name;
            try { FamilyName = type.FamilyName; } catch { FamilyName = "System"; }
            DisplayName = $"{FamilyName} - {Name}";
        }
    }
}
