using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.HoanThien.Models;

namespace Antigravity.HoanThien.Services.Analysis
{
    public interface ITopConstraintResolver
    {
        void ResolveCeiling(BoundarySurface surface, Document doc);
    }

    public class TopConstraintResolver : ITopConstraintResolver
    {
        public void ResolveCeiling(BoundarySurface surface, Document doc)
        {
            if (surface.Curve == null) return;
            
            // Get midpoint of the boundary curve
            var midPoint = surface.Curve.Evaluate(0.5, true);
            
            // Offset slightly inside the room to ensure we shoot up from the room interior
            // In a real scenario, we'd calculate the room normal. 
            // For now, we shoot straight up from the curve midpoint + offset Z
            var startPoint = new XYZ(midPoint.X, midPoint.Y, midPoint.Z + 1.0);
            var direction = XYZ.BasisZ;

            // Find ceilings
            var ceilingFilter = new ElementCategoryFilter(BuiltInCategory.OST_Ceilings);
            
            // Need a 3D view to run ReferenceIntersector
            var view3D = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate);

            if (view3D == null) return;

            var intersector = new ReferenceIntersector(ceilingFilter, FindReferenceTarget.Element, view3D);
            var context = intersector.FindNearest(startPoint, direction);

            if (context != null)
            {
                var refPoint = context.GetReference().GlobalPoint;
                surface.ResolvedCeilingHeight = refPoint.Z;
                surface.CeilingElementId = context.GetReference().ElementId;
            }
        }
    }
}
