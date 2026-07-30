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
using Antigravity.WallMepClash.Models;
using Antigravity.WallMepClash.Services;
using Antigravity.Core.Services;

namespace Antigravity.WallMepClash.UI
{
    public partial class WallMepClashDialog : Window
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly MepLinkCollectorService _linkCollector;
        private readonly WallCollectorService _wallCollector;
        private readonly ObservableCollection<ClashResultViewModel> _viewModels;
        private List<ClashResult> _lastResults;
        private SimpleEventHandler _eventHandler;
        private ExternalEvent _externalEvent;

        public WallMepClashDialog(UIDocument uiDoc)
        {
            InitializeComponent();

            _uiDoc = uiDoc;
            _doc = uiDoc.Document;
            _linkCollector = new MepLinkCollectorService(_doc);
            _wallCollector = new WallCollectorService(_doc);
            _viewModels = new ObservableCollection<ClashResultViewModel>();
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
            List<LinkItem> links = _linkCollector.GetLoadedLinks()
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

        private void CmbLinks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(CmbLinks.SelectedItem is LinkItem selectedLink))
            {
                CmbCategories.ItemsSource = null;
                return;
            }

            Document linkDoc = selectedLink.Instance.GetLinkDocument();
            if (linkDoc == null)
            {
                CmbCategories.ItemsSource = null;
                return;
            }

            IList<BuiltInCategory> availableCategories = _linkCollector.GetAvailableMepCategories(linkDoc);
            List<CategoryItem> categoryItems = availableCategories
                .Select(cat => new CategoryItem(cat))
                .ToList();

            // Mặc định check tất cả các categories có sẵn
            foreach (var item in categoryItems)
            {
                item.IsChecked = true;
            }

