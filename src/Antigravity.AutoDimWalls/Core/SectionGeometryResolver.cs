using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.AutoDimWalls.Models;

namespace Antigravity.AutoDimWalls.Core
{
    public static class SectionGeometryResolver
    {
        public static IList<ReferenceInfo> GetVerticalReferences(
            Wall wall,
            View activeView,
            List<string> logs)
        {
            var results = new List<ReferenceInfo>();

            var options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true
            };

            GeometryElement geometry = wall.get_Geometry(options);
            if (geometry == null)
            {
                logs?.Add($"Wall {wall.Id.Value} has no geometry in active section view.");
                return results;
            }

            CollectHorizontalFaces(geometry, Transform.Identity, results, logs);

            logs?.Add($"[SectionGeometryResolver] Wall {wall.Id.Value}: Found {results.Count} vertical horizontal-face candidates.");
            return results;
        }

        public static IList<ReferenceInfo> GetIntersectingBeamReferences(
            Document doc,
            View view,
            StraightWallInfo wallInfo,
            List<string> logs)
        {
            var results = new List<ReferenceInfo>();
            BoundingBoxXYZ wallBBox = wallInfo.Wall.get_BoundingBox(view);
            if (wallBBox == null)
                return results;

            var outline = new Outline(wallBBox.Min, wallBBox.Max);
            var bboxFilter = new BoundingBoxIntersectsFilter(outline);

            var beamFilter = new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming);
            var logicalFilter = new LogicalAndFilter(beamFilter, bboxFilter);

            var intersectingBeams = new FilteredElementCollector(doc, view.Id)
                .WherePasses(logicalFilter)
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .ToList();

            logs?.Add($"[SectionGeometryResolver] Found {intersectingBeams.Count} intersecting beams in view for Wall {wallInfo.Wall.Id.Value}");

            foreach (FamilyInstance beam in intersectingBeams)
            {
                try
                {
                    var options = new Options
                    {
                        ComputeReferences = true,
                        IncludeNonVisibleObjects = false
                    };
                    GeometryElement geom = beam.get_Geometry(options);
                    if (geom == null) continue;

                    CollectHorizontalFaces(geom, Transform.Identity, results, logs);
                }
                catch (Exception ex)
                {
                    logs?.Add($"    Beam {beam.Id.Value}: ERROR {ex.Message}");
                }
            }

            return results;
        }

        public static IList<ReferenceInfo> GetIntersectingFloorReferences(
            Document doc,
            View view,
            StraightWallInfo wallInfo,
            List<string> logs)
        {
            var results = new List<ReferenceInfo>();
            BoundingBoxXYZ wallBBox = wallInfo.Wall.get_BoundingBox(view);
            if (wallBBox == null)
                return results;

            var outline = new Outline(wallBBox.Min, wallBBox.Max);
            var bboxFilter = new BoundingBoxIntersectsFilter(outline);

            var floorFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
            var logicalFilter = new LogicalAndFilter(floorFilter, bboxFilter);

            var intersectingFloors = new FilteredElementCollector(doc, view.Id)
                .WherePasses(logicalFilter)
                .WhereElementIsNotElementType()
                .OfType<Floor>()
                .ToList();

            logs?.Add($"[SectionGeometryResolver] Found {intersectingFloors.Count} intersecting floors in view for Wall {wallInfo.Wall.Id.Value}");

            foreach (Floor floor in intersectingFloors)
            {
                try
                {
                    var options = new Options
                    {
                        ComputeReferences = true,
                        IncludeNonVisibleObjects = false
                    };
                    GeometryElement geom = floor.get_Geometry(options);
                    if (geom == null) continue;

                    CollectHorizontalFaces(geom, Transform.Identity, results, logs);
                }
                catch (Exception ex)
                {
                    logs?.Add($"    Floor {floor.Id.Value}: ERROR {ex.Message}");
                }
            }

            return results;
        }

        private static void CollectHorizontalFaces(
            GeometryElement geometry,
            Transform transform,
            ICollection<ReferenceInfo> results,
            List<string> logs)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid && solid.Volume > 0.0)
                {
                    ProcessSolid(solid, transform, results, logs);
                }
                else if (obj is GeometryInstance geomInst)
                {
                    GeometryElement instGeom = geomInst.GetInstanceGeometry();
                    if (instGeom != null)
                    {
                        CollectHorizontalFaces(instGeom, transform.Multiply(geomInst.Transform), results, logs);
                    }
                }
            }
        }

        private static void ProcessSolid(
            Solid solid,
            Transform transform,
            ICollection<ReferenceInfo> results,
            List<string> logs)
        {
            foreach (Face face in solid.Faces)
            {
                if (!(face is PlanarFace pf) || pf.Reference == null)
                    continue;

                XYZ faceNormal = transform.OfVector(pf.FaceNormal);

                // A face is horizontal if normal is vertical (parallel to Z-axis)
                if (Math.Abs(faceNormal.Z) > 0.99)
                {
                    BoundingBoxUV box = pf.GetBoundingBox();
                    UV mid = new UV((box.Min.U + box.Max.U) * 0.5, (box.Min.V + box.Max.V) * 0.5);
                    XYZ center = transform.OfPoint(pf.Evaluate(mid));

                    results.Add(new ReferenceInfo
                    {
                        Reference = pf.Reference,
                        Parameter = center.Z
                    });
                }
            }
        }
    }
}
