using Autodesk.Revit.DB;
using Antigravity.ZoneSplit.Models;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.ZoneSplit.Services
{
    public static class ZoneVolumeReader
    {
        public const string PARAM_MARK = "Mark";
        public const string PARAM_ZONE_ID = "BIM_ZoneID";

        public static IReadOnlyList<ZoneVolume> ReadFromDocument(Document doc)
        {
            var result = new List<ZoneVolume>();

            using (var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_GenericModel))
            {
                foreach (var elem in collector)
                {
                    // Ưu tiên đọc từ Mark, nếu trống thì đọc BIM_ZoneID
                    string zoneId = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
                    if (string.IsNullOrWhiteSpace(zoneId))
                    {
                        zoneId = elem.LookupParameter(PARAM_ZONE_ID)?.AsString();
                    }

                    if (string.IsNullOrWhiteSpace(zoneId)) continue;

                    // Lấy ZoneName từ Comments hoặc Type Name
                    string zoneName = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
                    if (string.IsNullOrWhiteSpace(zoneName))
                    {
                        var typeId = elem.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            var typeElem = doc.GetElement(typeId);
                            zoneName = typeElem?.Name;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(zoneName)) zoneName = zoneId;

                    var solid = SolidExtractor.GetUnionSolid(elem)
                             ?? SolidExtractor.GetLargestSolid(elem);

                    if (solid == null) continue;

                    result.Add(new ZoneVolume(elem.Id, zoneId, zoneName, solid));
                }
            }

            return result;
        }

        public static IReadOnlyList<string> ReadZoneIdsFromLinkedDocuments(Document hostDoc)
        {
            if (hostDoc == null) return new List<string>();

            var zoneIds = new HashSet<string>();
            var linkInstances = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>();

            foreach (var linkInstance in linkInstances)
            {
                var linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null) continue;

                foreach (var zoneId in ReadZoneIds(linkDoc))
                {
                    zoneIds.Add(zoneId);
                }
            }

            return zoneIds.OrderBy(x => x).ToList();
        }

        private static IEnumerable<string> ReadZoneIds(Document doc)
        {
            using (var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_GenericModel))
            {
                foreach (var elem in collector)
                {
                    string zoneId = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
                    if (string.IsNullOrWhiteSpace(zoneId))
                    {
                        zoneId = elem.LookupParameter(PARAM_ZONE_ID)?.AsString();
                    }

                    if (!string.IsNullOrWhiteSpace(zoneId))
                    {
                        yield return zoneId;
                    }
                }
            }
        }
    }
}
