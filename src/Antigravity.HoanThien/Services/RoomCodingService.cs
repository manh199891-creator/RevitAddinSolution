using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Antigravity.HoanThien.Services
{
    public class RoomCodingService
    {
        public static void CodeRooms(Document doc, List<Room> rooms, string prefix, bool useSharedParameter)
        {
            if (rooms == null || !rooms.Any()) return;

            // Sắp xếp các phòng theo tọa độ Y giảm dần (trên xuống dưới), X tăng dần (trái qua phải)
            var sortedRooms = rooms.OrderByDescending(r => r.Location is LocationPoint lp ? lp.Point.Y : 0)
                                   .ThenBy(r => r.Location is LocationPoint lp ? lp.Point.X : 0)
                                   .ToList();

            int counter = 1;
            foreach (var room in sortedRooms)
            {
                string code = $"{prefix}{counter:D2}"; // VD: GRV_01

                if (useSharedParameter)
                {
                    var param = room.LookupParameter("AG_Room_Code");
                    if (param != null && !param.IsReadOnly)
                    {
                        param.Set(code);
                    }
                    else
                    {
                        // Fallback
                        room.Number = code;
                    }
                }
                else
                {
                    // Ghi đè vào Room Number mặc định
                    // Note: Room Number phải là duy nhất trong toàn dự án
                    try
                    {
                        room.Number = code;
                    }
                    catch
                    {
                        // Bỏ qua nếu lỗi trùng Number hoặc readonly
                    }
                }
                
                counter++;
            }
        }
    }
}
