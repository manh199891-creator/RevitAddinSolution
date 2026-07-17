using System.Windows;
using System.Windows.Controls;

namespace Antigravity.Core.UI.Controls
{
    public partial class BrandSignature : UserControl
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(BrandSignature),
            new PropertyMetadata("@manhns"));

        public BrandSignature()
        {
            InitializeComponent();
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
    }
}
