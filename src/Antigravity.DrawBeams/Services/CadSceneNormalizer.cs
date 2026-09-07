using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.DrawBeams.Services
{
    public class CadSceneNormalizerOptions
    {
        /// <summary>Maximum endpoint distance for snapping (mm). Default: 1.0mm.</summary>
        public double SnapTolerance { get; set; } = 1.0;

        /// <summary>Zero or degenerate segment length threshold (mm). Default: 1e-6.</summary>
        public double ZeroLengthThreshold { get; set; } = 1e-6;

        /// <summary>Whether to remove zero/degenerate segments. Default: false (preserve geometry).</summary>
        public bool RemoveZeroLengthSegments { get; set; } = false;
    }

    /// <summary>
    /// Pure deterministic CadScene normalization service outside all COM loops.
    /// Provides endpoint canonical ordering, endpoint snapping, and deterministic scene sorting
    /// while preserving all entity metadata and provenance.
    /// </summary>
    public class CadSceneNormalizer
    {
        private readonly CadSceneNormalizerOptions _options;

        public CadSceneNormalizer(CadSceneNormalizerOptions options = null)
        {
            _options = options ?? new CadSceneNormalizerOptions();
        }

        public CadScene Normalize(CadScene scene)
        {
            return Normalize(scene, _options);
        }

        public CadScene Normalize(CadScene scene, double snapTolerance)
        {
            var opts = new CadSceneNormalizerOptions
            {
                SnapTolerance = snapTolerance,
                ZeroLengthThreshold = _options.ZeroLengthThreshold,
                RemoveZeroLengthSegments = _options.RemoveZeroLengthSegments
            };
            return Normalize(scene, opts);
        }

        public CadScene Normalize(CadScene scene, CadSceneNormalizerOptions options)
        {
            if (scene == null) return new CadScene();

            var opts = options ?? _options ?? new CadSceneNormalizerOptions();
            var result = new CadScene();

            if (scene.Segments == null || scene.Segments.Count == 0)
            {
                if (scene.Texts != null)
                {
                    result.Texts = scene.Texts
                        .Select(CloneText)
                        .OrderBy(t => t.X)
                        .ThenBy(t => t.Y)
                        .ThenBy(t => t.TextString ?? string.Empty)
                        .ThenBy(t => t.Layer ?? string.Empty)
                        .ThenBy(t => t.Id ?? string.Empty)
                        .ToList();
                }
                return result;
            }

            // Step 1: Copy segments and canonicalize endpoint orientation
            var processedSegments = scene.Segments
                .Select(s => CloneSegment(s))
                .ToList();

            foreach (var seg in processedSegments)
            {
                CanonicalizeEndpoints(seg);
            }

            // Step 2: Endpoint snapping within explicit bounded tolerance
            if (opts.SnapTolerance > 0)
            {
                SnapEndpoints(processedSegments, opts.SnapTolerance);

                // Re-canonicalize endpoint orientation after snapping
                foreach (var seg in processedSegments)
                {
                    CanonicalizeEndpoints(seg);
                }
            }

            // Step 3: Zero / short segment handling
            var validSegments = new List<CadSegment>();
            foreach (var seg in processedSegments)
            {
                double len = Math.Sqrt(Math.Pow(seg.EndX - seg.StartX, 2) + Math.Pow(seg.EndY - seg.StartY, 2));
                if (len <= opts.ZeroLengthThreshold)
                {
                    if (!opts.RemoveZeroLengthSegments)
                    {
                        // Deterministic handling: preserve zero/very-short segment with snapped endpoints
                        seg.EndX = seg.StartX;
                        seg.EndY = seg.StartY;
                        validSegments.Add(seg);
                    }
                }
                else
                {
                    validSegments.Add(seg);
                }
            }

            // Step 4: Deterministic ordering of segments
            result.Segments = validSegments
                .OrderBy(s => Math.Round(s.StartX, 6))
                .ThenBy(s => Math.Round(s.StartY, 6))
                .ThenBy(s => Math.Round(s.EndX, 6))
                .ThenBy(s => Math.Round(s.EndY, 6))
                .ThenBy(s => s.Layer ?? string.Empty)
                .ThenBy(s => s.Color)
                .ThenBy(s => s.PolylineWidth)
                .ThenBy(s => s.GroupId ?? string.Empty)
                .ThenBy(s => s.Id ?? string.Empty)
                .ThenBy(s => s.Provenance?.EntityHandle ?? string.Empty)
                .ThenBy(s => s.Provenance?.SegmentIndex ?? 0)
                .ToList();

            // Step 5: Preserve and sort texts deterministically
            if (scene.Texts != null)
            {
                result.Texts = scene.Texts
                    .Select(CloneText)
                    .OrderBy(t => Math.Round(t.X, 6))
                    .ThenBy(t => Math.Round(t.Y, 6))
                    .ThenBy(t => t.TextString ?? string.Empty)
                    .ThenBy(t => t.Layer ?? string.Empty)
                    .ThenBy(t => t.Id ?? string.Empty)
                    .ToList();
            }

            return result;
        }

        private static void CanonicalizeEndpoints(CadSegment seg)
        {
            if (!IsFirstPointSmaller(seg.StartX, seg.StartY, seg.EndX, seg.EndY))
            {
                double tempX = seg.StartX;
                double tempY = seg.StartY;
                seg.StartX = seg.EndX;
                seg.StartY = seg.EndY;
                seg.EndX = tempX;
                seg.EndY = tempY;
            }
        }

        private static bool IsFirstPointSmaller(double x1, double y1, double x2, double y2)
        {
            const double eps = 1e-9;
            if (x1 < x2 - eps) return true;
            if (x1 > x2 + eps) return false;
            if (y1 < y2 - eps) return true;
            if (y1 > y2 + eps) return false;
            return true;
        }

        private static void SnapEndpoints(List<CadSegment> segments, double snapTolerance)
        {
            // Collect all unique endpoints
            var pointList = new List<Tuple<double, double>>();
            foreach (var seg in segments)
            {
                pointList.Add(Tuple.Create(seg.StartX, seg.StartY));
                pointList.Add(Tuple.Create(seg.EndX, seg.EndY));
            }

            // Sort points deterministically by X then Y
            var sortedPoints = pointList
                .Distinct()
                .OrderBy(p => p.Item1)
                .ThenBy(p => p.Item2)
                .ToList();

            // Cluster endpoints within snapTolerance
            var clusters = new List<Tuple<double, double>>();
            var snapMap = new Dictionary<Tuple<double, double>, Tuple<double, double>>();

            foreach (var pt in sortedPoints)
            {
                Tuple<double, double> bestCluster = null;
                double minSqDist = double.MaxValue;
                double tolSq = snapTolerance * snapTolerance;

                foreach (var c in clusters)
                {
                    double dx = pt.Item1 - c.Item1;
                    double dy = pt.Item2 - c.Item2;
                    double sqDist = dx * dx + dy * dy;
                    if (sqDist <= tolSq && sqDist < minSqDist)
                    {
                        minSqDist = sqDist;
                        bestCluster = c;
                    }
                }

                if (bestCluster != null)
                {
                    snapMap[pt] = bestCluster;
                }
                else
                {
                    clusters.Add(pt);
                    snapMap[pt] = pt;
                }
            }

            // Apply snapped endpoints to segments
            foreach (var seg in segments)
            {
                var startKey = Tuple.Create(seg.StartX, seg.StartY);
                if (snapMap.TryGetValue(startKey, out var snappedStart))
                {
                    seg.StartX = snappedStart.Item1;
                    seg.StartY = snappedStart.Item2;
                }

                var endKey = Tuple.Create(seg.EndX, seg.EndY);
                if (snapMap.TryGetValue(endKey, out var snappedEnd))
                {
                    seg.EndX = snappedEnd.Item1;
                    seg.EndY = snappedEnd.Item2;
                }
            }
        }

        private static CadSegment CloneSegment(CadSegment source)
        {
            if (source == null) return null;
            return new CadSegment
            {
                StartX = source.StartX,
                StartY = source.StartY,
                EndX = source.EndX,
                EndY = source.EndY,
                Id = source.Id,
                Layer = source.Layer,
                Color = source.Color,
                PolylineWidth = source.PolylineWidth,
                GroupId = source.GroupId,
                Provenance = CloneProvenance(source.Provenance)
            };
        }

        private static CadText CloneText(CadText source)
        {
            if (source == null) return null;
            return new CadText
            {
                Id = source.Id,
                TextString = source.TextString,
                X = source.X,
                Y = source.Y,
                Rotation = source.Rotation,
                TextHeight = source.TextHeight,
                Layer = source.Layer,
                ObjectName = source.ObjectName,
                Provenance = CloneProvenance(source.Provenance)
            };
        }

        private static CadEntityProvenance CloneProvenance(CadEntityProvenance source)
        {
            if (source == null) return null;
            return new CadEntityProvenance
            {
                EntityHandle = source.EntityHandle,
                EntityId = source.EntityId,
                ParentEntityHandle = source.ParentEntityHandle,
                ParentEntityId = source.ParentEntityId,
                Layer = source.Layer,
                Color = source.Color,
                Linetype = source.Linetype,
                Lineweight = source.Lineweight,
                SourceKind = source.SourceKind,
                ObjectName = source.ObjectName,
                PolylineParentIdentity = source.PolylineParentIdentity,
                SegmentIndex = source.SegmentIndex
            };
        }
    }
}
