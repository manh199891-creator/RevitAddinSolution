using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Antigravity.IssueManager.UI
{
    public partial class MarkupEditorWindow : Window
    {
        public string ResultImagePath { get; private set; }

        private Color _currentColor = Colors.Red;
        private double _penThickness = 3.0;
        private readonly Stack<Stroke> _undoStack = new Stack<Stroke>();
        private readonly Stack<UIElement> _shapeUndoStack = new Stack<UIElement>();

        private bool _isDrawingShape;
        private Point _shapeStart;
        private UIElement _currentShape;

        public MarkupEditorWindow(string imagePath)
        {
            InitializeComponent();
            LoadImage(imagePath);
            SetupInkCanvas();
            InkLayer.StrokeCollected += (s, e) => _undoStack.Push(e.Stroke);
        }

        private void LoadImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(imagePath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            ImgBase.Source = bmp;
        }

        private void SetupInkCanvas()
        {
            UpdateDrawingAttributes();
            BtnPen.IsChecked = true;
            InkLayer.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void UpdateDrawingAttributes()
        {
            if (InkLayer == null) return;
            InkLayer.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = _currentColor,
                Width = _penThickness,
                Height = _penThickness,
                FitToCurve = true
            };
        }

        private void BtnPen_Checked(object sender, RoutedEventArgs e)
        {
            if (InkLayer == null) return;
            UncheckOthers(BtnPen);
            InkLayer.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void BtnRect_Checked(object sender, RoutedEventArgs e)
        {
            if (InkLayer == null) return;
            UncheckOthers(BtnRect);
            InkLayer.EditingMode = InkCanvasEditingMode.None;
        }

        private void BtnArrow_Checked(object sender, RoutedEventArgs e)
        {
            if (InkLayer == null) return;
            UncheckOthers(BtnArrow);
            InkLayer.EditingMode = InkCanvasEditingMode.None;
        }

        private void BtnText_Checked(object sender, RoutedEventArgs e)
        {
            if (InkLayer == null) return;
            UncheckOthers(BtnText);
            InkLayer.EditingMode = InkCanvasEditingMode.None;
        }

        private void UncheckOthers(System.Windows.Controls.Primitives.ToggleButton active)
        {
            var buttons = new[] { BtnPen, BtnRect, BtnArrow, BtnText };
            foreach (var btn in buttons)
            {
                if (btn != null && btn != active)
                {
                    btn.IsChecked = false;
                }
            }
        }

        private void InkLayer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (BtnRect.IsChecked == true)
            {
                _isDrawingShape = true;
                _shapeStart = e.GetPosition(InkLayer);
                var rect = new Rectangle
                {
                    Stroke = new SolidColorBrush(_currentColor),
                    StrokeThickness = _penThickness,
                    Fill = Brushes.Transparent
                };
                InkCanvas.SetLeft(rect, _shapeStart.X);
                InkCanvas.SetTop(rect, _shapeStart.Y);
                _currentShape = rect;
                InkLayer.Children.Add(rect);
            }
            else if (BtnArrow.IsChecked == true)
            {
                _isDrawingShape = true;
                _shapeStart = e.GetPosition(InkLayer);
            }
            else if (BtnText.IsChecked == true)
            {
                Point pos = e.GetPosition(InkLayer);
                var textBox = new TextBox
                {
                    Background = Brushes.Transparent,
                    BorderBrush = new SolidColorBrush(_currentColor),
                    BorderThickness = new Thickness(1),
                    Foreground = new SolidColorBrush(_currentColor),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    MinWidth = 100,
                    AcceptsReturn = true
                };

                InkCanvas.SetLeft(textBox, pos.X);
                InkCanvas.SetTop(textBox, pos.Y);

                InkLayer.Children.Add(textBox);
                _shapeUndoStack.Push(textBox);

                // Focus the textbox
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.Focus();
                }));

                // When the text box loses focus, remove the border
                textBox.LostFocus += (s, args) =>
                {
                    textBox.BorderThickness = new Thickness(0);
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        InkLayer.Children.Remove(textBox);
                    }
                };

                // Focus again to show border
                textBox.GotFocus += (s, args) =>
                {
                    textBox.BorderBrush = new SolidColorBrush(_currentColor);
                    textBox.BorderThickness = new Thickness(1);
                };
            }
        }

        private void InkLayer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawingShape || e.LeftButton != MouseButtonState.Pressed) return;
            Point cur = e.GetPosition(InkLayer);

            if (BtnRect.IsChecked == true && _currentShape is Rectangle r)
            {
                double x = Math.Min(_shapeStart.X, cur.X);
                double y = Math.Min(_shapeStart.Y, cur.Y);
                InkCanvas.SetLeft(r, x);
                InkCanvas.SetTop(r, y);
                r.Width = Math.Abs(cur.X - _shapeStart.X);
                r.Height = Math.Abs(cur.Y - _shapeStart.Y);
            }
        }

        private void InkLayer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawingShape) return;
            _isDrawingShape = false;
            if (_currentShape != null)
            {
                _shapeUndoStack.Push(_currentShape);
                _currentShape = null;
            }
            else if (BtnArrow.IsChecked == true)
            {
                Point end = e.GetPosition(InkLayer);
                var arrow = BuildArrow(_shapeStart, end, _currentColor, _penThickness);
                foreach (var el in arrow)
                {
                    InkLayer.Children.Add(el);
                    _shapeUndoStack.Push(el);
                }
            }
        }

        private static List<UIElement> BuildArrow(Point from, Point to, Color color, double thickness)
        {
            var brush = new SolidColorBrush(color);
            var line = new Line
            {
                X1 = from.X, Y1 = from.Y,
                X2 = to.X,   Y2 = to.Y,
                Stroke = brush, StrokeThickness = thickness
            };

            double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
            double headLen = 15;
            double theta = 0.52; // approx 30 degrees pointing back
            double a1 = angle + theta;
            double a2 = angle - theta;
            var h1 = new Line
            {
                X1 = to.X, Y1 = to.Y,
                X2 = to.X - headLen * Math.Cos(a1),
                Y2 = to.Y - headLen * Math.Sin(a1),
                Stroke = brush, StrokeThickness = thickness
            };
            var h2 = new Line
            {
                X1 = to.X, Y1 = to.Y,
                X2 = to.X - headLen * Math.Cos(a2),
                Y2 = to.Y - headLen * Math.Sin(a2),
                Stroke = brush, StrokeThickness = thickness
            };
            return new List<UIElement> { line, h1, h2 };
        }

        private void CmbColor_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbColor != null && CmbColor.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                _currentColor = (Color)ColorConverter.ConvertFromString(item.Tag.ToString());
                UpdateDrawingAttributes();
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (InkLayer.EditingMode == InkCanvasEditingMode.Ink && _undoStack.Count > 0)
            {
                InkLayer.Strokes.Remove(_undoStack.Pop());
                return;
            }
            if (_shapeUndoStack.Count > 0)
            {
                // Note: since an arrow consists of 3 elements (line, h1, h2), we might want to pop multiple times
                // if the last element popped was part of an arrow. But simple pop of the last element is also okay.
                // Let's do a simple pop for now as specified in the plan.
                InkLayer.Children.Remove(_shapeUndoStack.Pop());
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            InkLayer.Strokes.Clear();
            InkLayer.Children.Clear();
            _undoStack.Clear();
            _shapeUndoStack.Clear();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MarkupPanel.UpdateLayout();
                var rtb = new RenderTargetBitmap(
                    (int)MarkupPanel.ActualWidth,
                    (int)MarkupPanel.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);
                rtb.Render(MarkupPanel);

                string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AntigravityIssueManager", "Markups");
                Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, $"markup_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                using (var stream = File.Create(path))
                {
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(rtb));
                    enc.Save(stream);
                }

                ResultImagePath = path;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lưu markup thất bại: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
