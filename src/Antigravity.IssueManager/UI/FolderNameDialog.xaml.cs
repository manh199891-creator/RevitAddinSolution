using System.Windows;

namespace Antigravity.IssueManager.UI
{
    public partial class FolderNameDialog : Window
    {
        public FolderNameDialog()
        {
            InitializeComponent();
            Loaded += (sender, args) => TxtFolderName.Focus();
        }

        public string FolderName { get; private set; }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string folderName = TxtFolderName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(folderName))
            {
                MessageBox.Show("Folder name is required.", "New Folder", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtFolderName.Focus();
                return;
            }

            FolderName = folderName;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
