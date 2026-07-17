using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.AutoDimWalls.Core
{
    public sealed class StraightWallInfo
    {
        public Wall Wall { get; set; }
        public Line LocationLine { get; set; }
        public XYZ Direction { get; set; }
        public XYZ Normal { get; set; }
        public XYZ Start { get; set; }
        public XYZ End { get; set; }
        public double WidthFeet { get; set; }
    }

    public static class WallGeometryUtils
    {
        private const double DirectionTolerance = 0.985;

        public static bool TryGetStraightWallInfo(Wall wall, List<string> logs, out StraightWallInfo info)
        {
            info = null;
            if (!(wall?.Location is LocationCurve locationCurve))
            {
                logs?.Add($"Wall {wall?.Id.Value} skipped: Location is not a curve.");
                return false;
            }

            if (!(locationCurve.Curve is Line line))
            {
                logs?.Add($"Wall {wall.Id.Value} skipped: Location is not a straight line.");
                return false;
            }

            XYZ direction = (line.GetEndPoint(1) - line.GetEndPoint(0)).Normalize();
            direction = new XYZ(direction.X, direction.Y, 0.0);
            if (direction.IsZeroLength())
            {
                logs?.Add($"Wall {wall.Id.Value} skipped: Direction is zero length (XY plane).");
                return false;
            }

            direction = direction.Normalize();
            XYZ normal = new XYZ(-direction.Y, direction.X, 0.0).Normalize();

            info = new StraightWallInfo
            {
                Wall = wall,
                LocationLine = line,
                Direction = direction,
                Normal = normal,
                Start = line.GetEndPoint(0),
                End = line.GetEndPoint(1),
                WidthFeet = wall.Width
            };
            return true;
        }

        public static IList<Reference> GetEndFaceReferences(Wall wall, StraightWallInfo info, View activeView, List<string> logs)
        {
            var options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true
            };

            GeometryElement geometry = wall.get_Geometry(options);
            if (geometry == null)
                return Array.Empty<Reference>();

            var candidates = new List<Tuple<double, Reference>>();
            CollectEndFaces(geometry, info, candidates, logs);

            Reference startReference = candidates
                .OrderBy(c => c.Item1)
                .Select(c => c.Item2)
                .FirstOrDefault();

            Reference endReference = candidates
                .OrderByDescending(c => c.Item1)
                .Select(c => c.Item2)
                .FirstOrDefault();

            if (startReference == null || endReference == null || ReferenceEquals(startReference, endReference))
            {
                logs?.Add($"Wall {wall.Id.Value} skipped overall dimension: Could not find both end faces.");
                return Array.Empty<Reference>();
            }

            return new[] { startReference, endReference };
        }

        public static Line CreateDimensionLine(StraightWallInfo info, double offsetFeet)
        {
            XYZ offset = info.Normal * offsetFeet;
            return Line.CreateBound(info.Start + offset, info.End + offset);
        }

        public static double ResolveOffsetFeet(StraightWallInfo info, double offsetMm, XYZ pickedPoint)
        {
            if (pickedPoint != null)
            {
                XYZ vector = pickedPoint - info.Start;
                double signed = vector.DotProduct(info.Normal);
                if (Math.Abs(signed) > 0.01)
                    return signed;
            }

            return UnitUtils.ConvertToInternalUnits(Math.Max(100.0, offsetMm), UnitTypeId.Millimeters);
        }

        public static double ProjectParameter(StraightWallInfo info, Reference reference)
        {
            ElementReferenceType type = reference.ElementReferenceType;
            _ = type;
            return 0.0;
        }

        public static void CollectEndFaces(
            GeometryElement geometry,
            StraightWallInfo info,
            ICollection<Tuple<double, Reference>> candidates,
            List<string> logs)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (!(face is PlanarFace planarFace) || planarFace.Reference == null)
                            continue;

                        XYZ normal = new XYZ(planarFace.FaceNormal.X, planarFace.FaceNormal.Y, 0.0);
                        if (normal.IsZeroLength())
                            continue;

                        normal = normal.Normalize();
                        double parallel = Math.Abs(normal.DotProduct(info.Direction));
                        if (parallel < DirectionTolerance)
                            continue;

                        BoundingBoxUV box = planarFace.GetBoundingBox();
                        UV mid = new UV((box.Min.U + box.Max.U) * 0.5, (box.Min.V + box.Max.V) * 0.5);
                        XYZ center = planarFace.Evaluate(mid);
                        double parameter = (center - info.Start).DotProduct(info.Direction);
                        candidates.Add(Tuple.Create(parameter, planarFace.Reference));
                    }
                }
                else if (obj is GeometryInstance instance)
                {
                    CollectEndFaces(instance.GetInstanceGeometry(), info, candidates, logs);
                }
            }
        }
    }
}
