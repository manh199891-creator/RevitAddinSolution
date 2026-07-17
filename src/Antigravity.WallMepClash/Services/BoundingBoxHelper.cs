using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.WallMepClash.Services
{
    public static class BoundingBoxHelper
    {
        // Transform BB từ link coords → host coords bằng cách transform cả 8 đỉnh để tạo AABB bao quanh
        public static BoundingBoxXYZ TransformToHost(BoundingBoxXYZ linkBB, Transform linkTransform)
        {
            if (linkBB == null) return null;
            if (linkTransform == null || linkTransform.IsIdentity) return linkBB;

            XYZ min = linkBB.Min;
            XYZ max = linkBB.Max;

            // 8 đỉnh của Bounding Box trong không gian Link
            var pts = new XYZ[]
            {
                min,
                new XYZ(max.X, min.Y, min.Z),
                new XYZ(min.X, max.Y, min.Z),
                new XYZ(max.X, max.Y, min.Z),
                new XYZ(min.X, min.Y, max.Z),
                new XYZ(max.X, min.Y, max.Z),
                new XYZ(min.X, max.Y, max.Z),
                max
            };

            // Transform từng đỉnh sang không gian Host
            var transformedPts = pts.Select(p => linkTransform.OfPoint(p)).ToList();

            // Tính Min, Max bao quanh
            double minX = transformedPts.Min(p => p.X);
            double minY = transformedPts.Min(p => p.Y);
            double minZ = transformedPts.Min(p => p.Z);

            double maxX = transformedPts.Max(p => p.X);
            double maxY = transformedPts.Max(p => p.Y);
            double maxZ = transformedPts.Max(p => p.Z);

            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ)
            };
        }

        // Kiểm tra 2 AABB có giao nhau không (3D Axis-Aligned Bounding Box Intersection)
        public static bool Intersects(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            if (a == null || b == null) return false;

            return (a.Min.X <= b.Max.X && a.Max.X >= b.Min.X) &&
                   (a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y) &&
                   (a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z);
        }

        // Lấy direction vector của MEP element (Pipe/Duct/Conduit/CableTray)
        public static XYZ GetElementDirection(Element mepElement)
        {
            if (mepElement == null) return null;

            Location location = mepElement.Location;
            if (location is LocationCurve locCurve)
            {
                Curve curve = locCurve.Curve;
                if (curve != null)
                {
                    if (curve is Line line)
                    {
                        return line.Direction.Normalize();
                    }
                    else
                    {
                        // Lấy tangent vector tại midpoint
                        Transform tangentTransform = curve.ComputeDerivatives(0.5, true);
                        return tangentTransform.BasisX.Normalize();
                    }
                }
            }

            return null; // Trả về null cho point-based elements (MechanicalEquipment, v.v.)
        }

        // Lấy direction vector của Wall (trục vẽ của tường, vuông góc với Normal)
        public static XYZ GetWallDirection(Wall wall)
        {
            if (wall == null) return null;

            Location location = wall.Location;
            if (location is LocationCurve locCurve)
            {
                Curve curve = locCurve.Curve;
                if (curve != null)
                {
                    if (curve is Line line)
                    {
                        return line.Direction.Normalize();
                    }
                    else
                    {
                        // Cho wall curved, lấy tangent vector tại midpoint
                        Transform tangentTransform = curve.ComputeDerivatives(0.5, true);
                        return tangentTransform.BasisX.Normalize();
                    }
                }
            }

            // Fallback: dùng Orientation (normal vector) xoay đi 90 độ
            XYZ normal = wall.Orientation;
            if (normal != null)
            {
                return new XYZ(-normal.Y, normal.X, 0).Normalize();
            }

            return null;
        }
    }
}
