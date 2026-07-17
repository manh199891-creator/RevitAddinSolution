namespace Antigravity.ZoneSplit.Models
{
    /// <summary>
    /// 2D bounding box dùng model coordinates (feet, Revit internal).
    /// Value type để tránh heap allocation khi so sánh nhiều rooms.
    /// </summary>
    public struct BoundingBox2D
    {
        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }

        public double Width  => MaxX - MinX;
        public double Height => MaxY - MinY;

        public BoundingBox2D(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>
        /// Kiểm tra điểm (x, y) có nằm trong bbox không.
        /// Biên MinX/MinY inclusive, MaxX/MaxY exclusive (half-open interval)
        /// → room trên biên phải sẽ thuộc zone bên phải.
        /// </summary>
        public bool Contains(double x, double y)
        {
            return x >= MinX && x < MaxX
                && y >= MinY && y < MaxY;
        }

        /// <summary>
        /// Overload cho điểm nằm trên cạnh MaxX hoặc MaxY (edge của toàn bộ bbox).
        /// Dùng khi cần inclusive cả 4 biên (ví dụ room cuối cùng).
        /// </summary>
        public bool ContainsInclusive(double x, double y)
        {
            return x >= MinX && x <= MaxX
                && y >= MinY && y <= MaxY;
        }

        /// <summary>
        /// Tính union của hai BoundingBox2D — dùng để tính tổng bbox của tất cả rooms.
        /// </summary>
        public static BoundingBox2D Union(BoundingBox2D a, BoundingBox2D b)
        {
            return new BoundingBox2D(
                System.Math.Min(a.MinX, b.MinX),
                System.Math.Min(a.MinY, b.MinY),
                System.Math.Max(a.MaxX, b.MaxX),
                System.Math.Max(a.MaxY, b.MaxY));
        }

        /// <summary>
        /// Chuyển từ Revit BoundingBoxXYZ (feet, 3D) sang BoundingBox2D (bỏ qua Z).
        /// Caller phải kiểm tra BoundingBoxXYZ.IsEmpty trước khi gọi.
        /// </summary>
        /// <remarks>API NOTE: BoundingBoxXYZ.Min/Max dùng feet — không cần convert.</remarks>
        public static BoundingBox2D FromRevitBBox(Autodesk.Revit.DB.BoundingBoxXYZ bbox)
        {
            return new BoundingBox2D(
                bbox.Min.X,
                bbox.Min.Y,
                bbox.Max.X,
                bbox.Max.Y);
        }

        public override string ToString()
            => $"BBox2D[({MinX:F2},{MinY:F2}) → ({MaxX:F2},{MaxY:F2})]";
    }
}
