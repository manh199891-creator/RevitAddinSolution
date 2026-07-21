using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using Autodesk.Revit.UI;

namespace Antigravity.CheckFloorElevation.UI
{
    public partial class PenOverlayWindow : Window
    {
        private UIDocument _uiDoc;
        public StrokeCollection Strokes { get; private set; }
        public bool IsDone { get; private set; }

        public PenOverlayWindow(UIDocument uiDoc)
        {
            InitializeComponent();
            _uiDoc = uiDoc;
            Strokes = new StrokeCollection();
            
            Loaded += PenOverlayWindow_Loaded;
        }

        public void PenOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDrawingAttributes((SolidColorBrush)new BrushConverter().ConvertFrom("#FF0000"));
        }

        public void CmbColors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbColors.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(item.Tag.ToString());
                UpdateDrawingAttributes(brush);
            }
        }

        private void UpdateDrawingAttributes(SolidColorBrush brush)
        {
            if (PenCanvas != null)
            {
                var attr = new DrawingAttributes
                {
                    Color = brush.Color,
                    Width = 4,
                    Height = 4,
                    FitToCurve = true
                };
                PenCanvas.DefaultDrawingAttributes = attr;
            }
        }

        public void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (PenCanvas.Strokes.Count > 0)
            {
                PenCanvas.Strokes.RemoveAt(PenCanvas.Strokes.Count - 1);
            }
        }

        public void Clear_Click(object sender, RoutedEventArgs e)
        {
            PenCanvas.Strokes.Clear();
        }

        public void Done_Click(object sender, RoutedEventArgs e)
        {
            Strokes = PenCanvas.Strokes;
            IsDone = true;
            this.Close();
        }

        public void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsDone = false;
            this.Close();
        }
    }
}
