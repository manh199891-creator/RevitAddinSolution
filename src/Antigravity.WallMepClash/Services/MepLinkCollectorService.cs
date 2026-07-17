using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.WallMepClash.Services
{
    public class MepLinkCollectorService
    {
        private readonly Document _hostDoc;

        public static readonly List<BuiltInCategory> MepCategories = new List<BuiltInCategory>
        {
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_PipeAccessory,
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_DuctFitting,
            BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_Conduit,
            BuiltInCategory.OST_ConduitFitting,
            BuiltInCategory.OST_CableTray,
            BuiltInCategory.OST_CableTrayFitting,
            BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_ElectricalEquipment,
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures,
            BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_FireAlarmDevices,
            BuiltInCategory.OST_DataDevices,
            BuiltInCategory.OST_CommunicationDevices
        };

        public MepLinkCollectorService(Document hostDoc)
        {
            _hostDoc = hostDoc;
        }

        // Lấy tất cả loaded links trong host
        public IList<RevitLinkInstance> GetLoadedLinks()
        {
            return new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(link => link.GetLinkDocument() != null)
                .OrderBy(link => link.Name)
                .ToList();
        }

        // Lấy categories có element trong link document, chỉ lọc các MEP categories
        public IList<BuiltInCategory> GetAvailableMepCategories(Document linkDoc)
        {
            if (linkDoc == null)
                return new List<BuiltInCategory>();

            var available = new List<BuiltInCategory>();

            foreach (var category in MepCategories)
            {
                var collector = new FilteredElementCollector(linkDoc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType();

                if (collector.Any())
                {
                    available.Add(category);
                }
            }

            return available;
        }

        // Collect MEP elements theo categories được chọn, trong link document
        public IList<Element> GetMepElements(Document linkDoc, IList<BuiltInCategory> categories)
        {
            if (linkDoc == null || categories == null || categories.Count == 0)
                return new List<Element>();

            // Sử dụng ElementMulticategoryFilter để tối ưu hóa việc query nhiều categories
            var filter = new ElementMulticategoryFilter(categories);

            return new FilteredElementCollector(linkDoc)
                .WherePasses(filter)
                .WhereElementIsNotElementType()
                .ToList();
        }
    }
}
