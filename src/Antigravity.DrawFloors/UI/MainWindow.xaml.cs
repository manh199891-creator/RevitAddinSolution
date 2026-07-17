using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.DrawFloors.Models;
using Antigravity.DrawFloors.Services;

namespace Antigravity.DrawFloors.UI
{
    public partial class MainWindow : Window
    {
        // ─────────────────────────────────────────────
        // FIELDS
        // ─────────────────────────────────────────────

        private readonly ExternalCommandData _commandData;
        private readonly Document _doc;
        private readonly UIApplication _uiApp;
        private readonly UIDocument _uiDoc;

        // ExternalEvent infrastructure (fix lỗi "outside of API context")
        private readonly FloorCreationHandler _handler;
        private readonly ExternalEvent _exEvent;

        // CadService - lazy init, tái dùng cho mọi thao tác
        private CadInteropService _cadService;

        public List<Level> Levels { get; private set; }
        public List<FloorType> FloorTypes { get; private set; }

        // ─────────────────────────────────────────────
        // CONSTRUCTOR
        // ─────────────────────────────────────────────

        public MainWindow(ExternalCommandData commandData,
                          FloorCreationHandler handler,
                          ExternalEvent exEvent)
        {
            InitializeComponent();
            _commandData = commandData;
            _uiApp      = commandData.Application;
            _uiDoc      = _uiApp.ActiveUIDocument;
            _doc        = _uiDoc.Document;
            _handler    = handler;
            _exEvent    = exEvent;

            // Callback từ handler khi tạo sàn xong (chạy trên UI thread qua Dispatcher)
            _handler.OnComplete = (success, msg) =>
                Dispatcher.Invoke(() =>
                    MessageBox.Show(msg,
                        success ? "Completed" : "Error",
                        MessageBoxButton.OK,
                        success ? MessageBoxImage.Information : MessageBoxImage.Error));

            LoadRevitData();
        }

        // ─────────────────────────────────────────────
        // LOAD DATA TỪ REVIT
        // ─────────────────────────────────────────────

