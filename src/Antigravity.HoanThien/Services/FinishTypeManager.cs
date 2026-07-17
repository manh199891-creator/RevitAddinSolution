using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.HoanThien.Services
{
    public class FinishTypeManager
    {
        public static WallType GetOrCreateWallType(Document doc, string typeName, double thicknessMm, MaterialFunctionAssignment materialFunction)
        {
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            var defaultType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Kind == WallKind.Basic);

            if (defaultType == null)
                throw new InvalidOperationException("No basic wall type found to duplicate.");

            WallType newType = defaultType.Duplicate(typeName) as WallType;
            CompoundStructure cs = newType.GetCompoundStructure();
            if (cs != null)
            {
                double thicknessFt = UnitUtils.ConvertToInternalUnits(thicknessMm, UnitTypeId.Millimeters);
                if (thicknessMm < 5) 
                {
                    // Revit might reject < 5mm or < 2mm. Ensure minimum valid thickness for structure
                    thicknessFt = UnitUtils.ConvertToInternalUnits(5.0, UnitTypeId.Millimeters);
                }
                
                // Clear existing layers
                var layers = cs.GetLayers();
                cs.SetLayerFunction(0, materialFunction);
                cs.SetLayerWidth(0, thicknessFt);
                
                // Keep only one layer
                while (cs.LayerCount > 1)
                {
                    cs.DeleteLayer(1);
                }

                newType.SetCompoundStructure(cs);
            }
            return newType;
        }

        public static FloorType GetOrCreateFloorType(Document doc, string typeName, double thicknessMm)
        {
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault(ft => ft.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            var defaultType = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault();

            if (defaultType == null)
                throw new InvalidOperationException("No floor type found to duplicate.");

            FloorType newType = defaultType.Duplicate(typeName) as FloorType;
            CompoundStructure cs = newType.GetCompoundStructure();
            if (cs != null)
            {
                double thicknessFt = UnitUtils.ConvertToInternalUnits(thicknessMm, UnitTypeId.Millimeters);
                if (thicknessMm < 5)
                {
                    thicknessFt = UnitUtils.ConvertToInternalUnits(5.0, UnitTypeId.Millimeters);
                }
                
                cs.SetLayerWidth(0, thicknessFt);
                while (cs.LayerCount > 1)
                {
                    cs.DeleteLayer(1);
                }

                newType.SetCompoundStructure(cs);
            }
            return newType;
        }
    }
}
