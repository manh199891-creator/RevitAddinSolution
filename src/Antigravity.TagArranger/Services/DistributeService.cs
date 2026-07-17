using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.TagArranger.Models;

namespace Antigravity.TagArranger.Services
{
    /// <summary>
    /// Service phân bố đều khoảng cách giữa các annotation.
    /// Giữ nguyên vị trí phần tử đầu và cuối, chia đều các phần tử ở giữa.
    /// </summary>
    public static class DistributeService
    {
        public static ArrangeResult Execute(Document doc, List<AnnotationBox> boxes, DistributeMode mode)
        {
            var result = new ArrangeResult();

            if (boxes == null || boxes.Count < 3)
            {
                result.Message = "Cần chọn ít nhất 3 annotation để phân bố đều.";
                return result;
            }

            // Sắp xếp theo trục phân bố
            List<AnnotationBox> sorted;
            if (mode == DistributeMode.Horizontal)
            {
                sorted = boxes.OrderBy(b => b.HeadPosition.X).ToList();
            }
            else
            {
                sorted = boxes.OrderBy(b => b.HeadPosition.Y).ToList();
            }

            // Tính khoảng cách đều giữa đầu và cuối
            var first = sorted[0];
            var last = sorted[sorted.Count - 1];
            int gapCount = sorted.Count - 1;

            double totalSpan, spacing;
            if (mode == DistributeMode.Horizontal)
            {
                totalSpan = last.HeadPosition.X - first.HeadPosition.X;
                spacing = totalSpan / gapCount;
            }
            else
            {
                totalSpan = last.HeadPosition.Y - first.HeadPosition.Y;
                spacing = totalSpan / gapCount;
            }

            result.LogMessages.Add($"[DistributeService] Mode={mode}, Count={sorted.Count}, Span={totalSpan:F3}ft, Spacing={spacing:F3}ft");

            using (var tx = new Transaction(doc, "Antigravity Distribute Tags"))
            {
                tx.Start();

                // Phần tử đầu và cuối giữ nguyên, chỉ di chuyển các phần tử ở giữa
                for (int i = 1; i < sorted.Count - 1; i++)
                {
                    var box = sorted[i];
                    if (!box.IsPositionAdjustable)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    XYZ currentPos = box.HeadPosition;
                    XYZ newPos;

                    if (mode == DistributeMode.Horizontal)
                    {
                        double targetX = first.HeadPosition.X + spacing * i;
                        newPos = new XYZ(targetX, currentPos.Y, currentPos.Z);
                    }
                    else
                    {
                        double targetY = first.HeadPosition.Y + spacing * i;
                        newPos = new XYZ(currentPos.X, targetY, currentPos.Z);
                    }

                    if (currentPos.DistanceTo(newPos) < 0.001)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    if (AlignService.ApplyPosition(doc, box, newPos))
                    {
                        result.MovedCount++;
                        result.LogMessages.Add($"  Moved {box.ElementId}: pos[{i}] → ({newPos.X:F3},{newPos.Y:F3})");
                    }
                    else
                    {
                        result.SkippedCount++;
                    }
                    result.ProcessedCount++;
                }

                tx.Commit();
            }

            result.Message = $"Distribute {mode}: Đã phân bố đều {result.MovedCount} annotation (spacing={spacing * 304.8:F1}mm).";
            return result;
        }
    }
}