            CmbCategories.ItemsSource = categoryItems;
            UpdateCategoriesText();
        }

        private void CategoryCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateCategoriesText();
        }

        private List<CategoryItem> GetCategoryItems()
        {
            return (CmbCategories.ItemsSource as IEnumerable<CategoryItem>)?.ToList() ?? new List<CategoryItem>();
        }

        private void UpdateCategoriesText()
        {
            List<CategoryItem> items = GetCategoryItems();
            int checkedCount = items.Count(item => item.IsChecked);
            CmbCategories.Text = checkedCount == 0 ? "No categories selected" : checkedCount + " categories selected";
        }

        private IList<BuiltInCategory> GetSelectedCategories()
        {
            return GetCategoryItems()
                .Where(item => item.IsChecked)
                .Select(item => item.Category)
                .ToList();
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (!(CmbLinks.SelectedItem is LinkItem selectedLink))
            {
                MessageBox.Show(this, "Please select a loaded linked MEP model.", "Missing Link", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IList<BuiltInCategory> selectedCategories = GetSelectedCategories();
            if (selectedCategories.Count == 0)
            {
                MessageBox.Show(this, "Please select at least one category to check.", "Missing Category", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtThreshold.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parallelThresholdDeg) || parallelThresholdDeg < 0 || parallelThresholdDeg > 90)
            {
                MessageBox.Show(this, "Parallel threshold must be between 0 and 90 degrees.", "Invalid Threshold", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetRunningState(true);
            _viewModels.Clear();
            _lastResults = new List<ClashResult>();
            WindowHeader.SubtitleText = string.Empty;

            bool activeViewOnly = ChkActiveViewOnly.IsChecked == true;
            IList<ElementId> selectedLevelIds = GetSelectedLevelIds();
            string selectedLevelNames = GetSelectedLevelNames();

            ExecuteOnRevitThread(() =>
            {
                try
                {
                    Document linkDoc = selectedLink.Instance.GetLinkDocument();
                    if (linkDoc == null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(this, "Linked document could not be retrieved.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            SetRunningState(false);
                        });
                        return;
                    }

                    // 1. Thu thập Tường trong Host (Có lọc theo Level)
                    IList<Wall> hostWalls = _wallCollector.GetHostWalls(_uiDoc.ActiveView, activeViewOnly, selectedLevelIds);

                    // 2. Thu thập MEP trong Link
                    IList<Element> mepElements = _linkCollector.GetMepElements(linkDoc, selectedCategories);

                    Dispatcher.Invoke(() => {
                        PbProgress.Maximum = Math.Max(hostWalls.Count, 1);
                        PbProgress.Value = 0;
                        TxtProgress.Text = "Running Clash Detection... 0 / " + hostWalls.Count + " walls checked";
                    });

                    var settings = new ClashCheckSettings
                    {
                        SelectedLink = selectedLink.Instance,
                        CategoriesToCheck = selectedCategories.ToList(),
                        ParallelThresholdDeg = parallelThresholdDeg,
                        ActiveViewOnly = activeViewOnly
                    };

                    var detector = new WallMepClashDetector();
                    _lastResults = detector.RunCheck(
                        _doc,
                        hostWalls,
                        selectedLink.Instance,
                        mepElements,
                        settings,
                        (current, total) =>
                        {
                            Dispatcher.Invoke(() => {
                                PbProgress.Maximum = Math.Max(total, 1);
                                PbProgress.Value = current;
                                TxtProgress.Text = "Checking " + current + " / " + total + " walls...";
                            });
                        });

                    Dispatcher.Invoke(() => {
                        foreach (ClashResult result in _lastResults)
                        {
                            _viewModels.Add(new ClashResultViewModel(result));
                        }

                        WindowHeader.SubtitleText = _lastResults.Count + " Clashes Found";
                        TxtStatusBar.Text = "Completed. Checked " + hostWalls.Count + " walls and " + mepElements.Count + " MEP elements. Level: " + selectedLevelNames;
                        
                        bool hasResults = _lastResults.Count > 0;
                        BtnShow3D.IsEnabled = hasResults;
                        BtnApplyColor.IsEnabled = hasResults;
                        BtnExportHtml.IsEnabled = hasResults;
                        BtnResetColor.IsEnabled = hasResults;
                    });
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "[WallMepClash] Clash detection failed");
                    Dispatcher.Invoke(() => MessageBox.Show(this, "Check failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
                finally
                {
                    Dispatcher.Invoke(() => SetRunningState(false));
                }
            });
        }

        private void BtnShow3D_Click(object sender, RoutedEventArgs e)
        {
            if (!(GridResults.SelectedItem is ClashResultViewModel selectedClash))
            {
                MessageBox.Show(this, "Select a clash result row first.", "No Clash Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!(CmbLinks.SelectedItem is LinkItem selectedLink))
                return;

            int wallId = selectedClash.HostWallId;
            int mepId = selectedClash.LinkMepId;

            ExecuteOnRevitThread(() =>
            {
                try
                {
                    new ClashViewService(_uiDoc, _wallCollector).ShowClashIn3D(wallId, mepId, selectedLink.Instance);
                    Dispatcher.Invoke(() => TxtStatusBar.Text = $"Isolated Wall {wallId} and MEP {mepId} in 3D.");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "[WallMepClash] Show 3D failed");
                    Dispatcher.Invoke(() => MessageBox.Show(this, "Show 3D failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private void BtnApplyColor_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0)
                return;

            ExecuteOnRevitThread(() =>
            {
                try
                {
                    new ColorOverrideService(_doc, _uiDoc.ActiveView).ApplyOverrides(_lastResults);
                    Dispatcher.Invoke(() => TxtStatusBar.Text = "Applied red overrides to clashing host walls.");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "[WallMepClash] Apply color overrides failed");
                    Dispatcher.Invoke(() => MessageBox.Show(this, "Apply color failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private void BtnExportHtml_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0)
                return;

            if (!(CmbLinks.SelectedItem is LinkItem selectedLink))
                return;

            double.TryParse(TxtThreshold.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parallelThresholdDeg);

            var categories = GetSelectedCategories();
            var activeViewOnly = ChkActiveViewOnly.IsChecked == true;

            string defaultFileName = "WallMepClash_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".html";
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Wall-MEP Clash Report",
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
                    var settings = new ClashCheckSettings
                    {
                        SelectedLink = selectedLink.Instance,
                        CategoriesToCheck = categories.ToList(),
                        ParallelThresholdDeg = parallelThresholdDeg,
                        ActiveViewOnly = activeViewOnly
                    };

                    string path = new ReportService().ExportHtml(_lastResults, settings, _doc.Title, selectedLink.Name, exportPath);
                    
                    // Mở file HTML
                    System.Diagnostics.Process.Start(path);

                    Dispatcher.Invoke(() => TxtStatusBar.Text = "HTML report exported: " + path);
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "[WallMepClash] Export HTML failed");
                    Dispatcher.Invoke(() => MessageBox.Show(this, "Export HTML failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private void BtnResetColor_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0)
                return;

            ExecuteOnRevitThread(() =>
            {
                try
                {
                    new ColorOverrideService(_doc, _uiDoc.ActiveView).ResetOverrides(_lastResults);
                    Dispatcher.Invoke(() => TxtStatusBar.Text = "Reset graphics override in the active view.");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "[WallMepClash] Reset color overrides failed");
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

        private void GridResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = GridResults.SelectedItem != null;
            BtnShow3D.IsEnabled = hasSelection;
        }

        private void LoadHostLevels()
        {
            List<LevelItem> levels = new List<LevelItem>();
            levels.Add(LevelItem.AllLevels());
            levels.AddRange(_wallCollector.GetHostLevels().Select(level => new LevelItem(level)));

            CmbHostLevels.ItemsSource = levels;
            levels[0].IsChecked = true;
            CmbHostLevels.SelectedIndex = 0;
            UpdateLevelsText();
        }

        private void LevelCheckBox_Click(object sender, RoutedEventArgs e)
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
            }
            else if (!clicked.IsAllLevels && clicked.IsChecked)
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

            UpdateLevelsText();
        }

        private List<LevelItem> GetLevelItems()
        {
            return (CmbHostLevels.ItemsSource as IEnumerable<LevelItem>)?.ToList() ?? new List<LevelItem>();
        }

        private void UpdateLevelsText()
        {
            List<LevelItem> items = GetLevelItems();
            if (items.Any(item => item.IsAllLevels && item.IsChecked))
            {
                CmbHostLevels.Text = "All host levels";
                return;
            }

            int checkedCount = items.Count(item => item.IsChecked);
            CmbHostLevels.Text = checkedCount == 0 ? "No levels selected" : checkedCount + " levels selected";
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
    }

    internal class LinkItem
    {
        public LinkItem(RevitLinkInstance instance)
        {
            Instance = instance;
            Name = instance.Name;
        }

        public string Name { get; }
        public RevitLinkInstance Instance { get; }
    }

    internal class CategoryItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public CategoryItem(BuiltInCategory category)
        {
            Category = category;
            Name = GetCategoryDisplayName(category);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get; }
        public BuiltInCategory Category { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        private static string GetCategoryDisplayName(BuiltInCategory category)
        {
            switch (category)
            {
                case BuiltInCategory.OST_PipeCurves: return "Pipes";
                case BuiltInCategory.OST_PipeFitting: return "Pipe Fittings";
                case BuiltInCategory.OST_PipeAccessory: return "Pipe Accessories";
                case BuiltInCategory.OST_DuctCurves: return "Ducts";
                case BuiltInCategory.OST_DuctFitting: return "Duct Fittings";
                case BuiltInCategory.OST_DuctAccessory: return "Duct Accessories";
                case BuiltInCategory.OST_Conduit: return "Conduits";
                case BuiltInCategory.OST_ConduitFitting: return "Conduit Fittings";
                case BuiltInCategory.OST_CableTray: return "Cable Trays";
                case BuiltInCategory.OST_CableTrayFitting: return "Cable Tray Fittings";
                case BuiltInCategory.OST_MechanicalEquipment: return "Mechanical Equipment";
                case BuiltInCategory.OST_ElectricalEquipment: return "Electrical Equipment";
                case BuiltInCategory.OST_ElectricalFixtures: return "Electrical Fixtures";
                case BuiltInCategory.OST_LightingFixtures: return "Lighting Fixtures";
                case BuiltInCategory.OST_LightingDevices: return "Lighting Devices";
                case BuiltInCategory.OST_FireAlarmDevices: return "Fire Alarm Devices";
                case BuiltInCategory.OST_DataDevices: return "Data Devices";
                case BuiltInCategory.OST_CommunicationDevices: return "Communication Devices";
                default: return category.ToString().Replace("OST_", "");
            }
        }
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

        public string Name { get; }
        public ElementId LevelId { get; }
        public bool IsAllLevels { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
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

    internal class ClashResultViewModel
    {
        private readonly ClashResult _result;

        public ClashResultViewModel(ClashResult result)
        {
            _result = result;
        }

        public int HostWallId => _result.HostWallId;
        public int LinkMepId => _result.LinkMepId;
        public string WallTypeName => _result.WallTypeName;
        public string MepName => _result.MepName;
        public double AngleDeg => _result.AngleDeg;
        public string LevelName => _result.LevelName;
        public bool IsClash => _result.IsClash;
    }
}
