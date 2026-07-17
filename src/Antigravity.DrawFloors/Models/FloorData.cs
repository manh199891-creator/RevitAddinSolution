using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Antigravity.DrawFloors.Models
{
    public class FloorData
    {
        public XYZ Origin { get; set; }
        public Level Level { get; set; }
        public FloorType FloorType { get; set; }
        public double HeightOffset { get; set; }

        // Mảng các đường bao (Vòng ngoài và các lỗ mở)
        // Mỗi List<Curve> là một CurveLoop
        public List<List<Curve>> Boundaries { get; set; }

        public FloorData()
        {
            Boundaries = new List<List<Curve>>();
        }
    }
}
