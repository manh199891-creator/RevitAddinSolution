using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Antigravity.Core.Services;

namespace Antigravity.CheckFloorElevation.Services
{
    public static class ElevationService
    {
        private const double FeetToMmFactor = 304.8;

        public static double? GetZTopFeet(Floor floor, Transform transform)
        {
            if (floor == null)
                return null;

            try
            {
                IList<Reference> topFaceRefs = HostObjectUtils.GetTopFaces(floor);
                if (topFaceRefs == null || topFaceRefs.Count == 0)
                    return FallbackZTopFeet(floor, transform);

                double maxZ = double.MinValue;
                foreach (Reference faceRef in topFaceRefs)
                {
                    Face face = floor.GetGeometryObjectFromReference(faceRef) as Face;
                    if (face == null)
                        continue;

                    Mesh mesh = face.Triangulate();
                    if (mesh != null && mesh.Vertices != null && mesh.Vertices.Count > 0)
                    {
                        foreach (XYZ vertex in mesh.Vertices)
                        {
                            XYZ point = TransformPoint(vertex, transform);
                            maxZ = Math.Max(maxZ, point.Z);
                        }
                    }
                    else
                    {
                        BoundingBoxUV bounds = face.GetBoundingBox();
                        if (bounds == null)
                            continue;

                        UV center = new UV(
                            (bounds.Min.U + bounds.Max.U) / 2.0,
                            (bounds.Min.V + bounds.Max.V) / 2.0);

                        XYZ point = TransformPoint(face.Evaluate(center), transform);
                        maxZ = Math.Max(maxZ, point.Z);
                    }
                }

                return maxZ == double.MinValue ? FallbackZTopFeet(floor, transform) : (double?)maxZ;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "[CheckFloorElevation] GetZTopFeet failed, using bounding box fallback");
                return FallbackZTopFeet(floor, transform);
            }
        }

        public static XYZ GetMidpointAbove(Floor floor, double offsetFeet)
        {
            BoundingBoxXYZ bbox = floor == null ? null : floor.get_BoundingBox(null);
            if (bbox == null)
                return null;

            return new XYZ(
                (bbox.Min.X + bbox.Max.X) / 2.0,
                (bbox.Min.Y + bbox.Max.Y) / 2.0,
                bbox.Max.Z + offsetFeet);
        }

        public static IList<XYZ> GetProbePointsAbove(Floor floor, double offsetFeet)
        {
            var points = new List<XYZ>();
            BoundingBoxXYZ bbox = floor == null ? null : floor.get_BoundingBox(null);
            if (bbox == null)
                return points;

            double centerX = (bbox.Min.X + bbox.Max.X) / 2.0;
            double centerY = (bbox.Min.Y + bbox.Max.Y) / 2.0;
            double z = bbox.Max.Z + offsetFeet;
            points.Add(new XYZ(centerX, centerY, z));

            double marginX = Math.Min(Math.Abs(bbox.Max.X - bbox.Min.X) * 0.2, 3.0);
            double marginY = Math.Min(Math.Abs(bbox.Max.Y - bbox.Min.Y) * 0.2, 3.0);
            double minX = bbox.Min.X + marginX;
            double maxX = bbox.Max.X - marginX;
            double minY = bbox.Min.Y + marginY;
            double maxY = bbox.Max.Y - marginY;

            if (maxX > minX && maxY > minY)
            {
                points.Add(new XYZ(minX, minY, z));
                points.Add(new XYZ(maxX, minY, z));
                points.Add(new XYZ(minX, maxY, z));
                points.Add(new XYZ(maxX, maxY, z));
            }

            return points;
        }

        public static double FeetToMillimeters(double feet)
        {
            return feet * FeetToMmFactor;
        }

        public static double MillimetersToFeet(double millimeters)
        {
            return millimeters / FeetToMmFactor;
        }

        private static double? FallbackZTopFeet(Floor floor, Transform transform)
        {
            BoundingBoxXYZ bbox = floor.get_BoundingBox(null);
            if (bbox == null)
                return null;

            XYZ maxPoint = TransformPoint(bbox.Max, transform);
            return maxPoint.Z;
        }

        private static XYZ TransformPoint(XYZ point, Transform transform)
        {
            if (point == null)
                return null;

            return transform == null || transform.IsIdentity ? point : transform.OfPoint(point);
        }
    }
}
