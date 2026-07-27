namespace Antigravity.DrawBeams.Models
{
    public enum BeamOverlapMode
    {
        LegacySafe,
        ExperimentalEnvelope
    }

    public class BeamOverlapOptions
    {
        public BeamOverlapMode OverlapMode { get; set; } = BeamOverlapMode.LegacySafe;
        public double AngularToleranceDegrees { get; set; } = 1.0;
        public double CenterlineDistanceToleranceMm { get; set; } = 25.0;
        public double EndpointToleranceMm { get; set; } = 50.0;
        public double MinimumOverlapRatio { get; set; } = 0.80;
        public double ContainmentToleranceMm { get; set; } = 50.0;
    }
}
