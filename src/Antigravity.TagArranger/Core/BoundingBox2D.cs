using System;

namespace Antigravity.TagArranger.Core
{
    /// <summary>
    /// Bounding box 2D đơn giản (axis-aligned) dùng cho phát hiện chồng lấp annotation.
    /// Tọa độ trong hệ view (feet).
    /// </summary>
    public struct BoundingBox2D
    {
        public double MinX;
        public double MinY;
        public double MaxX;
        public double MaxY;

        public BoundingBox2D(double minX, double minY, double maxX, double maxY)
        {
            MinX = Math.Min(minX, maxX);
            MinY = Math.Min(minY, maxY);
            MaxX = Math.Max(minX, maxX);
            MaxY = Math.Max(minY, maxY);
        }

        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public double CenterX => (MinX + MaxX) * 0.5;
        public double CenterY => (MinY + MaxY) * 0.5;

        /// <summary>Kiểm tra 2 box có giao nhau không (AABB intersection).</summary>
        public bool Intersects(BoundingBox2D other)
        {
            return MinX < other.MaxX && MaxX > other.MinX
                && MinY < other.MaxY && MaxY > other.MinY;
        }

        /// <summary>Kiểm tra 2 box có giao nhau không, có tính thêm khoảng padding.</summary>
        public bool IntersectsWithPadding(BoundingBox2D other, double padding)
        {
            return (MinX - padding) < other.MaxX && (MaxX + padding) > other.MinX
                && (MinY - padding) < other.MaxY && (MaxY + padding) > other.MinY;
        }

        /// <summary>Mở rộng box theo mọi hướng.</summary>
        public BoundingBox2D ExpandBy(double amount)
        {
            return new BoundingBox2D(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);
        }

        /// <summary>Dịch chuyển box theo delta.</summary>
        public BoundingBox2D Translate(double dx, double dy)
        {
            return new BoundingBox2D(MinX + dx, MinY + dy, MaxX + dx, MaxY + dy);
        }

        /// <summary>Tính khoảng cách giữa 2 center.</summary>
        public double DistanceTo(BoundingBox2D other)
        {
            double dx = CenterX - other.CenterX;
            double dy = CenterY - other.CenterY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString()
        {
            return $"BB2D[({MinX:F3},{MinY:F3})-({MaxX:F3},{MaxY:F3})]";
        }
    }
}
