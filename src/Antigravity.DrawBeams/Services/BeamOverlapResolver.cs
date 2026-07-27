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

            // Step 1: Canonicalize Start -> End direction
            var canonicalBeams = inputList.Select(b => CanonicalizeBeam(b)).ToList();

            // Step 2: Sort candidates by priority score descending, then length descending
            var sortedCandidates = canonicalBeams
                .OrderByDescending(b => GetPriorityScore(b))
                .ThenByDescending(b => GetBeamLength(b))
                .ThenBy(b => b.StartX)
                .ThenBy(b => b.StartY)
                .ToList();

            var suppressed = new HashSet<CadBeamData>();

            for (int i = 0; i < sortedCandidates.Count; i++)
            {
                var beamA = sortedCandidates[i];
                if (suppressed.Contains(beamA)) continue;

                string idA = !string.IsNullOrEmpty(beamA.DiagnosticId) ? beamA.DiagnosticId : $"CAND_{Math.Round(beamA.StartX)}_{Math.Round(beamA.StartY)}_{Math.Round(beamA.EndX)}_{Math.Round(beamA.EndY)}";

                for (int j = i + 1; j < sortedCandidates.Count; j++)
                {
                    var beamB = sortedCandidates[j];
                    if (suppressed.Contains(beamB)) continue;

                    string idB = !string.IsNullOrEmpty(beamB.DiagnosticId) ? beamB.DiagnosticId : $"CAND_{Math.Round(beamB.StartX)}_{Math.Round(beamB.StartY)}_{Math.Round(beamB.EndX)}_{Math.Round(beamB.EndY)}";

                    double scoreA = GetPriorityScore(beamA);
                    double scoreB = GetPriorityScore(beamB);

                    var decision = EvaluateOverlapPair(beamA, beamB, opt);
                    if (decision == OverlapDecision.SuppressB)
                    {
                        suppressed.Add(beamB);
                        BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
                        {
                            CandidateId = idB,
                            DiagnosticId = idB,
                            ObjectType = "SuppressedCandidate",
                            ParentDiagnosticIds = beamB.ParentDiagnosticIds ?? new List<string>(),
                            RootRawCandidateIds = beamB.RootRawCandidateIds ?? new List<string>(),
                            RelatedCandidateId = idA,
                            WinnerDiagnosticId = idA,
                            LoserDiagnosticId = idB,
                            Stage = BeamDiagnosticStage.OverlapDecision,
                            Action = BeamDiagnosticAction.Suppressed,
                            Reason = $"Suppressed by higher priority candidate {idA}",
                            DetectionMethod = beamB.DetectionMethod,
                            Confidence = beamB.Confidence,
                            PriorityScore = scoreB,
                            CompetingPriorityScore = scoreA,
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
                            ParentDiagnosticIds = beamA.ParentDiagnosticIds ?? new List<string>(),
                            RootRawCandidateIds = beamA.RootRawCandidateIds ?? new List<string>(),
                            RelatedCandidateId = idB,
                            WinnerDiagnosticId = idB,
                            LoserDiagnosticId = idA,
                            Stage = BeamDiagnosticStage.OverlapDecision,
                            Action = BeamDiagnosticAction.Suppressed,
                            Reason = $"Suppressed by higher priority candidate {idB}",
                            DetectionMethod = beamA.DetectionMethod,
                            Confidence = beamA.Confidence,
                            PriorityScore = scoreA,
                            CompetingPriorityScore = scoreB,
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
                string id = !string.IsNullOrEmpty(b.DiagnosticId) ? b.DiagnosticId : $"CAND_{Math.Round(b.StartX)}_{Math.Round(b.StartY)}_{Math.Round(b.EndX)}_{Math.Round(b.EndY)}";
                BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
                {
                    CandidateId = id,
                    DiagnosticId = id,
                    ObjectType = "ActiveCandidate",
                    ParentDiagnosticIds = b.ParentDiagnosticIds ?? new List<string>(),
                    RootRawCandidateIds = b.RootRawCandidateIds ?? new List<string>(),
                    Stage = BeamDiagnosticStage.OverlapDecision,
                    Action = BeamDiagnosticAction.Kept,
                    Reason = "Kept by BeamOverlapResolver",
                    DetectionMethod = b.DetectionMethod,
                    Confidence = b.Confidence,
                    PriorityScore = GetPriorityScore(b),
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
            double baseScore;
            switch (b.DetectionMethod)
            {
                case BeamDetectionMethod.PairedLines:
                    baseScore = b.HasDimensionText ? 10000.0 : 6000.0;
                    break;
                case BeamDetectionMethod.PolylineWidth:
                    baseScore = b.HasDimensionText ? 8000.0 : 4000.0;
                    break;
                case BeamDetectionMethod.SingleLineFallback:
                    baseScore = 2000.0;
                    break;
                case BeamDetectionMethod.DimensionSplit:
                    baseScore = 1000.0;
                    break;
                default:
                    baseScore = 500.0;
                    break;
            }
            return baseScore + b.Confidence;
        }

        private enum OverlapDecision
        {
            KeepBoth,
            SuppressA,
            SuppressB
        }

        private OverlapDecision EvaluateOverlapPair(CadBeamData a, CadBeamData b, BeamOverlapOptions opt)
        {
            // 1. Angle Check
            double angleA = GetBeamAngle(a);
            double angleB = GetBeamAngle(b);
            double angleDiff = Math.Abs(angleA - angleB);
            if (angleDiff > Math.PI / 2.0) angleDiff = Math.PI - angleDiff;

            if (angleDiff * 180.0 / Math.PI > opt.AngularToleranceDegrees)
            {
                return OverlapDecision.KeepBoth; // Angle difference > tolerance -> separate
            }

            // 2. Perpendicular Centerline Distance Check
            double lenA = GetBeamLength(a);
            double lenB = GetBeamLength(b);
            if (lenA < 1e-3 || lenB < 1e-3) return OverlapDecision.KeepBoth;

            double midAx = (a.StartX + a.EndX) / 2.0;
            double midAy = (a.StartY + a.EndY) / 2.0;
            double perpDist = DistancePointToLine(midAx, midAy, b.StartX, b.StartY, b.EndX, b.EndY);

            if (perpDist > opt.CenterlineDistanceToleranceMm)
            {
                return OverlapDecision.KeepBoth; // Centerline offset > tolerance -> parallel separate physical beams
            }

            // 3. 1D Interval Overlap Projection
            double ux = (a.EndX - a.StartX) / lenA;
            double uy = (a.EndY - a.StartY) / lenA;

            double tA_start = a.StartX * ux + a.StartY * uy;
            double tA_end = a.EndX * ux + a.EndY * uy;
            double minA = Math.Min(tA_start, tA_end);
            double maxA = Math.Max(tA_start, tA_end);

            double tB_start = b.StartX * ux + b.StartY * uy;
            double tB_end = b.EndX * ux + b.EndY * uy;
            double minB = Math.Min(tB_start, tB_end);
            double maxB = Math.Max(tB_start, tB_end);

            double overlapStart = Math.Max(minA, minB);
            double overlapEnd = Math.Min(maxA, maxB);
            double overlapLen = Math.Max(0.0, overlapEnd - overlapStart);

            double minLen = Math.Min(lenA, lenB);
            double overlapRatio = minLen > 0 ? overlapLen / minLen : 0;

            bool isContained = (minA >= minB - opt.ContainmentToleranceMm && maxA <= maxB + opt.ContainmentToleranceMm)
                            || (minB >= minA - opt.ContainmentToleranceMm && maxB <= maxA + opt.ContainmentToleranceMm);

            // Check Shared SourceLineIds
            bool sharesSourceLines = a.SourceLineIds != null && b.SourceLineIds != null
                && a.SourceLineIds.Overlaps(b.SourceLineIds);

            if (sharesSourceLines && overlapLen > 10.0)
            {
                double scoreA = GetPriorityScore(a);
                double scoreB = GetPriorityScore(b);
                return scoreA >= scoreB ? OverlapDecision.SuppressB : OverlapDecision.SuppressA;
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
                    double scoreA = GetPriorityScore(a);
                    double scoreB = GetPriorityScore(b);
                    return scoreA >= scoreB ? OverlapDecision.SuppressB : OverlapDecision.SuppressA;
                }
                return OverlapDecision.KeepBoth;
            }
            else // Different Dimensions
            {
                if (overlapRatio >= opt.MinimumOverlapRatio || isContained)
                {
                    double scoreA = GetPriorityScore(a);
                    double scoreB = GetPriorityScore(b);

                    // If one beam is a long beam covering smaller region beams, discard the lower confidence long beam
                    if (lenA > lenB * 1.5 && scoreA < scoreB)
                    {
                        return OverlapDecision.SuppressA;
                    }
                    if (lenB > lenA * 1.5 && scoreB < scoreA)
                    {
                        return OverlapDecision.SuppressB;
                    }

                    return scoreA >= scoreB ? OverlapDecision.SuppressB : OverlapDecision.SuppressA;
                }

                return OverlapDecision.KeepBoth;
            }
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
                    SourceLineIds = b.SourceLineIds != null ? new HashSet<string>(b.SourceLineIds, StringComparer.OrdinalIgnoreCase) : new HashSet<string>()
                };
            }

            return b;
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

        private double DistancePointToLine(double px, double py, double lx1, double ly1, double lx2, double ly2)
        {
            double dx = lx2 - lx1;
            double dy = ly2 - ly1;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-9) return Math.Sqrt((px - lx1) * (px - lx1) + (py - ly1) * (py - ly1));

            double cross = Math.Abs((lx2 - lx1) * (ly1 - py) - (lx1 - px) * (ly2 - ly1));
            return cross / Math.Sqrt(lenSq);
        }
    }
}
