using Autodesk.Revit.DB;
using Antigravity.HoanThien.Models;

namespace Antigravity.HoanThien.Services.Analysis
{
    public interface ISubstrateClassifier
    {
        SubstrateClass Classify(Element element);
    }

    public class SubstrateClassifier : ISubstrateClassifier
    {
        public SubstrateClass Classify(Element element)
        {
            if (element is Wall wall)
            {
                if (wall.WallType.Kind == WallKind.Curtain)
                {
                    return SubstrateClass.Glazing;
                }

                // Simplified logic: Check wall function or name
                string typeName = wall.WallType.Name.ToLower();
                if (typeName.Contains("concrete") || typeName.Contains("bê tông"))
                    return SubstrateClass.Concrete;
                if (typeName.Contains("gypsum") || typeName.Contains("thạch cao"))
                    return SubstrateClass.Gypsum;
                
                return SubstrateClass.Masonry; // Default
            }

            if (element is FamilyInstance column && column.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns)
            {
                return SubstrateClass.Column;
            }

            return SubstrateClass.Unknown;
        }
    }
}
