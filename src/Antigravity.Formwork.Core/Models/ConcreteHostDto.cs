using System.Collections.Generic;

namespace Antigravity.Formwork.Core.Models
{
    public class ConcreteHostDto
    {
        public string HostUniqueId { get; set; }
        public string HostCategory { get; set; }
        
        // For straight walls, we simplify the face to a line segment and height.
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        
        public double BaseElevation { get; set; }
        public double Height { get; set; }

        public string CycleId { get; set; }
        public string FaceKey { get; set; }
    }
}
