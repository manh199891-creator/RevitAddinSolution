using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.TagArranger.Models;

namespace Antigravity.TagArranger.Services
{
    /// <summary>
    /// Service căn thẳng hàng các annotation theo đường cơ sở (baseline).
    /// Hỗ trợ 6 hướng: Top, Bottom, Left, Right, CenterH, CenterV.
    /// </summary>
    public static class AlignService
    {
        public static ArrangeResult Execute(Document doc, List<AnnotationBox> boxes, AlignMode mode, double offsetFeet = 0)
        {
            var result = new ArrangeResult();

            if (boxes == null || boxes.Count < 2)
            {
                result.Message = "Cần chọn ít nhất 2 annotation để align.";
                return result;
            }

            // Determine reference box and baseline
            AnnotationBox refBox = GetReferenceBox(boxes, mode);
            double baseline = CalculateBaseline(refBox, boxes, mode);
            result.LogMessages.Add($"[AlignService] Mode={mode}, Baseline={baseline:F4}ft, Offset={offsetFeet:F4}ft, Count={boxes.Count}");

            using (var tx = new Transaction(doc, "Antigravity Tag Align"))
            {
                tx.Start();

                foreach (var box in boxes)
                {
                    if (!box.IsPositionAdjustable)
                    {
                        result.SkippedCount++;
                        result.LogMessages.Add($"  Skip {box.ElementId} — not adjustable");
                        continue;
                    }

                    if (offsetFeet != 0 && box == refBox)
                    {
                        // Reference box is not moved if there is an offset
                        result.SkippedCount++;
                        result.LogMessages.Add($"  Skip {box.ElementId} — is reference box");
                        continue;
                    }

                    XYZ currentPos = box.HeadPosition;
                    XYZ newPos = CalculateNewPosition(currentPos, box.Box, baseline, mode, offsetFeet);

                    if (currentPos.DistanceTo(newPos) < 0.001) // < 0.3mm → skip
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    if (ApplyPosition(doc, box, newPos))
                    {
                        result.MovedCount++;
                        result.LogMessages.Add($"  Moved {box.ElementId}: ({currentPos.X:F3},{currentPos.Y:F3}) → ({newPos.X:F3},{newPos.Y:F3})");
                    }
                    else
                    {
                        result.SkippedCount++;
                    }
                    result.ProcessedCount++;
                }

                tx.Commit();
            }

            result.Message = $"Align {mode}: Đã di chuyển {result.MovedCount}/{boxes.Count} annotation.";
            return result;
        }

        private static AnnotationBox GetReferenceBox(List<AnnotationBox> boxes, AlignMode mode)
        {
            var dims = boxes.Where(b => b.Type == AnnotationType.DimensionSegment).ToList();
            if (dims.Count == 1)
            {
                return dims[0]; // If exactly 1 dimension, it's always the reference
            }

            switch (mode)
            {
                case AlignMode.Top: return boxes.OrderByDescending(b => b.Box.MaxY).First();
                case AlignMode.Bottom: return boxes.OrderBy(b => b.Box.MinY).First();
                case AlignMode.Left: return boxes.OrderBy(b => b.Box.MinX).First();
                case AlignMode.Right: return boxes.OrderByDescending(b => b.Box.MaxX).First();
                default: return boxes.First();
            }
        }

        private static double CalculateBaseline(AnnotationBox refBox, List<AnnotationBox> boxes, AlignMode mode)
        {
            switch (mode)
            {
                case AlignMode.Top: return refBox.Box.MaxY;
                case AlignMode.Bottom: return refBox.Box.MinY;
                case AlignMode.Left: return refBox.Box.MinX;
                case AlignMode.Right: return refBox.Box.MaxX;
                case AlignMode.CenterHorizontal:
                    var dims = boxes.Where(b => b.Type == AnnotationType.DimensionSegment).ToList();
                    return dims.Count == 1 ? refBox.HeadPosition.Y : boxes.Average(b => b.HeadPosition.Y);
                case AlignMode.CenterVertical:
                    var dims2 = boxes.Where(b => b.Type == AnnotationType.DimensionSegment).ToList();
                    return dims2.Count == 1 ? refBox.HeadPosition.X : boxes.Average(b => b.HeadPosition.X);
                default: return 0;
            }
        }

