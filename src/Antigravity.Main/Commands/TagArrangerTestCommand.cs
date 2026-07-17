using System;
using Autodesk.Revit.DB;
using Antigravity.Core.Services;
using Antigravity.TagArranger.Services;
using Antigravity.TagArranger.Models;

namespace Antigravity.Main.Commands
{
    public class TagArrangerTestCommand
    {
        public static void Execute2(Document doc)
        {
            AutomationLogger.Write("TagArrangerTestCommand", "Bắt đầu thực thi AutoTagService.Execute");

            try
            {
                if (doc.ActiveView == null)
                {
                    AutomationLogger.Write("TagArrangerTestCommand", "Lỗi: Không có ActiveView để chạy lệnh.");
                    return;
                }

                // Thiết lập tuỳ chọn AutoTag
                ArrangeOptions options = new ArrangeOptions
                {
                    MinSpacingFeet = 1.0, // Khoảng cách tối thiểu
                    MaxMergeDistanceFeet = 5.0, // Khoảng cách gộp tag
                    AutoTagLeaderStyle = LeaderFormatStyle.Orthogonal,
                    LeaderAngleDegrees = 45.0
                };

                // Tính toán một tọa độ giả (PickedPoint) thay vì bắt người dùng click
                // Để mô phỏng tính năng "Dóng hàng Orthogonal" như ảnh 2
                XYZ autoPickedPoint = null;
                var collector = new FilteredElementCollector(doc, doc.ActiveView.Id).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().ToElements();
                if (collector.Count > 0)
                {
                    double maxX = double.MinValue;
                    double maxY = double.MinValue;
                    foreach(var e in collector)
                    {
                        var bbox = e.get_BoundingBox(doc.ActiveView);
                        if (bbox != null)
                        {
                            if (bbox.Max.X > maxX) maxX = bbox.Max.X;
                            if (bbox.Max.Y > maxY) maxY = bbox.Max.Y;
                        }
                    }
                    // Đặt điểm dóng hàng nằm bên ngoài lề phải của bản vẽ (cách 10 feet)
                    autoPickedPoint = new XYZ(maxX + 10.0, maxY, doc.ActiveView.Origin.Z);
                }

                // Gọi trực tiếp vào logic lõi
                var result = AutoTagService.Execute(
                    doc: doc,
                    view: doc.ActiveView,
                    selectedIds: null,
                    scope: AutoTagScope.ActiveView, // Test trên toàn bộ Active View
                    category: BuiltInCategory.OST_Walls, // Tag tường
                    tagTypeId: ElementId.InvalidElementId, // Dùng loại tag mặc định
                    hasLeader: true,
                    autoUntangle: true,
                    tagOffsetFeet: 2.0,
                    options: options,
                    pickedPoint: autoPickedPoint
                );

                // Lưu lại file để user có thể mở lên xem thành quả Orthogonal
                try
                {
                    doc.Save();
                    AutomationLogger.Write("TagArrangerTestCommand", $"Đã LƯU file kết quả để kiểm tra giao diện Orthogonal.");
                }
                catch (Exception ex)
                {
                    AutomationLogger.Write("TagArrangerTestCommand", $"Lỗi khi lưu file: {ex.Message}");
                }

                AutomationLogger.Write("TagArrangerTestCommand", $"Kết quả: {result.Message}");
                if (result.LogMessages != null && result.LogMessages.Count > 0)
                {
                    foreach (var msg in result.LogMessages)
                    {
                        AutomationLogger.Write("TagArrangerTestCommand", $"[Chi tiết] {msg}");
                    }
                }
            }
            catch (Exception ex)
            {
                AutomationLogger.WriteError("TagArrangerTestCommand", ex);
            }
        }
    }
}
