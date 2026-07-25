using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

using Antigravity.DrawColumns.Models;
using Antigravity.DrawColumns.Services;

namespace Antigravity.DrawColumns.UI
{
    public partial class MainWindow : Window
    {
        private UIApplication _uiapp;
        private Document _doc;
        
        private CadInteropService _cadService = new CadInteropService();
        private BuildColumnsEventHandler _handler;
        private ExternalEvent _exEvent;

        public MainWindow(UIApplication uiapp)
        {
            InitializeComponent();
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;
            
            _handler = new BuildColumnsEventHandler();
            _exEvent = ExternalEvent.Create(_handler);
            
            LoadLevels();
            LoadFamilies();
        }

        private void LoadLevels()
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            ICollection<Element> levels = collector.OfClass(typeof(Level)).ToElements();
            
            foreach (Level lvl in levels)
            {
                cmbLevelBot.Items.Add(new ComboBoxItem() { Content = lvl.Name, Tag = lvl });
                cmbLevelTop.Items.Add(new ComboBoxItem() { Content = lvl.Name, Tag = lvl });
            }
            
            if (cmbLevelBot.Items.Count > 0)
            {
                cmbLevelBot.SelectedIndex = 0;
            }
            if (cmbLevelTop.Items.Count > 1)
            {
                cmbLevelTop.SelectedIndex = 1;
            }
            else if (cmbLevelTop.Items.Count > 0)
            {
                cmbLevelTop.SelectedIndex = 0;
            }
        }

        private void LoadFamilies()
        {
            cmbColumnCircle.Items.Clear();
            cmbColumnRectang.Items.Clear();

            FilteredElementCollector structCollector = new FilteredElementCollector(_doc);
            structCollector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralColumns);

