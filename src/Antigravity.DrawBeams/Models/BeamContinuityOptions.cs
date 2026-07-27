namespace Antigravity.DrawBeams.Models
{
    public class BeamContinuityOptions
    {
        public double AngularToleranceDegrees { get; set; } = 2.5;
        public double EndpointGapToleranceMm { get; set; } = 100.0;
        public double LateralOffsetToleranceMm { get; set; } = 30.0;
        public double WidthToleranceRatio { get; set; } = 0.20;
        public double MinimumOverlapMm { get; set; } = 200.0;
        public double JunctionToleranceMm { get; set; } = 50.0;
    }
}
