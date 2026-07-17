using Autodesk.Revit.DB;
using Antigravity.HoanThien.Models;
using System.Linq;

namespace Antigravity.HoanThien.Services.Generation
{
    public interface IWallGenerationService
    {
        ElementId GenerateWall(Document doc, BoundarySurface surface, FinishLayerRule rule, Level roomLevel);
    }

    public class WallGenerationService : IWallGenerationService
    {
        public ElementId GenerateWall(Document doc, BoundarySurface surface, FinishLayerRule rule, Level roomLevel)
        {
            if (surface.Curve == null) return ElementId.InvalidElementId;

            // Find the wall type by name
            var wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(w => w.Name == rule.ElementTypeName);

            if (wallType == null) return ElementId.InvalidElementId;

            // Calculate offset (simple approximation here)
            // In a full implementation, we offset the curve inward by half core wall width + half finish wall width
            var offsetCurve = surface.Curve.CreateOffset(rule.Thickness / 304.8 / 2.0, XYZ.BasisZ); // Revit uses feet
            
            // Create the wall
            var wall = Wall.Create(doc, offsetCurve, wallType.Id, roomLevel.Id, 
                                   surface.ResolvedCeilingHeight > 0 ? surface.ResolvedCeilingHeight : 10.0, // Default 10ft if no ceiling
                                   0, false, false);
            
            // Apply top offset
            if (rule.TopConstraint == TopConstraintMode.CeilingPlusOffset)
            {
                var topOffsetParam = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET);
                if (topOffsetParam != null)
                {
                    topOffsetParam.Set(rule.TopOffset / 304.8); // mm to feet
                }
            }

            return wall.Id;
        }
    }
}
