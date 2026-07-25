using System;
using System.Globalization;
using Autodesk.Revit.DB;

namespace LOQN1_Location_element
{
    /// <summary>
    /// Utility class xử lý chuyển đổi đơn vị và định dạng số cho Revit 2024 API (.NET Framework 4.8).
    /// </summary>
    public static class RevitUnitUtils
    {
        /// <summary>
        /// Chuyển đổi giá trị từ Feet (đơn vị nội bộ Revit) sang Meters (Mét).
        /// Sử dụng API chuẩn UnitTypeId của Revit 2024.
        /// </summary>
        public static double FeetToMeters(double feetValue)
        {
            return UnitUtils.ConvertFromInternalUnits(feetValue, UnitTypeId.Meters);
        }

        /// <summary>
        /// Định dạng giá trị Elevation Offset (tính bằng Feet) sang chuỗi số thực 3 chữ số thập phân,
        /// luôn kèm dấu (+) hoặc (-), sử dụng dấu phẩy làm phần phân cách thập phân.
        /// Ví dụ: +0,000 | +1,200 | -0,500
        /// </summary>
        public static string FormatElevationOffset(double offsetInFeet)
        {
            double offsetInMeters = FeetToMeters(offsetInFeet);

            // Xử lý làm tròn số 0 tuyệt đối để tránh hiển thị -0,000
            if (Math.Abs(offsetInMeters) < 0.0005)
            {
                offsetInMeters = 0.0;
            }

            // Format số thực với dấu + hoặc - và 3 chữ số thập phân
            // Dùng CultureInfo với NumberDecimalSeparator = ","
            CultureInfo customCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ",";

            string formatted;
            if (offsetInMeters >= 0)
            {
                formatted = "+" + offsetInMeters.ToString("0.000", customCulture);
            }
            else
            {
                formatted = offsetInMeters.ToString("0.000", customCulture);
            }

            return formatted;
        }
    }
}
