using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Antigravity.ZoneSplit.Models
{
    public sealed class ZoneElementContribution
    {
        public ElementId ElementId { get; set; }
        public string Category { get; set; }
        public string ZoneId { get; set; }
        public double VolumeM3 { get; set; }
        public double LengthM { get; set; }
    }

    public sealed class ZoneProcessResult
    {
        public int TotalProcessed { get; set; }
        public int AssignedCount { get; set; }
        public int MultiZoneCount { get; set; }
        public int PhysicallySplitCount { get; set; }
        public int SplitSegmentCount { get; set; }
        public int SplitSkippedCount { get; set; }
        public int SkippedNoSolid { get; set; }
        public List<string> Warnings { get; } = new List<string>();
        public List<ZoneElementContribution> Contributions { get; } = new List<ZoneElementContribution>();
        public string ReportPath { get; set; }
    }
}
