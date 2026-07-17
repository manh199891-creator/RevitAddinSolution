using Autodesk.Revit.DB;

namespace Antigravity.AutoDimWalls.Models
{
    public enum AutoDimScope
    {
        Selection,
        ActiveView
    }

    public enum OpeningReferenceMode
    {
        Center,
        Jambs
    }

    public sealed class AutoDimOptions
    {
        public bool IsSectionView { get; set; } = false;
        public AutoDimScope Scope { get; set; } = AutoDimScope.Selection;
        public OpeningReferenceMode OpeningMode { get; set; } = OpeningReferenceMode.Center;
        public bool IncludeOpenings { get; set; } = true;
        public bool IncludeIntersectingWalls { get; set; }
        public bool IncludeGrids { get; set; }
        public bool IncludeLinks { get; set; }
        public ElementId SelectedLinkInstanceId { get; set; }
        public bool IncludeHostStructural { get; set; } = true;
        public double OffsetMm { get; set; } = 1000.0;
        public XYZ PickedPoint { get; set; }
        public DimensionType DimensionType { get; set; }
    }

    public class ReferenceInfo
    {
        public Reference Reference { get; set; }
        public double Parameter { get; set; }
    }
}
