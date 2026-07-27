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

            // 2. Deterministic initial ordering of input segments to eliminate input-order dependence
            var sortedInput = validSegments
                .OrderByDescending(s => s.Length)
                .ThenBy(s => s.StartX)
                .ThenBy(s => s.StartY)
                .ThenBy(s => s.EndX)
                .ThenBy(s => s.EndY)
                .ThenBy(s => s.Layer ?? "")
                .ToList();

            var unassigned = new HashSet<CadBeamSegment>(sortedInput);
            var chains = new List<BeamChain>();

            while (unassigned.Count > 0)
            {
                // Pick longest remaining unassigned segment as seed
                var seed = sortedInput.First(s => unassigned.Contains(s));
                var currentChainSegments = new List<CadBeamSegment> { seed };
                unassigned.Remove(seed);

                bool addedAny;
                do
                {
                    addedAny = false;

                    // Compute current chain axis from current chain segments
                    var (ux, uy, theta) = ComputeChainAxis(currentChainSegments);
                    double nx = -uy;
                    double ny = ux;

                    // Search unassigned candidates that are compatible with chain axis and all existing segments
                    CadBeamSegment bestCandidate = null;
                    double minDistance = double.MaxValue;

                    foreach (var candidate in sortedInput.Where(s => unassigned.Contains(s)))
                    {
                        if (IsSegmentCompatibleWithChain(candidate, currentChainSegments, ux, uy, theta, nx, ny))
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
                chains.Add(chain);
            }

            // Sort final chains deterministically
            return chains
                .OrderBy(c => c.StartX)
                .ThenBy(c => c.StartY)
                .ThenBy(c => c.EndX)
                .ThenBy(c => c.EndY)
                .ToList();
        }

        private (double ux, double uy, double theta) ComputeChainAxis(List<CadBeamSegment> segments)
        {
            // Reference direction from longest segment
            var refSeg = segments.OrderByDescending(s => s.Length).First();
            double refUx = refSeg.DirectionX / refSeg.Length;
            double refUy = refSeg.DirectionY / refSeg.Length;

            double sumX = 0;
            double sumY = 0;

            foreach (var s in segments)
            {
                double segUx = s.DirectionX / s.Length;
                double segUy = s.DirectionY / s.Length;

                // Handle reversed direction segments by flipping vector if dot product < 0
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

            // Enforce Canonical Axis Direction (ux > 0, or if |ux| <= 1e-9 then uy > 0)
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
            var allSegments = chainSegments.Concat(new[] { candidate });
            double minLateral = double.MaxValue;
            double maxLateral = double.MinValue;

            foreach (var s in allSegments)
            {
                double lat1 = s.StartX * nx + s.StartY * ny;
                double lat2 = s.EndX * nx + s.EndY * ny;
                minLateral = Math.Min(minLateral, Math.Min(lat1, lat2));
                maxLateral = Math.Max(maxLateral, Math.Max(lat1, lat2));
            }

            if ((maxLateral - minLateral) > _options.LateralOffsetToleranceMm) return false;

            // 4. Longitudinal Gap / Overlap Check against closest adjacent segment in chain
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
                    // Touching at endpoint (zero overlap)
                    return true;
                }

                // True geometric duplicate check (min and max match within tolerance)
                bool isDuplicate = Math.Abs(min1 - min2) <= 1e-3 && Math.Abs(max1 - max2) <= 1e-3;
                if (isDuplicate)
                {
                    return true;
                }

                // Containment (non-duplicate) and partial overlap MUST satisfy MinimumOverlapMm
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
