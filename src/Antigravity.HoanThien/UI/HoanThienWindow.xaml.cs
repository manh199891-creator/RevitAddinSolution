using System;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Antigravity.HoanThien.Services;
using System.Collections.Generic;

namespace Antigravity.HoanThien.UI
{
    public partial class HoanThienWindow : Window
    {
        private UIApplication _app;
        private ExternalEvent _externalEvent;
        private Handlers.HoanThienRequestHandler _handler;
        public HoanThienViewModel ViewModel { get; set; }

        public HoanThienWindow(UIApplication app, ExternalEvent externalEvent, Handlers.HoanThienRequestHandler handler)
        {
            InitializeComponent();
            _app = app;
            _externalEvent = externalEvent;
            _handler = handler;
            ViewModel = new HoanThienViewModel();
            
            // Check if there are pre-selected rooms
            var selectedIds = app.ActiveUIDocument.Selection.GetElementIds();
            var doc = app.ActiveUIDocument.Document;
            bool hasSelectedRooms = selectedIds.Any(id => doc.GetElement(id) is Room);
            ViewModel.UsePreSelection = hasSelectedRooms;

            // Get unique room names in current view
            var roomsInView = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<Room>()
                .Where(r => r.Area > 0)
                .ToList();

            var roomNames = roomsInView
                .Select(r => r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString())
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            ViewModel.AvailableRoomNames.Add(new Models.RoomSelectionItem("[Tất cả phòng]"));
            foreach(var name in roomNames)
            {
                var item = new Models.RoomSelectionItem(name);
                item.PropertyChanged += (s, e) => { if (e.PropertyName == "IsSelected") ViewModel.UpdateSummary(); };
                ViewModel.AvailableRoomNames.Add(item);
            }

            // Load Wall Types
            var wallTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .OrderBy(wt => wt.Name)
                .ToList();

            foreach (var wt in wallTypes)
            {
                ViewModel.AvailableWallTypes.Add(wt);
            }
            if (ViewModel.AvailableWallTypes.Count > 0)
                ViewModel.SelectedWallType = ViewModel.AvailableWallTypes[0];

            // Load Floor Types
            var floorTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .OrderBy(ft => ft.Name)
                .ToList();

            foreach (var ft in floorTypes)
            {
                ViewModel.AvailableFloorTypes.Add(ft);
            }
            if (ViewModel.AvailableFloorTypes.Count > 0)
                ViewModel.SelectedFloorType = ViewModel.AvailableFloorTypes[0];

            this.DataContext = ViewModel;
        }

        private void BtnCheckRooms_Click(object sender, RoutedEventArgs e)
        {
            _handler.MakeRequest(Handlers.RequestId.CheckRooms, ViewModel, this);
            _externalEvent.Raise();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // Cập nhật lại trạng thái pre-selection ngay trước khi chạy (hỗ trợ modeless)
            var selectedIds = _app.ActiveUIDocument.Selection.GetElementIds();
            var doc = _app.ActiveUIDocument.Document;
            ViewModel.UsePreSelection = selectedIds.Any(id => doc.GetElement(id) is Room);

            _handler.MakeRequest(Handlers.RequestId.RunHoanThien, ViewModel, this);
            _externalEvent.Raise();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
