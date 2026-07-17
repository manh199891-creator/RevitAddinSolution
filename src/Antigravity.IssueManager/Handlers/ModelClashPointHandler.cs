using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.IssueManager.Models;

namespace Antigravity.IssueManager.Handlers
{
    public class ModelClashPointHandler : IExternalEventHandler
    {
        public ViewpointModel Viewpoint { get; set; }

        public void Execute(UIApplication app)
        {
            if (Viewpoint == null || !Viewpoint.HasClashPoint) return;

            UIDocument uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;
            Document doc = uidoc.Document;

            try
            {
                using (Transaction tx = new Transaction(doc, "Model Clash Point"))
                {
                    tx.Start();

                    // Convert ClashPoint coordinates (Meters) to XYZ (Internal Units: Feet)
                    double toFeet = 3.2808399;
                    XYZ center = new XYZ(Viewpoint.ClashPointX * toFeet, Viewpoint.ClashPointY * toFeet, Viewpoint.ClashPointZ * toFeet);

                    // Create a sphere using DirectShape (Radius = 0.5 feet)
                    double radius = 0.5;
                    
                    Frame frame = new Frame(center, XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ);
                    Arc arc = Arc.Create(center - radius * XYZ.BasisZ, center + radius * XYZ.BasisZ, center + radius * XYZ.BasisX);
                    CurveLoop profile = new CurveLoop();
                    profile.Append(arc);
                    profile.Append(Line.CreateBound(center + radius * XYZ.BasisZ, center - radius * XYZ.BasisZ));

                    Solid sphere = GeometryCreationUtilities.CreateRevolvedGeometry(frame, new CurveLoop[] { profile }, 0, 2 * Math.PI);

                    DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                    ds.SetShape(new List<GeometryObject>() { sphere });
                    ds.Name = "Clash Point Marker";

                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModelClashPointHandler Error: {ex.Message}");
            }
        }

        public string GetName()
        {
            return "Model Clash Point Handler";
        }
    }
}
