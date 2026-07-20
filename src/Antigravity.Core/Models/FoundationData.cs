using Autodesk.Revit.DB;

namespace Antigravity.Core.Models
{
    public class FoundationData
    {
        public XYZ Center { get; set; }
        public double RotationAngle { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
    }
}
