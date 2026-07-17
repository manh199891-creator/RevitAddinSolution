using System.Windows;
using System.Windows.Controls;

namespace Antigravity.Core.UI.Controls
{
    public partial class BrandHeader : UserControl
    {
        public static readonly DependencyProperty TitleTextProperty = DependencyProperty.Register(
            nameof(TitleText),
            typeof(string),
            typeof(BrandHeader),
            new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SubtitleTextProperty = DependencyProperty.Register(
            nameof(SubtitleText),
            typeof(string),
            typeof(BrandHeader),
            new PropertyMetadata(string.Empty));

        public BrandHeader()
        {
            InitializeComponent();
        }

        public string TitleText
        {
            get => (string)GetValue(TitleTextProperty);
            set => SetValue(TitleTextProperty, value);
        }

        public string SubtitleText
        {
            get => (string)GetValue(SubtitleTextProperty);
            set => SetValue(SubtitleTextProperty, value);
        }
    }
}
