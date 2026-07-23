using System.Windows;
using Antigravity.SharedParamMapper.ViewModels;

namespace Antigravity.SharedParamMapper.Views
{
    public partial class ParamMapperWindow : Window
    {
        public ParamMapperWindow(ParamMapperViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
