using System;

namespace Antigravity.DrawBeams.Models
{
    /// <summary>Pure affine 2D transform used while flattening block references.</summary>
    public struct CadTransform2D
    {
        public double M11 { get; private set; }
        public double M12 { get; private set; }
        public double M21 { get; private set; }
        public double M22 { get; private set; }
        public double Tx { get; private set; }
        public double Ty { get; private set; }

        public static CadTransform2D Identity => new CadTransform2D { M11 = 1, M22 = 1 };

        public static CadTransform2D Create(double x, double y, double rotation, double scaleX, double scaleY)
        {
            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);
            return new CadTransform2D { M11 = cos * scaleX, M12 = -sin * scaleY, M21 = sin * scaleX, M22 = cos * scaleY, Tx = x, Ty = y };
        }

        /// <summary>
        /// Builds a block-reference placement transform while honoring the block definition base point.
        /// AutoCAD block-definition geometry is expressed around that base point, not necessarily (0,0).
        /// </summary>
        public static CadTransform2D CreateBlockPlacement(
            double insertionX,
            double insertionY,
            double rotation,
            double scaleX,
            double scaleY,
            double basePointX,
            double basePointY)
        {
            var placement = Create(insertionX, insertionY, rotation, scaleX, scaleY);
            var originShift = Create(-basePointX, -basePointY, 0, 1, 1);
            return placement.Compose(originShift);
        }

        /// <summary>Returns this transform applied after <paramref name="child"/>.</summary>
        public CadTransform2D Compose(CadTransform2D child)
        {
            return new CadTransform2D
            {
                M11 = M11 * child.M11 + M12 * child.M21, M12 = M11 * child.M12 + M12 * child.M22,
                M21 = M21 * child.M11 + M22 * child.M21, M22 = M21 * child.M12 + M22 * child.M22,
                Tx = M11 * child.Tx + M12 * child.Ty + Tx, Ty = M21 * child.Tx + M22 * child.Ty + Ty
            };
        }

        public CadPoint2D Apply(double x, double y) => new CadPoint2D(M11 * x + M12 * y + Tx, M21 * x + M22 * y + Ty);

        /// <summary>Applies only the linear rotation/scale portion, excluding translation.</summary>
        public CadPoint2D ApplyVector(double x, double y) => new CadPoint2D(M11 * x + M12 * y, M21 * x + M22 * y);

        /// <summary>Transforms a direction angle through the affine linear portion.</summary>
        public double TransformAngle(double angle)
        {
            var vector = ApplyVector(Math.Cos(angle), Math.Sin(angle));
            if (Math.Abs(vector.X) < 1e-12 && Math.Abs(vector.Y) < 1e-12) return angle;
            return Math.Atan2(vector.Y, vector.X);
        }

        /// <summary>Transforms a length measured along the supplied local direction angle.</summary>
        public double TransformLength(double length, double directionAngle)
        {
            var vector = ApplyVector(Math.Cos(directionAngle) * length, Math.Sin(directionAngle) * length);
            return Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        }
    }

    public struct CadPoint2D
    {
        public CadPoint2D(double x, double y) { X = x; Y = y; }
        public double X { get; private set; }
        public double Y { get; private set; }
    }
}
