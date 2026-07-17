using Autodesk.Revit.DB;
using Antigravity.ZoneSplit.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.ZoneSplit.Services
{
    public sealed class PhysicalSplitSegment
    {
        public string ZoneId { get; }
        public string ZoneName { get; }
        public double Start { get; }
        public double End { get; }

        public PhysicalSplitSegment(string zoneId, string zoneName, double start, double end)
        {
            ZoneId = zoneId;
            ZoneName = zoneName;
            Start = start;
            End = end;
        }
    }

    public sealed class PhysicalSplitResult
    {
        public bool Succeeded { get; set; }
        public string Reason { get; set; }
        public List<(ElementId ElementId, string ZoneId, string ZoneName)> Assignments { get; }
            = new List<(ElementId ElementId, string ZoneId, string ZoneName)>();
    }

    public sealed class PhysicalSplitService
    {
        private const double MinSegmentLengthFeet = 0.01;

        private readonly Document _doc;

        public PhysicalSplitService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public bool CanSplit(Element elem, out string reason)
        {
            reason = null;

            if (!IsSupportedCategory(elem))
            {
                reason = "Only straight walls and structural framing are supported for physical split.";
                return false;
            }

            if (!(elem.Location is LocationCurve lc) || !(lc.Curve is Line))
            {
                reason = "Element is not a straight LocationCurve.";
                return false;
            }

            return true;
        }

        public bool TryBuildSegmentsForZones(
            Element elem,
            IReadOnlyList<ZoneVolume> zones,
            out List<PhysicalSplitSegment> segments,
            out string reason)
        {
            segments = new List<PhysicalSplitSegment>();
            reason = null;

            if (!CanSplit(elem, out reason)) return false;
            if (zones == null || zones.Count == 0)
            {
                reason = "No zones were provided.";
                return false;
            }

            var elemSolid = SolidExtractor.GetUnionSolid(elem) ?? SolidExtractor.GetLargestSolid(elem);
            if (elemSolid == null)
            {
                reason = "Element has no valid solid.";
                return false;
            }

            var lc = (LocationCurve)elem.Location;
            var line = (Line)lc.Curve;
            var origin = line.GetEndPoint(0);
            var length = line.Length;
            if (length < MinSegmentLengthFeet)
            {
                reason = "Element is shorter than the minimum split length.";
                return false;
            }

            foreach (var zone in zones)
            {
                var interSolid = SolidExtractor.IntersectionSolid(elemSolid, zone.Solid);
                if (interSolid == null) continue;

                if (!TryGetProjectionRange(elem, interSolid, out var start, out var end)) continue;

                start = Math.Max(0, Math.Min(length, start));
                end = Math.Max(0, Math.Min(length, end));
                if (end - start < MinSegmentLengthFeet) continue;

                segments.Add(new PhysicalSplitSegment(zone.ZoneId, zone.ZoneName, start, end));
            }

            segments = segments
                .OrderBy(x => x.Start)
                .ThenByDescending(x => x.End - x.Start)
                .ToList();

            if (segments.Count == 0)
            {
                reason = "Element does not intersect any zone.";
                return false;
            }

            return true;
        }

        public PhysicalSplitResult Split(Element elem, IReadOnlyList<PhysicalSplitSegment> segments)
        {
            var result = new PhysicalSplitResult();

            if (!CanSplit(elem, out var reason))
            {
                result.Reason = reason;
                return result;
            }

            var ordered = segments
                .OrderBy(x => x.Start)
                .Where(x => x.End - x.Start >= MinSegmentLengthFeet)
                .ToList();

            if (ordered.Any(x => string.IsNullOrWhiteSpace(x.ZoneId)))
            {
                result.Reason = "Every physical split segment must have a ZoneId.";
                return result;
            }

            if (ordered.Count < 2)
            {
                result.Reason = "Less than two valid split segments.";
                return result;
            }

            var lc = (LocationCurve)elem.Location;
            var originalLine = (Line)lc.Curve;
            var origin = originalLine.GetEndPoint(0);
            var direction = (originalLine.GetEndPoint(1) - origin).Normalize();

            var targetIds = new List<ElementId> { elem.Id };
            var copiedIds = new List<ElementId>();

            for (int i = 1; i < ordered.Count; i++)
            {
                var copies = ElementTransformUtils.CopyElement(_doc, elem.Id, XYZ.Zero);
                var copyId = copies.FirstOrDefault();
                if (copyId == null || copyId == ElementId.InvalidElementId)
                {
                    DeleteCopiedElements(copiedIds);
                    result.Reason = "Could not copy source element for split segment.";
                    return result;
                }

                targetIds.Add(copyId);
                copiedIds.Add(copyId);
            }

            try
            {
                for (int i = 0; i < ordered.Count; i++)
                {
                    var target = _doc.GetElement(targetIds[i]);
                    if (!(target?.Location is LocationCurve targetLc))
                    {
                        DeleteCopiedElements(copiedIds);
                        result.Reason = $"Copied element {targetIds[i]} does not expose LocationCurve.";
                        return result;
                    }

                    var segment = ordered[i];
                    var p0 = origin + direction.Multiply(segment.Start);
                    var p1 = origin + direction.Multiply(segment.End);
                    targetLc.Curve = Line.CreateBound(p0, p1);

                    result.Assignments.Add((targetIds[i], segment.ZoneId, segment.ZoneName));
                }
            }
            catch (Exception ex)
            {
                DeleteCopiedElements(copiedIds);
                result.Assignments.Clear();
                result.Reason = $"Could not resize copied split segments: {ex.Message}";
                return result;
            }

            result.Succeeded = true;
            return result;
        }

        private void DeleteCopiedElements(IEnumerable<ElementId> copiedIds)
        {
            var ids = copiedIds?.Where(id => id != null && id != ElementId.InvalidElementId).Distinct().ToList();
            if (ids == null || ids.Count == 0) return;

            try
            {
                _doc.Delete(ids);
            }
            catch
            {
                // Best-effort cleanup; the caller still receives a failed result.
            }
        }

        private static bool IsSupportedCategory(Element elem)
        {
            var categoryId = elem?.Category?.Id?.Value;
            return categoryId == (long)BuiltInCategory.OST_Walls
                || categoryId == (long)BuiltInCategory.OST_StructuralFraming;
        }

        private static bool TryGetProjectionRange(Element elem, Solid interSolid, out double start, out double end)
        {
            start = 0;
            end = 0;

            if (!TryGetElementAxis(elem, out var origin, out var dir)) return false;

            double min = double.MaxValue;
            double max = -double.MaxValue;
            bool found = false;

            foreach (Face face in interSolid.Faces)
            {
                var mesh = face.Triangulate();
                if (mesh == null) continue;

                foreach (XYZ vertex in mesh.Vertices)
                {
                    double proj = (vertex - origin).DotProduct(dir);
                    min = Math.Min(min, proj);
                    max = Math.Max(max, proj);
                    found = true;
                }
            }

            if (!found || max <= min) return false;

            start = min;
            end = max;
            return true;
        }

        private static bool TryGetElementAxis(Element elem, out XYZ origin, out XYZ direction)
        {
            origin = XYZ.Zero;
            direction = XYZ.BasisX;

            if (!(elem.Location is LocationCurve lc) || !(lc.Curve is Line line)) return false;

            var p0 = line.GetEndPoint(0);
            var p1 = line.GetEndPoint(1);
            var vector = p1 - p0;
            if (vector.GetLength() < 1e-6) return false;

            origin = p0;
            direction = vector.Normalize();
            return true;
        }
    }
}
