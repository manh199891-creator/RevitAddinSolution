using Autodesk.Revit.DB;

namespace Antigravity.HoanThien.Models
{
    public class FinishLayerConfig
    {
        public string LayerName { get; set; }
        public ElementId SelectedWallTypeId { get; set; }
        public ElementId SelectedFloorTypeId { get; set; }
        public MaterialFunctionAssignment Function { get; set; }
        public bool IsFloor { get; set; }
        public bool CreateFloor { get; set; } = true;
        public bool CreateWall { get; set; } = true;
        public bool ExcludeCurtainWalls { get; set; } = true;
        public double HeightOffsetAboveCeilingMm { get; set; } = 0;
        
        // Automation properties
        public bool EnableRoomCoding { get; set; } = true;
        public string RoomCodePrefix { get; set; } = "GRV_";
        public bool UseSharedParameters { get; set; } = true;
    }
}
