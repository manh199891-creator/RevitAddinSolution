using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Antigravity.AutoDimWalls.Models;
using Antigravity.AutoDimWalls.Services;

namespace Antigravity.AutoDimWalls.UI
{
    public partial class AutoDimWindow : Window
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;
        private readonly AutoDimEventHandler _eventHandler;
        private XYZ _pickedPoint;

        public AutoDimWindow(UIDocument uiDoc, ExternalEvent externalEvent, AutoDimEventHandler eventHandler)
        {
            InitializeComponent();
            _uiDoc = uiDoc;
            _doc = uiDoc.Document;
            _externalEvent = externalEvent;
            _eventHandler = eventHandler;
            LoadDimensionTypes();
            LoadRevitLinks();
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _eventHandler.Action = AutoDimAction.Create;
                _eventHandler.Options = ReadOptions();
                _eventHandler.OnCompleted = (result) =>
                {
                    TxtStatus.Text = result.Message;
                    if (result.LogMessages != null && result.LogMessages.Count > 0)
                    {
                        if (result.LogMessages.Count <= 15)
                        {
                            string logText = string.Join(Environment.NewLine, result.LogMessages);
                            MessageBox.Show(logText, "AutoDim Skip Logs", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoDim_Logs.txt");
                            System.IO.File.WriteAllLines(tempFile, result.LogMessages);
                            System.Diagnostics.Process.Start(tempFile);
                        }
                    }
                };
                _externalEvent.Raise();
                TxtStatus.Text = "Running AutoDim...";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "AutoDim failed: " + ex.Message;
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _eventHandler.Action = AutoDimAction.Delete;
                _eventHandler.OnCompleted = (result) =>
                {
                    TxtStatus.Text = result.Message;
                };
                _externalEvent.Raise();
                TxtStatus.Text = "Deleting AutoDim dimensions...";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Delete failed: " + ex.Message;
            }
        }

        private void BtnPickPoint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                _pickedPoint = _uiDoc.Selection.PickPoint(ObjectSnapTypes.None, "Pick dimension line placement point");
                if (_pickedPoint != null)
                {
                    double xMm = _pickedPoint.X * 304.8;
                    double yMm = _pickedPoint.Y * 304.8;
                    double zMm = _pickedPoint.Z * 304.8;
                    TxtPickStatus.Text = $"Picked: ({xMm:F0}, {yMm:F0})";
                    TxtPickStatus.Foreground = System.Windows.Media.Brushes.Gold;
                }
                else
                {
                    TxtPickStatus.Text = "No point picked";
                    TxtPickStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0xCC));
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                TxtPickStatus.Text = "Pick cancelled";
                TxtPickStatus.Foreground = System.Windows.Media.Brushes.Orange;
            }
            catch (Exception ex)
            {
                TxtPickStatus.Text = "Error: " + ex.Message;
                TxtPickStatus.Foreground = System.Windows.Media.Brushes.Red;
                TxtStatus.Text = "Pick point error: " + ex.Message;
            }
            finally
            {
                ShowSafe();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDimensionTypes();
            TxtStatus.Text = "Refreshed dimension styles.";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadDimensionTypes()
        {
            var dimensionTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(DimensionType))
                .OfType<DimensionType>()
                .Where(t => t.StyleType == DimensionStyleType.Linear)
                .OrderBy(t => t.Name)
                .ToList();

            CboDimensionTypes.ItemsSource = dimensionTypes;
            if (dimensionTypes.Count > 0)
                CboDimensionTypes.SelectedIndex = 0;
        }

        private AutoDimOptions ReadOptions()
        {
            double offset = 1000.0;
            if (!double.TryParse(TxtOffset.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out offset))
                double.TryParse(TxtOffset.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out offset);

            var selectedLinkItem = CboLinkInstances.SelectedItem as LinkItem;

            return new AutoDimOptions
            {
                IsSectionView = RadViewSection.IsChecked == true,
                Scope = RadActiveView.IsChecked == true ? AutoDimScope.ActiveView : AutoDimScope.Selection,
                IncludeOpenings = ChkOpenings.IsChecked == true,
                IncludeGrids = ChkGrids.IsChecked == true,
                IncludeLinks = ChkLinks.IsChecked == true,
                SelectedLinkInstanceId = selectedLinkItem?.Instance?.Id,
                IncludeHostStructural = ChkHostStructural.IsChecked == true,
                IncludeIntersectingWalls = ChkIntersections.IsChecked == true,
                OpeningMode = RadOpeningJambs.IsChecked == true ? OpeningReferenceMode.Jambs : OpeningReferenceMode.Center,
                OffsetMm = offset <= 0 ? 1000.0 : offset,
                PickedPoint = _pickedPoint,
                DimensionType = CboDimensionTypes.SelectedItem as DimensionType
            };
        }

        private void LoadRevitLinks()
        {
            var links = new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(link => link.GetLinkDocument() != null)
                .OrderBy(link => link.Name)
                .ToList();

            var list = new System.Collections.Generic.List<LinkItem>();
            list.Add(new LinkItem { Name = "-- Tất cả file Link --", Instance = null });

            foreach (var link in links)
            {
                list.Add(new LinkItem { Name = link.Name, Instance = link });
            }

            CboLinkInstances.ItemsSource = list;
            CboLinkInstances.SelectedIndex = 0;
        }

        public class LinkItem
        {
            public string Name { get; set; }
            public RevitLinkInstance Instance { get; set; }
        }

        private void ShowSafe()
        {
            if (!IsVisible)
                Show();

            Activate();
        }

        private void RadView_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtStatus == null) return;

            if (RadViewSection.IsChecked == true)
            {
                TxtStatus.Text = "Chế độ Mặt cắt hoạt động: Dim chiều cao tường, lanh tô, giằng và cao độ sàn.";
                // BtnRun.IsEnabled = true;
            }
            else
            {
                TxtStatus.Text = "Ready.";
                // BtnRun.IsEnabled = true;
            }
        }
    }
}
