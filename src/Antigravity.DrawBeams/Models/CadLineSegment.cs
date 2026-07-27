using System;

namespace Antigravity.DrawBeams.Models
{
    public class CadLineSegment
    {
        public double[] StartPoint { get; set; }
        public double[] EndPoint { get; set; }
        public string Id { get; set; }
        public string Layer { get; set; }
        public int Color { get; set; } = -1;
        /// <summary>Bề dày polyline (mm). 0 = Line thường hoặc Polyline không có width.</summary>
        public double PolylineWidth { get; set; } = 0;
        /// <summary>ID nhóm cặp song song (cho Closed Polyline hình chữ nhật).</summary>
        public string GroupId { get; set; } = null;

        // Vector hướng chuẩn hóa
        public double DirectionX => EndPoint != null && StartPoint != null ? EndPoint[0] - StartPoint[0] : 0;
        public double DirectionY => EndPoint != null && StartPoint != null ? EndPoint[1] - StartPoint[1] : 0;
        public double Length => Math.Sqrt(DirectionX * DirectionX + DirectionY * DirectionY);

        // Trung điểm
        public double MidX => EndPoint != null && StartPoint != null ? (StartPoint[0] + EndPoint[0]) / 2.0 : 0;
        public double MidY => EndPoint != null && StartPoint != null ? (StartPoint[1] + EndPoint[1]) / 2.0 : 0;

        // Góc hướng chuẩn hóa [0, π)
        public double Angle
        {
            get
            {
                double a = Math.Atan2(DirectionY, DirectionX);
                while (a < 0) a += Math.PI;
                while (a >= Math.PI) a -= Math.PI;
                return a;
            }
        }

        // Vector pháp tuyến (vuông góc với hướng)
        public double NormalX => Length > 0 ? -DirectionY / Length : 0;
        public double NormalY => Length > 0 ? DirectionX / Length : 0;
    }
}
