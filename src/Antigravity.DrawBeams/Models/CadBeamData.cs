using System;
using System.Collections.Generic;

namespace Antigravity.DrawBeams.Models
{
    public class CadBeamData
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        
        public string TextContent { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Mark { get; set; }
        
        // V3: Measured data from parallel lines
        public double MeasuredWidth { get; set; }
        public bool IsPaired { get; set; }

        // Cho phép Height = 0 để AssignMarksToBeams có thể tìm lại
        public bool IsValid => (Width > 0) || (IsPaired && MeasuredWidth > 0);
    }
}
