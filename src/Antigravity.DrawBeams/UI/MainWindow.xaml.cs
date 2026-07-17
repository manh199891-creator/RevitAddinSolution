using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Antigravity.DrawBeams.UI
{
    public class BeamCreationHandler : IExternalEventHandler
    {
        public MainWindow ParentWindow { get; set; }

        public void Execute(UIApplication app)
        {
            ParentWindow.DoProcessing();
        }

        public string GetName() => "Beam Creation Handler";
    }

    public partial class MainWindow : Window
    {
        private UIDocument _uiDoc;
        private Document _doc;
        private CadInteropService _cadService;
        private RevitBeamBuilder _beamBuilder;
        private ExternalEvent _exEvent;
        private BeamCreationHandler _handler;

        public MainWindow(UIDocument uiDoc)
        {
            InitializeComponent();
            _uiDoc = uiDoc;
            _doc = uiDoc.Document;
            _cadService = new CadInteropService();
            _beamBuilder = new RevitBeamBuilder(_doc);

            _handler = new BeamCreationHandler { ParentWindow = this };
            _exEvent = ExternalEvent.Create(_handler);

            LoadRevitData();
            TryConnectCad();
        }

        private void TryConnectCad()
        {
            try
            {
                if (_cadService.Connect())
                {
                    var layers = _cadService.GetLayers();
                    cbBeamLayer.ItemsSource = layers;
                    cbTextLayer.ItemsSource = layers;
                    
                    // Auto-select common layer names if found
                    cbBeamLayer.SelectedItem = layers.FirstOrDefault(l => l.ToLower().Contains("beam") || l.ToLower().Contains("dam"));
                    cbTextLayer.SelectedItem = layers.FirstOrDefault(l => l.ToLower().Contains("text") || l.ToLower().Contains("dim"));
                }
            }
            catch { }
        }

        private void LoadRevitData()
        {
            // Load Levels
            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
            cbLevel.ItemsSource = levels;
            cbLevel.DisplayMemberPath = "Name";
            cbLevel.SelectedIndex = 0;

            // Load Beam Families (Structural Framing)
            var families = new FilteredElementCollector(_doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => f.FamilyCategory.Id.Value == (long)BuiltInCategory.OST_StructuralFraming)
                .ToList();
            cbBeamFamily.ItemsSource = families;
            cbBeamFamily.DisplayMemberPath = "Name";
            if (families.Any()) cbBeamFamily.SelectedIndex = 0;
        }

        private void CbBeamFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbBeamFamily.SelectedItem is Family family)
            {
                var firstSymbolId = family.GetFamilySymbolIds().FirstOrDefault();
                if (firstSymbolId != null)
                {
                    var symbol = _doc.GetElement(firstSymbolId) as FamilySymbol;
                    var parameters = symbol.Parameters.Cast<Parameter>()
                        .Where(p => p.StorageType == StorageType.Double)
                        .Select(p => p.Definition.Name)
                        .OrderBy(n => n)
                        .ToList();

                    cbParamB.ItemsSource = parameters;
                    cbParamH.ItemsSource = parameters;

                    // Auto-select common names
                    cbParamB.SelectedItem = parameters.FirstOrDefault(n => n.ToLower() == "b" || n.ToLower().Contains("width"));
                    cbParamH.SelectedItem = parameters.FirstOrDefault(n => n.ToLower() == "h" || n.ToLower().Contains("height"));
                }
            }
        }

        private void BtnSetOrigin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Hide();
                XYZ revitPt = _uiDoc.Selection.PickPoint(
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

        private void BtnSelectCad_Click(object sender, RoutedEventArgs e)
        {
            if (!Antigravity.Core.Services.CoordinateService.IsOriginSet)
            {
                MessageBox.Show("Vui lòng thiết lập gốc tọa độ (Set Coordinate Origin) trước khi vẽ.", 
                    "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.Hide();
            _exEvent.Raise();
        }

        private void BtnPickBeamLayer_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                if (_cadService.Connect())
                {
                    var info = _cadService.GetEntityInfo();
                    if (info != null && info.ContainsKey("Layer"))
                    {
                        string layer = info["Layer"];
                        if (cbBeamLayer.ItemsSource == null || !((List<string>)cbBeamLayer.ItemsSource).Contains(layer))
                        {
                            var layers = _cadService.GetLayers();
                            cbBeamLayer.ItemsSource = layers;
                            cbTextLayer.ItemsSource = layers;
                        }
                        cbBeamLayer.SelectedItem = layer;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { this.Show(); }
        }

        private void BtnPickTextLayer_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                if (_cadService.Connect())
                {
                    var info = _cadService.GetEntityInfo();
                    if (info != null && info.ContainsKey("Layer"))
                    {
                        string layer = info["Layer"];
                        if (cbTextLayer.ItemsSource == null || !((List<string>)cbTextLayer.ItemsSource).Contains(layer))
                        {
                            var layers = _cadService.GetLayers();
                            cbBeamLayer.ItemsSource = layers;
                            cbTextLayer.ItemsSource = layers;
                        }
                        cbTextLayer.SelectedItem = layer;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { this.Show(); }
        }

        public void DoProcessing()
        {
            try
            {
                // 1. Get selections from UI
                Family selectedFamily = cbBeamFamily.SelectedItem as Family;
                string paramB = cbParamB.SelectedItem?.ToString();
                string paramH = cbParamH.SelectedItem?.ToString();
                Level level = cbLevel.SelectedItem as Level;

                if (selectedFamily == null || level == null || string.IsNullOrEmpty(paramB))
                {
                    MessageBox.Show("Vui lòng chọn đầy đủ Family, Level và Tham số.");
                    this.Show();
                    return;
                }

                // 2. Pick Beams from CAD
                _cadService.Connect();
                
                string beamLayer = cbBeamLayer.SelectedItem?.ToString();
                string textLayer = cbTextLayer.SelectedItem?.ToString();
                
                List<CadBeamData> cadBeams = _cadService.GetCadBeams(beamLayer, textLayer);

                if (cadBeams == null || cadBeams.Count == 0)
                {
                    MessageBox.Show("Không tìm thấy dầm nào phù hợp với Layer đã chọn hoặc không match được Text.");
                    this.Show();
                    return;
                }

                int justification = 1; // Default: Center
                if (rbLeft.IsChecked == true) justification = 0;
                else if (rbRight.IsChecked == true) justification = 2;

                    double offset = 0;
                    double.TryParse(txtOffset.Text, out offset);

                    // 5. Create Beams in Revit
                    using (Transaction trans = new Transaction(_doc, "Create Beams from CAD"))
                    {
                        trans.Start();

                        int successCount = 0;
                        string summary = "V11 - Danh sách dầm tìm được:\n";
                        var groups = cadBeams.GroupBy(b => $"{b.Width}x{b.Height}");
                        foreach (var g in groups) summary += $"- {g.Key}: {g.Count()} đoạn\n";

                        if (MessageBox.Show(summary + "\nBản cập nhật V13.1: Đã chặn dầm > 1200mm và lọc chồng lấn.\nBạn có muốn tiến hành vẽ không?", "Xác nhận V13.1 - Hotfix", MessageBoxButton.YesNo) == MessageBoxResult.No)
                        {
                            trans.RollBack();
                            this.Show();
                            return;
                        }

                        foreach (var cadBeam in cadBeams)
                        {
                            try
                            {
                                // Convert start/end to Revit space using Core CoordinateService
                                // Dùng cao độ của Level (level.Elevation) thay vì 0 để Offset = 0
                                XYZ start = Antigravity.Core.Services.CoordinateService.CadToRevit(cadBeam.StartX, cadBeam.StartY, level.Elevation);
                                XYZ end = Antigravity.Core.Services.CoordinateService.CadToRevit(cadBeam.EndX, cadBeam.EndY, level.Elevation);
                                Line line = Line.CreateBound(start, end);

                                double b = cadBeam.Width;
                                double h = cadBeam.Height;
                                int finalJustification = cadBeam.IsPaired ? 1 : justification;

                                // Fallback
                                if (b <= 0) b = 300;
                                if (h <= 0) h = 600;

                                FamilySymbol symbol = _beamBuilder.GetOrAddBeamType(selectedFamily, b, h, paramB, paramH);
                                if (symbol == null) continue;

                                _beamBuilder.CreateBeam(line, symbol, level, offset, finalJustification, cadBeam.Mark);
                                
                                successCount++;
                            }
                            catch { }
                        }

                        trans.Commit();
                        MessageBox.Show($"Thành công: Đã tạo {successCount} đoạn dầm chuẩn xác.");
                    }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
            finally
            {
                this.Show();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }

        private void BtnDrawPolyline_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tính năng vẽ đa giác (Draw Poly) cho dầm đang được phát triển.", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
