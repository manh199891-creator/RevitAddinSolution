using Autodesk.Revit.DB;

namespace Antigravity.WallMepClash.Models
{
    public class ClashResult
    {
        public int HostWallId { get; set; }
        public int LinkMepId { get; set; }
        public string WallTypeName { get; set; }
        public string MepName { get; set; }
        public string LevelName { get; set; }
        public double AngleDeg { get; set; }
        public bool IsClash => true;
        public XYZ ClashMidpoint { get; set; }
    }
}
