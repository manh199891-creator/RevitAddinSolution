using System;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;

namespace Antigravity.AutoFoundation.UI
{
    public partial class AutoFoundationWindow : Window
    {
        private ExternalEvent _externalEvent;

        public AutoFoundationWindow(AutoFoundationViewModel viewModel, ExternalEvent externalEvent)
        {
            InitializeComponent();
            this.DataContext = viewModel;
            _externalEvent = externalEvent;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void DrawFoundation_Click(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as AutoFoundationViewModel;
            if (vm == null) return;

            // Trigger Revit logic through External Event
            _externalEvent.Raise();
        }
    }
}