            FilteredElementCollector archCollector = new FilteredElementCollector(_doc);
            archCollector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_Columns);

            List<FamilySymbol> allSymbols = new List<FamilySymbol>();
            foreach (var elem in structCollector) allSymbols.Add((FamilySymbol)elem);
            foreach (var elem in archCollector) allSymbols.Add((FamilySymbol)elem);

            HashSet<string> addedFamilies = new HashSet<string>();

            foreach (FamilySymbol sym in allSymbols)
            {
                if (sym == null) continue;
                Family f = sym.Family;
                if (f != null && !addedFamilies.Contains(f.Name))
                {
                    addedFamilies.Add(f.Name);
                    
                    ComboBoxItem item1 = new ComboBoxItem() { Content = f.Name, Tag = f };
                    ComboBoxItem item2 = new ComboBoxItem() { Content = f.Name, Tag = f };
                    
                    cmbColumnCircle.Items.Add(item1);
                    cmbColumnRectang.Items.Add(item2);
                }
            }

            if (cmbColumnCircle.Items.Count > 0) cmbColumnCircle.SelectedIndex = 0;
            if (cmbColumnRectang.Items.Count > 0) cmbColumnRectang.SelectedIndex = 0;
        }

        private bool EnsureOrigins()
        {
            if (!Antigravity.Core.Services.CoordinateService.IsOriginSet)
            {
                MessageBox.Show("Vui lòng thiết lập gốc tọa độ (Set Coordinate Origin) trước khi vẽ.", 
                    "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void BtnSetOrigin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Hide();
                XYZ revitPt = _uiapp.ActiveUIDocument.Selection.PickPoint(
                    Autodesk.Revit.UI.Selection.ObjectSnapTypes.Intersections,
                    "Click the origin point on the Revit plan...");
                this.Show();

                _cadService.Connect();
                _cadService.SetOriginFromRevitPoint(revitPt);

                MessageBox.Show(
                    "✅ Coordinate origin set successfully!\nCAD↔Revit offset saved.",
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

        private void BtnSelectCADObjects_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureOrigins()) return;

            try
            {
                _cadService.Connect();
                var cadCols = _cadService.GetColumnData();

                if (cadCols.Count > 0)
                {
                    RunBuildingProcess(cadCols);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
        }

        private void RunBuildingProcess(List<RevitColumnData> cadCols)
        {
            ComboBoxItem selectedFamilyItem = cmbColumnRectang.SelectedItem as ComboBoxItem;
            if (selectedFamilyItem == null)
            {
                MessageBox.Show("Vui lòng chọn Family cột Rectang!");
                return;
            }

            ComboBoxItem selectedCircleItem = cmbColumnCircle.SelectedItem as ComboBoxItem;

            Family familyRect = selectedFamilyItem.Tag as Family;
            FamilySymbol baseSymbolRect = GetFirstSymbol(familyRect);

            FamilySymbol baseSymbolCirc = null;
            if (selectedCircleItem != null)
            {
                Family familyCirc = selectedCircleItem.Tag as Family;
                baseSymbolCirc = GetFirstSymbol(familyCirc);
            }

            if (baseSymbolRect == null)
            {
                MessageBox.Show("Không lấy được FamilyType cho Rectang.");
                return;
            }

            ComboBoxItem levelBotItem = cmbLevelBot.SelectedItem as ComboBoxItem;
            ComboBoxItem levelTopItem = cmbLevelTop.SelectedItem as ComboBoxItem;

            if (levelBotItem == null)
            {
                MessageBox.Show("Vui lòng chọn Level Bot!");
                return;
            }

            Level baseLevel = levelBotItem.Tag as Level;
            Level topLevel = levelTopItem?.Tag as Level;

            double botOffset = 0;
            double topOffset = 0;
            double.TryParse(txtOffsetBot.Text, out botOffset);
            double.TryParse(txtOffsetTop.Text, out topOffset);

            string pB = cmbParamB.Text;
            string pH = cmbParamH.Text;
            string pDia = cmbParamDia.Text;

            _handler.CadColumns = cadCols;
            _handler.BaseSymbolRect = baseSymbolRect;
            _handler.BaseSymbolCirc = baseSymbolCirc;
            _handler.BaseLevel = baseLevel;
            _handler.TopLevel = topLevel;
            _handler.BotOffset = botOffset;
            _handler.TopOffset = topOffset;
            _handler.ParamB = pB;
            _handler.ParamH = pH;
            _handler.ParamDia = pDia;

            _exEvent.Raise();
        }
        
        private FamilySymbol GetFirstSymbol(Family family)
        {
            if (family == null) return null;
            foreach (var sId in family.GetFamilySymbolIds())
            {
                return _doc.GetElement(sId) as FamilySymbol;
            }
            return null;
        }



        private void BtnDrawRectang_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureOrigins()) return;

            try
            {
                _cadService.Connect();
                var pts = _cadService.GetRectanglePoints();
                if (pts != null && pts.Length == 2)
                {
                    double[] p1 = pts[0];
                    double[] p2 = pts[1];

                    RevitColumnData data = new RevitColumnData();
                    data.Width = Math.Abs(p1[0] - p2[0]);
                    data.Height = Math.Abs(p1[1] - p2[1]);
                    data.X = (p1[0] + p2[0]) / 2.0;
                    data.Y = (p1[1] + p2[1]) / 2.0;
                    data.Rotation = 0;
                    data.IsRound = false;

                    RunBuildingProcess(new List<RevitColumnData> { data });
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("Hủy"))
                    MessageBox.Show("Lỗi: " + ex.Message);
            }
        }

        private void BtnDrawPolyline_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureOrigins()) return;

            try
            {
                _cadService.Connect();
                RevitColumnData data = _cadService.GetSingleEntity();
                if (data != null)
                {
                    RunBuildingProcess(new List<RevitColumnData> { data });
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("Hủy"))
                    MessageBox.Show("Lỗi: " + ex.Message);
            }
        }

        private void BtnDrawHatch_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureOrigins()) return;

            try
            {
                _cadService.Connect();
                var cadCols = _cadService.GetHatchData();
                if (cadCols.Count > 0)
                {
                    RunBuildingProcess(cadCols);
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("Hủy"))
                    MessageBox.Show("Lỗi: " + ex.Message);
            }
        }

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
