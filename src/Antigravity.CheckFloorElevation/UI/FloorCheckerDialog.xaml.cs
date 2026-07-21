using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using Antigravity.CheckFloorElevation.Models;
using Antigravity.CheckFloorElevation.Services;
using Antigravity.Core.Services;

namespace Antigravity.CheckFloorElevation.UI
{
    public partial class FloorCheckerDialog : Window
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly FloorCollectorService _collector;
        private readonly ObservableCollection<FloorResultViewModel> _viewModels;
        private List<FloorCheckResult> _lastResults;
        private SimpleEventHandler _eventHandler;
        private ExternalEvent _externalEvent;

        public FloorCheckerDialog(UIDocument uiDoc)
        {
            InitializeComponent();

            _uiDoc = uiDoc;
            _doc = uiDoc.Document;
            _collector = new FloorCollectorService(_doc);
            _viewModels = new ObservableCollection<FloorResultViewModel>();
            GridResults.ItemsSource = _viewModels;

            _eventHandler = new SimpleEventHandler();
            _externalEvent = ExternalEvent.Create(_eventHandler);

            LoadLinks();
            LoadHostLevels();
        }

        protected override void OnClosed(EventArgs e)
        {
            _externalEvent?.Dispose();
            _externalEvent = null;
            base.OnClosed(e);
        }

        private void ExecuteOnRevitThread(Action action)
        {
            _eventHandler.Enqueue(action);
            _externalEvent.Raise();
        }

        private void LoadLinks()
        {
            List<LinkItem> links = _collector.GetLoadedLinks()
                .Select(link => new LinkItem(link))
                .ToList();

            CmbLinks.ItemsSource = links;
            if (links.Count > 0)
            {
                CmbLinks.SelectedIndex = 0;
                TxtStatusBar.Text = links.Count + " loaded link(s) found.";
            }
            else
            {
                TxtStatusBar.Text = "No loaded Revit links found.";
            }
        }

        private void LoadHostLevels()
        {
            List<LevelItem> levels = new List<LevelItem>();
            levels.Add(LevelItem.AllLevels());
            levels.AddRange(_collector.GetHostLevels().Select(level => new LevelItem(level)));

            CmbHostLevels.ItemsSource = levels;
            levels[0].IsChecked = true;
            CmbHostLevels.SelectedIndex = 0;
        }

        public void LevelCheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            LevelItem clicked = checkBox?.DataContext as LevelItem;
            List<LevelItem> levels = GetLevelItems();
            if (clicked == null || levels.Count == 0)
                return;

            if (clicked.IsAllLevels && clicked.IsChecked)
            {
                foreach (LevelItem level in levels.Where(level => !level.IsAllLevels))
                    level.IsChecked = false;
                return;
            }

            if (!clicked.IsAllLevels && clicked.IsChecked)
            {
                LevelItem allLevels = levels.FirstOrDefault(level => level.IsAllLevels);
                if (allLevels != null)
                    allLevels.IsChecked = false;
            }

