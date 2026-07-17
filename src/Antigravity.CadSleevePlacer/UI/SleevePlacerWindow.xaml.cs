using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Antigravity.CadSleevePlacer.Models;
using Antigravity.CadSleevePlacer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Antigravity.CadSleevePlacer.UI
{
    public partial class SleevePlacerWindow : Window
    {
        private readonly UIApplication _uiApp;
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;
        private readonly SleevePlacementHandler _sleeveHandler;
        
        private XYZ _revitOrigin = XYZ.Zero;
        private List<CadSleeveInfo> _cadSleeves = new List<CadSleeveInfo>();
        private string _cadPath = "";

        public SleevePlacerWindow(UIApplication uiApp)
        {
            InitializeComponent();
            _uiApp = uiApp;
            _doc = uiApp.ActiveUIDocument.Document;

            _sleeveHandler = new SleevePlacementHandler();
            _externalEvent = ExternalEvent.Create(_sleeveHandler);

            LoadLevels();
            LoadFamilies();

            if (Antigravity.Core.Services.CoordinateService.IsOriginSet)
            {
                _revitOrigin = Antigravity.Core.Services.CoordinateService.GetOriginOffset();
                TxtRevitStatus.Text = "✅ Đã đồng nhất toạ độ Revit ↔ CAD.";
                TxtRevitStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 255, 150));
            }
        }

        private void LoadLevels()
        {
            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            ComboLevels.ItemsSource = levels;
            if (levels.Any()) ComboLevels.SelectedIndex = 0;
        }

        private void LoadFamilies()
        {
            var symbols = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .Cast<FamilySymbol>()
                .OrderBy(f => f.Family.Name).ThenBy(f => f.Name)
                .ToList();

            var list = new System.Collections.Generic.List<SleeveTypeItem>();
            list.Add(new SleeveTypeItem { Name = "< Tạo DirectShape - Không dùng Family >", Symbol = null, IsDirectShape = true });

            foreach (var sym in symbols)
            {
                list.Add(new SleeveTypeItem 
                { 
                    Name = $"{sym.Family.Name}: {sym.Name}", 
                    Symbol = sym, 
                    IsDirectShape = false 
                });
            }

            ComboFamilies.ItemsSource = list;
            ComboFamilies.DisplayMemberPath = "Name";
            if (list.Any()) ComboFamilies.SelectedIndex = 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var layers = CadTextInteropService.GetLayers();
                layers.Insert(0, "<Tất cả Layer>");
                ComboLayers.ItemsSource = layers;
                if (layers.Any()) ComboLayers.SelectedIndex = 0;
            }
            catch { }
        }

        private void BtnPickLayer_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                string layer = CadTextInteropService.PickCadLayer();
                if (!string.IsNullOrEmpty(layer))
                {
                    var layers = ComboLayers.ItemsSource as List<string>;
                    if (layers == null || !layers.Contains(layer))
                    {
                        var allLayers = CadTextInteropService.GetLayers();
                        allLayers.Insert(0, "<Tất cả Layer>");
                        ComboLayers.ItemsSource = allLayers;
                        layers = allLayers;
                    }
                    ComboLayers.SelectedItem = layer;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Show();
            }
        }

        private void BtnScanRegion_Click(object sender, RoutedEventArgs e)
        {
            string layer = ComboLayers.SelectedItem as string;
            string regexDN = TxtRegexDN.Text;
            string regexRect = TxtRegexRect.Text;
            string regexCOP = TxtRegexCOP.Text;
            try
            {
                this.Hide();
                var result = CadTextInteropService.SelectEntities(layer, regexDN, regexRect, regexCOP);
                if (result.HasValue)
                {
                    _cadSleeves = result.Value.Sleeves;
                    _cadPath = result.Value.FilePath;
                    UpdateStatus();
                    
                    if (_cadSleeves.Count > 0)
                    {
                        BtnProcess_Click(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "AutoCAD Interop Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Show();
            }
        }

        private void UpdateStatus()
        {
            TxtSelStatus.Text = $"Đã nhận diện {_cadSleeves.Count} sleeves từ CAD.";
            TxtSelStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 255, 150));
        }

        private void BtnSetOrigin_Click(object sender, RoutedEventArgs e)
        {
            DoPickOrigin();
        }

        public void DoPickOrigin()
        {
            try
            {
                this.Hide();
                XYZ revitPt = _uiApp.ActiveUIDocument.Selection.PickPoint(
                    Autodesk.Revit.UI.Selection.ObjectSnapTypes.Intersections,
                    "Click the origin point on the Revit plan...");

                double[] cadPt = CadTextInteropService.PickCadPoint();
                Autodesk.Revit.DB.XYZ offset = new Autodesk.Revit.DB.XYZ(
                    revitPt.X - cadPt[0] / 304.8,
                    revitPt.Y - cadPt[1] / 304.8,
                    0);
                    
                _revitOrigin = offset;
                Antigravity.Core.Services.CoordinateService.SetOriginOffset(offset);

                TxtRevitStatus.Text = "✅ Đã đồng nhất toạ độ Revit ↔ CAD.";
                TxtRevitStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 255, 150));
                
                this.Show();

                // Tự động quét vùng sau khi đồng nhất tọa độ thành công
                BtnScanRegion_Click(null, null);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) 
            { 
                this.Show(); 
            }
            catch (Exception ex)
            {
                this.Show();
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            if (!Antigravity.Core.Services.CoordinateService.IsOriginSet)
            {
                MessageBox.Show("Vui lòng thiết lập gốc tọa độ (Set Origin) trước khi thực hiện.",
                    "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var level = ComboLevels.SelectedItem as Level;
            if (level == null)
            {
                MessageBox.Show("Vui lòng chọn Level.", "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedItem = ComboFamilies.SelectedItem as SleeveTypeItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn Family Sleeve hoặc lựa chọn DirectShape.", "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_cadSleeves == null || _cadSleeves.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu CAD hợp lệ. Vui lòng chọn đối tượng hoặc quét layer trước.",
                    "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DoPlaceSleeves();
        }

        public void DoPlaceSleeves()
        {
            var level = ComboLevels.SelectedItem as Level;
            var selectedItem = ComboFamilies.SelectedItem as SleeveTypeItem;
            var mapper = new GridMappingService(0, 0, _revitOrigin);

            try
            {
                _sleeveHandler.Sleeves = _cadSleeves;
                _sleeveHandler.Mapper = mapper;
                _sleeveHandler.SleeveSymbol = selectedItem.Symbol;
                _sleeveHandler.LevelId = level.Id;
                _sleeveHandler.IsDirectShape = selectedItem.IsDirectShape;
                
                double defaultLength = 300.0;
                if (double.TryParse(TxtDefaultLength.Text, out double parsedLen))
                    defaultLength = parsedLen;
                _sleeveHandler.DefaultLengthMm = defaultLength;

                _externalEvent.Raise();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public class SleeveTypeItem
        {
            public string Name { get; set; }
            public FamilySymbol Symbol { get; set; }
            public bool IsDirectShape { get; set; }
        }
    }
}
