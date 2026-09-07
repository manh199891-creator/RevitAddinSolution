using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.DrawBeams.Services
{
    public class BeamCandidateScorer
    {
        private readonly BeamCandidateScoringOptions _options;

        public BeamCandidateScorer(BeamCandidateScoringOptions options = null)
        {
            _options = options ?? new BeamCandidateScoringOptions();
        }

        public BeamCandidateEvidence BuildEvidence(BeamCandidate candidate)
        {
            if (candidate == null) return new BeamCandidateEvidence();

            bool hasPartner = candidate.PartnerSegment != null;
            bool hasText = candidate.TextMatch?.Text != null;
            bool explicitWidth = candidate.Kind == BeamCandidateKind.PolylineWidth;
            bool closedGroup = candidate.Kind == BeamCandidateKind.ClosedPolylinePair;
            bool fallback = candidate.Kind == BeamCandidateKind.CommonWidthFallback;

            double parallel = hasPartner
                ? 1.0 - Normalize(candidate.AngleDifference, _options.MaximumPairAngleDifference)
                : 0.65;

            double overlap = hasPartner
                ? BeamCandidateScoringOptions.Clamp(candidate.OverlapRatio / 0.80, 0.0, 1.0)
                : 0.55;

            double widthAgreement = WidthAgreement(candidate);
            double geometry = Average(parallel, overlap, widthAgreement);

            double textDistance = hasText
                ? 1.0 - Normalize(candidate.TextMatch.DistanceToSegment, TextDistanceScale(candidate))
                : 0.0;

            double textAngle = hasText
                ? 1.0 - Normalize(candidate.TextMatch.AngleDifference, _options.MaximumTextAngleDifference)
                : 0.0;

            double semantic = hasText
                ? Average(1.0, textDistance, textAngle)
                : 0.0;

            double topology = TopologyScore(candidate.Kind);
            double penalty = 0.0;
            if (fallback) penalty += _options.FallbackPenalty;
            if (!hasText) penalty += _options.MissingTextPenalty;
            if (candidate.ParsedWidth > 0 && candidate.MeasuredWidth > 0)
            {
                double relativeError = Math.Abs(candidate.MeasuredWidth - candidate.ParsedWidth) / candidate.ParsedWidth;
                penalty += BeamCandidateScoringOptions.Clamp(relativeError - 0.10, 0.0, 0.25);
            }
            penalty = BeamCandidateScoringOptions.Clamp(penalty, 0.0, 1.0);

            double final =
                geometry * _options.GeometryWeight +
                semantic * _options.SemanticWeight +
                topology * _options.TopologyWeight -
                penalty;

            return new BeamCandidateEvidence
            {
                GeometryScore = Safe01(geometry),
                SemanticScore = Safe01(semantic),
                TopologyScore = Safe01(topology),
                Penalty = Safe01(penalty),
                FinalScore = Safe01(final),
                ParallelScore = Safe01(parallel),
                OverlapScore = Safe01(overlap),
                WidthAgreementScore = Safe01(widthAgreement),
                TextDistanceScore = Safe01(textDistance),
                TextAngleScore = Safe01(textAngle),
                HasDirectText = hasText,
                HasExplicitPolylineWidth = explicitWidth,
                HasClosedGroup = closedGroup,
                IsFallback = fallback,
                IsFragmentedSource = HasFragmentedSource(candidate),
                SourceSegmentIds = SourceSegmentIds(candidate),
                SourceTextId = candidate.TextMatch?.Text?.Id,
                SourceKind = SourceKind(candidate)
            };
        }

        private double WidthAgreement(BeamCandidate candidate)
        {
            double measured = candidate.MeasuredWidth;
            double expected = candidate.ParsedWidth;
            if (candidate.Kind == BeamCandidateKind.SingleLineWithText) return expected > 0 ? 0.65 : 0.0;
            if (measured <= 0 || expected <= 0) return measured > 0 ? 0.60 : 0.0;
            double relativeError = Math.Abs(measured - expected) / expected;
            return 1.0 - BeamCandidateScoringOptions.Clamp(relativeError / Math.Max(_options.MaximumWidthRelativeError, 1e-9), 0.0, 1.0);
        }

        private static double TopologyScore(BeamCandidateKind kind)
        {
            switch (kind)
            {
                case BeamCandidateKind.ClosedPolylinePair: return 1.0;
                case BeamCandidateKind.PolylineWidth: return 0.95;
                case BeamCandidateKind.PairedEdges: return 0.85;
                case BeamCandidateKind.SingleLineWithText: return 0.55;
                case BeamCandidateKind.CommonWidthFallback: return 0.40;
                default: return 0.0;
            }
        }

        private static string SourceKind(BeamCandidate candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate.MainSegment?.Provenance?.SourceKind))
                return candidate.MainSegment.Provenance.SourceKind;
            return candidate.Kind.ToString();
        }

        private static bool HasFragmentedSource(BeamCandidate candidate)
        {
            return SourceSegmentIds(candidate).Count > (candidate.PartnerSegment == null ? 1 : 2);
        }

        private static IReadOnlyList<string> SourceSegmentIds(BeamCandidate candidate)
        {
            var ids = new List<string>();
            AddSourceIds(ids, candidate.MainSegment?.Id);
            AddSourceIds(ids, candidate.PartnerSegment?.Id);
            return ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        }

        private static void AddSourceIds(List<string> ids, string identity)
        {
            if (string.IsNullOrWhiteSpace(identity)) return;
            foreach (string part in identity.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0) ids.Add(trimmed);
            }
        }

        private static double TextDistanceScale(BeamCandidate candidate)
        {
            double width = candidate.ParsedWidth > 0 ? candidate.ParsedWidth : candidate.MeasuredWidth;
            return Math.Max(5000.0, width * 3.0);
        }

        private static double Normalize(double value, double scale)
        {
            if (scale <= 1e-9) return 1.0;
            return BeamCandidateScoringOptions.Clamp(Math.Abs(value) / scale, 0.0, 1.0);
        }

        private static double Average(params double[] values)
        {
            if (values == null || values.Length == 0) return 0.0;
            return values.Sum(Safe01) / values.Length;
        }

        private static double Safe01(double value)
        {
            return BeamCandidateScoringOptions.Clamp(value, 0.0, 1.0);
        }
    }
}
