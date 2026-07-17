using System.Collections.ObjectModel;
using System.ComponentModel;
using Antigravity.HoanThien.Models;
using Autodesk.Revit.DB;

namespace Antigravity.HoanThien.UI
{
    public class HoanThienViewModel : INotifyPropertyChanged
    {
        public FinishLayerConfig Config { get; set; }

        private ObservableCollection<RoomSelectionItem> _availableRoomNames;
        public ObservableCollection<RoomSelectionItem> AvailableRoomNames
        {
            get => _availableRoomNames;
            set
            {
                _availableRoomNames = value;
                OnPropertyChanged(nameof(AvailableRoomNames));
            }
        }

        public string SelectedRoomsSummary
        {
            get
            {
                if (AvailableRoomNames == null) return "[Tất cả phòng]";
                
                var selected = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(AvailableRoomNames, x => x.IsSelected));
                if (selected.Count == 0 || selected.Count == AvailableRoomNames.Count) return "[Tất cả phòng]";
                
                if (selected.Count <= 2)
                {
                    return string.Join(", ", System.Linq.Enumerable.Select(selected, x => x.Name));
                }
                
                return $"{selected.Count} phòng được chọn";
            }
        }

        private ObservableCollection<WallType> _availableWallTypes;
        public ObservableCollection<WallType> AvailableWallTypes
        {
            get => _availableWallTypes;
            set
            {
                _availableWallTypes = value;
                OnPropertyChanged(nameof(AvailableWallTypes));
            }
        }

        private ObservableCollection<FloorType> _availableFloorTypes;
        public ObservableCollection<FloorType> AvailableFloorTypes
        {
            get => _availableFloorTypes;
            set
            {
                _availableFloorTypes = value;
                OnPropertyChanged(nameof(AvailableFloorTypes));
            }
        }

        private WallType _selectedWallType;
        public WallType SelectedWallType
        {
            get => _selectedWallType;
            set
            {
                _selectedWallType = value;
                if (value != null) Config.SelectedWallTypeId = value.Id;
                OnPropertyChanged(nameof(SelectedWallType));
            }
        }

        private FloorType _selectedFloorType;
        public FloorType SelectedFloorType
        {
            get => _selectedFloorType;
            set
            {
                _selectedFloorType = value;
                if (value != null) Config.SelectedFloorTypeId = value.Id;
                OnPropertyChanged(nameof(SelectedFloorType));
            }
        }



        private bool _usePreSelection;
        public bool UsePreSelection
        {
            get => _usePreSelection;
            set
            {
                _usePreSelection = value;
                OnPropertyChanged(nameof(UsePreSelection));
            }
        }

        public HoanThienViewModel()
        {
            Config = new FinishLayerConfig
            {
                LayerName = "Finish",
                Function = MaterialFunctionAssignment.Finish1,
                CreateWall = true,
                CreateFloor = true,
                HeightOffsetAboveCeilingMm = 0
            };
            AvailableRoomNames = new ObservableCollection<RoomSelectionItem>();
            AvailableWallTypes = new ObservableCollection<WallType>();
            AvailableFloorTypes = new ObservableCollection<FloorType>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void UpdateSummary()
        {
            OnPropertyChanged(nameof(SelectedRoomsSummary));
        }
    }
}