        private static XYZ CalculateNewPosition(XYZ current, Core.BoundingBox2D box, double baseline, AlignMode mode, double offsetFeet)
        {
            double newX = current.X;
            double newY = current.Y;

            switch (mode)
            {
                case AlignMode.Top:
                    double topOffset = box.MaxY - current.Y;
                    newY = baseline - topOffset - offsetFeet; 
                    break;
                case AlignMode.Bottom:
                    double bottomOffset = current.Y - box.MinY;
                    newY = baseline + bottomOffset + offsetFeet; 
                    break;
                case AlignMode.Left:
                    double leftOffset = current.X - box.MinX;
                    newX = baseline + leftOffset + offsetFeet; 
                    break;
                case AlignMode.Right:
                    double rightOffset = box.MaxX - current.X;
                    newX = baseline - rightOffset - offsetFeet; 
                    break;
                case AlignMode.CenterHorizontal:
                    newY = baseline - offsetFeet;
                    break;
                case AlignMode.CenterVertical:
                    newX = baseline + offsetFeet;
                    break;
            }

            return new XYZ(newX, newY, current.Z);
        }

        /// <summary>
        /// Áp dụng vị trí mới cho annotation element.
        /// Khi di chuyển Tag, tự động dịch chuyển LeaderElbow theo cùng delta
        /// để Leader không bị "banh" ra hướng sai.
        /// </summary>
        internal static bool ApplyPosition(Document doc, AnnotationBox box, XYZ newPos)
        {
            try
            {
                Element elem = doc.GetElement(box.ElementId);
                if (elem == null) return false;

                if (box.Type == AnnotationType.Tag && elem is IndependentTag tag)
                {
                    // B0-FIX: Tính delta dịch chuyển và đồng bộ LeaderElbow TRƯỚC khi set head position
                    XYZ oldPos = tag.TagHeadPosition;
                    if (oldPos != null && tag.HasLeader)
                    {
                        XYZ delta = newPos - oldPos;
                        SyncLeaderElbows(tag, delta);
                    }
                    tag.TagHeadPosition = newPos;
                    return true;
                }
                else if (box.Type == AnnotationType.DimensionSegment && elem is Dimension dim)
                {
                    if (box.SegmentIndex < 0)
                    {
                        // Single segment dimension
                        dim.TextPosition = newPos;
                        return true;
                    }
                    else if (dim.NumberOfSegments > box.SegmentIndex)
                    {
                        int idx = 0;
                        foreach (DimensionSegment seg in dim.Segments)
                        {
                            if (idx == box.SegmentIndex)
                            {
                                if (seg.IsTextPositionAdjustable())
                                {
                                    seg.TextPosition = newPos;
                                    return true;
                                }
                                return false;
                            }
                            idx++;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        /// <summary>
        /// B0-FIX: Dịch chuyển tất cả LeaderElbow của một tag theo delta vector.
        /// Giữ nguyên góc và hình dạng của leader, chỉ pan toàn bộ theo delta.
        /// LeaderEnd (điểm bám vào element) KHÔNG bị dịch chuyển — nó phải giữ nguyên.
        /// </summary>
        private static void SyncLeaderElbows(IndependentTag tag, XYZ delta)
        {
            // Chỉ sync khi delta đủ lớn để tránh floating point noise
            if (delta.GetLength() < 0.0001) return;

            try
            {
                var refs = tag.GetTaggedReferences();
                if (refs == null || refs.Count == 0) return;

                tag.LeaderEndCondition = LeaderEndCondition.Free;

                foreach (var r in refs)
                {
                    try
                    {
                        XYZ oldElbow = tag.GetLeaderElbow(r);
                        if (oldElbow != null)
                        {
                            tag.SetLeaderElbow(r, oldElbow + delta);
                        }
                    }
                    catch
                    {
                        // Bỏ qua nếu tag không có elbow (leader thẳng)
                    }
                }
            }
            catch
            {
                // Bỏ qua nếu không đọc được refs
            }
        }
    }
}
