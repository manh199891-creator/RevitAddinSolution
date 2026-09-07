using System;

namespace Antigravity.DrawBeams.Models
{
    public class CadSegment
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }

        public string Id { get; set; }
        public string Layer { get; set; }
        public int Color { get; set; } = -1;

        /// <summary>Bề dày polyline (mm). 0 = Line thường hoặc Polyline không có width.</summary>
        public double PolylineWidth { get; set; } = 0;

        /// <summary>ID nhóm cặp song song (cho Closed Polyline hình chữ nhật).</summary>
        public string GroupId { get; set; } = null;

        public CadEntityProvenance Provenance { get; set; }

        // Vector hướng chuẩn hóa
        public double DirectionX => EndX - StartX;
        public double DirectionY => EndY - StartY;
        public double Length => Math.Sqrt(DirectionX * DirectionX + DirectionY * DirectionY);

        // Trung điểm
        public double MidX => (StartX + EndX) / 2.0;
        public double MidY => (StartY + EndY) / 2.0;

        // Góc hướng chuẩn hóa [0, π)
        public double Angle
        {
            get
            {
                if (Length < 1e-9) return 0;
                double a = Math.Atan2(DirectionY, DirectionX);
                while (a < 0) a += Math.PI;
                while (a >= Math.PI) a -= Math.PI;
                return a;
            }
        }

        // Vector pháp tuyến (vuông góc với hướng)
        public double NormalX => Length > 1e-9 ? -DirectionY / Length : 0;
        public double NormalY => Length > 1e-9 ? DirectionX / Length : 0;
    }
}
