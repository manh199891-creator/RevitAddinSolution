using System.Collections.Generic;

namespace Antigravity.IssueManager.Models
{
    public class ClippingPlaneModel
    {
        public double LocationX { get; set; }
        public double LocationY { get; set; }
        public double LocationZ { get; set; }

        public double DirectionX { get; set; }
        public double DirectionY { get; set; }
        public double DirectionZ { get; set; }
    }

    public class ViewpointModel
    {
        // "Internal" matches NWC exported with Project Internal. "Shared" matches IFC/NWC exported with Shared Coordinates.
        public string CoordinateMode { get; set; }

        // 3D Camera coordinates for BCF (simplified)
        public double CameraX { get; set; }
        public double CameraY { get; set; }
        public double CameraZ { get; set; }
        
        public double CameraDirectionX { get; set; }
        public double CameraDirectionY { get; set; }
        public double CameraDirectionZ { get; set; }

        public double CameraUpX { get; set; }
        public double CameraUpY { get; set; }
        public double CameraUpZ { get; set; }

        // BCF orthogonal camera support
        public bool IsOrthogonal { get; set; }
        public double ViewToWorldScale { get; set; }

        // Specific to Navisworks XML
        public bool HasClashPoint { get; set; }
        public double ClashPointX { get; set; }
        public double ClashPointY { get; set; }
        public double ClashPointZ { get; set; }

        // Element IDs related to this issue (usually integer strings in Revit)
        public List<string> ElementIds { get; set; } = new List<string>();

        // Source model/file names for the two Navisworks clash objects.
        public List<string> ClashModelFiles { get; set; } = new List<string>();

        // Mapping from ElementId to IFC GUID (22 characters)
        public Dictionary<string, string> ComponentIfcGuids { get; set; } = new Dictionary<string, string>();

        // BCF clipping planes. Locations are meters in CoordinateMode; directions are unit vectors.
        public List<ClippingPlaneModel> ClippingPlanes { get; set; } = new List<ClippingPlaneModel>();

        // Path to snapshot image if any
        public string SnapshotFilePath { get; set; }

        // Embedded snapshot data for RVT Extensible Storage persistence.
        public string SnapshotBase64 { get; set; }
        
        // Path to second snapshot image (Clash Image)
        public string SnapshotFilePath2 { get; set; }

        // Embedded second snapshot data for RVT Extensible Storage persistence.
        public string SnapshotBase642 { get; set; }
    }
}
