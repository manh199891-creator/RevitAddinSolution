using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Ink;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Antigravity.CheckFloorElevation.Services
{
    public class Pen3DService
    {
        private readonly Document _doc;
        private readonly UIDocument _uiDoc;

        public Pen3DService(Document doc, UIDocument uiDoc)
        {
            _doc = doc;
            _uiDoc = uiDoc;
        }

        public void Create3DStrokes(StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0) return;

            var view = _uiDoc.ActiveView as View3D;
            if (view == null)
            {
                throw new InvalidOperationException("Active view must be a 3D view.");
            }

            var activeViews = _uiDoc.GetOpenUIViews();
            var currentUIView = activeViews.FirstOrDefault(v => v.ViewId == view.Id);
            if (currentUIView == null) return;

            // Get screen bounds
            var rect = currentUIView.GetWindowRectangle();
            double screenWidth = rect.Right - rect.Left;
            double screenHeight = rect.Bottom - rect.Top;

            if (screenWidth <= 0 || screenHeight <= 0) return;

            // Get 3D corners of the view projection
            IList<XYZ> corners = currentUIView.GetZoomCorners();
            if (corners == null || corners.Count < 2) return;

            XYZ pBottomLeft = corners[0];
            XYZ pTopRight = corners[1];

            // Calculate width and height vectors on the projection plane
            XYZ rightDir = view.RightDirection;
            XYZ upDir = view.UpDirection;

            double width3D = (pTopRight - pBottomLeft).DotProduct(rightDir);
            double height3D = (pTopRight - pBottomLeft).DotProduct(upDir);

            XYZ wVec = rightDir * width3D;
            XYZ hVec = upDir * height3D;

            XYZ pTopLeft = pBottomLeft + hVec;

            List<Curve> allCurves = new List<Curve>();

            foreach (Stroke stroke in strokes)
            {
                StylusPointCollection pts = stroke.StylusPoints;
                if (pts.Count < 2) continue;

                List<XYZ> raw3DPts = new List<XYZ>();
                foreach (StylusPoint pt in pts)
                {
                    double u = pt.X / screenWidth;
                    double v = pt.Y / screenHeight;

                    XYZ pt3D = pTopLeft + (wVec * u) - (hVec * v);
                    raw3DPts.Add(pt3D);
                }

                // Filter points to ensure they are far enough apart for Revit's geometry engine
                List<XYZ> filteredPts = FilterPoints(raw3DPts, 0.1);

                if (filteredPts.Count == 2)
                {
                    allCurves.Add(Line.CreateBound(filteredPts[0], filteredPts[1]));
                }
                else if (filteredPts.Count > 2)
                {
                    try
                    {
                        allCurves.Add(HermiteSpline.Create(filteredPts, false));
                    }
                    catch
                    {
                        // Fallback to line segments if spline fails
                        for (int i = 0; i < filteredPts.Count - 1; i++)
                        {
                            allCurves.Add(Line.CreateBound(filteredPts[i], filteredPts[i + 1]));
                        }
                    }
                }
            }

            if (allCurves.Count > 0)
            {
                using (Transaction t = new Transaction(_doc, "Create 3D Pen Markup"))
                {
                    t.Start();
                    
                    // Use DirectShape to group all strokes into a single element
                    DirectShape ds = DirectShape.CreateElement(_doc, new ElementId(BuiltInCategory.OST_GenericModel));
                    ds.ApplicationId = "AutoFloorCheck";
                    ds.ApplicationDataId = "3DPenMarkup_" + Guid.NewGuid().ToString();
                    ds.Name = "3D Pen Markup";
                    
                    List<GeometryObject> shapeList = new List<GeometryObject>();
                    foreach(var curve in allCurves)
                    {
                        shapeList.Add(curve);
                    }
                    
                    ds.SetShape(shapeList);
                    
                    // Apply color override based on the first stroke's color
                    var firstStrokeColor = strokes[0].DrawingAttributes.Color;
                    var revitColor = new Autodesk.Revit.DB.Color(firstStrokeColor.R, firstStrokeColor.G, firstStrokeColor.B);
                    
                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                    ogs.SetProjectionLineColor(revitColor);
                    ogs.SetProjectionLineWeight(6);
                    
                    view.SetElementOverrides(ds.Id, ogs);
                    
                    t.Commit();
                }
            }
        }

        private List<XYZ> FilterPoints(List<XYZ> points, double minDistance)
        {
            List<XYZ> filtered = new List<XYZ>();
            if (points.Count == 0) return filtered;

            filtered.Add(points[0]);
            for (int i = 1; i < points.Count; i++)
            {
                if (filtered.Last().DistanceTo(points[i]) >= minDistance)
                {
                    filtered.Add(points[i]);
                }
            }
            
            if (filtered.Count > 1 && filtered.Last().DistanceTo(points.Last()) > minDistance * 0.5)
            {
                if (filtered.Last().DistanceTo(points.Last()) >= 0.001)
                {
                    filtered.Add(points.Last());
                }
            }

            return filtered;
        }
    }
}
