using System.Collections.Generic;

namespace Antigravity.DrawBeams.Models
{
    /// <summary>
    /// Inspectable deterministic evidence attached to a beam candidate.
    /// All score components are bounded to [0, 1]. Penalty is also [0, 1].
    /// </summary>
    public class BeamCandidateEvidence
    {
        public double GeometryScore { get; set; }
        public double SemanticScore { get; set; }
        public double TopologyScore { get; set; }
        public double Penalty { get; set; }
        public double FinalScore { get; set; }

        public double ParallelScore { get; set; }
        public double OverlapScore { get; set; }
        public double WidthAgreementScore { get; set; }
        public double TextDistanceScore { get; set; }
        public double TextAngleScore { get; set; }

        public bool HasDirectText { get; set; }
        public bool HasExplicitPolylineWidth { get; set; }
        public bool HasClosedGroup { get; set; }
        public bool IsFallback { get; set; }
        public bool IsFragmentedSource { get; set; }

        public IReadOnlyList<string> SourceSegmentIds { get; set; } = new List<string>();
        public string SourceTextId { get; set; }
        public string SourceKind { get; set; }
    }
}
