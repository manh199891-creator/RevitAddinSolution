using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Antigravity.SharedParamMapper.Views
{
    public class ExportCategoryItem
    {
        public string Name { get; set; }
        public bool IsSelected { get; set; }
    }

    public partial class ExportOptionsWindow : Window
    {
        public List<ExportCategoryItem> Categories { get; private set; }
        
        public ExportOptionsWindow(List<string> categoryNames, string title = "Export Options", string instruction = "Select categories to export:", string buttonText = "Export")
        {
            InitializeComponent();
            this.Title = title;
            InstructionText.Text = instruction;
            ActionBtn.Content = buttonText;
            Categories = categoryNames.OrderBy(n => n).Select(n => new ExportCategoryItem { Name = n, IsSelected = true }).ToList();
            CategoryListBox.ItemsSource = Categories;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (!Categories.Any(c => c.IsSelected))
            {
                MessageBox.Show("Please select at least one category.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
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
