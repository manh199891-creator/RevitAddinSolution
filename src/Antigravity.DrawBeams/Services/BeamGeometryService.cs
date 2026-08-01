using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.DrawBeams.Services
{
    public class BeamGeometryService
    {
        public double GetPerpendicularDistance(CadSegment reference, CadSegment candidate)
        {
            if (reference == null || candidate == null) return 0;
            return GetPerpendicularDistance(reference, candidate.StartX, candidate.StartY);
        }

        public double GetPerpendicularDistance(CadSegment reference, double pointX, double pointY)
        {
            if (reference == null) return 0;
            double dx = reference.EndX - reference.StartX;
            double dy = reference.EndY - reference.StartY;
            double L2 = dx * dx + dy * dy;
            if (L2 < 1e-18) return 0;
            return Math.Abs(dy * pointX - dx * pointY + reference.EndX * reference.StartY - reference.EndY * reference.StartX) / Math.Sqrt(L2);
        }

        public double GetAngleDifference(CadSegment first, CadSegment second)
        {
            if (first == null || second == null) return 0;
            if (first.Length < 1e-9 || second.Length < 1e-9) return 0;

            double diff = Math.Abs(first.Angle - second.Angle);
            while (diff > Math.PI / 2.0) diff = Math.PI - diff;
            return Math.Abs(diff);
        }

        public double GetOverlapLength(CadSegment first, CadSegment second)
        {
            if (first == null || second == null) return 0;
            double L = first.Length;
            if (L < 1e-9) return 0;

            double dx = first.DirectionX, dy = first.DirectionY;
            double ux = dx / L, uy = dy / L;

            double t1 = ((second.StartX - first.StartX) * ux + (second.StartY - first.StartY) * uy) / L;
            double t2 = ((second.EndX - first.StartX) * ux + (second.EndY - first.StartY) * uy) / L;

            double start = Math.Max(0.0, Math.Min(t1, t2));
            double end = Math.Min(1.0, Math.Max(t1, t2));

            if (start < end) return (end - start) * L;
            return 0;
        }

        public double GetOverlapRatio(CadSegment first, CadSegment second)
        {
            if (first == null || second == null) return 0;
            double overlap = GetOverlapLength(first, second);
            double maxLen = Math.Max(first.Length, second.Length);
            return maxLen > 1e-9 ? overlap / maxLen : 0;
        }

        public bool IsProjectionWithinRange(CadSegment anchor, CadSegment partner)
        {
            if (anchor == null || partner == null) return false;
            double len = anchor.Length;
            if (len < 1e-9) return false;

            double ux = anchor.DirectionX / len;
            double uy = anchor.DirectionY / len;

            double proj = ((partner.MidX - anchor.StartX) * ux + (partner.MidY - anchor.StartY) * uy) / len;
            return proj > -0.3 && proj < 1.3;
        }

        public bool AreParallel(CadSegment first, CadSegment second, double minimumAbsoluteDot = 0.999)
        {
            if (first == null || second == null) return false;
            double len1 = first.Length;
            double len2 = second.Length;
            if (len1 < 1e-9 || len2 < 1e-9) return false;

            double dot = Math.Abs((first.DirectionX * second.DirectionX + first.DirectionY * second.DirectionY) / (len1 * len2));
            return dot >= minimumAbsoluteDot;
        }

        public List<CadSegment> PreProcessSegments(IEnumerable<CadSegment> source)
        {
            if (source == null) return new List<CadSegment>();
            var sourceList = source.ToList();
            if (sourceList.Count <= 1) return sourceList.ToList();

            var result = sourceList
                .Where(s => s.GroupId != null || s.PolylineWidth > 0)
                .ToList();

            var normalSegments = sourceList
                .Where(s => s.GroupId == null && s.PolylineWidth <= 0 && s.Length > 50)
                .ToList();

            var groups = normalSegments.GroupBy(s =>
            {
                double angleKey = Math.Round(s.Angle / 0.01);
                double normalX = -Math.Sin(s.Angle);
                double normalY = Math.Cos(s.Angle);
                double distanceKey = Math.Round(((s.StartX * normalX) + (s.StartY * normalY)) / 20.0);
                return $"{s.Layer}|{s.Color}|{angleKey}|{distanceKey}";
            });

            foreach (var group in groups)
            {
                var items = group.ToList();
                if (items.Count == 1)
                {
                    result.Add(items[0]);
                    continue;
                }

                double angle = items[0].Angle;
                double ux = Math.Cos(angle);
                double uy = Math.Sin(angle);

                var intervals = items
                    .Select(s =>
                    {
                        double t1 = s.StartX * ux + s.StartY * uy;
                        double t2 = s.EndX * ux + s.EndY * uy;
                        return new
                        {
                            Segment = s,
                            Min = Math.Min(t1, t2),
                            Max = Math.Max(t1, t2)
                        };
                    })
                    .OrderBy(i => i.Min)
                    .ToList();

                var cluster = new List<CadSegment> { intervals[0].Segment };
                double clusterMin = intervals[0].Min;
                double clusterMax = intervals[0].Max;

                for (int i = 1; i < intervals.Count; i++)
                {
                    if (intervals[i].Min - clusterMax <= 200.0)
                    {
                        cluster.Add(intervals[i].Segment);
                        clusterMax = Math.Max(clusterMax, intervals[i].Max);
                    }
                    else
                    {
                        result.Add(CreateMergedSegment(cluster, clusterMin, clusterMax, ux, uy));
                        cluster = new List<CadSegment> { intervals[i].Segment };
                        clusterMin = intervals[i].Min;
                        clusterMax = intervals[i].Max;
                    }
                }

                result.Add(CreateMergedSegment(cluster, clusterMin, clusterMax, ux, uy));
            }

            return result;
        }

        private CadSegment CreateMergedSegment(List<CadSegment> cluster, double minT, double maxT, double ux, double uy)
        {
            var first = cluster[0];
            double nx = -uy;
            double ny = ux;
            double offset = cluster.Average(s => s.StartX * nx + s.StartY * ny);

            return new CadSegment
            {
                StartX = minT * ux + offset * nx,
                StartY = minT * uy + offset * ny,
                EndX = maxT * ux + offset * nx,
                EndY = maxT * uy + offset * ny,
                Id = string.Join("+", cluster.Select(s => s.Id)),
                Layer = first.Layer,
                Color = first.Color
            };
        }
    }
}
