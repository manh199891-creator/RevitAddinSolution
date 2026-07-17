using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.AutoDimWalls.Core
{
    public class LinkedFaceInfo
    {
        public Reference Reference { get; set; }
        public double DistanceFromStart { get; set; }
        public XYZ Normal { get; set; }
    }

    public static class LinkResolver
    {
        private const double DirectionTolerance = 0.85; // Relaxed from 0.985 to catch slightly rotated columns

        public static IList<LinkedFaceInfo> GetLinkedIntersectingFaces(Document doc, View view, StraightWallInfo wallInfo, ElementId selectedLinkInstanceId, List<string> logs)
        {
            var results = new List<LinkedFaceInfo>();
            // Max perpendicular distance: half wall width + 3 feet buffer for column overlap
            double maxDistToLine = wallInfo.WidthFeet / 2.0 + 3.0;

            logs?.Add($"[LinkResolver] Wall {wallInfo.Wall.Id.Value}: Dir=({wallInfo.Direction.X:F2},{wallInfo.Direction.Y:F2}), Len={wallInfo.LocationLine.Length:F2}ft, W={wallInfo.WidthFeet:F2}ft, MaxDist={maxDistToLine:F2}ft");

            // Scan ALL link instances in document (not view-scoped — Callout views don't contain links)
            var linkInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            if (selectedLinkInstanceId != null && selectedLinkInstanceId != ElementId.InvalidElementId)
            {
                linkInstances = linkInstances.Where(li => li.Id == selectedLinkInstanceId).ToList();
                logs?.Add($"[LinkResolver] Đã lọc theo Link Instance ID={selectedLinkInstanceId.Value}");
            }

            logs?.Add($"[LinkResolver] {linkInstances.Count} link instances in view");

            foreach (RevitLinkInstance linkInstance in linkInstances)
            {
                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null)
                {
                    logs?.Add($"  Link '{linkInstance.Name}': null doc, skip");
                    continue;
                }

                Transform linkTransform = linkInstance.GetTransform();

                var catFilter = new ElementMulticategoryFilter(new[]
                {
                    BuiltInCategory.OST_StructuralColumns,
                    BuiltInCategory.OST_Walls
                });

                var linkedElements = new FilteredElementCollector(linkDoc)
                    .WherePasses(catFilter)
                    .WhereElementIsNotElementType()
                    .ToElements();

                logs?.Add($"  Link '{linkInstance.Name}': {linkedElements.Count} elements");

                foreach (Element elem in linkedElements)
                {
                    try
                    {
                        var options = new Options
                        {
                            ComputeReferences = true,
                            IncludeNonVisibleObjects = false
                        };
                        GeometryElement geom = elem.get_Geometry(options);
                        if (geom == null) continue;

                        int before = results.Count;
                        ProcessGeometry(geom, linkTransform, wallInfo, linkInstance, maxDistToLine, results, logs);

                        int added = results.Count - before;
                        if (added > 0)
                            logs?.Add($"    Elem {elem.Id.Value} ({elem.Category?.Name} '{elem.Name}'): +{added} faces");
                    }
                    catch (Exception ex)
                    {
                        logs?.Add($"    Elem {elem.Id.Value}: ERROR {ex.Message}");
                    }
                }
            }

            logs?.Add($"[LinkResolver] Total: {results.Count} face references");
            return results;
        }

        private static void ProcessGeometry(
            GeometryElement geometry,
            Transform currentTransform,
            StraightWallInfo info,
            RevitLinkInstance linkInstance,
            double maxDistToLine,
            ICollection<LinkedFaceInfo> results,
            List<string> logs)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid && solid.Volume > 0)
                {
                    ProcessSolid(solid, currentTransform, info, linkInstance, maxDistToLine, results, logs);
                }
                else if (obj is GeometryInstance subGeomInst)
                {
                    GeometryElement symbolGeom = subGeomInst.GetSymbolGeometry();
                    if (symbolGeom != null)
                    {
                        Transform combinedTransform = currentTransform.Multiply(subGeomInst.Transform);
                        ProcessGeometry(symbolGeom, combinedTransform, info, linkInstance, maxDistToLine, results, logs);
                    }
                }
            }
        }

        private static void ProcessSolid(
            Solid solid,
            Transform transform,
            StraightWallInfo info,
            RevitLinkInstance linkInstance,
            double maxDistToLine,
            ICollection<LinkedFaceInfo> results,
            List<string> logs)
        {
            foreach (Face face in solid.Faces)
            {
                if (!(face is PlanarFace pf))
                    continue;

                if (pf.Reference == null)
                    continue;

                // Transform face normal from symbol space to host space
                XYZ faceNormalInHost = transform.OfVector(pf.FaceNormal);
                XYZ normalXY = new XYZ(faceNormalInHost.X, faceNormalInHost.Y, 0.0);

                if (normalXY.IsZeroLength())
                    continue;

                normalXY = normalXY.Normalize();

                // We want faces whose normal is PARALLEL to wall direction
                double parallel = Math.Abs(normalXY.DotProduct(info.Direction));
                if (parallel < DirectionTolerance)
                    continue;

                // Get face center in host coordinates
                BoundingBoxUV box = pf.GetBoundingBox();
                UV mid = new UV((box.Min.U + box.Max.U) * 0.5, (box.Min.V + box.Max.V) * 0.5);
                XYZ centerInSymbol = pf.Evaluate(mid);
                XYZ centerInHost = transform.OfPoint(centerInSymbol);

                // Project onto wall line
                XYZ vecToStart = centerInHost - info.Start;
                double projectedDist = vecToStart.DotProduct(info.Direction);
                double distToLine = Math.Abs(vecToStart.DotProduct(info.Normal));

                // Check: face center must be within wall segment (with 1ft tolerance at ends)
                if (projectedDist >= -1.0 && projectedDist <= info.LocationLine.Length + 1.0 && distToLine < maxDistToLine)
                {
                    try
                    {
                        Reference hostRef = pf.Reference.CreateLinkReference(linkInstance);
                        if (hostRef != null)
                        {
                            results.Add(new LinkedFaceInfo
                            {
                                Reference = hostRef,
                                DistanceFromStart = projectedDist,
                                Normal = normalXY
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        logs?.Add($"      CreateLinkRef failed: {ex.Message}");
                    }
                }
            }
        }
    }
}
