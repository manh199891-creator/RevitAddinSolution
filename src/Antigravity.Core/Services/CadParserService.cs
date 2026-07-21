using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.Core.Geometry;
using Antigravity.Core.Models;

namespace Antigravity.Core.Services
{
    public class CadParserService : ICadParserService
    {
        private const int MaxSegmentsPerElevation = 5000;
        private const double ElevationTolerance = 0.01;

        public List<FoundationData> ExtractFoundationData(object cadLink, string layerName)
        {
            var importInstance = cadLink as ImportInstance;
            if (importInstance == null) return new List<FoundationData>();
            var geometry = importInstance.get_Geometry(new Options());
            if (geometry == null) return new List<FoundationData>();

            var loops = new List<IList<XYZ>>();
            var looseLines = new List<Line>();
            ProcessGeometry(geometry, importInstance.Document, layerName ?? string.Empty,
                Transform.Identity, loops, looseLines);
            var results = new List<FoundationData>();

            foreach (var loop in loops)
            {
                if (loop.Count < 3 || loop.Max(p => p.Z) - loop.Min(p => p.Z) > ElevationTolerance) continue;
                var segments = new List<Segment2>();
                for (var i = 0; i < loop.Count - 1; i++) segments.Add(ToSegment(loop[i], loop[i + 1]));
                if (loop[0].DistanceTo(loop[loop.Count - 1]) > 0.001)
                    segments.Add(ToSegment(loop[loop.Count - 1], loop[0]));
                results.AddRange(ExtractFromSegments(segments, loop.Average(p => p.Z)));
            }

            foreach (var group in GroupByElevation(looseLines.Where(IsHorizontal)))
            {
                if (group.Count > MaxSegmentsPerElevation)
                    throw new InvalidOperationException($"The CAD layer '{layerName}' contains {group.Count} horizontal lines at one elevation; the safe limit is {MaxSegmentsPerElevation}.");
                results.AddRange(ExtractFromSegments(group.Select(line =>
                    ToSegment(line.GetEndPoint(0), line.GetEndPoint(1))), group.Average(MidElevation)));
            }
            return Deduplicate(results);
        }

        public static IEnumerable<FoundationData> ExtractFromSegments(IEnumerable<Segment2> segments, double elevation)
        {
            var faces = new PlanarFaceExtractor(0.001, 1e-8, MaxSegmentsPerElevation).Extract(segments).Faces;
            var results = new List<FoundationData>();
            foreach (var face in faces)
            {
                Rectangle2 rectangle;
                if (!Rectangle2.TryCreate(face, out rectangle, 0.05)
                    || rectangle.Length <= 0.1 || rectangle.Width <= 0.1) continue;
                var candidate = new FoundationData
                {
                    X = rectangle.Center.X, Y = rectangle.Center.Y, Z = elevation,
                    Length = rectangle.Length, Width = rectangle.Width,
                    RotationAngle = rectangle.RotationRadians
                };
                if (!results.Any(existing => AreEquivalent(existing, candidate))) results.Add(candidate);
            }
            return results;
        }

        private static List<List<Line>> GroupByElevation(IEnumerable<Line> lines)
        {
            var groups = new List<List<Line>>();
            foreach (var line in lines.OrderBy(MidElevation))
            {
                var z = MidElevation(line);
                var group = groups.FirstOrDefault(g => Math.Abs(g.Average(MidElevation) - z) <= ElevationTolerance);
                if (group == null) groups.Add(new List<Line> { line }); else group.Add(line);
            }
            return groups;
        }

        private static List<FoundationData> Deduplicate(IEnumerable<FoundationData> source)
        {
            var results = new List<FoundationData>();
            foreach (var item in source) if (!results.Any(existing => AreEquivalent(existing, item))) results.Add(item);
            return results;
        }

        private static bool AreEquivalent(FoundationData a, FoundationData b)
        {
            if (Math.Abs(a.X - b.X) >= 0.1 || Math.Abs(a.Y - b.Y) >= 0.1 || Math.Abs(a.Z - b.Z) >= 0.1) return false;
            if (SameDimensions(a.Length, a.Width, b.Length, b.Width)) return Parallel(a.RotationAngle, b.RotationAngle);
            return SameDimensions(a.Length, a.Width, b.Width, b.Length) && Parallel(a.RotationAngle, b.RotationAngle + Math.PI / 2.0);
        }

        private static bool SameDimensions(double a, double b, double c, double d) => Math.Abs(a - c) < 0.1 && Math.Abs(b - d) < 0.1;
        private static bool Parallel(double a, double b)
        {
            var difference = Math.Abs(a - b) % Math.PI;
            if (difference > Math.PI / 2.0) difference = Math.PI - difference;
            return difference < 0.05;
        }
        private static bool IsHorizontal(Line line) => Math.Abs(line.GetEndPoint(0).Z - line.GetEndPoint(1).Z) <= ElevationTolerance;
        private static double MidElevation(Line line) => (line.GetEndPoint(0).Z + line.GetEndPoint(1).Z) / 2.0;
        private static Segment2 ToSegment(XYZ a, XYZ b) => new Segment2(new Point2(a.X, a.Y), new Point2(b.X, b.Y));

        private static void ProcessGeometry(GeometryElement geometry, Document document, string layerName,
            Transform transform, ICollection<IList<XYZ>> loops, ICollection<Line> looseLines)
        {
            foreach (var item in geometry)
            {
                var instance = item as GeometryInstance;
                if (instance != null)
                {
                    ProcessGeometry(instance.GetSymbolGeometry(), document, layerName, transform.Multiply(instance.Transform), loops, looseLines);
                    continue;
                }
                if (!IsOnLayer(item, document, layerName)) continue;
                var polyline = item as PolyLine;
                if (polyline != null)
                {
                    var points = polyline.GetCoordinates().Select(transform.OfPoint).ToList();
                    if (points.Count > 2 && points[0].DistanceTo(points[points.Count - 1]) < ElevationTolerance) loops.Add(points);
                    else AddPolylineSegments(points, looseLines);
                    continue;
                }
                var line = item as Line;
                if (line == null) continue;
                var start = transform.OfPoint(line.GetEndPoint(0));
                var end = transform.OfPoint(line.GetEndPoint(1));
                if (start.DistanceTo(end) > ElevationTolerance) looseLines.Add(Line.CreateBound(start, end));
            }
        }

        private static bool IsOnLayer(GeometryObject item, Document document, string layerName)
        {
            if (item.GraphicsStyleId == ElementId.InvalidElementId) return false;
            var style = document.GetElement(item.GraphicsStyleId) as GraphicsStyle;
            return style != null && style.GraphicsStyleCategory != null
                && string.Equals(style.GraphicsStyleCategory.Name, layerName, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddPolylineSegments(IList<XYZ> points, ICollection<Line> lines)
        {
            for (var i = 0; i < points.Count - 1; i++)
                if (points[i].DistanceTo(points[i + 1]) > ElevationTolerance) lines.Add(Line.CreateBound(points[i], points[i + 1]));
        }
    }
}