            if (!levels.Any(level => level.IsChecked))
            {
                LevelItem allLevels = levels.FirstOrDefault(level => level.IsAllLevels);
                if (allLevels != null)
                    allLevels.IsChecked = true;
            }
        }

        private List<LevelItem> GetLevelItems()
        {
            return (CmbHostLevels.ItemsSource as IEnumerable<LevelItem>)?.ToList() ?? new List<LevelItem>();
        }

        private IList<ElementId> GetSelectedLevelIds()
        {
            List<LevelItem> levels = GetLevelItems();
            if (levels.Any(level => level.IsAllLevels && level.IsChecked))
                return new List<ElementId>();

            return levels
                .Where(level => !level.IsAllLevels && level.IsChecked)
                .Select(level => level.LevelId)
                .ToList();
        }

        private string GetSelectedLevelNames()
        {
            List<LevelItem> levels = GetLevelItems();
            if (levels.Any(level => level.IsAllLevels && level.IsChecked))
                return "All host levels";

            List<string> names = levels
                .Where(level => !level.IsAllLevels && level.IsChecked)
                .Select(level => level.Name)
                .ToList();

            return names.Count == 0 ? "All host levels" : string.Join(", ", names);
        }

        public void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (!(CmbLinks.SelectedItem is LinkItem selectedLink))
            {
                MessageBox.Show(this, "Please select a loaded linked model.", "Missing Link", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtTolerance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double toleranceMm) || toleranceMm <= 0)
            {
                MessageBox.Show(this, "Tolerance must be a positive number in millimeters.", "Invalid Tolerance", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetRunningState(true);
            _viewModels.Clear();
            _lastResults = new List<FloorCheckResult>();
            WindowHeader.SubtitleText = string.Empty;

            bool activeViewOnly = ChkActiveViewOnly.IsChecked == true;
            IList<ElementId> selectedLevelIds = GetSelectedLevelIds();
            string selectedLevelNames = GetSelectedLevelNames();

            ExecuteOnRevitThread(() =>
            {
                try
                {
                    View3D view3D = _collector.FindSuitable3DView(_uiDoc.ActiveView);
                    if (view3D == null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(this, "No suitable 3D view was found. Create a printable 3D view and run again.", "3D View Required", MessageBoxButton.OK, MessageBoxImage.Error);
                            SetRunningState(false);
                        });
                        return;
                    }

                    IList<Floor> hostFloors = _collector.GetHostFloors(_uiDoc.ActiveView, activeViewOnly, selectedLevelIds);
                    
                    Dispatcher.Invoke(() => {
                        PbProgress.Maximum = Math.Max(hostFloors.Count, 1);
                        PbProgress.Value = 0;
                        TxtProgress.Text = "Checking 0 / " + hostFloors.Count + " floor(s)...";
                    });

                    var service = new ElevationComparisonService(_doc, selectedLink.Instance, view3D);
                    _lastResults = service.RunCheck(hostFloors, toleranceMm, (current, total) =>
                    {
                        Dispatcher.Invoke(() => {
                            PbProgress.Maximum = Math.Max(total, 1);
                            PbProgress.Value = current;
                            TxtProgress.Text = "Checking " + current + " / " + total + " floor(s)...";
                        });
                    });

                    Dispatcher.Invoke(() => {
                        foreach (FloorCheckResult result in _lastResults)
                            _viewModels.Add(new FloorResultViewModel(result));

                        int errorCount = _lastResults.Count(result => result.IsError);
                        int noMatchCount = _lastResults.Count(result => result.IsNoMatch);
                        int okCount = _lastResults.Count - errorCount - noMatchCount;

                        WindowHeader.SubtitleText = okCount + " OK | " + errorCount + " Error | " + noMatchCount + " No Match";
                        TxtStatusBar.Text = "Completed. Checked " + _lastResults.Count + " host floor(s). Level: " + selectedLevelNames + ". 3D view: " + view3D.Name;
                        BtnShow3D.IsEnabled = _lastResults.Count > 0;
                        BtnApplyColor.IsEnabled = _lastResults.Count > 0;
                        BtnExportHtml.IsEnabled = _lastResults.Count > 0;
                        BtnResetColor.IsEnabled = _lastResults.Count > 0;
                    });
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "[CheckFloorElevation] Run failed");
                    Dispatcher.Invoke(() => MessageBox.Show(this, "Check failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
                finally
                {
                    Dispatcher.Invoke(() => SetRunningState(false));
                }
            });
        }

        public void BtnShow3D_Click(object sender, RoutedEventArgs e)
        {
            if (!(GridResults.SelectedItem is FloorResultViewModel selectedResult))
            {
                MessageBox.Show(this, "Select a result row first.", "No Floor Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int hostFloorId = selectedResult.HostFloorId;

            ExecuteOnRevitThread(() =>
            {
                try
                {
                    new HostFloorViewService(_uiDoc, _collector).ShowHostFloorIn3D(hostFloorId);
                    Dispatcher.Invoke(() => TxtStatusBar.Text = "Showing host floor " + hostFloorId + " in 3D.");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "[CheckFloorElevation] Show 3D failed");
                    Dispatcher.Invoke(() => MessageBox.Show(this, "Show 3D failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        public void Btn3DPen_Click(object sender, RoutedEventArgs e)
        {
            var penWindow = new PenOverlayWindow(_uiDoc);
            
            var activeViews = _uiDoc.GetOpenUIViews();
            var currentView = activeViews.FirstOrDefault(v => v.ViewId == _uiDoc.ActiveView.Id);
            if (currentView != null)
            {
                var rect = currentView.GetWindowRectangle();
                penWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                penWindow.Left = rect.Left;
                penWindow.Top = rect.Top;
                penWindow.Width = rect.Right - rect.Left;
                penWindow.Height = rect.Bottom - rect.Top;
                penWindow.WindowState = WindowState.Normal;
            }
            else
            {
                penWindow.Owner = this;
            }

            penWindow.ShowDialog();

            if (penWindow.IsDone && penWindow.Strokes.Count > 0)
            {
                ExecuteOnRevitThread(() =>
                {
                    try
                    {
                        new Pen3DService(_doc, _uiDoc).Create3DStrokes(penWindow.Strokes);
                        Dispatcher.Invoke(() => TxtStatusBar.Text = "3D Pen strokes added to model.");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error(ex, "[CheckFloorElevation] 3D Pen failed");
                        Dispatcher.Invoke(() => MessageBox.Show(this, "3D Pen failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                });
            }
        }

        public void BtnApplyColor_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0)
                return;

            ExecuteOnRevitThread(() =>
            {
                try
                {
                    new ColorOverrideService(_doc, _uiDoc.ActiveView).ApplyOverrides(_lastResults);
                    Dispatcher.Invoke(() => TxtStatusBar.Text = "Color overrides applied to the active view.");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "[CheckFloorElevation] Apply color failed");
                    Dispatcher.Invoke(() => MessageBox.Show(this, "Apply color failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        public void BtnExportHtml_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0)
                return;

            if (!(CmbLinks.SelectedItem is LinkItem selectedLink))
                return;

            double.TryParse(TxtTolerance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double toleranceMm);
            if (toleranceMm <= 0)
                toleranceMm = 20.0;

            bool activeViewOnly = ChkActiveViewOnly.IsChecked == true;
            IList<ElementId> selectedLevelIds = GetSelectedLevelIds();
            string selectedLevelNames = GetSelectedLevelNames();

            string defaultFileName = "FloorElevationCheck_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".html";
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Floor Elevation HTML Report",
                FileName = defaultFileName,
                DefaultExt = ".html",
                Filter = "HTML report (*.html)|*.html|All files (*.*)|*.*",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (saveDialog.ShowDialog(this) != true)
                return;

            string exportPath = saveDialog.FileName;

            ExecuteOnRevitThread(() =>
            {
                try
                {
                    var settings = new CheckSettings
                    {
                        ToleranceMm = toleranceMm,
                        SelectedLinkInstance = selectedLink.Instance,
                        CheckActiveViewOnly = activeViewOnly,
                        SelectedHostLevelIds = selectedLevelIds,
                        SelectedHostLevelName = selectedLevelNames
                    };

                    string path = new ReportService().ExportHtml(_lastResults, settings, _doc.Title, selectedLink.Name, exportPath);
                    Dispatcher.Invoke(() => TxtStatusBar.Text = "HTML report exported: " + path);
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "[CheckFloorElevation] Export HTML failed");
                    Dispatcher.Invoke(() => MessageBox.Show(this, "Export HTML failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        public void BtnResetColor_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0)
                return;

            ExecuteOnRevitThread(() =>
            {
                try
                {
                    new ColorOverrideService(_doc, _uiDoc.ActiveView).ResetOverrides(_lastResults);
                    Dispatcher.Invoke(() => TxtStatusBar.Text = "Color overrides reset in the active view.");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "[CheckFloorElevation] Reset color failed");
                    Dispatcher.Invoke(() => MessageBox.Show(this, "Reset color failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private void SetRunningState(bool isRunning)
        {
            BtnRun.IsEnabled = !isRunning;
            PanelProgress.Visibility = isRunning ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            SepProgress.Visibility = isRunning ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            if (isRunning)
            {
                BtnShow3D.IsEnabled = false;
                BtnApplyColor.IsEnabled = false;
                BtnExportHtml.IsEnabled = false;
                BtnResetColor.IsEnabled = false;
            }
        }
    }

    internal class LinkItem
    {
        public LinkItem(RevitLinkInstance instance)
        {
            Instance = instance;
            Name = instance.Name;
        }

        public string Name { get; private set; }

        public RevitLinkInstance Instance { get; private set; }
    }

    internal class LevelItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public LevelItem(Level level)
        {
            LevelId = level.Id;
            Name = level.Name;
            IsAllLevels = false;
        }

        private LevelItem()
        {
            LevelId = ElementId.InvalidElementId;
            Name = "All host levels";
            IsAllLevels = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get; private set; }

        public ElementId LevelId { get; private set; }

        public bool IsAllLevels { get; private set; }

        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked == value)
                    return;

                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public static LevelItem AllLevels()
        {
            return new LevelItem();
        }

        public override string ToString()
        {
            return Name;
        }
    }

    internal class FloorResultViewModel
    {
        private readonly FloorCheckResult _result;

        public FloorResultViewModel(FloorCheckResult result)
        {
            _result = result;
        }

        public int HostFloorId { get { return _result.HostFloorId; } }

        public string LinkFloorIdDisplay
        {
            get { return _result.LinkFloorId >= 0 ? _result.LinkFloorId.ToString(CultureInfo.InvariantCulture) : "-"; }
        }

        public string HostTypeName { get { return string.IsNullOrWhiteSpace(_result.HostTypeName) ? "-" : _result.HostTypeName; } }

        public string LinkTypeNameDisplay { get { return string.IsNullOrWhiteSpace(_result.LinkTypeName) ? "-" : _result.LinkTypeName; } }

        public string LevelName { get { return _result.LevelName; } }

        public string ZTopHostDisplay { get { return Format(_result.ZTopHostMm); } }

        public string ZTopLinkDisplay { get { return _result.IsNoMatch ? "-" : Format(_result.ZTopLinkMm); } }

        public string DeltaZDisplay { get { return _result.IsNoMatch ? "-" : Format(_result.DeltaZMm); } }

        public string StatusDisplay
        {
            get
            {
                if (_result.IsNoMatch)
                    return "No Match";

                return _result.IsError ? "Error" : "OK";
            }
        }

        public string ErrorMessage { get { return _result.ErrorMessage; } }

        public bool IsError { get { return _result.IsError; } }

        public bool IsNoMatch { get { return _result.IsNoMatch; } }

        private static string Format(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
