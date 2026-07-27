using System.Collections.Generic;

namespace Antigravity.DrawBeams.Models
{
    public enum BeamPairDecisionReason
    {
        AcceptedMutualNearest,
        AcceptedCorridorText,
        RejectedInterveningParallelLine,
        RejectedTextOutsideCorridor,
        RejectedNonMutualPair,
        RejectedWidthMismatch,
        RejectedLowOverlap,
        SkippedSourceLineAlreadyUsed
    }

    public class BeamPairOptions
    {
        public double MinimumPairOverlapRatio { get; set; } = 0.70;
        public double MaximumEndpointMismatchMm { get; set; } = 300.0;
        public double PairTextLateralMarginMm { get; set; } = 100.0;
        public double PairTextProjectionMarginMm { get; set; } = 150.0;
    }

    public class BeamPairCorridor
    {
        public string LineAId { get; set; }
        public string LineBId { get; set; }

        public double MeasuredWidth { get; set; }
        public double OverlapLength { get; set; }
        public double OverlapRatio { get; set; }
        public double EndpointMismatch { get; set; }
        public double LongitudinalOffset { get; set; }

        public int InterveningParallelLineCount { get; set; }
        public double DistanceToNearestPartnerA { get; set; }
        public double DistanceToNearestPartnerB { get; set; }
        public bool IsNearestPartnerForA { get; set; }
        public bool IsNearestPartnerForB { get; set; }
        public bool IsMutualNearest => IsNearestPartnerForA && IsNearestPartnerForB;

        public List<CadDimensionText> AcceptedCorridorTexts { get; set; } = new List<CadDimensionText>();
        public List<CadDimensionText> RejectedOutsideTexts { get; set; } = new List<CadDimensionText>();
        public bool HasStrongCorridorText { get; set; }

        public double Score { get; set; }
        public BeamPairDecisionReason Decision { get; set; }
        public string DecisionReasonText { get; set; }
    }
}
