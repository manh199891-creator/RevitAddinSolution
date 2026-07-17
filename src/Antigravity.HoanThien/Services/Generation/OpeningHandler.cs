using Autodesk.Revit.DB;
using System.Linq;

namespace Antigravity.HoanThien.Services.Generation
{
    public interface IOpeningHandler
    {
        void CutOpenings(Document doc, ElementId finishWallId, ElementId hostWallId);
    }

    public class OpeningHandler : IOpeningHandler
    {
        public void CutOpenings(Document doc, ElementId finishWallId, ElementId hostWallId)
        {
            var hostWall = doc.GetElement(hostWallId) as Wall;
            var finishWall = doc.GetElement(finishWallId) as Wall;

            if (hostWall == null || finishWall == null) return;

            // Find doors/windows hosted by the core wall
            var hostedElements = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(f => f.Host != null && f.Host.Id == hostWall.Id)
                .ToList();

            foreach (var instance in hostedElements)
            {
                // In Revit, a wall opening created by a family instance (door/window)
                // usually automatically cuts the joined finish wall if they are properly joined.
                // However, for explicit cutting, we can try to copy the opening or use BooleanOperationsUtils.
                
                // For this phase, we rely on JoinGeometry. If that's not enough, 
                // we would use BooleanOperationsUtils.ExecuteBooleanOperation to explicitly subtract the bounding box.
                
                // Placeholder for explicit cut logic if JoinGeometry isn't sufficient:
                // Create a void matching the instance's geometry and cut the finish wall.
            }
        }
    }
}
