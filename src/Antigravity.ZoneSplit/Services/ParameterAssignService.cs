using Autodesk.Revit.DB;
using Antigravity.ZoneSplit.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.ZoneSplit.Services
{
    public sealed class ParameterAssignService
    {
        private readonly Document _doc;

        public ParameterAssignService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Gán tự động Parameter BIM_ZoneID cho các cấu kiện con (Parts hoặc cấu kiện cắt vật lý) 
        /// dựa vào vị trí tâm (Centroid/BoundingBox Center) của chúng có nằm trong Zone Solid hay không.
        /// </summary>
        public int AssignParametersToSubElements(IEnumerable<ElementId> subElementIds, IReadOnlyList<ZoneVolume> zones)
        {
            if (subElementIds == null || !subElementIds.Any() || zones == null || !zones.Any())
                return 0;

            int assignedCount = 0;
            using (var tx = new Transaction(_doc, "ZoneSplit - Assign Parameters"))
            {
                tx.Start();

                foreach (var elemId in subElementIds)
                {
                    var elem = _doc.GetElement(elemId);
                    if (elem == null) continue;

                    var centerPoint = GetElementCenter(elem);
                    if (centerPoint == null) continue;

                    // Tìm Zone chứa điểm trung tâm này
                    var matchingZone = FindZoneContainingPoint(centerPoint, zones);
                    if (matchingZone != null)
                    {
                        // Gán Parameter BIM_ZoneID
                        WriteStringParam(elem, ParameterSetupService.PARAM_ZONE_ID, matchingZone.ZoneId);
                        WriteStringParam(elem, ParameterSetupService.PARAM_ZONE_NAME, matchingZone.ZoneName);
                        assignedCount++;
                    }
                }

                tx.Commit();
            }

            return assignedCount;
        }

        public int AssignKnownZoneParameters(IEnumerable<(ElementId ElementId, string ZoneId, string ZoneName)> assignments)
        {
            var items = assignments?.ToList() ?? new List<(ElementId ElementId, string ZoneId, string ZoneName)>();
            if (!items.Any()) return 0;

            int assignedCount = 0;
            using (var tx = new Transaction(_doc, "ZoneSplit - Assign Known Zone Parameters"))
            {
                tx.Start();

                foreach (var item in items)
                {
                    var elem = _doc.GetElement(item.ElementId);
                    if (elem == null) continue;

                    WriteStringParam(elem, ParameterSetupService.PARAM_ZONE_ID, item.ZoneId);
                    WriteStringParam(elem, ParameterSetupService.PARAM_ZONE_NAME, item.ZoneName);
                    assignedCount++;
                }

                tx.Commit();
            }

            return assignedCount;
        }

        private XYZ GetElementCenter(Element elem)
        {
            // Cố gắng lấy trọng tâm từ Solid trước (chuẩn xác hơn cho Parts)
            var solid = SolidExtractor.GetLargestSolid(elem);
            if (solid != null && solid.Volume > 0)
            {
                return solid.ComputeCentroid();
            }

            // Nếu không có Solid hợp lệ, dùng BoundingBox
            var bbox = elem.get_BoundingBox(null);
            if (bbox != null)
            {
                return (bbox.Min + bbox.Max) / 2.0;
            }

            // Nếu là LocationCurve
            if (elem.Location is LocationCurve lc && lc.Curve != null)
            {
                return lc.Curve.Evaluate(0.5, true);
            }

            // Nếu là LocationPoint
            if (elem.Location is LocationPoint lp)
            {
                return lp.Point;
            }

            return null;
        }

        private ZoneVolume FindZoneContainingPoint(XYZ point, IReadOnlyList<ZoneVolume> zones)
        {
            // Để kiểm tra điểm có nằm trong Solid hay không một cách chính xác trong Revit API
            // Có thể dùng Face.Project hoặc thuật toán Ray-tracing.
            // Giải pháp nhanh gọn: Kiểm tra điểm có nằm trong BoundingBox của Zone không,
            // sau đó dùng SolidExtractor.IntersectionSolid giữa 1 khối hộp nhỏ quanh điểm và Zone Solid.

            foreach (var zone in zones)
            {
                var bbox = zone.Solid.GetBoundingBox();
                if (!TryGetWorldBounds(bbox, out var min, out var max)) continue;

                // Kiểm tra BoundingBox trước (nhanh hơn)
                if (point.X >= min.X && point.X <= max.X &&
                    point.Y >= min.Y && point.Y <= max.Y &&
                    point.Z >= min.Z && point.Z <= max.Z)
                {
                    // Tạo một hộp Solid cực nhỏ tại điểm đó để kiểm tra giao cắt
                    var smallBox = CreateSmallBox(point);
                    if (smallBox != null)
                    {
                        var intersection = SolidExtractor.IntersectionSolid(zone.Solid, smallBox);
                        if (intersection != null && intersection.Volume > 0)
                        {
                            return zone; // Đã tìm thấy Zone chứa điểm này
                        }
                    }
                }
            }

            return null;
        }

        private Solid CreateSmallBox(XYZ center)
        {
            double s = 0.1; // 0.1 feet
            var p0 = new XYZ(center.X - s, center.Y - s, center.Z - s);
            var p1 = new XYZ(center.X + s, center.Y + s, center.Z + s);

            var profile = new List<Curve>();
            profile.Add(Line.CreateBound(new XYZ(p0.X, p0.Y, p0.Z), new XYZ(p1.X, p0.Y, p0.Z)));
            profile.Add(Line.CreateBound(new XYZ(p1.X, p0.Y, p0.Z), new XYZ(p1.X, p1.Y, p0.Z)));
            profile.Add(Line.CreateBound(new XYZ(p1.X, p1.Y, p0.Z), new XYZ(p0.X, p1.Y, p0.Z)));
            profile.Add(Line.CreateBound(new XYZ(p0.X, p1.Y, p0.Z), new XYZ(p0.X, p0.Y, p0.Z)));

            var curveLoop = CurveLoop.Create(profile);
            var solidOptions = new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId);
            
            try 
            {
                return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { curveLoop }, XYZ.BasisZ, 2 * s, solidOptions);
            } 
            catch 
            {
                return null;
            }
        }

        private static void WriteStringParam(Element elem, string paramName, string value)
        {
            var parameter = GetStableSharedParameter(elem, paramName) ?? elem.LookupParameter(paramName);
            if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.String)
            {
                parameter.Set(value ?? string.Empty);
            }
        }

        private static Parameter GetStableSharedParameter(Element elem, string paramName)
        {
            if (elem == null || string.IsNullOrWhiteSpace(paramName)) return null;

            try
            {
                return elem.get_Parameter(ParameterSetupService.GetSharedParameterGuid(paramName));
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetWorldBounds(BoundingBoxXYZ bbox, out XYZ min, out XYZ max)
        {
            min = null;
            max = null;
            if (bbox == null) return false;

            var transform = bbox.Transform ?? Transform.Identity;
            var corners = new[]
            {
                new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Min.Z),
                new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Max.Z),
                new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z),
                new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Max.Z),
                new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z),
                new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Max.Z),
                new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Min.Z),
                new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Max.Z)
            }.Select(c => transform.OfPoint(c)).ToList();

            min = new XYZ(corners.Min(p => p.X), corners.Min(p => p.Y), corners.Min(p => p.Z));
            max = new XYZ(corners.Max(p => p.X), corners.Max(p => p.Y), corners.Max(p => p.Z));
            return true;
        }
    }
}
