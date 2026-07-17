using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.TagArranger.Models;
using Antigravity.TagArranger.Core;

namespace Antigravity.TagArranger.Services
{
    /// <summary>
    /// Service thực hiện xếp chồng thông minh (Smart Stack) và căn góc Orthogonal Leader (trực giao) cho các Tag được chọn.
    /// </summary>
    public static class SmartStackService
    {
        public static ArrangeResult ExecuteSmartStack(Document doc, View view, ICollection<ElementId> tagIds, double horizontalOffsetFeet, bool alignRight = true)
        {
            var result = new ArrangeResult();

            // 1. Thu thập các Tag hợp lệ có leader
            IEnumerable<Element> elements;
            if (tagIds != null && tagIds.Count > 0)
            {
                elements = tagIds.Select(id => doc.GetElement(id)).Where(e => e != null);
            }
            else
            {
                elements = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType();
            }

            var tags = elements
                .OfType<IndependentTag>()
                .Where(t => t.HasLeader)
                .ToList();

            if (tags.Count == 0)
            {
                result.Message = "Không tìm thấy Tag nào có Leader để xếp chồng.";
                return result;
            }

            // 2. Sắp xếp các Tag theo tọa độ Y giảm dần (từ trên xuống dưới)
            tags = tags.OrderByDescending(t => t.TagHeadPosition.Y).ToList();

            // C0-FIX: Dùng Median X thay vì X của tag đầu tiên để tránh outlier lệch cột
            // Median ổn định hơn Mean khi có 1-2 tag ở vị trí bất thường
            var sortedByX = tags.Select(t => t.TagHeadPosition.X).OrderBy(x => x).ToList();
            double targetX = sortedByX.Count % 2 == 1
                ? sortedByX[sortedByX.Count / 2]
                : (sortedByX[sortedByX.Count / 2 - 1] + sortedByX[sortedByX.Count / 2]) / 2.0;
            double currentY = tags[0].TagHeadPosition.Y;

            // Quy đổi khoảng cách 2mm trên giấy sang model space làm buffer
            double paperBuffer = 2.0; 
            double bufferFeet = (paperBuffer / 304.8) * view.Scale;

            using (var tx = new Transaction(doc, "Antigravity Smart Stack Tags"))
            {
                tx.Start();

                for (int i = 0; i < tags.Count; i++)
                {
                    IndependentTag tag = tags[i];
                    try
                    {
                        XYZ headPos = tag.TagHeadPosition;
                        if (headPos == null) { result.SkippedCount++; continue; }

                        double newY = headPos.Y;
                        if (i > 0)
                        {
                            // Tính toán khoảng cách (Spacing) tự động từ BoundingBox để các tag không bị đè lên nhau
                            var prevTag = tags[i - 1];
                            var prevBox = AnnotationBoxExtractor.ExtractFromTag(prevTag, view);
                            var currBox = AnnotationBoxExtractor.ExtractFromTag(tag, view);

                            // Lấy halfHeight của Bounding Box ước lượng
                            double halfHeightPrev = prevBox != null 
                                ? (prevBox.Box.MaxY - prevBox.Box.MinY) * 0.5 
                                : (0.008 * view.Scale * 0.6);

                            double halfHeightCurr = currBox != null 
                                ? (currBox.Box.MaxY - currBox.Box.MinY) * 0.5 
                                : (0.008 * view.Scale * 0.6);

                            double step = halfHeightPrev + halfHeightCurr + bufferFeet;
                            currentY = currentY - step;
                            newY = currentY;
                        }

                        // Cập nhật vị trí Tag Head mới
                        XYZ newHeadPos = new XYZ(targetX, newY, headPos.Z);
                        tag.LeaderEndCondition = LeaderEndCondition.Free;
                        tag.TagHeadPosition = newHeadPos;

                        // Chỉnh Elbow và LeaderEnd của các tham chiếu
                        var taggedRefs = tag.GetTaggedReferences();
                        if (taggedRefs != null && taggedRefs.Count > 0)
                        {
                            foreach (Reference r in taggedRefs)
                            {
                                XYZ leaderEnd = tag.GetLeaderEnd(r);
                                if (leaderEnd == null) continue;

                                // Elbow nằm ngang với TagHead (cùng Y), cách X một khoảng offset ngang
                                double elbowX = alignRight 
                                    ? targetX + horizontalOffsetFeet 
                                    : targetX - horizontalOffsetFeet;

                                XYZ elbowPos = new XYZ(elbowX, newY, headPos.Z);

                                // Logic thả leader thẳng đứng 90 độ cho Pipe, Duct, CableTray ngang
                                XYZ newLeaderEnd = leaderEnd;
                                Element taggedElem = doc.GetElement(r.ElementId);
                                if (taggedElem != null && taggedElem is MEPCurve mepCurve)
                                {
                                    LocationCurve locCurve = mepCurve.Location as LocationCurve;
                                    if (locCurve != null && locCurve.Curve is Line curveLine)
                                    {
                                        XYZ p1 = curveLine.GetEndPoint(0);
                                        XYZ p2 = curveLine.GetEndPoint(1);

                                        // Kiểm tra nếu ống/tuyến nằm ngang (Z gần bằng nhau)
                                        if (Math.Abs(p1.Z - p2.Z) < 0.01)
                                        {
                                            double minX = Math.Min(p1.X, p2.X);
                                            double maxX = Math.Max(p1.X, p2.X);

                                            // Nếu elbowX nằm trong khoảng X của đường ống
                                            if (elbowX >= minX && elbowX <= maxX)
                                            {
                                                if (Math.Abs(p2.X - p1.X) > 0.0001)
                                                {
                                                    // Nội suy điểm trên tim ống có X = elbowX để thả đứng góc 90 độ
                                                    double t = (elbowX - p1.X) / (p2.X - p1.X);
                                                    newLeaderEnd = p1 + t * (p2 - p1);
                                                }
                                            }
                                        }
                                    }
                                }

                                tag.SetLeaderElbow(r, elbowPos);
                                tag.SetLeaderEnd(r, newLeaderEnd);
                            }
                        }

                        result.MovedCount++;
                        result.LogMessages.Add($"  Stacked {tag.Id}: Y={newY:F3}");
                    }
                    catch (Exception ex)
                    {
                        result.SkippedCount++;
                        result.LogMessages.Add($"  Lỗi {tag.Id}: {ex.Message}");
                    }
                    result.ProcessedCount++;
                }

                tx.Commit();
            }

            result.Message = $"Smart Stack {(alignRight ? "Right" : "Left")}: Đã chỉnh chồng xếp {result.MovedCount}/{tags.Count} tag.";
            return result;
        }
    }
}
