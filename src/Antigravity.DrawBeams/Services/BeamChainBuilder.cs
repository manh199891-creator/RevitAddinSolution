using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.DrawBeams.Services
{
    public class BeamChain
    {
        public List<CadBeamSegment> Segments { get; set; } = new List<CadBeamSegment>();

        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }

        public double Width => Segments
            .Where(s => s.Width > 0)
            .OrderByDescending(s => s.Confidence)
            .ThenByDescending(s => s.Length)
            .FirstOrDefault()?.Width ?? 0;

        public double Height => Segments
            .Where(s => s.Height > 0)
            .OrderByDescending(s => s.Confidence)
            .ThenByDescending(s => s.Length)
            .FirstOrDefault()?.Height ?? 0;

        public string Mark => Segments
            .Where(s => !string.IsNullOrEmpty(s.Mark))
            .OrderByDescending(s => s.Confidence)
            .ThenByDescending(s => s.Length)
            .FirstOrDefault()?.Mark;

        // Lineage & Traceability Properties
        public string DiagnosticId { get; set; }
        public List<string> ParentDiagnosticIds { get; set; } = new List<string>();
        public List<string> RootRawCandidateIds { get; set; } = new List<string>();
    }

    public class BeamChainBuilder
    {
        private readonly BeamContinuityOptions _options;

        public BeamChainBuilder(BeamContinuityOptions options = null)
        {
            _options = options ?? new BeamContinuityOptions();
        }

        public List<BeamChain> BuildChains(IEnumerable<CadBeamSegment> segments)
        {
            if (segments == null) return new List<BeamChain>();

            // 1. Filter out null or zero-length segments
            var validSegments = segments
                .Where(s => s != null && s.Length > 1e-3)
                .ToList();

            if (validSegments.Count == 0) return new List<BeamChain>();

            BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
            {
                Stage = BeamDiagnosticStage.ContinuityEvaluation,
                Action = BeamDiagnosticAction.Evaluated,
                Reason = $"Continuity evaluation started with {validSegments.Count} valid segments.",
                InputDiagnosticIds = validSegments.Select(s => s.DiagnosticId ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList()
            });

            // 2. Pre-process segments: Split long segments at non-collinear intersections (junctions)
            var preprocessedSegments = PreprocessSegmentJunctionSplits(validSegments);

            // 3. Deterministic initial ordering of input segments
            var sortedInput = preprocessedSegments
                .OrderByDescending(s => s.Length)
                .ThenBy(s => s.StartX)
                .ThenBy(s => s.StartY)
                .ThenBy(s => s.EndX)
                .ThenBy(s => s.EndY)
                .ThenBy(s => s.Layer ?? "")
                .ToList();

            var unassigned = new HashSet<CadBeamSegment>(sortedInput);
            var chains = new List<BeamChain>();
            int chainIdx = 0;

            while (unassigned.Count > 0)
            {
                var seed = sortedInput.First(s => unassigned.Contains(s));
                var currentChainSegments = new List<CadBeamSegment> { seed };
                unassigned.Remove(seed);

                bool addedAny;
                do
                {
                    addedAny = false;
                    var (ux, uy, theta) = ComputeChainAxis(currentChainSegments);
                    double nx = -uy;
                    double ny = ux;

                    CadBeamSegment bestCandidate = null;
                    double minDistance = double.MaxValue;

                    foreach (var candidate in sortedInput.Where(s => unassigned.Contains(s)))
                    {
                        if (IsSegmentCompatibleWithChain(candidate, currentChainSegments, sortedInput, ux, uy, theta, nx, ny))
                        {
                            double dist = ComputeDistanceToChain(candidate, currentChainSegments, ux, uy);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                bestCandidate = candidate;
                            }
                        }
                    }

                    if (bestCandidate != null)
                    {
                        currentChainSegments.Add(bestCandidate);
                        unassigned.Remove(bestCandidate);
                        addedAny = true;
                    }

                } while (addedAny);

                var chain = FinalizeChain(currentChainSegments);

                chain.DiagnosticId = $"CHAIN_{++chainIdx:D4}";
                chain.ParentDiagnosticIds = currentChainSegments.Select(s => s.DiagnosticId ?? "").Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                chain.RootRawCandidateIds = currentChainSegments.SelectMany(s => s.RootRawCandidateIds ?? new List<string>()).Distinct().ToList();

                BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
                {
                    CandidateId = chain.DiagnosticId,
                    DiagnosticId = chain.DiagnosticId,
                    ObjectType = "ContinuityChain",
                    ParentDiagnosticIds = chain.ParentDiagnosticIds,
                    RootRawCandidateIds = chain.RootRawCandidateIds,
                    Stage = BeamDiagnosticStage.ContinuityChainCreated,
                    Action = BeamDiagnosticAction.Created,
                    Reason = $"Created chain from {currentChainSegments.Count} segment(s).",
                    StartX = chain.StartX, StartY = chain.StartY, EndX = chain.EndX, EndY = chain.EndY,
                    Width = chain.Width, Height = chain.Height, Mark = chain.Mark
                });

                chains.Add(chain);
            }

            var summary = BeamDiagnosticCollector.Instance.CurrentSession?.PipelineSummary;
            if (summary != null)
            {
                summary.ContinuityChainsCount = chains.Count;
            }

            return chains
                .OrderBy(c => c.StartX)
                .ThenBy(c => c.StartY)
                .ThenBy(c => c.EndX)
                .ThenBy(c => c.EndY)
                .ToList();
        }

        private List<CadBeamSegment> PreprocessSegmentJunctionSplits(List<CadBeamSegment> segments)
        {
            var result = new List<CadBeamSegment>();
            int jsplitIdx = 0;

            foreach (var seg in segments)
            {
                var splitPoints = new List<double>(); // Projection t values along seg

                foreach (var other in segments)
                {
                    if (ReferenceEquals(seg, other)) continue;

                    // Only consider non-collinear other segments
                    double angleDiff = Math.Abs(seg.Angle - other.Angle);
                    if (angleDiff > Math.PI / 2.0) angleDiff = Math.PI - angleDiff;
                    if (angleDiff * 180.0 / Math.PI <= _options.AngularToleranceDegrees) continue; // Collinear -> ignore

                    // Compute line-line intersection or point-line proximity
                    if (TryGetSegmentIntersection(seg, other, out double tSeg, out double tOther))
                    {
                        // tSeg must be strictly inside seg interior (margin = JunctionToleranceMm)
                        double marginT = _options.JunctionToleranceMm / seg.Length;
                        if (tSeg > marginT && tSeg < (1.0 - marginT) && tOther >= -0.1 && tOther <= 1.1)
                        {
                            splitPoints.Add(tSeg);
                        }
                    }
                }

                if (splitPoints.Count == 0)
                {
                    result.Add(seg);
                }
                else
                {
                    splitPoints = splitPoints.Distinct().OrderBy(t => t).ToList();
                    double currentT = 0;
                    jsplitIdx++;

                    var children = new List<CadBeamSegment>();
                    int childSubIdx = 0;

                    foreach (double t in splitPoints)
                    {
                        if (t - currentT > 1e-3)
                        {
                            var sub = CreateSubSegment(seg, currentT, t);
                            sub.DiagnosticId = $"JSPLIT_{jsplitIdx:D4}_{(char)('A' + childSubIdx++)}";
                            children.Add(sub);
                            currentT = t;
                        }
                    }
                    if (1.0 - currentT > 1e-3)
                    {
                        var sub = CreateSubSegment(seg, currentT, 1.0);
                        sub.DiagnosticId = $"JSPLIT_{jsplitIdx:D4}_{(char)('A' + childSubIdx++)}";
                        children.Add(sub);
                    }

                    var summary = BeamDiagnosticCollector.Instance.CurrentSession?.PipelineSummary;
                    if (summary != null)
                    {
                        summary.JunctionSplitsCount++;
                    }

                    BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
                    {
                        DiagnosticId = seg.DiagnosticId ?? "",
                        ObjectType = "JunctionSplitParent",
                        ParentDiagnosticIds = seg.ParentDiagnosticIds,
                        RootRawCandidateIds = seg.RootRawCandidateIds,
                        OutputDiagnosticIds = children.Select(c => c.DiagnosticId).ToList(),
                        Stage = BeamDiagnosticStage.JunctionSplit,
                        Action = BeamDiagnosticAction.Split,
                        Reason = $"Junction split into {children.Count} sub-segments at intersection points.",
                        StartX = seg.StartX, StartY = seg.StartY, EndX = seg.EndX, EndY = seg.EndY
                    });

                    result.AddRange(children);
                }
            }

            return result;
        }

        private bool TryGetSegmentIntersection(CadBeamSegment s1, CadBeamSegment s2, out double t1, out double t2)
        {
            t1 = 0; t2 = 0;
            double dx1 = s1.EndX - s1.StartX;
            double dy1 = s1.EndY - s1.StartY;
            double dx2 = s2.EndX - s2.StartX;
            double dy2 = s2.EndY - s2.StartY;

            double denom = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denom) < 1e-9) return false;

            double dx3 = s2.StartX - s1.StartX;
            double dy3 = s2.StartY - s1.StartY;

            t1 = (dx3 * dy2 - dy3 * dx2) / denom;
            t2 = (dx3 * dy1 - dy3 * dx1) / denom;

            return true;
        }

        private CadBeamSegment CreateSubSegment(CadBeamSegment parent, double tStart, double tEnd)
        {
            double sx = parent.StartX + (parent.EndX - parent.StartX) * tStart;
            double sy = parent.StartY + (parent.EndY - parent.StartY) * tStart;
            double ex = parent.StartX + (parent.EndX - parent.StartX) * tEnd;
            double ey = parent.StartY + (parent.EndY - parent.StartY) * tEnd;

            return new CadBeamSegment
            {
                StartX = sx,
                StartY = sy,
                EndX = ex,
                EndY = ey,
                Width = parent.Width,
                Height = parent.Height,
                MeasuredWidth = parent.MeasuredWidth,
                Mark = parent.Mark,
                TextContent = parent.TextContent,
                IsPaired = parent.IsPaired,
                SourceLineIds = parent.SourceLineIds != null ? new List<string>(parent.SourceLineIds) : new List<string>(),
                Confidence = parent.Confidence,
                Layer = parent.Layer
            };
        }

        private (double ux, double uy, double theta) ComputeChainAxis(List<CadBeamSegment> segments)
        {
            var refSeg = segments.OrderByDescending(s => s.Length).First();
            double refUx = refSeg.DirectionX / refSeg.Length;
            double refUy = refSeg.DirectionY / refSeg.Length;

            double sumX = 0;
            double sumY = 0;

            foreach (var s in segments)
            {
                double segUx = s.DirectionX / s.Length;
                double segUy = s.DirectionY / s.Length;

                if (segUx * refUx + segUy * refUy < 0)
                {
                    segUx = -segUx;
                    segUy = -segUy;
                }

                sumX += s.Length * segUx;
                sumY += s.Length * segUy;
            }

            double len = Math.Sqrt(sumX * sumX + sumY * sumY);
            double ux = (len < 1e-9) ? refUx : sumX / len;
            double uy = (len < 1e-9) ? refUy : sumY / len;

            if (ux < -1e-9 || (Math.Abs(ux) <= 1e-9 && uy < -1e-9))
            {
                ux = -ux;
                uy = -uy;
            }

            double theta = Math.Atan2(uy, ux);
            while (theta < 0) theta += Math.PI;
            while (theta >= Math.PI) theta -= Math.PI;

            return (ux, uy, theta);
        }

        private bool IsSegmentCompatibleWithChain(
            CadBeamSegment candidate,
            List<CadBeamSegment> chainSegments,
            List<CadBeamSegment> allSegments,
            double ux, double uy, double theta,
            double nx, double ny)
        {
            // 1. Angular Check against chain axis
            double candidateAngleDiff = Math.Abs(candidate.Angle - theta);
            if (candidateAngleDiff > Math.PI / 2.0) candidateAngleDiff = Math.PI - candidateAngleDiff;
            if (candidateAngleDiff * 180.0 / Math.PI > _options.AngularToleranceDegrees) return false;

            // 2. Check against ALL existing segments in chain to prevent cumulative drift & dimension changes
            foreach (var s in chainSegments)
            {
                double pairAngleDiff = Math.Abs(candidate.Angle - s.Angle);
                if (pairAngleDiff > Math.PI / 2.0) pairAngleDiff = Math.PI - pairAngleDiff;
                if (pairAngleDiff * 180.0 / Math.PI > _options.AngularToleranceDegrees) return false;

                // Dimension Change Check (Width B and Height H)
                if (candidate.Width > 0 && s.Width > 0)
                {
                    double maxW = Math.Max(candidate.Width, s.Width);
                    double diffW = Math.Abs(candidate.Width - s.Width);
                    if (diffW > 5.0 && (diffW / maxW > _options.WidthToleranceRatio || diffW >= 20.0)) return false;
                }

                if (candidate.Height > 0 && s.Height > 0)
                {
                    double maxH = Math.Max(candidate.Height, s.Height);
                    double diffH = Math.Abs(candidate.Height - s.Height);
                    if (diffH > 5.0 && (diffH / maxH > _options.WidthToleranceRatio || diffH >= 20.0)) return false;
                }
            }

            // 3. Lateral Offset Check against chain axis
            var allChainPlusCandidate = chainSegments.Concat(new[] { candidate });
            double minLateral = double.MaxValue;
            double maxLateral = double.MinValue;

            foreach (var s in allChainPlusCandidate)
            {
                double lat1 = s.StartX * nx + s.StartY * ny;
                double lat2 = s.EndX * nx + s.EndY * ny;
                minLateral = Math.Min(minLateral, Math.Min(lat1, lat2));
                maxLateral = Math.Max(maxLateral, Math.Max(lat1, lat2));
            }

            if ((maxLateral - minLateral) > _options.LateralOffsetToleranceMm) return false;

            // 4. Junction Check (MAJOR 1): Ensure candidate does not connect across a Junction Node with a non-collinear segment
            foreach (var s in chainSegments)
            {
                if (IsJunctionBetween(candidate, s, allSegments, ux, uy))
                {
                    return false; // Junction node detected -> DO NOT MERGE
                }
            }

            // 5. Longitudinal Gap / Overlap Check against closest adjacent segment in chain
            bool connectsToAny = false;
            foreach (var s in chainSegments)
            {
                if (AreTwoSegmentsAdjacent(candidate, s, ux, uy))
                {
                    connectsToAny = true;
                    break;
                }
            }

            return connectsToAny;
        }

        private bool IsJunctionBetween(CadBeamSegment s1, CadBeamSegment s2, List<CadBeamSegment> allSegments, double ux, double uy)
        {
            // Find connection region between s1 and s2
            double t1_1 = s1.StartX * ux + s1.StartY * uy;
            double t1_2 = s1.EndX * ux + s1.EndY * uy;
            double min1 = Math.Min(t1_1, t1_2);
            double max1 = Math.Max(t1_1, t1_2);

            double t2_1 = s2.StartX * ux + s2.StartY * uy;
            double t2_2 = s2.EndX * ux + s2.EndY * uy;
            double min2 = Math.Min(t2_1, t2_2);
            double max2 = Math.Max(t2_1, t2_2);

            double connectionT;
            if (max1 <= min2) connectionT = (max1 + min2) / 2.0;
            else if (max2 <= min1) connectionT = (max2 + min1) / 2.0;
            else connectionT = (Math.Max(min1, min2) + Math.Min(max1, max2)) / 2.0;

            // Calculate world coords of connection point
            var (refUx, refUy, _) = ComputeChainAxis(new List<CadBeamSegment> { s1, s2 });
            double nx = -refUy;
            double ny = refUx;
            double avgOffset = (s1.StartX * nx + s1.StartY * ny + s2.StartX * nx + s2.StartY * ny) / 2.0;

            double connX = connectionT * refUx + avgOffset * nx;
            double connY = connectionT * refUy + avgOffset * ny;

            // Check if any non-collinear segment in allSegments meets near (connX, connY)
            foreach (var other in allSegments)
            {
                if (ReferenceEquals(other, s1) || ReferenceEquals(other, s2)) continue;

                // Angle check
                double angleDiff = Math.Abs(s1.Angle - other.Angle);
                if (angleDiff > Math.PI / 2.0) angleDiff = Math.PI - angleDiff;
                if (angleDiff * 180.0 / Math.PI <= _options.AngularToleranceDegrees) continue; // Collinear -> ignore

                // Proximity check from conn point to other segment
                double distToOther = DistancePointToSegment(connX, connY, other.StartX, other.StartY, other.EndX, other.EndY);
                if (distToOther <= _options.JunctionToleranceMm)
                {
                    return true; // Junction detected!
                }
            }

            return false;
        }

        private double DistancePointToSegment(double px, double py, double sx, double sy, double ex, double ey)
        {
            double dx = ex - sx;
            double dy = ey - sy;
            double lenSq = dx * dx + dy * dy;

            if (lenSq < 1e-9) return Math.Sqrt((px - sx) * (px - sx) + (py - sy) * (py - sy));

            double t = ((px - sx) * dx + (py - sy) * dy) / lenSq;
            t = Math.Max(0, Math.Min(1, t));

            double projX = sx + t * dx;
            double projY = sy + t * dy;

            return Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
        }

        private bool AreTwoSegmentsAdjacent(CadBeamSegment s1, CadBeamSegment s2, double ux, double uy)
        {
            double t1_1 = s1.StartX * ux + s1.StartY * uy;
            double t1_2 = s1.EndX * ux + s1.EndY * uy;
            double min1 = Math.Min(t1_1, t1_2);
            double max1 = Math.Max(t1_1, t1_2);

            double t2_1 = s2.StartX * ux + s2.StartY * uy;
            double t2_2 = s2.EndX * ux + s2.EndY * uy;
            double min2 = Math.Min(t2_1, t2_2);
            double max2 = Math.Max(t2_1, t2_2);

            if (max1 < min2 - 1e-4) // s1 strictly before s2
            {
                double gap = min2 - max1;
                return gap <= _options.EndpointGapToleranceMm;
            }
            else if (max2 < min1 - 1e-4) // s2 strictly before s1
            {
                double gap = min1 - max2;
                return gap <= _options.EndpointGapToleranceMm;
            }
            else // Overlapping or touching
            {
                double overlap = Math.Min(max1, max2) - Math.Max(min1, min2);
                if (overlap <= 1e-4)
                {
                    return true;
                }

                bool isDuplicate = Math.Abs(min1 - min2) <= 1e-3 && Math.Abs(max1 - max2) <= 1e-3;
                if (isDuplicate)
                {
                    return true;
                }

                return overlap >= _options.MinimumOverlapMm;
            }
        }

        private double ComputeDistanceToChain(CadBeamSegment candidate, List<CadBeamSegment> chainSegments, double ux, double uy)
        {
            double candT1 = candidate.StartX * ux + candidate.StartY * uy;
            double candT2 = candidate.EndX * ux + candidate.EndY * uy;
            double candMin = Math.Min(candT1, candT2);
            double candMax = Math.Max(candT1, candT2);

            double minDistance = double.MaxValue;
            foreach (var s in chainSegments)
            {
                double sT1 = s.StartX * ux + s.StartY * uy;
                double sT2 = s.EndX * ux + s.EndY * uy;
                double sMin = Math.Min(sT1, sT2);
                double sMax = Math.Max(sT1, sT2);

                double dist;
                if (candMax < sMin) dist = sMin - candMax;
                else if (sMax < candMin) dist = candMin - sMax;
                else dist = 0;

                if (dist < minDistance) minDistance = dist;
            }

            return minDistance;
        }

        private BeamChain FinalizeChain(List<CadBeamSegment> segments)
        {
            var (ux, uy, theta) = ComputeChainAxis(segments);
            double nx = -uy;
            double ny = ux;

            var projected = segments.Select(s =>
            {
                double tStart = s.StartX * ux + s.StartY * uy;
                double tEnd = s.EndX * ux + s.EndY * uy;
                double minT = Math.Min(tStart, tEnd);
                double maxT = Math.Max(tStart, tEnd);
                return new { Segment = s, MinT = minT, MaxT = maxT };
            })
            .OrderBy(p => p.MinT)
            .ThenByDescending(p => p.MaxT)
            .ThenBy(p => p.Segment.StartX)
            .ThenBy(p => p.Segment.StartY)
            .ToList();

            var orderedSegments = projected.Select(p => p.Segment).ToList();

            var allPoints = new List<Tuple<double, double, double>>();
            foreach (var s in segments)
            {
                double tStart = s.StartX * ux + s.StartY * uy;
                double tEnd = s.EndX * ux + s.EndY * uy;
                allPoints.Add(Tuple.Create(tStart, s.StartX, s.StartY));
                allPoints.Add(Tuple.Create(tEnd, s.EndX, s.EndY));
            }

            allPoints = allPoints.OrderBy(p => p.Item1).ToList();
            var minPt = allPoints.First();
            var maxPt = allPoints.Last();

            double avgOffset = allPoints.Average(p => p.Item2 * nx + p.Item3 * ny);

            double startX = Math.Round(minPt.Item1 * ux + avgOffset * nx, 4);
            double startY = Math.Round(minPt.Item1 * uy + avgOffset * ny, 4);
            double endX = Math.Round(maxPt.Item1 * ux + avgOffset * nx, 4);
            double endY = Math.Round(maxPt.Item1 * uy + avgOffset * ny, 4);

            return new BeamChain
            {
                Segments = orderedSegments,
                StartX = startX,
                StartY = startY,
                EndX = endX,
                EndY = endY
            };
        }
    }
}
