using System;
using Autodesk.Revit.DB;

namespace Antigravity.WallMepClash.Services
{
    public static class AngleClassifier
    {
        public enum AngleClass
        {
            Parallel,
            Perpendicular,
            Skew,
            Undetermined
        }

        // Tính góc thực (degrees, 0-90) giữa hai vector đường thẳng (không có hướng)
        public static double GetAngleDeg(XYZ wallDir, XYZ mepDir)
        {
            if (wallDir == null || mepDir == null) return 0.0;

            // Đảm bảo vector được chuẩn hóa
            XYZ w = wallDir.Normalize();
            XYZ m = mepDir.Normalize();

            // Tính góc (0 - PI)
            double angleRad = w.AngleTo(m);
            double angleDeg = angleRad * (180.0 / Math.PI);

            // Vì đây là đường thẳng không phân biệt hướng đi/về, chuẩn hóa góc về [0, 90]
            if (angleDeg > 90.0)
            {
                angleDeg = 180.0 - angleDeg;
            }

            return angleDeg;
        }

        // Phân loại quan hệ góc
        public static AngleClass Classify(XYZ wallDir, XYZ mepDir, double parallelThresholdDeg = 10.0)
        {
            if (wallDir == null || mepDir == null) return AngleClass.Undetermined;

            double angleDeg = GetAngleDeg(wallDir, mepDir);

            if (angleDeg <= parallelThresholdDeg)
            {
                return AngleClass.Parallel;
            }
            if (angleDeg >= 90.0 - parallelThresholdDeg)
            {
                return AngleClass.Perpendicular;
            }
            return AngleClass.Skew;
        }
    }
}
