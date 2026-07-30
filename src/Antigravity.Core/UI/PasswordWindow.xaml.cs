using System.Windows;
using System.Windows.Input;

namespace Antigravity.Core.UI
{
    public partial class PasswordWindow : Window
    {
        public bool IsUnlocked { get; private set; } = false;

        public PasswordWindow()
        {
            InitializeComponent();
            // Focus vào ô mật khẩu ngay khi mở
            Loaded += (s, e) => txtPassword.Focus();
        }

        private void BtnUnlock_Click(object sender, RoutedEventArgs e)
        {
            TryUnlock();
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            // Cho phép nhấn Enter thay vì phải bấm nút
            if (e.Key == Key.Enter)
                TryUnlock();
        }

        private void TryUnlock()
        {
            string input = txtPassword.Password;

            if (Services.SecurityService.CheckPassword(input))
            {
                IsUnlocked = true;
                this.Close();
            }
            else
            {
                lblError.Text = "❌ Incorrect password. Please try again.";
                txtPassword.Clear();
                txtPassword.Focus();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsUnlocked = false;
            this.Close();
        }
    }
}
