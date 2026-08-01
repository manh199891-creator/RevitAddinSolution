using System.Collections.Generic;

namespace Antigravity.DrawBeams.Models
{
    public class BeamCandidate
    {
        public string Id { get; set; }

        public BeamCandidateKind Kind { get; set; }

        public CadSegment MainSegment { get; set; }
        public CadSegment PartnerSegment { get; set; }

        public BeamTextMatch TextMatch { get; set; }

        public double MeasuredWidth { get; set; }
        public double ParsedWidth { get; set; }
        public double ParsedHeight { get; set; }

        public double OverlapLength { get; set; }
        public double OverlapRatio { get; set; }
        public double AngleDifference { get; set; }

        public string TextContent { get; set; }
        public string Mark { get; set; }

        public string SourceGroupId { get; set; }

        public bool HasPartner => PartnerSegment != null;
        public bool HasText => TextMatch != null;
    }
}
