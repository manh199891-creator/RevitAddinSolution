using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.AutoDimWalls.Core
{
    public static class HostStructuralResolver
    {
        private const double DirectionTolerance = 0.85;

        public static IList<LinkedFaceInfo> GetHostIntersectingFaces(
            Document doc, 
            View view, 
            StraightWallInfo wallInfo, 
            List<ElementId> excludeElementIds,
            List<string> logs)
        {
            var results = new List<LinkedFaceInfo>();
            double maxDistToLine = wallInfo.WidthFeet / 2.0 + 3.0; // Same buffer

            logs?.Add($"[HostStructuralResolver] Wall {wallInfo.Wall.Id.Value}: searching host elements. MaxDist={maxDistToLine:F2}ft");

            var catFilter = new ElementMulticategoryFilter(new[]
            {
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_Walls
            });

            // Find elements in host view (excluding the wall itself and collinear walls!)
            var hostElements = new FilteredElementCollector(doc, view.Id)
                .WherePasses(catFilter)
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(e => e.Id != wallInfo.Wall.Id && (excludeElementIds == null || !excludeElementIds.Contains(e.Id)))
                .ToList();

            logs?.Add($"[HostStructuralResolver] Found {hostElements.Count} candidate host elements in view (after exclusions)");

            foreach (Element elem in hostElements)
            {
                try
                {
                    var options = new Options
                    {
                        ComputeReferences = true,
                        IncludeNonVisibleObjects = false,
                        View = view
                    };
                    GeometryElement geom = elem.get_Geometry(options);
                    if (geom == null) continue;

                    int before = results.Count;
                    ProcessGeometry(geom, Transform.Identity, wallInfo, maxDistToLine, results, logs);

                    int added = results.Count - before;
                    if (added > 0)
                        logs?.Add($"    Host Elem {elem.Id.Value} ({elem.Category?.Name} '{elem.Name}'): +{added} faces");
                }
                catch (Exception ex)
                {
                    logs?.Add($"    Host Elem {elem.Id.Value}: ERROR {ex.Message}");
                }
            }

            return results;
        }

        private static void ProcessGeometry(
            GeometryElement geometry,
            Transform transform,
            StraightWallInfo info,
            double maxDistToLine,
            ICollection<LinkedFaceInfo> results,
            List<string> logs)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid && solid.Volume > 0)
                {
                    ProcessSolid(solid, transform, info, maxDistToLine, results, logs);
                }
                else if (obj is GeometryInstance subGeomInst)
                {
                    GeometryElement instGeom = subGeomInst.GetInstanceGeometry();
                    if (instGeom != null)
                    {
                        ProcessGeometry(instGeom, transform.Multiply(subGeomInst.Transform), info, maxDistToLine, results, logs);
                    }
                }
            }
        }

        private static void ProcessSolid(
            Solid solid,
            Transform transform,
            StraightWallInfo info,
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

                XYZ faceNormal = transform.OfVector(pf.FaceNormal);
                XYZ normalXY = new XYZ(faceNormal.X, faceNormal.Y, 0.0);

                if (normalXY.IsZeroLength())
                    continue;

                normalXY = normalXY.Normalize();

                double parallel = Math.Abs(normalXY.DotProduct(info.Direction));
                if (parallel < DirectionTolerance)
                    continue;

                BoundingBoxUV box = pf.GetBoundingBox();
                UV mid = new UV((box.Min.U + box.Max.U) * 0.5, (box.Min.V + box.Max.V) * 0.5);
                XYZ center = transform.OfPoint(pf.Evaluate(mid));

                XYZ vecToStart = center - info.Start;
                double projectedDist = vecToStart.DotProduct(info.Direction);
                double distToLine = Math.Abs(vecToStart.DotProduct(info.Normal));

                if (projectedDist >= -1.0 && projectedDist <= info.LocationLine.Length + 1.0 && distToLine < maxDistToLine)
                {
                    Reference hostRef = pf.Reference;
                    results.Add(new LinkedFaceInfo
                    {
                        Reference = hostRef,
                        DistanceFromStart = projectedDist,
                        Normal = normalXY
                    });
                }
            }
        }
    }
}
