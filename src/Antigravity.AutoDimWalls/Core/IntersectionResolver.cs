using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.AutoDimWalls.Models;

namespace Antigravity.AutoDimWalls.Core
{
    public static class IntersectionResolver
    {
        public static IList<ReferenceInfo> GetGridReferences(
            Document doc, 
            View view, 
            StraightWallInfo wallInfo, 
            bool includeLinks, 
            ElementId selectedLinkInstanceId,
            List<string> logs)
        {
            var references = new List<ReferenceInfo>();
            var wallCurve = wallInfo.LocationLine;

            // 1. Host Grids
            foreach (Grid grid in new FilteredElementCollector(doc, view.Id).OfClass(typeof(Grid)))
            {
                CheckAndAddGridReference(grid, wallCurve, null, references, logs);
            }

            // 2. Linked Grids
            if (includeLinks)
            {
                var linkInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                if (selectedLinkInstanceId != null && selectedLinkInstanceId != ElementId.InvalidElementId)
                {
                    linkInstances = linkInstances.Where(li => li.Id == selectedLinkInstanceId).ToList();
                }

                // Scan link instances in document (not view-scoped — Callout views don't contain links)
                foreach (RevitLinkInstance linkInstance in linkInstances)
                {
                    Document linkDoc = linkInstance.GetLinkDocument();
                    if (linkDoc == null) continue;

                    foreach (Grid grid in new FilteredElementCollector(linkDoc).OfClass(typeof(Grid)))
                    {
                        CheckAndAddGridReference(grid, wallCurve, linkInstance, references, logs);
                    }
                }
            }

            return references;
        }

        private static void CheckAndAddGridReference(Grid grid, Line wallCurve, RevitLinkInstance linkInstance, List<ReferenceInfo> references, List<string> logs)
        {
            try
            {
                Curve gridCurve = grid.Curve;
                if (gridCurve is Line gridLine)
                {
                    if (linkInstance != null)
                    {
                        gridCurve = gridCurve.CreateTransformed(linkInstance.GetTransform());
                        gridLine = gridCurve as Line;
                    }

                    XYZ p1Flat = new XYZ(wallCurve.GetEndPoint(0).X, wallCurve.GetEndPoint(0).Y, 0);
                    XYZ p2Flat = new XYZ(wallCurve.GetEndPoint(1).X, wallCurve.GetEndPoint(1).Y, 0);
                    Line flatWall = Line.CreateBound(p1Flat, p2Flat);

                    XYZ g1Flat = new XYZ(gridLine.GetEndPoint(0).X, gridLine.GetEndPoint(0).Y, 0);
                    XYZ g2Flat = new XYZ(gridLine.GetEndPoint(1).X, gridLine.GetEndPoint(1).Y, 0);
                    
                    if (g1Flat.DistanceTo(g2Flat) < 0.01) return;

                    Line flatGrid = Line.CreateBound(g1Flat, g2Flat);
                    flatGrid.MakeUnbound();

                    if (flatWall.Intersect(flatGrid, out IntersectionResultArray ir) == SetComparisonResult.Overlap)
                    {
                        XYZ ip = ir.get_Item(0).XYZPoint;
                        double d1 = ip.DistanceTo(p1Flat);
                        double d2 = ip.DistanceTo(p2Flat);
                        double len = p1Flat.DistanceTo(p2Flat);

                        if (d1 + d2 <= len + 0.01) // on segment
                        {
                            Reference r = null;
                            if (linkInstance != null)
                            {
                                r = new Reference(grid).CreateLinkReference(linkInstance);
                            }
                            else
                            {
                                r = new Reference(grid);
                            }

                            if (r != null)
                            {
                                references.Add(new ReferenceInfo { Reference = r, Parameter = d1 });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logs?.Add($"Grid {grid.Id.Value} skipped: {ex.Message}");
            }
        }

        public static IList<Reference> GetIntersectingWallReferences(Document doc, View view, Wall sourceWall, List<string> logs)
        {
            // Wall-to-wall chained dimensions need face references from the intersecting
            // wall at the exact crossing location. V1 keeps this hook explicit and safe.
            logs?.Add($"Wall {sourceWall.Id.Value}: Intersecting wall dimensions are not yet supported in V1.");
            _ = doc;
            _ = view;
            _ = sourceWall;
            return Array.Empty<Reference>();
        }
    }
}
