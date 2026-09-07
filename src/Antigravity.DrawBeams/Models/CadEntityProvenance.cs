namespace Antigravity.DrawBeams.Models
{
    /// <summary>Metadata captured at the fail-soft AutoCAD COM boundary.</summary>
    public class CadEntityProvenance
    {
        public string EntityHandle { get; set; }
        public string EntityId { get; set; }
        public string ParentEntityHandle { get; set; }
        public string ParentEntityId { get; set; }
        public string Layer { get; set; }
        public int? Color { get; set; }
        public string Linetype { get; set; }
        public int? Lineweight { get; set; }
        public string SourceKind { get; set; }
        public string ObjectName { get; set; }
        public string PolylineParentIdentity { get; set; }
        public int? SegmentIndex { get; set; }
    }
}
