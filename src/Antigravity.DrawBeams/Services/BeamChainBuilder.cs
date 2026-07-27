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

        public double Width => Segments.FirstOrDefault(s => s.Width > 0)?.Width ?? 0;
        public double Height => Segments.FirstOrDefault(s => s.Height > 0)?.Height ?? 0;
        public string Mark => Segments.FirstOrDefault(s => !string.IsNullOrEmpty(s.Mark))?.Mark;
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

            var list = segments.Where(s => s != null && s.Length > 1e-3).ToList();
            if (list.Count == 0) return new List<BeamChain>();

            int n = list.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int i)
            {
                if (parent[i] == i) return i;
                return parent[i] = Find(parent[i]);
            }

            void Union(int i, int j)
            {
                int rootI = Find(i);
                int rootJ = Find(j);
                if (rootI != rootJ)
                {
                    parent[rootI] = rootJ;
                }
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (AreSegmentsContinuous(list[i], list[j]))
                    {
                        Union(i, j);
                    }
                }
            }

            var groups = new Dictionary<int, List<CadBeamSegment>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!groups.ContainsKey(root))
                {
                    groups[root] = new List<CadBeamSegment>();
                }
                groups[root].Add(list[i]);
            }

            var chains = new List<BeamChain>();
            foreach (var kvp in groups)
            {
                var chainSegments = kvp.Value;
                var chain = CreateChainFromSegments(chainSegments);
                chains.Add(chain);
            }

            return chains;
        }

        private bool AreSegmentsContinuous(CadBeamSegment s1, CadBeamSegment s2)
        {
            // 1. Check Angular Tolerance
            double angleDiff = Math.Abs(s1.Angle - s2.Angle);
            if (angleDiff > Math.PI / 2.0) angleDiff = Math.PI - angleDiff;
            double angleDiffDeg = angleDiff * 180.0 / Math.PI;
            if (angleDiffDeg > _options.AngularToleranceDegrees) return false;

            // 2. Check Width Tolerance
            if (s1.Width > 0 && s2.Width > 0)
            {
                double maxW = Math.Max(s1.Width, s2.Width);
                double diffW = Math.Abs(s1.Width - s2.Width);
                if (diffW / maxW > _options.WidthToleranceRatio && diffW > 5.0)
                {
                    return false;
                }
            }

            // 3. Check Direction and Axis
            double ux = Math.Cos(s1.Angle);
            double uy = Math.Sin(s1.Angle);
            double nx = -uy;
            double ny = ux;

            // Lateral distance from s2 mid & endpoints to s1 axis
            double distStart = Math.Abs((s2.StartX - s1.StartX) * nx + (s2.StartY - s1.StartY) * ny);
            double distEnd = Math.Abs((s2.EndX - s1.StartX) * nx + (s2.EndY - s1.StartY) * ny);
            if (distStart > _options.LateralOffsetToleranceMm || distEnd > _options.LateralOffsetToleranceMm)
            {
                return false;
            }

            // 4. Longitudinal Gap / Overlap Check
            double t1_1 = s1.StartX * ux + s1.StartY * uy;
            double t1_2 = s1.EndX * ux + s1.EndY * uy;
            double min1 = Math.Min(t1_1, t1_2);
            double max1 = Math.Max(t1_1, t1_2);

            double t2_1 = s2.StartX * ux + s2.StartY * uy;
            double t2_2 = s2.EndX * ux + s2.EndY * uy;
            double min2 = Math.Min(t2_1, t2_2);
            double max2 = Math.Max(t2_1, t2_2);

            double gap;
            if (max1 < min2)
            {
                gap = min2 - max1;
            }
            else if (max2 < min1)
            {
                gap = min1 - max2;
            }
            else
            {
                gap = 0; // Overlapping or touching
            }

            return gap <= _options.EndpointGapToleranceMm;
        }

        private BeamChain CreateChainFromSegments(List<CadBeamSegment> segments)
        {
            var chain = new BeamChain { Segments = segments };
            if (segments.Count == 1)
            {
                var s = segments[0];
                chain.StartX = s.StartX;
                chain.StartY = s.StartY;
                chain.EndX = s.EndX;
                chain.EndY = s.EndY;
                return chain;
            }

            // Calculate overall direction vector using average angle
            double avgAngle = segments[0].Angle;
            double ux = Math.Cos(avgAngle);
            double uy = Math.Sin(avgAngle);
            double nx = -uy;
            double ny = ux;

            // Project all endpoints onto the principal axis (ux, uy)
            var points = new List<Tuple<double, double, double>>(); // (t, x, y)
            foreach (var s in segments)
            {
                double tStart = s.StartX * ux + s.StartY * uy;
                double tEnd = s.EndX * ux + s.EndY * uy;
                points.Add(Tuple.Create(tStart, s.StartX, s.StartY));
                points.Add(Tuple.Create(tEnd, s.EndX, s.EndY));
            }

            points = points.OrderBy(p => p.Item1).ToList();
            var minPt = points.First();
            var maxPt = points.Last();

            // Calculate average lateral offset to align chain center
            double avgOffset = points.Average(p => p.Item2 * nx + p.Item3 * ny);

            chain.StartX = Math.Round(minPt.Item1 * ux + avgOffset * nx, 4);
            chain.StartY = Math.Round(minPt.Item1 * uy + avgOffset * ny, 4);
            chain.EndX = Math.Round(maxPt.Item1 * ux + avgOffset * nx, 4);
            chain.EndY = Math.Round(maxPt.Item1 * uy + avgOffset * ny, 4);

            return chain;
        }
    }
}
