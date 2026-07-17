using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DoorClearanceBox.Core
{
    public class ClashResult
    {
        public ElementId DoorId { get; set; }
        public string DoorMark { get; set; }
        public string DoorName { get; set; }
        public ElementId ClashingElementId { get; set; }
        public string ClashingCategory { get; set; }
        public string ClashingName { get; set; }
        public double ClashingVolumeM3 { get; set; }
        public DirectShape ClearanceShape { get; set; }
    }

    public static class ClashChecker
    {
        public static List<ClashResult> Scan(Document doc)
        {
            var results = new List<ClashResult>();
            var shapes = ReserveSpaceGeometry.GetAll(doc);

            if (shapes.Count == 0)
                return results;

            var categoryFilter = new ElementMulticategoryFilter(new[]
            {
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors
            });

            foreach (var shape in shapes)
            {
                try
                {
                    string mark = shape.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
                    if (string.IsNullOrEmpty(mark) || !mark.StartsWith(Constants.MarkPrefix, StringComparison.Ordinal))
                        continue;

                    string idText = mark.Substring(Constants.MarkPrefix.Length);
                    if (!long.TryParse(idText, out long parentValue))
                        continue;

                    var parentId = new ElementId(parentValue);
                    var parentElement = doc.GetElement(parentId);
                    if (parentElement == null)
                        continue;

                    var clearanceSolids = GetElementSolids(shape);
                    if (clearanceSolids.Count == 0)
                        continue;

                    string doorMark = parentElement.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()
                                   ?? parentElement.get_Parameter(BuiltInParameter.DOOR_NUMBER)?.AsString()
                                   ?? "N/A";
                    string doorName = parentElement.Name;
                    ElementId hostId = GetHostId(parentElement);

                    var clashIds = new HashSet<ElementId>();
                    foreach (var clearanceSolid in clearanceSolids)
                    {
                        var collector = new FilteredElementCollector(doc)
                            .WherePasses(categoryFilter)
                            .WherePasses(new ElementIntersectsSolidFilter(clearanceSolid))
                            .WhereElementIsNotElementType();

                        foreach (var clashingElement in collector)
                        {
                            if (ShouldSkip(clashingElement, parentId, hostId))
                                continue;

                            clashIds.Add(clashingElement.Id);
                        }
                    }

                    foreach (var clashId in clashIds)
                    {
                        Element clashingElement = doc.GetElement(clashId);
                        if (clashingElement == null)
                            continue;

                        double volumeM3 = CalculateIntersectionVolumeM3(clearanceSolids, GetElementSolids(clashingElement));

                        results.Add(new ClashResult
                        {
                            DoorId = parentId,
                            DoorMark = doorMark,
                            DoorName = doorName,
                            ClashingElementId = clashingElement.Id,
                            ClashingCategory = clashingElement.Category?.Name ?? "Structural",
                            ClashingName = clashingElement.Name,
                            ClashingVolumeM3 = Math.Round(volumeM3, 4),
                            ClearanceShape = shape
                        });
                    }
                }
                catch
                {
                    // Ignore transient geometry errors on a single clearance shape.
                }
            }

            return results;
        }

        private static ElementId GetHostId(Element element)
        {
            if (element is FamilyInstance fi && fi.Host != null)
                return fi.Host.Id;

            return ElementId.InvalidElementId;
        }

        private static bool ShouldSkip(Element element, ElementId parentId, ElementId hostId)
        {
            if (element == null || element is DirectShape)
                return true;

            if (element.Id == parentId)
                return true;

            return hostId != ElementId.InvalidElementId && element.Id == hostId;
        }

        private static double CalculateIntersectionVolumeM3(
            IReadOnlyCollection<Solid> clearanceSolids,
            IReadOnlyCollection<Solid> elementSolids)
        {
            double volume = 0.0;
            if (clearanceSolids.Count == 0 || elementSolids.Count == 0)
                return volume;

            foreach (var clearanceSolid in clearanceSolids)
            {
                foreach (var elementSolid in elementSolids)
                {
                    try
                    {
                        Solid intersect = BooleanOperationsUtils.ExecuteBooleanOperation(
                            clearanceSolid,
                            elementSolid,
                            BooleanOperationsType.Intersect);

                        if (intersect != null && intersect.Volume > 0)
                            volume += UnitUtils.ConvertFromInternalUnits(intersect.Volume, UnitTypeId.CubicMeters);
                    }
                    catch
                    {
                        // Boolean operations can fail on invalid Revit solids; keep the clash result.
                    }
                }
            }

            return volume;
        }

        private static List<Solid> GetElementSolids(Element element)
        {
            var solids = new List<Solid>();
            var options = new Options
            {
                DetailLevel = ViewDetailLevel.Medium,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geometry = element.get_Geometry(options);
            if (geometry == null)
                return solids;

            ExtractSolids(geometry, solids);
            return solids;
        }

        private static void ExtractSolids(GeometryElement geometry, ICollection<Solid> solids)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid && solid.Volume > 0)
                {
                    solids.Add(solid);
                    continue;
                }

                if (obj is GeometryInstance instance)
                    ExtractSolids(instance.GetInstanceGeometry(), solids);
            }
        }
    }
}
