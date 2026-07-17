using System.Collections.Generic;
using System.Linq;

namespace Antigravity.ArchModeling.Models
{
    public class HatchLineDefinition
    {
        public double AngleDegrees { get; set; }
        public double BaseXMm      { get; set; }
        public double BaseYMm      { get; set; }
        public double OffsetXMm    { get; set; }
        public double OffsetYMm    { get; set; }
        public List<double> DashLengthsMm { get; set; }
    }

    public class HatchPatternInfo
    {
        public List<HatchLineDefinition> Lines { get; set; } = new List<HatchLineDefinition>();

        /// <summary>Human-readable description shown in tooltip.</summary>
        public string Description
        {
            get
            {
                if (Lines == null || Lines.Count == 0)
                    return "(Không có thông tin định nghĩa nét)";

                return string.Join("\n", Lines.Select((l, i) =>
                    $"Nét {i + 1}:  Góc {l.AngleDegrees:F1}°  —  Khoảng {l.OffsetXMm:F1} × {l.OffsetYMm:F1} mm"));
            }
        }
    }

    /// <summary>Return value of HatchBoundaryService.GetHatchBoundaries().</summary>
    public class HatchScanResult
    {
        public Dictionary<string, List<System.Collections.Generic.IList<Autodesk.Revit.DB.CurveLoop>>> Boundaries { get; set; }
            = new Dictionary<string, List<System.Collections.Generic.IList<Autodesk.Revit.DB.CurveLoop>>>();

        public Dictionary<string, HatchPatternInfo> PatternInfos { get; set; }
            = new Dictionary<string, HatchPatternInfo>();
    }
}