        private void LoadRevitData()
        {
            Levels = new FilteredElementCollector(_doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .OrderBy(l => l.Elevation)
                        .ToList();

            FloorTypes = new FilteredElementCollector(_doc)
                            .OfClass(typeof(FloorType))
                            .Cast<FloorType>()
                            .ToList();

            CboLevels.ItemsSource = Levels;
            if (Levels.Count > 0) CboLevels.SelectedIndex = 0;

            CboFloorTypes.ItemsSource = FloorTypes;
            if (FloorTypes.Count > 0) CboFloorTypes.SelectedIndex = 0;
        }

        // ─────────────────────────────────────────────
        // ĐỌC THÔNG SỐ TỪ UI
        // ─────────────────────────────────────────────

        private Level SelectedLevel    => CboLevels.SelectedItem as Level;
        private FloorType SelectedFloorType => CboFloorTypes.SelectedItem as FloorType;

        private double OffsetMm
        {
            get { double v; return double.TryParse(TxtOffset.Text, out v) ? v : 0.0; }
        }

        private bool ValidateCommonParams()
        {
            if (SelectedLevel == null)
            {
                MessageBox.Show("Please select a Level.", "Missing Information",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (SelectedFloorType == null)
            {
                MessageBox.Show("Please select a Floor Type.", "Missing Information",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        // ─────────────────────────────────────────────
        // KẾT NỐI CAD (lazy init)
        // ─────────────────────────────────────────────

        private CadInteropService GetCadService()
        {
            if (_cadService == null)
                _cadService = new CadInteropService();
            return _cadService;
        }

        // ─────────────────────────────────────────────
        // COMBOBOX — CẬP NHẬT ĐỘ DÀY
        // ─────────────────────────────────────────────

        private void CboFloorTypes_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selected = CboFloorTypes.SelectedItem as FloorType;
            if (selected == null) { TxtThickness.Text = "—"; return; }

            Parameter tp = selected.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
            TxtThickness.Text = tp != null
                ? (tp.AsDouble() * 304.8).ToString("F0")
                : "—";
        }

        // ─────────────────────────────────────────────
        // NÚT: ĐẶT GỐC TỌA ĐỘ
        // ─────────────────────────────────────────────

        private void BtnSetOrigin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pick điểm trong Revit (API context vẫn OK vì đây không phải Transaction)
                this.Hide();
                XYZ revitPt = _uiDoc.Selection.PickPoint(
                    Autodesk.Revit.UI.Selection.ObjectSnapTypes.Intersections,
                    "Click the origin point on the Revit floor plan...");
                this.Show();

                var cadSvc = GetCadService();
                cadSvc.SetOriginFromRevitPoint(revitPt);

                MessageBox.Show(
                    "✅ Coordinate origin set successfully!\n" +
                    "CAD↔Revit offset saved.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { this.Show(); }
            catch (Exception ex)
            {
                this.Show();
                MessageBox.Show("Error setting origin:\n" + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────
        // NÚT 1: VẼ HÌNH CHỮ NHẬT
        // ─────────────────────────────────────────────

        private void BtnDrawRectangle_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateCommonParams()) return;
            try
            {
                this.Hide();
                var cadSvc = GetCadService();
                var profile = cadSvc.DrawRectangleInteractive();
                this.Show();

                if (profile == null || profile.Count == 0)
                {
                    MessageBox.Show("Could not create a rectangle from the selected data.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Set params và raise ExternalEvent — Transaction chạy trong API context
                _handler.IsBatchMode      = false;
                _handler.ProfilePoints    = profile;
                _handler.ProfileCurves    = null;
                _handler.TargetLevelId    = SelectedLevel.Id;
                _handler.TargetFloorTypeId = SelectedFloorType.Id;
                _handler.OffsetMm         = OffsetMm;
                _handler.TransactionName  = "Create rectangular floor from CAD";
                _exEvent.Raise();
            }
            catch (Exception ex)
            {
                this.Show();
                MessageBox.Show("Lỗi:\n" + ex.Message, "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────
        // NÚT 2: CHỌN POLYLINE CÓ SẴN
        // ─────────────────────────────────────────────

        private void BtnSelectPolyline_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateCommonParams()) return;
            try
            {
                this.Hide();
                var cadSvc = GetCadService();
                var profile = cadSvc.SelectPolylineBoundaries();
                this.Show();

                if (profile == null || profile.Count == 0)
                {
                    MessageBox.Show("No valid Polyline found in the selection.",
                        "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // [0] = outer boundary, [1..n] = holes — đúng chuẩn Revit
                _handler.IsBatchMode      = false;
                _handler.ProfilePoints    = null;
                _handler.ProfileCurves    = profile;
                _handler.TargetLevelId    = SelectedLevel.Id;
                _handler.TargetFloorTypeId = SelectedFloorType.Id;
                _handler.OffsetMm         = OffsetMm;
                _handler.TransactionName  = "Create floor from CAD Polyline";
                _exEvent.Raise();
            }
            catch (Exception ex)
            {
                this.Show();
                MessageBox.Show("Error:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────
        // NÚT 3: TẠO SÀN TỰ ĐỘNG TỪ HATCH
        // ─────────────────────────────────────────────

        private void BtnAutoHatch_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateCommonParams()) return;
            try
            {
                this.Hide();
                var cadSvc = GetCadService();
                var hatches = cadSvc.SelectAndParseHatches();
                this.Show();

                if (hatches.Count == 0)
                {
                    MessageBox.Show("No Hatch entities found in the selection.",
                        "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Mở cửa sổ mapping
                var mappingWindow = new HatchMappingWindow(_doc, SelectedLevel);
                mappingWindow.Owner = this;

                int totalHatches = 0;
                foreach (var kvp in hatches)
                {
                    totalHatches += kvp.Value.Count;
                    foreach (var hatchObj in kvp.Value)
                        mappingWindow.AddMapping(kvp.Key, hatchObj);
                }

                if (mappingWindow.ShowDialog() != true) return;

                // Build danh sách BatchItems từ kết quả mapping
                var batchItems = new List<FloorCreationItem>();
                foreach (var map in mappingWindow.Mappings)
                {
                    if (map.SelectedFloorType == null) continue;
                    foreach (var hatchObj in map.ConnectedCadHatches)
                    {
                        try
                        {
                            var loops = cadSvc.ExtractHatchBoundaries(hatchObj);
                            if (loops == null || loops.Count == 0) continue;
                            batchItems.Add(new FloorCreationItem
                            {
                                LevelId       = SelectedLevel.Id,
                                FloorTypeId   = map.SelectedFloorType.Id,
                                OffsetMm      = map.OffsetFromLevel,
                                ProfileCurves = loops
                            });
                        }
                        catch { /* bỏ qua hatch lỗi */ }
                    }
                }

                if (batchItems.Count == 0)
                {
                    MessageBox.Show("No floors prepared. Please check the mapping table.",
                        "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Raise ExternalEvent — Transaction chạy trong API context
                _handler.IsBatchMode     = true;
                _handler.BatchItems      = batchItems;
                _handler.ProfilePoints   = null;
                _handler.ProfileCurves   = null;
                _handler.TotalScannedHatches = totalHatches;
                _handler.TransactionName = "Auto Floor Creation from Hatch";
                _exEvent.Raise();
            }
            catch (Exception ex)
            {
                this.Show();
                MessageBox.Show("Error:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────
        // NÚT: CANCEL / ĐÓNG CỬA SỔ
        // ─────────────────────────────────────────────

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
