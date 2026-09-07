using System;

namespace Antigravity.DrawBeams.Services
{
    public class BeamCandidateScoringOptions
    {
        public double MinimumAnchorLength { get; set; } = 500.0;
        public double MinimumPartnerLength { get; set; } = 200.0;
        public double MinimumBeamWidth { get; set; } = 50.0;
        public double MaximumPairedWidth { get; set; } = 1200.0;
        public double MaximumFallbackWidth { get; set; } = 3000.0;
        public double MinimumPolylineWidth { get; set; } = 100.0;
        public double MaximumPolylineWidth { get; set; } = 3000.0;

        public double MaximumPairAngleDifference { get; set; } = Math.Acos(0.999);
        public double MaximumTextAngleDifference { get; set; } = Math.PI / 9.0;
        public double MaximumWidthRelativeError { get; set; } = 0.30;
        public double CommonWidthRelativeError { get; set; } = 0.15;
        public double MinimumLengthToWidthRatio { get; set; } = 1.2;

        public double MinimumAdaptiveOverlap { get; set; } = 100.0;
        public double MaximumAdaptiveOverlap { get; set; } = 300.0;
        public double AdaptiveOverlapRatio { get; set; } = 0.05;

        public double GeometryWeight { get; set; } = 0.50;
        public double SemanticWeight { get; set; } = 0.30;
        public double TopologyWeight { get; set; } = 0.20;
        public double FallbackPenalty { get; set; } = 0.20;
        public double MissingTextPenalty { get; set; } = 0.10;

        public double RequiredOverlap(double firstLength, double secondLength)
        {
            double shorter = Math.Max(0.0, Math.Min(firstLength, secondLength));
            return Clamp(shorter * AdaptiveOverlapRatio, MinimumAdaptiveOverlap, MaximumAdaptiveOverlap);
        }

        public bool IsWidthAcceptable(double measuredWidth, double expectedWidth)
        {
            if (measuredWidth <= 0 || expectedWidth <= 0) return false;
            return Math.Abs(measuredWidth - expectedWidth) / expectedWidth <= MaximumWidthRelativeError;
        }

        public static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
