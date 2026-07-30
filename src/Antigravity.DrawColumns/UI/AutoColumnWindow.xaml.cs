using System;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;
using Antigravity.DrawColumns.UI;
using Antigravity.DrawColumns.Services;

namespace Antigravity.DrawColumns.UI
{
    public partial class AutoColumnWindow : Window
    {
        private ExternalEvent _externalEvent;
        private AutoColumnRevitEventHandler _handler;

        public AutoColumnWindow(AutoColumnViewModel viewModel, AutoColumnRevitEventHandler handler, ExternalEvent externalEvent)
        {
            InitializeComponent();
            this.DataContext = viewModel;
            _handler = handler;
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

        private void DrawRect_Click(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as AutoColumnViewModel;
            if (vm == null) return;

            // Prepare payload
            _handler.Payload = new AutoColumnPayload
            {
                IsCircle = false,
                LevelBot = vm.SelectedLevelBot,
                LevelTop = vm.SelectedLevelTop,
                OffsetBot = ConvertOffset(vm.OffsetBot),
                OffsetTop = ConvertOffset(vm.OffsetTop),
                FamilyType = vm.SelectedRectFamily,
                ParamB = vm.ParamB,
                ParamH = vm.ParamH
            };

            _externalEvent.Raise();
        }

        private void DrawCircle_Click(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as AutoColumnViewModel;
            if (vm == null) return;

            // Prepare payload
            _handler.Payload = new AutoColumnPayload
            {
                IsCircle = true,
                LevelBot = vm.SelectedLevelBot,
                LevelTop = vm.SelectedLevelTop,
                OffsetBot = ConvertOffset(vm.OffsetBot),
                OffsetTop = ConvertOffset(vm.OffsetTop),
                FamilyType = vm.SelectedCircleFamily,
                ParamDia = vm.ParamDia
            };

            _externalEvent.Raise();
        }
        
        private double ConvertOffset(string offsetStr)
        {
            if (double.TryParse(offsetStr, out double offsetMm))
            {
                // Convert mm to ft
                return offsetMm / 304.8;
            }
            return 0;
        }
    }
}

