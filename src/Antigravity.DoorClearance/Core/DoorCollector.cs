using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace DoorClearanceBox.Core
{
    /// <summary>
    /// Layer 1 — DoorCollector
    /// Responsible for discovering FamilyInstance doors from a given scope (view or document),
    /// and for extracting their geometric dimensions via a prioritised parameter fallback chain.
    /// </summary>
    internal static class DoorCollector
    {
        // ── Collection ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns all valid base elements (Doors/Windows/Panels/CurtainWalls) visible in the specified view.
        /// </summary>
        public static IList<Element> CollectFromView(Document doc, View view, bool includeDoors, bool includeWindows, bool includePanels, bool includeCurtainWalls)
        {
            if (doc == null || view == null) return Array.Empty<Element>();

            var filter = CreateCategoryFilter(includeDoors, includeWindows, includePanels, includeCurtainWalls);
            if (filter == null) return Array.Empty<Element>();

            return new FilteredElementCollector(doc, view.Id)
                .WherePasses(filter)
                .WhereElementIsNotElementType()
                .Where(el => IsUsable(el, includeCurtainWalls))
                .ToList();
        }

        /// <summary>
        /// Returns all valid base elements in the entire document.
        /// </summary>
        public static IList<Element> CollectFromDocument(Document doc, bool includeDoors, bool includeWindows, bool includePanels, bool includeCurtainWalls)
        {
            if (doc == null) return Array.Empty<Element>();

            var filter = CreateCategoryFilter(includeDoors, includeWindows, includePanels, includeCurtainWalls);
            if (filter == null) return Array.Empty<Element>();

            return new FilteredElementCollector(doc)
                .WherePasses(filter)
                .WhereElementIsNotElementType()
                .Where(el => IsUsable(el, includeCurtainWalls))
                .ToList();
        }

        /// <summary>
        /// Returns valid base elements on specific levels.
        /// </summary>
        public static IList<Element> CollectFromLevels(Document doc, IEnumerable<Level> levels, bool includeDoors, bool includeWindows, bool includePanels, bool includeCurtainWalls)
        {
            if (doc == null || levels == null || !levels.Any()) return Array.Empty<Element>();

            var filter = CreateCategoryFilter(includeDoors, includeWindows, includePanels, includeCurtainWalls);
            if (filter == null) return Array.Empty<Element>();

            var levelFilters = levels.Select(l => new ElementLevelFilter(l.Id)).Cast<ElementFilter>().ToList();
            var levelFilter = new LogicalOrFilter(levelFilters);

            return new FilteredElementCollector(doc)
                .WherePasses(filter)
                .WherePasses(levelFilter)
                .WhereElementIsNotElementType()
                .Where(el => IsUsable(el, includeCurtainWalls))
                .ToList();
        }

        /// <summary>
        /// Returns valid base elements from a Revit link, optionally filtered by levels.
        /// </summary>
        public static IList<Element> CollectFromLink(RevitLinkInstance linkInstance, IEnumerable<Level> levels, bool includeDoors, bool includeWindows, bool includePanels, bool includeCurtainWalls)
        {
            if (linkInstance == null) return Array.Empty<Element>();

            Document linkDoc = linkInstance.GetLinkDocument();
            if (linkDoc == null) return Array.Empty<Element>();

            var filter = CreateCategoryFilter(includeDoors, includeWindows, includePanels, includeCurtainWalls);
            if (filter == null) return Array.Empty<Element>();

            var collector = new FilteredElementCollector(linkDoc).WherePasses(filter);

            if (levels != null && levels.Any())
            {
                var levelFilters = levels.Select(l => new ElementLevelFilter(l.Id)).Cast<ElementFilter>().ToList();
                collector.WherePasses(new LogicalOrFilter(levelFilters));
            }

            return collector
                .WhereElementIsNotElementType()
                .Where(el => IsUsable(el, includeCurtainWalls))
                .ToList();
        }

        private static ElementMulticategoryFilter CreateCategoryFilter(bool includeDoors, bool includeWindows, bool includePanels, bool includeCurtainWalls)
        {
            var categories = new List<BuiltInCategory>();
            if (includeDoors)   categories.Add(BuiltInCategory.OST_Doors);
            if (includeWindows) categories.Add(BuiltInCategory.OST_Windows);
            if (includePanels)  categories.Add(BuiltInCategory.OST_CurtainWallPanels);
            if (includeCurtainWalls) categories.Add(BuiltInCategory.OST_Walls);

            return (categories.Count > 0) ? new ElementMulticategoryFilter(categories) : null;
        }

        // ── Dimension Extraction ────────────────────────────────────────────────

        /// <summary>
        /// Retrieves door width in Revit internal units (decimal feet).
        /// Priority chain:
        ///   1. Symbol FAMILY_WIDTH_PARAM  (type-level, most reliable)
        ///   2. Instance DOOR_WIDTH        (less common instance override)
        ///   3. Symbol parameter named "Width"
        ///   4. Instance parameter named "Width"
        ///   5. Hard-coded default (900 mm)
        /// </summary>
        public static double GetDoorWidth(Element element)
        {
            if (element == null) return 0;

            double v;

            // Curtain Wall "Length"
            v = GetBuiltInDouble(element, BuiltInParameter.CURVE_ELEM_LENGTH);
            if (v > 0) return v;

            if (element is FamilyInstance fi)
            {
                v = GetBuiltInDouble(fi.Symbol, BuiltInParameter.FAMILY_WIDTH_PARAM);
                if (v > 0) return v;

                v = GetBuiltInDouble(fi, BuiltInParameter.DOOR_WIDTH);
                if (v > 0) return v;

                v = LookupDouble(fi.Symbol, "Width");
                if (v > 0) return v;

                v = LookupDouble(fi, "Width");
                if (v > 0) return v;

                // Curtain Panel specific parameters
                v = GetBuiltInDouble(fi, BuiltInParameter.WINDOW_WIDTH);
                if (v > 0) return v;
            }

            return UnitUtils.ConvertToInternalUnits(Constants.DefaultWidthMm, UnitTypeId.Millimeters);
        }

        /// <summary>
        /// Retrieves element height in Revit internal units (decimal feet).
        /// Same priority chain as GetDoorWidth.
        /// </summary>
        public static double GetDoorHeight(Element element)
        {
            if (element == null) return 0;

            double v;

            // Curtain Wall "Unconnected Height"
            v = GetBuiltInDouble(element, BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            if (v > 0) return v;

            if (element is FamilyInstance fi)
            {
                v = GetBuiltInDouble(fi.Symbol, BuiltInParameter.FAMILY_HEIGHT_PARAM);
                if (v > 0) return v;

                v = GetBuiltInDouble(fi, BuiltInParameter.DOOR_HEIGHT);
                if (v > 0) return v;

                v = LookupDouble(fi.Symbol, "Height");
                if (v > 0) return v;

                v = LookupDouble(fi, "Height");
                if (v > 0) return v;

                // Curtain Panel specific parameters
                v = GetBuiltInDouble(fi, BuiltInParameter.WINDOW_HEIGHT);
                if (v > 0) return v;
            }

            return UnitUtils.ConvertToInternalUnits(Constants.DefaultHeightMm, UnitTypeId.Millimeters);
        }

        // ── Validation ──────────────────────────────────────────────────────────

        /// <summary>
        /// An element is usable only when:
        ///   • it is a Door/Window/Panel with orientation
        ///   • it is a Curtain Wall
        /// </summary>
        private static bool IsUsable(Element el, bool includeCurtainWalls)
        {
            if (el == null) return false;

            if (el is FamilyInstance fi)
            {
                // If it's a curtain panel or a wall-hosted door, it's generally usable
                // We'll trust the category filter more and the host check less
                return fi.Location is LocationPoint || fi.get_BoundingBox(null) != null;
            }

            if (includeCurtainWalls && el is Wall w)
            {
                return w.WallType.Kind == WallKind.Curtain;
            }

            return false;
        }

        // ── Private Helpers ──────────────────────────────────────────────────────

        private static double GetBuiltInDouble(Element element, BuiltInParameter bip)
        {
            if (element == null) return 0;
            var p = element.get_Parameter(bip);
            return (p?.StorageType == StorageType.Double) ? p.AsDouble() : 0;
        }

        private static double LookupDouble(Element element, string name)
        {
            if (element == null) return 0;
            var p = element.LookupParameter(name);
            return (p?.StorageType == StorageType.Double) ? p.AsDouble() : 0;
        }
    }
}
