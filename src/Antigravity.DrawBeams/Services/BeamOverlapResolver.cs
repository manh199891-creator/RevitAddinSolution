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

            int exactDuplicatesCount = 0;
            int sameCenterlineCount = 0;
            int boundaryFallbackCount = 0;
            int envelopeSuppressionsCount = 0;
            int pairedVsPairedSuppressionsCount = 0;
            int ambiguousKeptCount = 0;

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

                    var decision = EvaluateOverlapPairInternal(
                        beamA, beamB, opt, candidatePriorities,
                        out string suppressionReason, out BeamOverlapPairMetrics metrics,
                        ref exactDuplicatesCount, ref sameCenterlineCount, ref boundaryFallbackCount,
                        ref envelopeSuppressionsCount, ref pairedVsPairedSuppressionsCount, ref ambiguousKeptCount);

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
                summary.OverlapMode = opt.OverlapMode.ToString();
                summary.BeforeOverlapCount = sortedCandidates.Count;
                summary.SuppressedOverlapsCount = suppressed.Count;
                summary.AfterOverlapCount = activeList.Count;

                summary.ExactDuplicatesSuppressed = exactDuplicatesCount;
                summary.SameCenterlineSuppressed = sameCenterlineCount;
                summary.BoundaryFallbackSuppressed = boundaryFallbackCount;
                summary.EnvelopeSuppressions = envelopeSuppressionsCount;
                summary.PairedVsPairedSuppressions = pairedVsPairedSuppressionsCount;
                summary.AmbiguousKept = ambiguousKeptCount;
            }

            if (opt.OverlapMode == BeamOverlapMode.LegacySafe && pairedVsPairedSuppressionsCount > 0)
            {
                BeamDiagnosticCollector.Instance.RecordWarning(
                    $"LegacySafeWarning: Unexpected PairedVsPairedSuppressions count = {pairedVsPairedSuppressionsCount} in LegacySafe mode.");
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
            out BeamOverlapPairMetrics metrics,
            ref int exactDuplicatesCount,
            ref int sameCenterlineCount,
            ref int boundaryFallbackCount,
            ref int envelopeSuppressionsCount,
            ref int pairedVsPairedSuppressionsCount,
            ref int ambiguousKeptCount)
        {
            if (opt.OverlapMode == BeamOverlapMode.LegacySafe)
            {
                return EvaluateLegacySafePair(
                    a, b, opt, priorities, out suppressionReason, out metrics,
                    ref exactDuplicatesCount, ref sameCenterlineCount, ref boundaryFallbackCount,
                    ref pairedVsPairedSuppressionsCount, ref ambiguousKeptCount);
            }
            else
            {
                return EvaluateExperimentalEnvelopePair(
                    a, b, opt, priorities, out suppressionReason, out metrics,
                    ref exactDuplicatesCount, ref sameCenterlineCount, ref boundaryFallbackCount,
                    ref envelopeSuppressionsCount, ref pairedVsPairedSuppressionsCount);
            }
        }

        private OverlapDecision EvaluateLegacySafePair(
            CadBeamData a,
            CadBeamData b,
            BeamOverlapOptions opt,
            Dictionary<CadBeamData, BeamPriorityResult> priorities,
            out string suppressionReason,
            out BeamOverlapPairMetrics metrics,
            ref int exactDuplicatesCount,
            ref int sameCenterlineCount,
            ref int boundaryFallbackCount,
            ref int pairedVsPairedSuppressionsCount,
            ref int ambiguousKeptCount)
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

            if (angleDiffDeg > opt.AngularToleranceDegrees || lenA < 1e-3 || lenB < 1e-3 || (overlapRatio < 0.05 && !isContained))
            {
                return OverlapDecision.KeepBoth;
            }

            // 1. Exact geometry duplicate (same centerline <= 10mm, overlap >= 95% or contained, same dimensions)
            bool isExactDup = (overlapRatio >= 0.95 || isContained) && perpDist <= 10.0;
            if (isExactDup)
            {
                bool sameDims = Math.Abs(a.Width - b.Width) <= 5.0 && Math.Abs(a.Height - b.Height) <= 5.0;
                if (sameDims)
                {
                    exactDuplicatesCount++;
                    if (a.DetectionMethod == BeamDetectionMethod.PairedLines && b.DetectionMethod == BeamDetectionMethod.PairedLines)
                    {
                        // Exact duplicate paired lines count under exact duplicates
                    }
                    suppressionReason = $"ExactDuplicateGeometry (A={idA(a)}, B={idA(b)})";
                    return prioA.FinalPriorityScore >= prioB.FinalPriorityScore ? OverlapDecision.SuppressB : OverlapDecision.SuppressA;
                }
            }

            // 2. Shared SourceLineIds or Lineage with near-same centerline (<= 25mm)
            bool sharesSource = a.SourceLineIds != null && b.SourceLineIds != null && a.SourceLineIds.Overlaps(b.SourceLineIds);
            bool sharesLineage = a.RootRawCandidateIds != null && b.RootRawCandidateIds != null
                && a.RootRawCandidateIds.Intersect(b.RootRawCandidateIds, StringComparer.OrdinalIgnoreCase).Any();

            if ((sharesSource || sharesLineage) && perpDist <= opt.CenterlineDistanceToleranceMm && (overlapRatio >= opt.MinimumOverlapRatio || isContained))
            {
                sameCenterlineCount++;
                suppressionReason = sharesSource ? $"SharedSourceLines (A={idA(a)}, B={idA(b)})" : $"SharedLineage (A={idA(a)}, B={idA(b)})";
                return prioA.FinalPriorityScore >= prioB.FinalPriorityScore ? OverlapDecision.SuppressB : OverlapDecision.SuppressA;
            }

            // 3. Phase 4 Strict Boundary Fallback Rule
            bool isFallbackA = a.DetectionMethod == BeamDetectionMethod.SingleLineFallback;
            bool isFallbackB = b.DetectionMethod == BeamDetectionMethod.SingleLineFallback;

            if ((isFallbackA || isFallbackB) && (overlapRatio >= 0.90 || isContained))
            {
                var paired = isFallbackB ? a : b;
                var fallback = isFallbackB ? b : a;

                bool pairedValid = paired.DetectionMethod == BeamDetectionMethod.PairedLines && paired.Width > 0 && paired.Height > 0 && paired.HasDimensionText;
                bool fallbackNoText = !fallback.HasDimensionText;

                double halfW = BeamPhysicalEnvelope.GetHalfWidth(paired);
                bool nearHalfWidth = halfW > 0 && Math.Abs(perpDist - halfW) <= 20.0;

                if (pairedValid && fallbackNoText && nearHalfWidth)
                {
                    bool hasSourceLineageEvidence = (fallback.SourceLineIds != null && paired.SourceLineIds != null && fallback.SourceLineIds.Overlaps(paired.SourceLineIds))
                        || (fallback.RootRawCandidateIds != null && paired.RootRawCandidateIds != null && fallback.RootRawCandidateIds.Intersect(paired.RootRawCandidateIds, StringComparer.OrdinalIgnoreCase).Any());

                    if (hasSourceLineageEvidence)
                    {
                        boundaryFallbackCount++;
                        suppressionReason = "SingleLineFallbackInsidePairedBeamEnvelope";
                        return isFallbackB ? OverlapDecision.SuppressB : OverlapDecision.SuppressA;
                    }
                    else
                    {
                        ambiguousKeptCount++;
                        BeamDiagnosticCollector.Instance.RecordWarning(
                            $"AmbiguousBoundaryCandidate: Boundary fallback {GetDiagnosticId(fallback)} kept near paired candidate {GetDiagnosticId(paired)} due to missing source/lineage evidence.");
                        return OverlapDecision.KeepBoth;
                    }
                }
            }

            // 4. Near-Same-Centerline (<= 25mm) for True Incomplete candidates
            if (perpDist <= opt.CenterlineDistanceToleranceMm && (overlapRatio >= opt.MinimumOverlapRatio || isContained))
            {
                // If BOTH are PairedLines, LegacySafe REQUIRES KeepBoth unless exact/shared-source!
                if (a.DetectionMethod == BeamDetectionMethod.PairedLines && b.DetectionMethod == BeamDetectionMethod.PairedLines)
                {
                    return OverlapDecision.KeepBoth;
                }

                // Check True Incomplete
                bool aIncomplete = (a.Width <= 0 || a.Height <= 0) && !a.HasDimensionText;
                bool bComplete = b.Width > 0 && b.Height > 0 && b.HasDimensionText;
                if (aIncomplete && bComplete)
                {
                    sameCenterlineCount++;
                    suppressionReason = "IncompleteCandidateInsideDimensionedBeamEnvelope";
                    return OverlapDecision.SuppressA;
                }

                bool bIncomplete = (b.Width <= 0 || b.Height <= 0) && !b.HasDimensionText;
                bool aComplete = a.Width > 0 && a.Height > 0 && a.HasDimensionText;
                if (bIncomplete && aComplete)
                {
                    sameCenterlineCount++;
                    suppressionReason = "IncompleteCandidateInsideDimensionedBeamEnvelope";
                    return OverlapDecision.SuppressB;
                }
            }

            // DEFAULT LEGACY SAFE: KEEP BOTH
            return OverlapDecision.KeepBoth;

            string idA(CadBeamData beam) => GetDiagnosticId(beam);
        }

        private OverlapDecision EvaluateExperimentalEnvelopePair(
            CadBeamData a,
            CadBeamData b,
            BeamOverlapOptions opt,
            Dictionary<CadBeamData, BeamPriorityResult> priorities,
            out string suppressionReason,
            out BeamOverlapPairMetrics metrics,
            ref int exactDuplicatesCount,
            ref int sameCenterlineCount,
            ref int boundaryFallbackCount,
            ref int envelopeSuppressionsCount,
            ref int pairedVsPairedSuppressionsCount)
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

            if (angleDiffDeg > opt.AngularToleranceDegrees || lenA < 1e-3 || lenB < 1e-3 || (overlapRatio < 0.05 && !isContained))
            {
                return OverlapDecision.KeepBoth;
            }

            // Experimental physical envelope suppression
            bool isEnvelopeOverlap = BeamPhysicalEnvelope.IsEnvelopeOverlap(a, b, opt);
            if (isEnvelopeOverlap)
            {
                envelopeSuppressionsCount++;
                if (a.DetectionMethod == BeamDetectionMethod.PairedLines && b.DetectionMethod == BeamDetectionMethod.SingleLineFallback)
                {
                    boundaryFallbackCount++;
                    suppressionReason = "SingleLineFallbackInsidePairedBeamEnvelope";
                    return OverlapDecision.SuppressB;
                }
                if (b.DetectionMethod == BeamDetectionMethod.PairedLines && a.DetectionMethod == BeamDetectionMethod.SingleLineFallback)
                {
                    boundaryFallbackCount++;
                    suppressionReason = "SingleLineFallbackInsidePairedBeamEnvelope";
                    return OverlapDecision.SuppressA;
                }

                if (a.DetectionMethod == BeamDetectionMethod.PairedLines && b.DetectionMethod == BeamDetectionMethod.PairedLines)
                {
                    pairedVsPairedSuppressionsCount++;
                }

                suppressionReason = "ExperimentalEnvelopeOverlap";
                return prioA.FinalPriorityScore >= prioB.FinalPriorityScore ? OverlapDecision.SuppressB : OverlapDecision.SuppressA;
            }

            return OverlapDecision.KeepBoth;
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
