using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Autodesk.Revit.DB;

namespace LOQN1_Location_element
{
    public class ComboItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
        public override string ToString() => Name;
    }

    public class LevelItem
    {
        public int Number { get; set; }
        public string OriginalName { get; set; }
        public string ElevationString { get; set; }
        public string DisplayName { get; set; }
    }

    public partial class LevelMappingWindow : Window
    {
        private Document _doc;
        public Dictionary<string, string> LevelMapping { get; private set; }
        public bool IsConfirmed { get; private set; }

        public bool UseAllInstances => RbAllInstances.IsChecked == true;
        public ElementId SelectedCategoryId => (CmbCategory.SelectedItem as ComboItem)?.Id;
        public ElementId SelectedFamilyTypeId => (CmbFamilyType.SelectedItem as ComboItem)?.Id;

        public ObservableCollection<LevelItem> LevelItems { get; set; }

        public LevelMappingWindow(Document doc, bool hasPreSelection, List<Tuple<string, double>> levelNamesWithElevations, Dictionary<string, string> previousMapping)
        {
            InitializeComponent();
            _doc = doc;
            LevelMapping = new Dictionary<string, string>();
            IsConfirmed = false;
            LevelItems = new ObservableCollection<LevelItem>();

            if (previousMapping == null || previousMapping.Count == 0)
            {
                previousMapping = LevelMappingStorage.Load();
            }

            if (hasPreSelection)
                RbSelected.IsChecked = true;
            else
                RbAllInstances.IsChecked = true;

            for (int i = 0; i < levelNamesWithElevations.Count; i++)
            {
                string originalName = levelNamesWithElevations[i].Item1;
                double elevation = levelNamesWithElevations[i].Item2;

                string displayName = "";
                if (previousMapping != null && previousMapping.ContainsKey(originalName))
                {
                    displayName = previousMapping[originalName];
                }

                LevelItems.Add(new LevelItem
                {
                    Number = i + 1,
                    OriginalName = originalName,
                    ElevationString = elevation.ToString("0.000").Replace(".", ","),
                    DisplayName = displayName
                });
            }

            DgvMapping.ItemsSource = LevelItems;
            LoadCategories();
        }

        private void LoadCategories()
        {
            try
            {
                var items = new List<ComboItem>();
                items.Add(new ComboItem { Name = "<All Categories>", Id = ElementId.InvalidElementId });
                
                var categories = new HashSet<ElementId>();
                var elems = new FilteredElementCollector(_doc).WhereElementIsNotElementType().ToElements();
                
                List<Category> cats = new List<Category>();
                foreach (var e in elems)
                {
                    if (e.Category != null && categories.Add(e.Category.Id))
                    {
                        cats.Add(e.Category);
                    }
                }
                
                foreach (var c in cats.OrderBy(c => c.Name))
                {
                    items.Add(new ComboItem { Name = c.Name, Id = c.Id });
                }
                
                CmbCategory.ItemsSource = items;
                if (items.Count > 0) CmbCategory.SelectedIndex = 0;
            }
            catch { }
        }

        private void CmbCategory_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                var items = new List<ComboItem>();
                items.Add(new ComboItem { Name = "<All Family Types>", Id = ElementId.InvalidElementId });
                
                var selectedCatId = SelectedCategoryId;
                if (selectedCatId != null && selectedCatId != ElementId.InvalidElementId)
                {
                    var typesCollector = new FilteredElementCollector(_doc).OfCategoryId(selectedCatId).WhereElementIsElementType();
                    foreach (ElementType t in typesCollector.OrderBy(t => t.Name))
                    {
                        string familyName = t.FamilyName;
                        string typeName = t.Name;
                        string display = string.IsNullOrEmpty(familyName) ? typeName : $"{familyName} - {typeName}";
                        items.Add(new ComboItem { Name = display, Id = t.Id });
                    }
                }
                CmbFamilyType.ItemsSource = items;
                if (items.Count > 0) CmbFamilyType.SelectedIndex = 0;
            }
            catch { }
        }

        private Dictionary<string, string> CollectMapping()
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            foreach (var item in LevelItems)
            {
                string originalName = item.OriginalName;
                string displayName = item.DisplayName?.Trim() ?? "";

                if (!string.IsNullOrEmpty(originalName))
                {
                    map[originalName] = string.IsNullOrEmpty(displayName) ? originalName : displayName;
                }
            }
            return map;
        }

        private void BtnAutoFill_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in LevelItems)
            {
                if (string.IsNullOrEmpty(item.DisplayName?.Trim()))
                {
                    item.DisplayName = item.OriginalName;
                }
            }
            DgvMapping.Items.Refresh();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in LevelItems)
            {
                item.DisplayName = "";
            }
            DgvMapping.Items.Refresh();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, string> map = CollectMapping();
            LevelMappingStorage.Save(map);
            LblStatus.Text = "✓ Mapping saved successfully!";

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s2, e2) =>
            {
                LblStatus.Text = "";
                timer.Stop();
            };
            timer.Start();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            this.Close();
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            LevelMapping = CollectMapping();
            IsConfirmed = true;
            this.DialogResult = true;
            this.Close();
        }
    }
}
