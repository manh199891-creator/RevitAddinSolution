using System.Collections.Generic;

namespace Antigravity.Formwork.Core.Models
{
    public class PlacementResult
    {
        public string HostUniqueId { get; set; }
        public string HostCategory { get; set; }
        public string FaceKey { get; set; }
        public string CycleId { get; set; }
        public string SystemId { get; set; }
        
        public List<PanelDto> PlacedPanels { get; set; } = new List<PanelDto>();
    }

    public class PanelDto
    {
        public string CatalogItemId { get; set; }
        
        // Panel Center XYZ Coordinates
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double CenterZ { get; set; }
        
        // Rotation around Z axis (radians)
        public double RotationAngle { get; set; }
        
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsFiller { get; set; }
    }
}
