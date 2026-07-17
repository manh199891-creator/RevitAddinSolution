using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Antigravity.DrawFloors.Models;

namespace Antigravity.DrawFloors.UI
{
    public partial class HatchMappingWindow : Window
    {
        private readonly Document _doc;
        public ObservableCollection<FloorType> AvailableFloorTypes { get; set; }
        public ObservableCollection<HatchMapping> Mappings { get; set; }


        public HatchMappingWindow(Document doc, Level selectedLevel)
        {
            InitializeComponent();
            _doc = doc;

            AvailableFloorTypes = new ObservableCollection<FloorType>(
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(FloorType))
                    .Cast<FloorType>()
                    .OrderBy(ft => ft.Name)
                    .ToList());


            Mappings = new ObservableCollection<HatchMapping>();
            DgMapping.ItemsSource = Mappings;
            this.DataContext = this;
        }

        /// <summary>
        /// Thêm một dòng mapping cho patternName (gộp nếu đã tồn tại).
        /// </summary>
        public void AddMapping(string patternName, object cadElement = null)
        {
            var existing = Mappings.FirstOrDefault(m => m.PatternName == patternName);
            if (existing != null)
            {
                if (cadElement != null) existing.ConnectedCadHatches.Add(cadElement);
            }
            else
            {
                var newMap = new HatchMapping
                {
                    PatternName = patternName,
                    SelectedFloorType = AvailableFloorTypes.FirstOrDefault(),
                    OffsetFromLevel = 0.0
                };
                if (cadElement != null) newMap.ConnectedCadHatches.Add(cadElement);
                Mappings.Add(newMap);
            }
        }


        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra mọi dòng đã được gán Floor Type
            var unmapped = Mappings.Where(m => m.SelectedFloorType == null).ToList();
            if (unmapped.Count > 0)
            {
                var names = string.Join(", ", unmapped.Select(m => m.PatternName));
                var result = MessageBox.Show(
                    $"The following patterns have no Floor Type assigned:\n  {names}\n\nThese Hatches will be ignored. Continue?",
                    "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
            }

            this.DialogResult = true;
            this.Close();
        }
    }
}
