using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace LOQN1_Location_element
{
    /// <summary>
    /// Class chứa dữ liệu tổng hợp vị trí của một Element.
    /// </summary>
    public class ElementPositionData
    {
        public BoundingBoxXYZ BoundingBox { get; set; }
        public XYZ CenterPoint { get; set; }
        public Level ReferenceLevel { get; set; }
        public double ElevationOffsetInFeet { get; set; }
        public double MinX => BoundingBox != null ? BoundingBox.Min.X : CenterPoint.X;
        public double MaxX => BoundingBox != null ? BoundingBox.Max.X : CenterPoint.X;
        public double MinY => BoundingBox != null ? BoundingBox.Min.Y : CenterPoint.Y;
        public double MaxY => BoundingBox != null ? BoundingBox.Max.Y : CenterPoint.Y;
    }

    /// <summary>
    /// Helper class chuyên xử lý trích xuất Center Point, BoundingBoxXYZ, Level tham chiếu và Elevation Offset.
    /// </summary>
    public static class ElementPositionHelper
    {
        /// <summary>
        /// Thuật toán chính trích xuất toàn bộ thông tin vị trí của Element.
        /// </summary>
        public static ElementPositionData ExtractPositionData(Element elem, Document doc, List<Level> sortedLevels)
        {
            ElementPositionData data = new ElementPositionData();

            // 1. Trích xuất BoundingBoxXYZ trong không gian World Coordinate
            data.BoundingBox = elem.get_BoundingBox(null);

            // 2. Trích xuất CenterPoint / Location Point
            data.CenterPoint = CalculateCenterPoint(elem, data.BoundingBox);

            // 3. Xác định Level tham chiếu (Dùng BuiltInParameter trước, nếu Null dùng Z Fallback)
            data.ReferenceLevel = DetermineReferenceLevel(elem, doc, data.CenterPoint, sortedLevels);

            // 4. Tính toán Elevation Offset (Feet)
            if (data.ReferenceLevel != null)
            {
                data.ElevationOffsetInFeet = data.CenterPoint.Z - data.ReferenceLevel.Elevation;
            }
            else
            {
                data.ElevationOffsetInFeet = 0.0;
            }

            return data;
        }

        /// <summary>
        /// Tính toán Center Point chuẩn cho đa dạng các loại đối tượng (Point, Curve, Surface/Solid).
        /// </summary>
        private static XYZ CalculateCenterPoint(Element elem, BoundingBoxXYZ bbox)
        {
            Location loc = elem.Location;

            // 1. Đối tượng Point-based (MEP Equipment, Fixtures, Columns, Doors...)
            if (loc is LocationPoint locPoint && locPoint.Point != null)
            {
                return locPoint.Point;
            }

            // 2. Đối tượng Curve-based (Beams, Walls, Cable Trays, Ducts, Pipes...)
            if (loc is LocationCurve locCurve && locCurve.Curve != null)
            {
                Curve curve = locCurve.Curve;
                // Lấy điểm trung điểm của tuyến Curve
                XYZ midPoint = curve.Evaluate(0.5, true);

                // Nếu có BoundingBox, giữ lại Z từ BoundingBox Center để chính xác hơn về độ cao
                if (bbox != null)
                {
                    double centerZ = (bbox.Min.Z + bbox.Max.Z) / 2.0;
                    return new XYZ(midPoint.X, midPoint.Y, centerZ);
                }

                return midPoint;
            }

            // 3. Đối tượng Surface/Solid-based (Floors, Roofs, Ceilings, In-Place...)
            if (bbox != null)
            {
                return (bbox.Min + bbox.Max) / 2.0;
            }

            return XYZ.Zero;
        }

        /// <summary>
        /// Thuật toán xác định Level (Tầng tham chiếu).
        /// Bước 1: Thử đọc từ Parameter chuẩn (FAMILY_LEVEL_PARAM, SCHEDULE_LEVEL_PARAM, INSTANCE_REFERENCE_LEVEL_PARAM...).
        /// Bước 2: Thử đọc các Parameter Level liên quan đến loại Element (WALL_BASE_CONSTRAINT, ROOF_BASE_LEVEL_PARAM...).
        /// Bước 3: Nếu Null (ví dụ Model-in-place), dùng tọa độ Z trung tâm so sánh với toàn bộ Level để chọn Level gần nhất bên dưới.
        /// </summary>
        public static Level DetermineReferenceLevel(Element elem, Document doc, XYZ centerPoint, List<Level> sortedLevels)
        {
            // Bước 1 & 2: Đọc từ BuiltInParameters phổ biến
            BuiltInParameter[] levelParams = new BuiltInParameter[]
            {
                BuiltInParameter.FAMILY_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_LEVEL_PARAM,
                BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                BuiltInParameter.LEVEL_PARAM,
                BuiltInParameter.WALL_BASE_CONSTRAINT,
                BuiltInParameter.ROOF_BASE_LEVEL_PARAM,
                BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL,
                BuiltInParameter.MULTISTORY_STAIRS_REF_LEVEL
            };

            foreach (BuiltInParameter bip in levelParams)
            {
                Parameter p = elem.get_Parameter(bip);
                if (p != null && p.HasValue && p.StorageType == StorageType.ElementId)
                {
                    ElementId levelId = p.AsElementId();
                    if (levelId != null && levelId != ElementId.InvalidElementId)
                    {
                        if (doc.GetElement(levelId) is Level level)
                        {
                            return level;
                        }
                    }
                }
            }

            // Thử lấy Level property trực tiếp nếu có
            if (elem.LevelId != null && elem.LevelId != ElementId.InvalidElementId)
            {
                if (doc.GetElement(elem.LevelId) is Level lvl)
                {
                    return lvl;
                }
            }

            // Bước 3: Fallback theo tọa độ Z trung tâm đối tượng
            return FindLevelBelowZ(centerPoint.Z, sortedLevels);
        }

        /// <summary>
        /// Thuật toán Fallback: Tìm Level có cao độ (Elevation) gần nhất bên dưới cao độ Z của đối tượng.
        /// </summary>
        private static Level FindLevelBelowZ(double targetZ, List<Level> sortedLevels)
        {
            if (sortedLevels == null || sortedLevels.Count == 0)
                return null;

            Level selectedLevel = null;

            // Duyệt danh sách Level đã sắp xếp tăng dần theo Elevation
            foreach (Level lvl in sortedLevels)
            {
                if (lvl.Elevation <= targetZ)
                {
                    selectedLevel = lvl;
                }
                else
                {
                    // Đã vượt quá targetZ, dừng vòng lặp
                    break;
                }
            }

            // Nếu Z nhỏ hơn cả Level thấp nhất trong dự án, chọn Level thấp nhất
            if (selectedLevel == null)
            {
                selectedLevel = sortedLevels.FirstOrDefault();
            }

            return selectedLevel;
        }
    }
}
