using System.Collections.Generic;
using System.Windows;
using Antigravity.Formwork.Core.Models;

namespace Antigravity.Formwork.UI
{
    public partial class AutoFormworkWindow : Window
    {
        public bool IsConfirmed { get; private set; }
        public FormworkPanelSystem SelectedSystem => CmbSystems.SelectedItem as FormworkPanelSystem;

        public AutoFormworkWindow(List<FormworkPanelSystem> systems)
        {
            InitializeComponent();
            CmbSystems.ItemsSource = systems;
            if (systems.Count > 0)
                CmbSystems.SelectedIndex = 0;
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }
    }
}
