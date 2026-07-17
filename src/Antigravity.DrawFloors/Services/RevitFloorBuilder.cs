using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Antigravity.Core.Services;

namespace Antigravity.DrawFloors.Services
{
    /// <summary>
    /// Dịch vụ tạo đối tượng sàn (Floor) trong Revit từ dữ liệu biên (CurveLoop).
    /// </summary>
    public class RevitFloorBuilder
    {
        private readonly Document _doc;

        public RevitFloorBuilder(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Tạo một đối tượng sàn từ danh sách Curve (outer boundary duy nhất).
        /// </summary>
        public Floor CreateFloor(
            Level level,
            FloorType floorType,
            double heightOffsetMm,
            IList<Curve> profileCurves)
        {
            if (profileCurves == null || profileCurves.Count == 0)
                throw new ArgumentException("Floor profile curves are invalid or empty.");

            var loop = CurveLoop.Create(profileCurves);
            return CreateFloor(level, floorType, heightOffsetMm, new List<CurveLoop> { loop });
        }

        /// <summary>
        /// Tạo một đối tượng sàn từ danh sách CurveLoop (outer + holes).
        /// </summary>
        /// <param name="level">Level chứa sàn</param>
        /// <param name="floorType">FloorType (loại sàn)</param>
        /// <param name="heightOffsetMm">Độ lệch cao độ, đơn vị mm (âm = hạ cốt)</param>
        /// <param name="profile">Danh sách CurveLoop: [0] = outer boundary, [1..n] = holes</param>
        /// <returns>Đối tượng Floor vừa tạo, hoặc null nếu thất bại</returns>
        public Floor CreateFloor(
            Level level,
            FloorType floorType,
            double heightOffsetMm,
            IList<CurveLoop> profile)
        {
            if (profile == null || profile.Count == 0)
                throw new ArgumentException("Floor profile list is invalid or empty.");

            if (level == null)
                throw new ArgumentNullException("level", "Level not selected.");

            if (floorType == null)
                throw new ArgumentNullException("floorType", "Floor Type not selected.");

            // Revit 2023+ API: Floor.Create(doc, IList<CurveLoop>, FloorTypeId, LevelId)
            Floor floor = Floor.Create(_doc, profile, floorType.Id, level.Id);

            if (floor != null && heightOffsetMm != 0.0)
            {
                // Đặt Height Offset From Level (mm → feet)
                Parameter offsetParam = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                if (offsetParam != null && !offsetParam.IsReadOnly)
                {
                    double offsetFeet = heightOffsetMm / 304.8;
                    offsetParam.Set(offsetFeet);
                }
            }

            return floor;
        }

        /// <summary>
        /// Tạo nhiều sàn từ nhiều tập hợp profile khác nhau trong một Transaction duy nhất.
        /// </summary>
        public List<Floor> CreateFloors(
            Level level,
            FloorType floorType,
            double heightOffsetMm,
            List<IList<CurveLoop>> profiles)
        {
            var created = new List<Floor>();
            foreach (var profile in profiles)
            {
                try
                {
                    var floor = CreateFloor(level, floorType, heightOffsetMm, profile);
                    if (floor != null)
                        created.Add(floor);
                }
                catch (Exception ex)
                {
                    // Ghi log nhưng tiếp tục tạo các sàn còn lại
                    AppLogger.Warning("CreateFloor error: " + ex.Message);
                }
            }
            return created;
        }
    }
}
