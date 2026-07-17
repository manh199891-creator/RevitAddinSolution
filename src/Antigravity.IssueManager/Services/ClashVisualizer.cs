using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.IssueManager.Models;

namespace Antigravity.IssueManager.Services
{
    public class ClashVisualizer
    {
        public static void CreateClashBox(UIApplication uiApp, ViewpointModel viewpoint)
        {
            if (viewpoint == null) return;

            UIDocument uidoc = uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (doc.ActiveView is View3D view3d)
            {
                using (Transaction tx = new Transaction(doc, "Create Clash Box"))
                {
                    tx.Start();

                    // Convert ClashPoint coordinates (assuming feet from XML for this example)
                    XYZ clashPoint = new XYZ(viewpoint.ClashPointX, viewpoint.ClashPointY, viewpoint.ClashPointZ);
                    
                    // Create a bounding box around the clash point (e.g. 5x5x5 feet)
                    double offset = 2.5; 
                    BoundingBoxXYZ bbox = new BoundingBoxXYZ();
                    bbox.Min = new XYZ(clashPoint.X - offset, clashPoint.Y - offset, clashPoint.Z - offset);
                    bbox.Max = new XYZ(clashPoint.X + offset, clashPoint.Y + offset, clashPoint.Z + offset);

                    view3d.SetSectionBox(bbox);
                    
                    tx.Commit();
                }
            }
            else
            {
                TaskDialog.Show("Clash Box", "Please open a 3D view to create a Section Box.");
            }
        }
    }
}
