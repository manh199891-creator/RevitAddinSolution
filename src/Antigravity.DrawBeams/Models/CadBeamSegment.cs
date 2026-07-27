using System;
using System.Collections.Generic;

namespace Antigravity.DrawBeams.Models
{
    public class CadBeamSegment
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }

        public double Width { get; set; }
        public double Height { get; set; }
        public double MeasuredWidth { get; set; }

        public string Mark { get; set; }
        public string TextContent { get; set; }
        public bool IsPaired { get; set; }

        public List<string> SourceLineIds { get; set; } = new List<string>();
        public double Confidence { get; set; }
        public string Layer { get; set; }

        public double DirectionX => EndX - StartX;
        public double DirectionY => EndY - StartY;

        public double Length => Math.Sqrt(DirectionX * DirectionX + DirectionY * DirectionY);

        public double MidX => (StartX + EndX) / 2.0;
        public double MidY => (StartY + EndY) / 2.0;

        /// <summary>
        /// Normalized angle in range [0, PI)
        /// </summary>
        public double Angle
        {
            get
            {
                if (Length < 1e-9) return 0;
                double a = Math.Atan2(DirectionY, DirectionX);
                while (a < 0) a += Math.PI;
                while (a >= Math.PI) a -= Math.PI;
                return a;
            }
        }
    }
}
