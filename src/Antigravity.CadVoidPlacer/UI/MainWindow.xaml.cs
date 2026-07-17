using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Antigravity.CadVoidPlacer.Models;
using Antigravity.CadVoidPlacer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Antigravity.CadVoidPlacer.UI
{
    public partial class MainWindow : Window
    {
        private readonly Document   _doc;
        private readonly UIDocument _uidoc;

        // State populated during the 4 steps
        private List<CadOpening> _openings  = new List<CadOpening>();
        private XYZ              _revitRef  = XYZ.Zero;
        private double           _cadRefX   = 0;
        private double           _cadRefY   = 0;

        // Results consumed by AppCommand
        public List<CadOpening>  ExtractedOpenings { get; private set; } = new List<CadOpening>();
        public GridMappingService GridMapper        { get; private set; }
        public double             DepthMm           { get; private set; } = 300.0;
        public ElementId          SelectedLevelId   { get; private set; }

        public MainWindow(Document doc, UIDocument uidoc)
        {
            InitializeComponent();
            _doc   = doc;
            _uidoc = uidoc;
            LoadLevels();
        }

        private void LoadLevels()
        {
            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            ComboLevels.ItemsSource = levels;
            if (levels.Count > 0)
                ComboLevels.SelectedIndex = 0;
        }

        // ── Step 1: Select geometry in AutoCAD ───────────────────────────────
        private void BtnSelectGeometry_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = CadInteropService.SelectEntities();
                if (!result.HasValue)
                {
                    SetStatus(TxtSelStatus, "Selection cancelled.", "#CC6600");
                    return;
                }

                _openings = result.Value.Openings;

                if (_openings.Count == 0)
                {
                    SetStatus(TxtSelStatus,
                        "No closed polygons found. Make sure you selected closed polylines or at least 3 connected line segments.",
                        "#CC0000");
                    return;
                }

                SetStatus(TxtSelStatus,
                    $"{_openings.Count} polygon(s) detected and ready for placement.",
                    "#107C10");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "AutoCAD Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TabScanLayer_Selected(object sender, RoutedEventArgs e)
        {
            try
            {
                var layers = CadInteropService.GetLayers();
                ComboLayers.ItemsSource = layers;
                if (layers.Count > 0 && ComboLayers.SelectedIndex == -1)
                    ComboLayers.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load AutoCAD layers: " + ex.Message);
            }
        }

        private void BtnScanLayer_Click(object sender, RoutedEventArgs e)
        {
            string layerName = ComboLayers.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(layerName)) return;

            try
            {
                var results = CadInteropService.ScanLayer(layerName);
                _openings = results;

                if (_openings.Count == 0)
                {
                    SetStatus(TxtSelStatus, $"No closed polygons found on layer '{layerName}'.", "#CC0000");
                }
                else
                {
                    SetStatus(TxtSelStatus, $"{_openings.Count} polygon(s) detected on layer '{layerName}'.", "#107C10");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "AutoCAD Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Step 2: Pick CAD reference point ─────────────────────────────────
        private void BtnPickCadRef_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pt = CadInteropService.PickReferencePoint();
                if (pt != null && pt.Length >= 2)
                {
                    _cadRefX            = pt[0];
                    _cadRefY            = pt[1];
                    TxtCadOriginX.Text  = pt[0].ToString("F3");
                    TxtCadOriginY.Text  = pt[1].ToString("F3");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("AutoCAD Error: " + ex.Message);
            }
        }

        // ── Step 3: Pick Revit reference point ───────────────────────────────
        private void BtnPickRevitRef_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                var pt = _uidoc.Selection.PickPoint(
                    ObjectSnapTypes.Intersections,
                    "Pick the grid-intersection reference point in Revit");
                _revitRef = pt;
                SetStatus(TxtRevitStatus,
                    $"Point selected: ({pt.X:F3}, {pt.Y:F3}, {pt.Z:F3}) ft",
                    "#107C10");
            }
            catch
            {
                // User cancelled — keep existing state
            }
            finally
            {
                this.ShowDialog();
            }
        }

        // ── Place Voids ───────────────────────────────────────────────────────
        private void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            // Validate geometry
            if (_openings.Count == 0)
            {
                MessageBox.Show("Please select geometry in AutoCAD first (Step 1).",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parse CAD reference from text boxes (allows manual edit)
            if (!double.TryParse(
                    TxtCadOriginX.Text.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double cadX) ||
                !double.TryParse(
                    TxtCadOriginY.Text.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double cadY))
            {
                MessageBox.Show("CAD reference X / Y must be valid numbers.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate depth
            if (!double.TryParse(
                    TxtDepth.Text.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double depthMm) || depthMm <= 0)
            {
                MessageBox.Show("Void depth must be a positive number (mm).",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtDepth.Focus();
                return;
            }

            ExtractedOpenings = _openings;
            GridMapper        = new GridMappingService(cadX, cadY, _revitRef);
            DepthMm           = depthMm;
            SelectedLevelId   = (ComboLevels.SelectedItem as Level)?.Id ?? ElementId.InvalidElementId;

            this.DialogResult = true;
            this.Close();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private void SetStatus(System.Windows.Controls.TextBlock tb, string msg, string hex)
        {
            tb.Text       = msg;
            tb.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        }
    }
}
