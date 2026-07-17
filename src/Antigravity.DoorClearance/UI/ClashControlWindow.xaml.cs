using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DoorClearanceBox.Core;

namespace DoorClearanceBox.UI
{
    /// <summary>
    /// Provides clash scanning, zoom-to-element, and CSV reporting.
    /// </summary>
    public partial class ClashControlWindow : Window
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private List<ClashResult> _clashes;

        public ClashControlWindow(UIDocument uiDoc)
        {
            InitializeComponent();
            _uiDoc = uiDoc;
            _doc = uiDoc.Document;
            _clashes = new List<ClashResult>();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtNoClashes.Visibility = System.Windows.Visibility.Visible;
            TxtNoClashes.Text = "Nhan 'QUET XUNG DOT' de bat dau quet...";
        }

        private void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnScan.IsEnabled = false;
                BtnScan.Content = "DANG QUET...";

                _clashes = ClashChecker.Scan(_doc);

                GridClashes.ItemsSource = null;
                GridClashes.ItemsSource = _clashes;
                TxtTotalClashes.Text = _clashes.Count.ToString();

                bool hasClashes = _clashes.Count > 0;
                TxtNoClashes.Visibility = hasClashes
                    ? System.Windows.Visibility.Collapsed
                    : System.Windows.Visibility.Visible;
                TxtNoClashes.Text = hasClashes ? string.Empty : "Khong phat hien xung dot khoang mo.";
                BtnShow.IsEnabled = hasClashes;
                BtnExport.IsEnabled = hasClashes;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Clash Control", $"Qua trinh quet gap loi:\n{ex.Message}");
            }
            finally
            {
                BtnScan.IsEnabled = true;
                BtnScan.Content = "QUET XUNG DOT";
            }
        }

        private void BtnShow_Click(object sender, RoutedEventArgs e)
        {
            if (!(GridClashes.SelectedItem is ClashResult selected))
            {
                TaskDialog.Show("Clash Control", "Vui long chon mot dong xung dot trong bang.");
                return;
            }

            try
            {
                var idsToSelect = new List<ElementId>();

                if (selected.ClearanceShape != null)
                    idsToSelect.Add(selected.ClearanceShape.Id);

                if (selected.ClashingElementId != ElementId.InvalidElementId)
                    idsToSelect.Add(selected.ClashingElementId);

                _uiDoc.Selection.SetElementIds(idsToSelect);

                if (selected.ClashingElementId != ElementId.InvalidElementId)
                    _uiDoc.ShowElements(selected.ClashingElementId);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Clash Control", $"Khong the zoom xem doi tuong:\n{ex.Message}");
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_clashes == null || _clashes.Count == 0)
                return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV UTF-8 (Comma delimited) (*.csv)|*.csv",
                FileName = "VilaiViet_BaoCao_XungDot_DoorClearance.csv",
                Title = "Luu bao cao xung dot khoang mo cua"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Door Mark,Door Name,Door Id,Clashing Category,Clashing Name,Clashing Id,Intersection Volume (m3)");

                foreach (var clash in _clashes)
                {
                    sb.AppendLine(string.Join(",",
                        EscapeCsv(clash.DoorMark),
                        EscapeCsv(clash.DoorName),
                        clash.DoorId.Value,
                        EscapeCsv(clash.ClashingCategory),
                        EscapeCsv(clash.ClashingName),
                        clash.ClashingElementId.Value,
                        clash.ClashingVolumeM3));
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
                TaskDialog.Show("Clash Control", $"Da xuat bao cao thanh cong:\n{dialog.FileName}");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Clash Control", $"Khong the xuat file bao cao:\n{ex.Message}");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static string EscapeCsv(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
