using System;
using Autodesk.Revit.DB;

namespace Antigravity.Core.Services
{
    /// <summary>
    /// Quản lý hệ tọa độ thống nhất giữa CAD và Revit cho toàn bộ các add-in.
    /// Lưu trữ offset (feet) để dùng chung.
    /// </summary>
    public static class CoordinateService
    {
        // Lưu trữ offset dùng chung trong suốt phiên làm việc của Revit
        private static XYZ _originOffset = XYZ.Zero;

        /// <summary>
        /// Gán giá trị offset trực tiếp.
        /// Offset = Revit_origin_in_feet - CAD_origin_in_feet
        /// </summary>
        public static void SetOriginOffset(XYZ offsetFeet)
        {
            _originOffset = offsetFeet;
        }

        /// <summary>
        /// Chuyển đổi tọa độ CAD (mm) sang Revit (feet), áp dụng Offset hiện tại.
        /// </summary>
        public static XYZ CadToRevit(double cadXMm, double cadYMm, double elevFeet = 0)
        {
            return new XYZ(
                cadXMm / 304.8 + _originOffset.X,
                cadYMm / 304.8 + _originOffset.Y,
                elevFeet);
        }

        /// <summary>
        /// Lấy giá trị offset hiện tại (feet).
        /// </summary>
        public static XYZ GetOriginOffset()
        {
            return _originOffset;
        }

        /// <summary>
        /// Kiểm tra xem người dùng đã thiết lập gốc tọa độ chung chưa.
        /// </summary>
        public static bool IsOriginSet => !_originOffset.IsAlmostEqualTo(XYZ.Zero);
    }
}
