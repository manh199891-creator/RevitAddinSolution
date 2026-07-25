using System.Windows;
using Antigravity.SharedParamMapper.ViewModels;

namespace Antigravity.SharedParamMapper.Views
{
    public partial class ParamMapperWindow : Window
    {
        private int _lastClickedFamilyTypeIndex = -1;

        public ParamMapperWindow(ParamMapperViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void FamilyTypeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox cb && cb.DataContext is FamilyTypeItem clickedItem)
            {
                var vm = DataContext as ParamMapperViewModel;
                if (vm == null) return;

                int currentIndex = vm.FamilyTypes.IndexOf(clickedItem);
                if (currentIndex == -1) return;

                if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift) && _lastClickedFamilyTypeIndex != -1)
                {
                    int start = System.Math.Min(_lastClickedFamilyTypeIndex, currentIndex);
                    int end = System.Math.Max(_lastClickedFamilyTypeIndex, currentIndex);
                    bool targetState = cb.IsChecked ?? false;

                    for (int i = start; i <= end; i++)
                    {
                        vm.FamilyTypes[i].IsSelected = targetState;
                    }
                }

                _lastClickedFamilyTypeIndex = currentIndex;
            }
        }
    }
}
