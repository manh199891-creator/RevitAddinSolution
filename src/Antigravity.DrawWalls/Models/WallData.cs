using System.Collections.Generic;

namespace Antigravity.DrawWalls.Models
{
    public class WallData
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double Thickness { get; set; }
        public string Handle { get; set; } // AutoCAD handle to track entity
        
        // For complex shapes or polylines
        public List<double[]> PolylinePoints { get; set; }
        public bool IsClosed { get; set; }

        public WallData()
        {
            PolylinePoints = new List<double[]>();
        }
    }
}
