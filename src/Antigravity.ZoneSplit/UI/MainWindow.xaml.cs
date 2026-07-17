using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;

namespace Antigravity.ZoneSplit.UI
{
    public partial class MainWindow : Window
    {
        private bool _isUpdating = false;

        public List<BuiltInCategory> SelectedCategories { get; private set; } = new List<BuiltInCategory>();
        
        public bool IsDataOnly { get; private set; }
        public bool IsCreateParts { get; private set; }
        public bool IsPhysicalSplit { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ChkAll_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || chkColumns == null || chkFraming == null || chkFloors == null || chkWalls == null || chkFoundation == null) return;
            _isUpdating = true;
            chkColumns.IsChecked = true;
            chkFraming.IsChecked = true;
            chkFloors.IsChecked = true;
            chkWalls.IsChecked = true;
            chkFoundation.IsChecked = true;
            _isUpdating = false;
        }

        private void ChkAll_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || chkColumns == null || chkFraming == null || chkFloors == null || chkWalls == null || chkFoundation == null) return;
            _isUpdating = true;
            chkColumns.IsChecked = false;
            chkFraming.IsChecked = false;
            chkFloors.IsChecked = false;
            chkWalls.IsChecked = false;
            chkFoundation.IsChecked = false;
            _isUpdating = false;
        }

        private void ChkItem_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || chkAll == null || chkColumns == null || chkFraming == null || chkFloors == null || chkWalls == null || chkFoundation == null) return;
            _isUpdating = true;

            bool allChecked = chkColumns.IsChecked == true &&
                              chkFraming.IsChecked == true &&
                              chkFloors.IsChecked == true &&
                              chkWalls.IsChecked == true &&
                              chkFoundation.IsChecked == true;

            chkAll.IsChecked = allChecked;
            _isUpdating = false;
        }

        private void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            SelectedCategories.Clear();

            if (chkColumns.IsChecked == true) SelectedCategories.Add(BuiltInCategory.OST_StructuralColumns);
            if (chkFraming.IsChecked == true) SelectedCategories.Add(BuiltInCategory.OST_StructuralFraming);
            if (chkFloors.IsChecked == true) SelectedCategories.Add(BuiltInCategory.OST_Floors);
            if (chkWalls.IsChecked == true) SelectedCategories.Add(BuiltInCategory.OST_Walls);
            if (chkFoundation.IsChecked == true) SelectedCategories.Add(BuiltInCategory.OST_StructuralFoundation);

            if (SelectedCategories.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một loại cấu kiện để tính toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsDataOnly = optDataOnly.IsChecked == true;
            IsCreateParts = optParts.IsChecked == true;
            IsPhysicalSplit = optPhysical.IsChecked == true;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
