using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Antigravity.DrawWalls.Models;
using Antigravity.DrawWalls.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Antigravity.DrawWalls.UI
{
    public partial class MainWindow : Window
    {
        private ExternalCommandData _commandData;
        private Document _doc;
        private ExternalEvent _externalEvent;
        private WallCreationHandler _wallHandler;

        public MainWindow(ExternalCommandData commandData)
        {
            InitializeComponent();
            _commandData = commandData;
            _doc = commandData.Application.ActiveUIDocument.Document;
            PopulateData();
            
            _wallHandler = new WallCreationHandler();
            _externalEvent = ExternalEvent.Create(_wallHandler);
        }

        private void PopulateData()
        {
            // Load Levels
            List<Level> levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            ComboBaseLevel.ItemsSource = levels;
            ComboBaseLevel.DisplayMemberPath = "Name";
            ComboBaseLevel.SelectedIndex = Math.Min(0, levels.Count - 1);

            ComboTopLevel.ItemsSource = levels;
            ComboTopLevel.DisplayMemberPath = "Name";
            ComboTopLevel.SelectedIndex = Math.Min(1, levels.Count - 1);

            // Load Wall Types
            List<WallType> wallTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .OrderBy(t => t.Name)
                .ToList();

            ComboWallType.ItemsSource = wallTypes;
            ComboWallType.DisplayMemberPath = "Name";
            ComboWallType.SelectedIndex = 0;
        }

        private void BtnSetOrigin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Hide();
                UIDocument uiDoc = _commandData.Application.ActiveUIDocument;
                XYZ revitPt = uiDoc.Selection.PickPoint(
                    Autodesk.Revit.UI.Selection.ObjectSnapTypes.Intersections,
                    "Click the origin point on the Revit plan...");
                this.Show();

                using (CadInteropService cadService = new CadInteropService())
                {
                    cadService.Connect();
                    cadService.SetOriginFromRevitPoint(revitPt);
                }

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

        private void BtnSelectCAD_Click(object sender, RoutedEventArgs e)
        {
            if (!Antigravity.Core.Services.CoordinateService.IsOriginSet)
            {
                MessageBox.Show("Vui lòng thiết lập gốc tọa độ (Set Coordinate Origin) trước khi vẽ.", 
                    "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                this.Hide();
                using (CadInteropService cadService = new CadInteropService())
                {
                    if (!cadService.Connect()) return;

                    // 2. Select entities in CAD
                    List<WallData> wallDatas = cadService.SelectWalls();
                    if (wallDatas == null || wallDatas.Count == 0)
                    {
                        this.Show();
                        return;
                    }

                    // 3. Prepare data for External Event Handler
                    Level baseLevel = ComboBaseLevel.SelectedItem as Level;
                    Level topLevel = ComboTopLevel.SelectedItem as Level;
                    double baseOffset = double.TryParse(TxtBaseOffset.Text, out double bO) ? bO : 0;
                    double topOffset = double.TryParse(TxtTopOffset.Text, out double tO) ? tO : 0;
                    WallType templateType = ComboWallType.SelectedItem as WallType;

                    _wallHandler.WallDatas = wallDatas;
                    _wallHandler.BaseLevel = baseLevel;
                    _wallHandler.TopLevel = topLevel;
                    _wallHandler.BaseOffset = baseOffset;
                    _wallHandler.TopOffset = topOffset;
                    _wallHandler.TemplateType = templateType;

                    // 4. Raise External Event to update Revit safely
                    _externalEvent.Raise();
                }
                this.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
                this.Show();
            }
        }

        private void BtnDrawRect_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tính năng 'Vẽ hình chữ nhật' đang được phát triển.", "Thông báo");
        }

        private void BtnDrawPoly_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tính năng 'Đồ lại Polyline' đang được phát triển.", "Thông báo");
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
