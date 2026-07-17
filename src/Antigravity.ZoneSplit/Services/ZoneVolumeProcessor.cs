using Autodesk.Revit.DB;
using Antigravity.ZoneSplit.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.ZoneSplit.Services
{
    public sealed class ZoneVolumeProcessor
    {
        private readonly Document _doc;
        private const double FT3_TO_M3 = 0.0283168;
        private const double FT_TO_M = 0.3048;
        private const double ProjectionToleranceFeet = 0.01;
        private const double DisplayVolumePrecisionM3 = 0.001;

        private static readonly BuiltInCategory[] TargetCategories =
        {
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Walls
        };

        public ZoneVolumeProcessor(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public ZoneProcessResult Process(
            IReadOnlyList<ZoneVolume> zones,
            ElementId levelId = null,
            List<BuiltInCategory> selectedCategories = null)
        {
            var result = new ZoneProcessResult();
            if (zones == null || zones.Count == 0) return result;

            var cats = selectedCategories != null && selectedCategories.Count > 0
                ? selectedCategories.Distinct().ToList()
                : TargetCategories.ToList();

            ParameterSetupService.EnsureDynamicZoneVolumeParameters(
                _doc,
                zones.Select(z => z.ZoneId),
                cats);

            var candidatesByZone = CollectCandidates(zones, levelId, cats);
            var allIds = candidatesByZone.Values.SelectMany(x => x).Distinct().ToList();
            result.TotalProcessed = allIds.Count;

            var solidCache = BuildSolidCache(allIds);
            result.SkippedNoSolid = allIds.Count - solidCache.Count;

            var bestZones = new Dictionary<ElementId, (ZoneVolume zone, double vol)>();
            var intersectionsByElement = new Dictionary<ElementId, List<ElementZoneIntersection>>();

            foreach (var zone in zones)
            {
                if (!candidatesByZone.TryGetValue(zone, out var candidates)) continue;

                foreach (var id in candidates)
                {
                    if (!solidCache.TryGetValue(id, out var elemSolid)) continue;

                    var elem = _doc.GetElement(id);

                    var interSolid = SolidExtractor.IntersectionSolid(elemSolid, zone.Solid);
                    if (interSolid == null)
                    {
                        result.Warnings.Add($"WARNING: Element {id} ({elem.Category?.Name}) intersects {zone.ZoneId} bounding box, but Revit Boolean operation failed to calculate exact volume.");
                        continue;
                    }

                    double vol = interSolid.Volume;
                    if (vol < 1e-9) continue;
                    var lengthM = ShouldCalculateLength(elem)
                        ? GetProjectedLength(elem, interSolid) * FT_TO_M
                        : 0;

                    result.Contributions.Add(new ZoneElementContribution
                    {
                        ElementId = id,
                        Category = elem.Category?.Name ?? "Unknown",
                        ZoneId = zone.ZoneId,
                        VolumeM3 = vol * FT3_TO_M3,
                        LengthM = lengthM
                    });

                    if (!intersectionsByElement.TryGetValue(id, out var intersections))
                    {
                        intersections = new List<ElementZoneIntersection>();
                        intersectionsByElement[id] = intersections;
                    }

                    var range = TryGetProjectionRange(elem, interSolid, out var start, out var end);
                    intersections.Add(new ElementZoneIntersection(zone, vol, range, start, end));

                    if (!bestZones.TryGetValue(id, out var current) || vol > current.vol)
                    {
                        bestZones[id] = (zone, vol);
                    }
                }
            }

            result.AssignedCount = bestZones.Count;
            result.MultiZoneCount = intersectionsByElement.Values.Count(x => x.Select(i => i.Zone.ZoneId).Distinct().Count() > 1);

            using (var tx = new Transaction(_doc, "ZoneSplit - Update BIM Volume Parameters"))
            {
                tx.Start();

                ResetBimVolumeParameters(CollectTargetElements(cats, levelId));

                foreach (var entry in bestZones)
                {
                    var elem = _doc.GetElement(entry.Key);
                    if (elem == null) continue;

                    var intersections = intersectionsByElement[entry.Key];

                    WriteStringParam(elem, ParameterSetupService.PARAM_ZONE_ID, entry.Value.zone.ZoneId);
                    WriteStringParam(elem, ParameterSetupService.PARAM_ZONE_NAME, entry.Value.zone.ZoneName);
                    WriteZoneVolumes(elem, intersections);
                }

                tx.Commit();
            }

            return result;
        }

        private void ResetBimVolumeParameters(IEnumerable<ElementId> elementIds)
        {
            foreach (var id in elementIds)
            {
                var elem = _doc.GetElement(id);
                if (elem == null) continue;

                WriteStringParam(elem, ParameterSetupService.PARAM_ZONE_ID, string.Empty);
                WriteStringParam(elem, ParameterSetupService.PARAM_ZONE_NAME, string.Empty);

                foreach (Parameter param in elem.Parameters)
                {
                    if (IsDynamicBimVolumeParameter(param))
                    {
                        ClearDoubleParam(param);
                    }
                }
            }
        }

        private static bool IsDynamicBimVolumeParameter(Parameter param)
        {
            return param?.Definition != null
                && param.Definition.Name.StartsWith(ParameterSetupService.PARAM_VOLUME_PREFIX, StringComparison.OrdinalIgnoreCase)
                && !param.Definition.Name.Equals(ParameterSetupService.PARAM_ZONE_ID, StringComparison.OrdinalIgnoreCase)
                && !param.Definition.Name.Equals(ParameterSetupService.PARAM_ZONE_NAME, StringComparison.OrdinalIgnoreCase)
                && !param.IsReadOnly
                && param.StorageType == StorageType.Double;
        }

        private static void WriteZoneVolumes(Element elem, List<ElementZoneIntersection> intersections)
        {
            foreach (var item in intersections
                .GroupBy(x => x.Zone.ZoneId)
                .Select(g => new
                {
                    ZoneId = g.Key,
                    VolumeFt3 = g.Sum(x => x.Volume),
                    VolumeM3 = Math.Round(g.Sum(x => x.Volume) * FT3_TO_M3, 3)
                })
                .Where(x => x.VolumeM3 >= DisplayVolumePrecisionM3))
            {
                WriteVolumeParam(elem, ParameterSetupService.GetVolumeParameterName(item.ZoneId), item.VolumeFt3, item.VolumeM3);
            }
        }

        private IEnumerable<ElementId> CollectTargetElements(List<BuiltInCategory> targetCats, ElementId levelId)
        {
            var filters = targetCats
                .Distinct()
                .Select(c => new ElementCategoryFilter(c))
                .Cast<ElementFilter>()
                .ToList();

            if (filters.Count == 0) return Enumerable.Empty<ElementId>();

            var collector = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .WherePasses(new LogicalOrFilter(filters));

            if (levelId != null && levelId != ElementId.InvalidElementId)
            {
                collector.WherePasses(new ElementLevelFilter(levelId));
            }

            return collector.ToElementIds();
        }

        private Dictionary<ElementId, Solid> BuildSolidCache(IEnumerable<ElementId> ids)
        {
            var solidCache = new Dictionary<ElementId, Solid>();
            foreach (var id in ids)
            {
                var elem = _doc.GetElement(id);
                var solid = SolidExtractor.GetUnionSolid(elem) ?? SolidExtractor.GetLargestSolid(elem);
                if (solid != null)
                {
                    solidCache[id] = solid;
                }
            }

            return solidCache;
        }

        private Dictionary<ZoneVolume, HashSet<ElementId>> CollectCandidates(
            IReadOnlyList<ZoneVolume> zones,
            ElementId levelId,
            List<BuiltInCategory> targetCats)
        {
            var map = new Dictionary<ZoneVolume, HashSet<ElementId>>();
            var filters = targetCats
                .Distinct()
                .Select(c => new ElementCategoryFilter(c))
                .Cast<ElementFilter>()
                .ToList();

            if (filters.Count == 0) return map;

            var catFilter = new LogicalOrFilter(filters);

            foreach (var zone in zones)
            {
                var zoneElem = _doc.GetElement(zone.SourceId);
                if (zoneElem == null)
                {
                    map[zone] = new HashSet<ElementId>();
                    continue;
                }

                var collector = new FilteredElementCollector(_doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(new ElementIntersectsElementFilter(zoneElem))
                    .WherePasses(catFilter);

                if (levelId != null && levelId != ElementId.InvalidElementId)
                {
                    collector.WherePasses(new ElementLevelFilter(levelId));
                }

                map[zone] = collector.ToElementIds().ToHashSet();
            }

            return map;
        }

        private static void WriteStringParam(Element elem, string paramName, string value)
        {
            var parameter = GetStableSharedParameter(elem, paramName) ?? elem.LookupParameter(paramName);
            if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.String)
            {
                parameter.Set(value ?? string.Empty);
            }
        }

        private static void WriteVolumeParam(Element elem, string paramName, double volumeFt3, double volumeM3)
        {
            var parameter = GetStableSharedParameter(elem, paramName) ?? elem.LookupParameter(paramName);
            if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.Double)
            {
                parameter.Set(IsVolumeParameter(parameter) ? volumeFt3 : volumeM3);
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

        private static bool IsVolumeParameter(Parameter parameter)
        {
            try
            {
                return parameter?.Definition?.GetDataType().Equals(SpecTypeId.Volume) == true;
            }
            catch
            {
                return false;
            }
        }

        private static void ClearDoubleParam(Parameter param)
        {
            if (param == null || param.IsReadOnly || param.StorageType != StorageType.Double) return;

            try
            {
                param.ClearValue();
            }
            catch
            {
                param.Set(0.0);
            }
        }

        private static bool ShouldCalculateLength(Element elem)
        {
            var categoryId = elem?.Category?.Id?.Value;
            return categoryId == (long)BuiltInCategory.OST_StructuralFraming
                || categoryId == (long)BuiltInCategory.OST_StructuralColumns;
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

        private static double GetProjectedLength(Element elem, Solid interSolid)
        {
            return TryGetProjectionRange(elem, interSolid, out var start, out var end)
                ? end - start
                : 0;
        }

        private static bool TryGetElementAxis(Element elem, out XYZ origin, out XYZ direction)
        {
            origin = XYZ.Zero;
            direction = XYZ.BasisZ;

            if (elem.Location is LocationCurve lc && lc.Curve != null)
            {
                var p0 = lc.Curve.GetEndPoint(0);
                var p1 = lc.Curve.GetEndPoint(1);
                var vector = p1 - p0;
                if (vector.GetLength() < 1e-6) return false;

                origin = p0;
                direction = vector.Normalize();
                return true;
            }

            if (elem.Location is LocationPoint lp)
            {
                origin = lp.Point;
                direction = XYZ.BasisZ;
                return true;
            }

            return false;
        }

        private sealed class ElementZoneIntersection
        {
            public ZoneVolume Zone { get; }
            public double Volume { get; }
            public bool HasProjectionRange { get; }
            public double Start { get; }
            public double End { get; }

            public ElementZoneIntersection(ZoneVolume zone, double volume, bool hasProjectionRange, double start, double end)
            {
                Zone = zone;
                Volume = volume;
                HasProjectionRange = hasProjectionRange;
                Start = start;
                End = end;
            }
        }
    }
}
