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

        public bool IsValid => (Width > 0 && Height > 0) || (IsPaired && MeasuredWidth > 0 && Height > 0);
    }
}
