using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.DrawBeams.Services
{
    public class BeamOverlapResolver
    {
        private readonly BeamOverlapOptions _options;

        public BeamOverlapResolver(BeamOverlapOptions options = null)
        {
            _options = options ?? new BeamOverlapOptions();
        }

        public List<CadBeamData> ResolveOverlaps(IEnumerable<CadBeamData> beams, BeamOverlapOptions options = null)
        {
            var opt = options ?? _options;
            if (beams == null) return new List<CadBeamData>();

            var inputList = beams.Where(b => b != null && b.IsValid).ToList();
            if (inputList.Count <= 1) return inputList;

            // Step 1: Canonicalize Start -> End direction & preserve lineage
            var canonicalBeams = inputList.Select(b => CanonicalizeBeam(b)).ToList();

            // Check Phase 3 measured-width sanity & calculate priorities
            var candidatePriorities = new Dictionary<CadBeamData, BeamPriorityResult>();
            foreach (var b in canonicalBeams)
            {
                var prio = BeamCandidatePriorityCalculator.CalculatePriority(b);
                candidatePriorities[b] = prio;

                if (prio.WidthAgreement == MeasuredWidthAgreement.Weak)
                {
                    BeamDiagnosticCollector.Instance.RecordWarning(
                        $"MeasuredWidthMismatch: Candidate {b.DiagnosticId} has Width={b.Width} but MeasuredWidth={b.MeasuredWidth} (ratio={prio.MeasuredWidthAgreementRatio:P1})");
                }
            }

            // Step 2: Sort candidates by PriorityScore descending, then length descending, then coordinates
            var sortedCandidates = canonicalBeams
                .OrderByDescending(b => candidatePriorities[b].FinalPriorityScore)
                .ThenByDescending(b => GetBeamLength(b))
                .ThenBy(b => b.StartX)
                .ThenBy(b => b.StartY)
                .ThenBy(b => b.EndX)
                .ThenBy(b => b.EndY)
                .ToList();

            var suppressed = new HashSet<CadBeamData>();

            for (int i = 0; i < sortedCandidates.Count; i++)
            {
                var beamA = sortedCandidates[i];
                if (suppressed.Contains(beamA)) continue;

                string idA = GetDiagnosticId(beamA);

                for (int j = i + 1; j < sortedCandidates.Count; j++)
                {
                    var beamB = sortedCandidates[j];
                    if (suppressed.Contains(beamB)) continue;

                    string idB = GetDiagnosticId(beamB);

                    var decision = EvaluateOverlapPairInternal(beamA, beamB, opt, candidatePriorities, out string suppressionReason, out BeamOverlapPairMetrics metrics);

                    if (decision == OverlapDecision.SuppressB)
                    {
                        suppressed.Add(beamB);
                        BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
                        {
                            CandidateId = idB,
                            DiagnosticId = idB,
                            ObjectType = "SuppressedCandidate",
                            ParentDiagnosticIds = beamB.ParentDiagnosticIds != null ? new List<string>(beamB.ParentDiagnosticIds) : new List<string>(),
                            RootRawCandidateIds = GetRootRawCandidateIds(beamB),
                            RelatedCandidateId = idA,
                            WinnerDiagnosticId = idA,
                            LoserDiagnosticId = idB,
                            Stage = BeamDiagnosticStage.OverlapDecision,
                            Action = BeamDiagnosticAction.Suppressed,
                            Reason = suppressionReason,
                            DetectionMethod = beamB.DetectionMethod,
                            Confidence = beamB.Confidence,
                            PriorityScore = metrics.PriorityScoreB,
                            CompetingPriorityScore = metrics.PriorityScoreA,
                            AngularDifferenceDegrees = metrics.AngularDifferenceDegrees,
                            CenterlineDistanceMm = metrics.CenterlineDistanceMm,
                            OverlapLengthMm = metrics.OverlapLengthMm,
                            OverlapRatio = metrics.OverlapRatio,
                            IsContained = metrics.IsContained,
                            StartX = beamB.StartX, StartY = beamB.StartY, EndX = beamB.EndX, EndY = beamB.EndY,
                            Width = beamB.Width, Height = beamB.Height, Mark = beamB.Mark
                        });
                    }
                    else if (decision == OverlapDecision.SuppressA)
                    {
                        suppressed.Add(beamA);
                        BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
                        {
                            CandidateId = idA,
                            DiagnosticId = idA,
                            ObjectType = "SuppressedCandidate",
                            ParentDiagnosticIds = beamA.ParentDiagnosticIds != null ? new List<string>(beamA.ParentDiagnosticIds) : new List<string>(),
                            RootRawCandidateIds = GetRootRawCandidateIds(beamA),
                            RelatedCandidateId = idB,
                            WinnerDiagnosticId = idB,
                            LoserDiagnosticId = idA,
                            Stage = BeamDiagnosticStage.OverlapDecision,
                            Action = BeamDiagnosticAction.Suppressed,
                            Reason = suppressionReason,
                            DetectionMethod = beamA.DetectionMethod,
                            Confidence = beamA.Confidence,
                            PriorityScore = metrics.PriorityScoreA,
                            CompetingPriorityScore = metrics.PriorityScoreB,
                            AngularDifferenceDegrees = metrics.AngularDifferenceDegrees,
                            CenterlineDistanceMm = metrics.CenterlineDistanceMm,
                            OverlapLengthMm = metrics.OverlapLengthMm,
                            OverlapRatio = metrics.OverlapRatio,
                            IsContained = metrics.IsContained,
                            StartX = beamA.StartX, StartY = beamA.StartY, EndX = beamA.EndX, EndY = beamA.EndY,
                            Width = beamA.Width, Height = beamA.Height, Mark = beamA.Mark
                        });
                        break;
                    }
                }
            }

            var activeList = sortedCandidates
                .Where(b => !suppressed.Contains(b))
                .OrderBy(b => b.StartX)
                .ThenBy(b => b.StartY)
                .ThenBy(b => b.EndX)
                .ThenBy(b => b.EndY)
                .ToList();

            foreach (var b in activeList)
            {
                string id = GetDiagnosticId(b);
                var prio = candidatePriorities[b];

                BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
                {
                    CandidateId = id,
                    DiagnosticId = id,
                    ObjectType = "ActiveCandidate",
                    ParentDiagnosticIds = b.ParentDiagnosticIds != null ? new List<string>(b.ParentDiagnosticIds) : new List<string>(),
                    RootRawCandidateIds = GetRootRawCandidateIds(b),
                    Stage = BeamDiagnosticStage.OverlapDecision,
                    Action = BeamDiagnosticAction.Kept,
                    Reason = "Kept by BeamOverlapResolver",
                    DetectionMethod = b.DetectionMethod,
                    Confidence = b.Confidence,
                    PriorityScore = prio.FinalPriorityScore,
                    StartX = b.StartX, StartY = b.StartY, EndX = b.EndX, EndY = b.EndY,
                    Width = b.Width, Height = b.Height, Mark = b.Mark
                });
            }

            var summary = BeamDiagnosticCollector.Instance.CurrentSession?.PipelineSummary;
            if (summary != null)
            {
                summary.BeforeOverlapCount = sortedCandidates.Count;
                summary.SuppressedOverlapsCount = suppressed.Count;
                summary.AfterOverlapCount = activeList.Count;
            }

            return activeList;
        }

        public static double GetPriorityScore(CadBeamData b)
        {
            if (b == null) return 0.0;
            var result = BeamCandidatePriorityCalculator.CalculatePriority(b);
            return result.FinalPriorityScore;
        }

        private enum OverlapDecision
        {
            KeepBoth,
            SuppressA,
            SuppressB
        }

        private struct BeamOverlapPairMetrics
        {
            public double AngularDifferenceDegrees;
            public double CenterlineDistanceMm;
            public double OverlapLengthMm;
            public double OverlapRatio;
            public bool IsContained;
            public double PriorityScoreA;
            public double PriorityScoreB;
        }

        private OverlapDecision EvaluateOverlapPairInternal(
            CadBeamData a,
            CadBeamData b,
            BeamOverlapOptions opt,
            Dictionary<CadBeamData, BeamPriorityResult> priorities,
            out string suppressionReason,
            out BeamOverlapPairMetrics metrics)
        {
            suppressionReason = null;

            double angleA = GetBeamAngle(a);
            double angleB = GetBeamAngle(b);
            double angleDiff = Math.Abs(angleA - angleB);
            if (angleDiff > Math.PI / 2.0) angleDiff = Math.PI - angleDiff;
            double angleDiffDeg = angleDiff * 180.0 / Math.PI;

            double lenA = GetBeamLength(a);
            double lenB = GetBeamLength(b);

            double perpDist = BeamPhysicalEnvelope.ComputeCenterlineDistance(a, b);
            double overlapRatio = BeamPhysicalEnvelope.ComputeOverlapRatio(a, b);
            double overlapLen = BeamPhysicalEnvelope.ComputeOverlapLength(a, b);
            bool isContained = BeamPhysicalEnvelope.ComputeIsContained(a, b, opt.ContainmentToleranceMm);

            var prioA = priorities.ContainsKey(a) ? priorities[a] : BeamCandidatePriorityCalculator.CalculatePriority(a);
            var prioB = priorities.ContainsKey(b) ? priorities[b] : BeamCandidatePriorityCalculator.CalculatePriority(b);

            metrics = new BeamOverlapPairMetrics
            {
                AngularDifferenceDegrees = angleDiffDeg,
                CenterlineDistanceMm = perpDist,
                OverlapLengthMm = overlapLen,
                OverlapRatio = overlapRatio,
                IsContained = isContained,
                PriorityScoreA = prioA.FinalPriorityScore,
                PriorityScoreB = prioB.FinalPriorityScore
            };

            // 1. Angle check
            if (angleDiffDeg > opt.AngularToleranceDegrees)
            {
                return OverlapDecision.KeepBoth;
            }

            if (lenA < 1e-3 || lenB < 1e-3)
            {
                return OverlapDecision.KeepBoth;
            }

            // RULE C: Keep both if independent evidence of two physical beams exists
            if (a.HasDimensionText && b.HasDimensionText)
            {
                bool sourceLinesDistinct = a.SourceLineIds != null && b.SourceLineIds != null
                    && !a.SourceLineIds.Overlaps(b.SourceLineIds);

                bool textsDistinct = !string.Equals(a.TextContent, b.TextContent, StringComparison.OrdinalIgnoreCase);

                if (sourceLinesDistinct || textsDistinct)
                {
                    double minCenterlineSeparation = (a.Width + b.Width) / 2.0 - 5.0;
                    if (perpDist >= minCenterlineSeparation || perpDist > opt.CenterlineDistanceToleranceMm)
                    {
                        return OverlapDecision.KeepBoth;
                    }
                }
            }

            // RULE A: Suppress SingleLineFallback lying inside paired beam envelope
            if (overlapRatio >= 0.80 || isContained)
            {
                if (a.DetectionMethod == BeamDetectionMethod.PairedLines && b.DetectionMethod == BeamDetectionMethod.SingleLineFallback)
                {
                    if (a.Width > 0 && perpDist <= (a.Width / 2.0) + 30.0 && (a.HasDimensionText || prioA.WidthAgreement != MeasuredWidthAgreement.Weak || a.MeasuredWidth > 0))
                    {
                        suppressionReason = "SingleLineFallbackInsidePairedBeamEnvelope";
                        return OverlapDecision.SuppressB;
                    }
                }
                if (b.DetectionMethod == BeamDetectionMethod.PairedLines && a.DetectionMethod == BeamDetectionMethod.SingleLineFallback)
                {
                    if (b.Width > 0 && perpDist <= (b.Width / 2.0) + 30.0 && (b.HasDimensionText || prioB.WidthAgreement != MeasuredWidthAgreement.Weak || b.MeasuredWidth > 0))
                    {
                        suppressionReason = "SingleLineFallbackInsidePairedBeamEnvelope";
                        return OverlapDecision.SuppressA;
                    }
                }
            }

            // RULE B: Suppress incomplete candidate inside dimensioned beam envelope
            if (overlapRatio >= 0.80 || isContained)
            {
                bool envelopeOverlap = BeamPhysicalEnvelope.IsEnvelopeOverlap(a, b, opt);
                if (envelopeOverlap)
                {
                    bool aDimensionedComplete = a.HasDimensionText && a.Width > 0 && a.Height > 0;
                    bool bIncomplete = !b.HasDimensionText || b.Height <= 0 || b.Width <= 0;

                    if (aDimensionedComplete && bIncomplete && prioA.EvidenceTier < prioB.EvidenceTier)
                    {
                        suppressionReason = "IncompleteCandidateInsideDimensionedBeamEnvelope";
                        return OverlapDecision.SuppressB;
                    }

                    bool bDimensionedComplete = b.HasDimensionText && b.Width > 0 && b.Height > 0;
                    bool aIncomplete = !a.HasDimensionText || a.Height <= 0 || a.Width <= 0;

                    if (bDimensionedComplete && aIncomplete && prioB.EvidenceTier < prioA.EvidenceTier)
                    {
                        suppressionReason = "IncompleteCandidateInsideDimensionedBeamEnvelope";
                        return OverlapDecision.SuppressA;
                    }
                }
            }

            // RULE D: Long incomplete candidate covering dimensioned regions
            if (overlapRatio >= 0.80 || isContained)
            {
                if (lenA >= lenB * 1.25 && !a.HasDimensionText && a.Height <= 0 && b.HasDimensionText && b.Width > 0 && b.Height > 0)
                {
                    suppressionReason = "LongIncompleteCandidateCoveredByDimensionedRegions";
                    return OverlapDecision.SuppressA;
                }
                if (lenB >= lenA * 1.25 && !b.HasDimensionText && b.Height <= 0 && a.HasDimensionText && a.Width > 0 && a.Height > 0)
                {
                    suppressionReason = "LongIncompleteCandidateCoveredByDimensionedRegions";
                    return OverlapDecision.SuppressB;
                }
            }

            // Check standard centerline tolerance if envelope rules did not match
            if (perpDist > opt.CenterlineDistanceToleranceMm)
            {
                return OverlapDecision.KeepBoth;
            }

            // Check Shared SourceLineIds
            bool sharesSourceLines = a.SourceLineIds != null && b.SourceLineIds != null
                && a.SourceLineIds.Overlaps(b.SourceLineIds);

            if (sharesSourceLines && overlapLen > 10.0)
            {
                suppressionReason = $"SharedSourceLines (A={idA(a)}, B={idA(b)})";
                return prioA.FinalPriorityScore >= prioB.FinalPriorityScore ? OverlapDecision.SuppressB : OverlapDecision.SuppressA;
            }

            if (overlapRatio < 0.05 && !isContained)
            {
                return OverlapDecision.KeepBoth;
            }

            // Dimension match check
            bool sameDims = Math.Abs(a.Width - b.Width) <= 5.0 && Math.Abs(a.Height - b.Height) <= 5.0;

            if (sameDims)
            {
                if (overlapRatio >= opt.MinimumOverlapRatio || isContained)
                {
                    suppressionReason = $"SameDimensionsOverlap (A={idA(a)}, B={idA(b)})";
                    return prioA.FinalPriorityScore >= prioB.FinalPriorityScore ? OverlapDecision.SuppressB : OverlapDecision.SuppressA;
                }
                return OverlapDecision.KeepBoth;
            }
            else // Different dimensions
            {
                if (overlapRatio >= opt.MinimumOverlapRatio || isContained)
                {
                    if (lenA > lenB * 1.5 && prioA.FinalPriorityScore < prioB.FinalPriorityScore)
                    {
                        suppressionReason = "LongerLowerPriorityCandidateSuppressed";
                        return OverlapDecision.SuppressA;
                    }
                    if (lenB > lenA * 1.5 && prioB.FinalPriorityScore < prioA.FinalPriorityScore)
                    {
                        suppressionReason = "LongerLowerPriorityCandidateSuppressed";
                        return OverlapDecision.SuppressB;
                    }

                    suppressionReason = $"DifferentDimensionsOverlap (A={idA(a)}, B={idA(b)})";
                    return prioA.FinalPriorityScore >= prioB.FinalPriorityScore ? OverlapDecision.SuppressB : OverlapDecision.SuppressA;
                }

                return OverlapDecision.KeepBoth;
            }

            string idA(CadBeamData beam) => GetDiagnosticId(beam);
        }

        private CadBeamData CanonicalizeBeam(CadBeamData b)
        {
            double dx = b.EndX - b.StartX;
            double dy = b.EndY - b.StartY;

            bool isReversed = dx < -1e-4 || (Math.Abs(dx) <= 1e-4 && dy < -1e-4);

            if (isReversed)
            {
                return new CadBeamData
                {
                    StartX = b.EndX,
                    StartY = b.EndY,
                    EndX = b.StartX,
                    EndY = b.StartY,
                    Width = b.Width,
                    Height = b.Height,
                    TextContent = b.TextContent,
                    Mark = b.Mark,
                    MeasuredWidth = b.MeasuredWidth,
                    IsPaired = b.IsPaired,
                    Confidence = b.Confidence,
                    SourceLayer = b.SourceLayer,
                    HasDimensionText = b.HasDimensionText,
                    DetectionMethod = b.DetectionMethod,
                    SourceLineIds = b.SourceLineIds != null ? new HashSet<string>(b.SourceLineIds, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(),
                    DiagnosticId = b.DiagnosticId,
                    ParentDiagnosticIds = b.ParentDiagnosticIds != null ? new List<string>(b.ParentDiagnosticIds) : new List<string>(),
                    RootRawCandidateIds = GetRootRawCandidateIds(b)
                };
            }

            return b;
        }

        private string GetDiagnosticId(CadBeamData b)
        {
            if (!string.IsNullOrEmpty(b.DiagnosticId)) return b.DiagnosticId;
            return $"CAND_{Math.Round(b.StartX)}_{Math.Round(b.StartY)}_{Math.Round(b.EndX)}_{Math.Round(b.EndY)}";
        }

        private List<string> GetRootRawCandidateIds(CadBeamData b)
        {
            if (b.RootRawCandidateIds != null && b.RootRawCandidateIds.Count > 0)
            {
                return new List<string>(b.RootRawCandidateIds);
            }
            if (b.ParentDiagnosticIds != null && b.ParentDiagnosticIds.Count > 0)
            {
                return new List<string>(b.ParentDiagnosticIds);
            }
            string id = GetDiagnosticId(b);
            return new List<string> { id };
        }

        private double GetBeamLength(CadBeamData b)
        {
            double dx = b.EndX - b.StartX;
            double dy = b.EndY - b.StartY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private double GetBeamAngle(CadBeamData b)
        {
            double len = GetBeamLength(b);
            if (len < 1e-9) return 0;
            double a = Math.Atan2(b.EndY - b.StartY, b.EndX - b.StartX);
            while (a < 0) a += Math.PI;
            while (a >= Math.PI) a -= Math.PI;
            return a;
        }
    }
}
