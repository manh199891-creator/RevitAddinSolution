using System;

namespace Antigravity.DrawColumns.Models
{
    public class RevitColumnData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Rotation { get; set; } // in radians
        
        public bool IsRound { get; set; }
        public double Width { get; set; }  // B (X dimension relative to element)
        public double Height { get; set; } // H (Y dimension relative to element)
        public double Radius { get; set; }
        public string ColumnName { get; set; }
        public string EntityType { get; set; }
    }
}
