namespace Antigravity.DrawBeams.Models
{
    public class BeamTextMatch
    {
        public CadText Text { get; set; }

        public double ParsedWidth { get; set; }
        public double ParsedHeight { get; set; }

        public string Content { get; set; }
        public string Mark { get; set; }

        public double DistanceToSegment { get; set; }
        public double AngleDifference { get; set; }
    }
}
