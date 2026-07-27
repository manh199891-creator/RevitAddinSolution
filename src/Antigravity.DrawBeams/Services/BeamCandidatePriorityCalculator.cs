using Antigravity.DrawBeams.Models;
using System;

namespace Antigravity.DrawBeams.Services
{
    public enum BeamEvidenceTier
    {
        Tier1_PairedWithTextAndSize = 1,
        Tier2_PairedWithMeasuredAndWidth = 2,
        Tier3_PolylineWidthWithText = 3,
        Tier4_PairedIncompleteOrNoText = 4,
        Tier5_SingleFallbackWithText = 5,
        Tier6_SingleFallbackNoText = 6
    }

    public enum MeasuredWidthAgreement
    {
        Strong,     // <= 0.10
        Acceptable, // <= 0.20
        Weak,       // > 0.20
        Unknown     // Width <= 0 or MeasuredWidth <= 0
    }

    public class BeamPriorityResult
    {
        public BeamEvidenceTier EvidenceTier { get; set; }
        public double NormalizedConfidence { get; set; }
        public double FinalPriorityScore { get; set; }
        public MeasuredWidthAgreement WidthAgreement { get; set; }
        public double MeasuredWidthAgreementRatio { get; set; }
        public string PriorityReason { get; set; }
    }

    public static class BeamCandidatePriorityCalculator
    {
        public static BeamPriorityResult CalculatePriority(CadBeamData candidate)
        {
            if (candidate == null)
            {
                return new BeamPriorityResult
                {
                    EvidenceTier = BeamEvidenceTier.Tier6_SingleFallbackNoText,
                    NormalizedConfidence = 0,
                    FinalPriorityScore = 0,
                    WidthAgreement = MeasuredWidthAgreement.Unknown,
                    PriorityReason = "Null candidate"
                };
            }

            var tier = CalculateEvidenceTier(candidate);
            var (agreement, ratio) = CalculateMeasuredWidthAgreement(candidate);
            double normConf = CalculateNormalizedConfidence(candidate);
            double score = CalculateFinalScore(candidate, tier, agreement, normConf);

            string reason = $"Tier: {tier}, NormConf: {normConf:F3}, WidthAgreement: {agreement} ({ratio:P1}), Score: {score:F1}";

            return new BeamPriorityResult
            {
                EvidenceTier = tier,
                NormalizedConfidence = normConf,
                FinalPriorityScore = score,
                WidthAgreement = agreement,
                MeasuredWidthAgreementRatio = ratio,
                PriorityReason = reason
            };
        }

        public static BeamEvidenceTier CalculateEvidenceTier(CadBeamData b)
        {
            if (b == null) return BeamEvidenceTier.Tier6_SingleFallbackNoText;

            bool validSize = b.Width > 0 && b.Height > 0;
            bool validWidth = b.Width > 0;
            bool validMeasuredWidth = b.MeasuredWidth > 0;

            switch (b.DetectionMethod)
            {
                case BeamDetectionMethod.PairedLines:
                    if (b.HasDimensionText && validSize)
                        return BeamEvidenceTier.Tier1_PairedWithTextAndSize;
                    if (validMeasuredWidth && validWidth)
                        return BeamEvidenceTier.Tier2_PairedWithMeasuredAndWidth;
                    return BeamEvidenceTier.Tier4_PairedIncompleteOrNoText;

                case BeamDetectionMethod.PolylineWidth:
                    if (b.HasDimensionText)
                        return BeamEvidenceTier.Tier3_PolylineWidthWithText;
                    return BeamEvidenceTier.Tier4_PairedIncompleteOrNoText;

                case BeamDetectionMethod.SingleLineFallback:
                    if (b.HasDimensionText)
                        return BeamEvidenceTier.Tier5_SingleFallbackWithText;
                    return BeamEvidenceTier.Tier6_SingleFallbackNoText;

                default:
                    // Default / DimensionSplit / Custom
                    if (b.HasDimensionText && validSize)
                        return BeamEvidenceTier.Tier1_PairedWithTextAndSize;
                    if (b.HasDimensionText)
                        return BeamEvidenceTier.Tier3_PolylineWidthWithText;
                    if (validWidth)
                        return BeamEvidenceTier.Tier4_PairedIncompleteOrNoText;
                    return BeamEvidenceTier.Tier6_SingleFallbackNoText;
            }
        }

        public static (MeasuredWidthAgreement Agreement, double Ratio) CalculateMeasuredWidthAgreement(CadBeamData b)
        {
            if (b == null || b.Width <= 0 || b.MeasuredWidth <= 0)
            {
                return (MeasuredWidthAgreement.Unknown, 0.0);
            }

            double ratio = Math.Abs(b.MeasuredWidth - b.Width) / b.Width;

            if (ratio <= 0.10)
                return (MeasuredWidthAgreement.Strong, ratio);
            if (ratio <= 0.20)
                return (MeasuredWidthAgreement.Acceptable, ratio);
            return (MeasuredWidthAgreement.Weak, ratio);
        }

        public static double CalculateNormalizedConfidence(CadBeamData b)
        {
            if (b == null || b.Confidence <= 0) return 0.0;

            // Raw confidence is normalized into range [0.0, 1.0]
            // We use a smooth bounding function: conf / (conf + 500)
            double norm = b.Confidence / (b.Confidence + 500.0);
            return Math.Max(0.0, Math.Min(1.0, norm));
        }

        private static double CalculateFinalScore(
            CadBeamData b,
            BeamEvidenceTier tier,
            MeasuredWidthAgreement agreement,
            double normConf)
        {
            // Base tier scores: Tier 1 highest (60,000), Tier 6 lowest (10,000)
            double baseScore = (7 - (int)tier) * 10000.0;

            // Sub-scores (total max < 10,000 so sub-score never bridges evidence tiers)
            // 1. Normalized confidence: max 3500
            double confScore = normConf * 3500.0;

            // 2. Measured width agreement: max 2500
            double agreementScore;
            switch (agreement)
            {
                case MeasuredWidthAgreement.Strong:
                    agreementScore = 2500.0;
                    break;
                case MeasuredWidthAgreement.Acceptable:
                    agreementScore = 1800.0;
                    break;
                case MeasuredWidthAgreement.Weak:
                    agreementScore = 300.0; // Penalty for weak agreement
                    break;
                default:
                    agreementScore = 1200.0; // Unknown
                    break;
            }

            // 3. Metadata completeness: max 2000
            double metaScore = 0.0;
            if (b.Height > 0) metaScore += 800.0;
            if (b.HasDimensionText) metaScore += 700.0;
            if (!string.IsNullOrEmpty(b.Mark)) metaScore += 500.0;

            // 4. Length bonus (bounded): max 1000
            double dx = b.EndX - b.StartX;
            double dy = b.EndY - b.StartY;
            double len = Math.Sqrt(dx * dx + dy * dy);
            double lengthScore = Math.Min(1.0, len / 10000.0) * 1000.0;

            return baseScore + confScore + agreementScore + metaScore + lengthScore;
        }
    }
}
