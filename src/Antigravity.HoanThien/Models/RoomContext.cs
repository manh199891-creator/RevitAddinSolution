using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Antigravity.HoanThien.Models
{
    public class RoomContext
    {
        public Room Room { get; }
        public ElementId RoomId => Room.Id;
        public string RoomNumber { get; }
        public string RoomName { get; }
        public double Area { get; }
        public double Perimeter { get; }

        public PlanStatus Status { get; set; } = PlanStatus.Ready;
        public string StatusMessage { get; set; } = string.Empty;

        public List<BoundarySurface> Surfaces { get; } = new List<BoundarySurface>();

        public RoomContext(Room room)
        {
            Room = room;
            RoomNumber = room.Number;
            RoomName = room.Name;
            Area = room.Area;
            Perimeter = room.Perimeter;
        }
    }
}
