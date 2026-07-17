using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.TagArranger.Models;

namespace Antigravity.TagArranger.Services
{
    /// <summary>
    /// Service quản lý leader line cho IndependentTag.
    /// Tính năng:
    /// - Straighten Leaders: Làm thẳng leader (landing line nằm ngang/dọc)
    /// - Set Leader Angle: Đặt góc leader theo độ (45°, 60°, 90°...)
    /// - Set Landing Distance: Đặt chiều dài landing line (đoạn ngang)
    /// - Parallel Leaders: Làm tất cả leader song song với nhau
    /// </summary>
    public static class LeaderService
    {
        /// <summary>
        /// Làm thẳng landing line (đoạn ngang) cho tất cả tag đã chọn.
        /// Landing line sẽ nằm ngang hoàn toàn (cùng Y với TagHead).
        /// </summary>
        public static ArrangeResult StraightenLeaders(Document doc, View view, ICollection<ElementId> selectedIds, double landingDistanceFeet)
        {
            var result = new ArrangeResult();
            var tags = CollectTags(doc, view, selectedIds);

            if (tags.Count == 0)
            {
                result.Message = "Không tìm thấy Tag nào có Leader. Hãy chọn tag trước.";
                return result;
            }

            using (var tx = new Transaction(doc, "Antigravity Straighten Leaders"))
            {
                tx.Start();

                foreach (var tag in tags)
                {
                    try
                    {
                        if (!tag.HasLeader) { result.SkippedCount++; continue; }

                        var taggedRefs = tag.GetTaggedReferences();
                        if (taggedRefs == null || taggedRefs.Count == 0) { result.SkippedCount++; continue; }

                        Reference taggedRef = taggedRefs.First();
                        XYZ headPos = tag.TagHeadPosition;

                        // Lấy vị trí element được tag
                        XYZ leaderEnd = tag.GetLeaderEnd(taggedRef);
                        if (leaderEnd == null) { result.SkippedCount++; continue; }

                        // Elbow nằm ngang với TagHead, thẳng đứng với LeaderEnd
                        // Tạo landing line nằm ngang
                        double elbowX = headPos.X > leaderEnd.X
                            ? headPos.X - landingDistanceFeet
                            : headPos.X + landingDistanceFeet;

                        XYZ elbowPos = new XYZ(elbowX, headPos.Y, headPos.Z);

                        tag.LeaderEndCondition = LeaderEndCondition.Free;
                        tag.SetLeaderElbow(taggedRef, elbowPos);

                        result.MovedCount++;
                        result.LogMessages.Add($"  Straightened {tag.Id}: elbow=({elbowPos.X:F3},{elbowPos.Y:F3})");
                    }
                    catch (Exception ex)
                    {
                        result.SkippedCount++;
                        result.LogMessages.Add($"  Error {tag.Id}: {ex.Message}");
                    }
                    result.ProcessedCount++;
                }

                tx.Commit();
            }

            result.Message = $"Straighten Leaders: Đã chỉnh {result.MovedCount}/{tags.Count} tag.";
            return result;
        }

        /// <summary>
        /// Đặt góc leader cho tất cả tag đã chọn.
        /// Góc tính từ phương ngang (0° = ngang, 90° = thẳng đứng).
        /// </summary>
        public static ArrangeResult SetLeaderAngle(Document doc, View view, ICollection<ElementId> selectedIds, double angleDegrees)
        {
            var result = new ArrangeResult();
            var tags = CollectTags(doc, view, selectedIds);

            if (tags.Count == 0)
            {
                result.Message = "Không tìm thấy Tag nào có Leader.";
                return result;
            }

            double angleRad = angleDegrees * Math.PI / 180.0;

            using (var tx = new Transaction(doc, "Antigravity Set Leader Angle"))
            {
                tx.Start();

                foreach (var tag in tags)
                {
                    try
                    {
                        if (!tag.HasLeader) { result.SkippedCount++; continue; }

                        var taggedRefs = tag.GetTaggedReferences();
                        if (taggedRefs == null || taggedRefs.Count == 0) { result.SkippedCount++; continue; }

                        Reference taggedRef = taggedRefs.First();
                        XYZ headPos = tag.TagHeadPosition;
                        XYZ leaderEnd = tag.GetLeaderEnd(taggedRef);
                        if (leaderEnd == null) { result.SkippedCount++; continue; }

                        // Tính khoảng cách giữa head và leader end
                        double dist = headPos.DistanceTo(leaderEnd);
                        if (dist < 0.01) { result.SkippedCount++; continue; }

                        // Tính hướng từ leaderEnd → headPos
                        double dirSign = headPos.X >= leaderEnd.X ? 1.0 : -1.0;

                        // Tính vị trí elbow dựa trên góc
                        // Elbow nằm trên đường thẳng từ leaderEnd với góc cho trước
                        double leaderLength = dist * 0.6; // leader chiếm 60% tổng khoảng cách
                        double elbowX = leaderEnd.X + dirSign * leaderLength * Math.Cos(angleRad);
                        double elbowY = leaderEnd.Y + leaderLength * Math.Sin(angleRad);

                        XYZ elbowPos = new XYZ(elbowX, elbowY, headPos.Z);

                        tag.LeaderEndCondition = LeaderEndCondition.Free;
                        tag.SetLeaderElbow(taggedRef, elbowPos);

                        // Cập nhật TagHead để landing line nằm ngang
                        XYZ newHeadPos = new XYZ(headPos.X, elbowY, headPos.Z);
                        tag.TagHeadPosition = newHeadPos;

                        result.MovedCount++;
                    }
                    catch (Exception ex)
                    {
                        result.SkippedCount++;
                        result.LogMessages.Add($"  Error {tag.Id}: {ex.Message}");
                    }
                    result.ProcessedCount++;
                }

                tx.Commit();
            }

            result.Message = $"Set Leader Angle {angleDegrees}°: Đã chỉnh {result.MovedCount}/{tags.Count} tag.";
            return result;
        }

        /// <summary>
        /// Làm tất cả leader song song với nhau (cùng góc).
        /// Lấy góc của tag đầu tiên làm chuẩn.
        /// </summary>
        public static ArrangeResult ParallelizeLeaders(Document doc, View view, ICollection<ElementId> selectedIds)
        {
            var result = new ArrangeResult();
            var tags = CollectTags(doc, view, selectedIds);

            if (tags.Count < 2)
            {
                result.Message = "Cần ít nhất 2 tag có Leader để song song hóa.";
                return result;
            }

            // Lấy góc của tag đầu tiên làm reference
            var firstTag = tags[0];
            var firstRefs = firstTag.GetTaggedReferences();
            if (firstRefs == null || firstRefs.Count == 0)
            {
                result.Message = "Tag đầu tiên không có reference.";
                return result;
            }

            Reference firstRef = firstRefs.First();
            XYZ firstHead = firstTag.TagHeadPosition;
            XYZ firstEnd = firstTag.GetLeaderEnd(firstRef);
            XYZ firstElbow;

            try { firstElbow = firstTag.GetLeaderElbow(firstRef); }
            catch { firstElbow = null; }

            if (firstEnd == null)
            {
                result.Message = "Không đọc được leader end của tag đầu tiên.";
                return result;
            }

            // Tính vector hướng leader (từ end → elbow hoặc end → head)
            XYZ refTarget = firstElbow ?? firstHead;
            XYZ leaderDir = (refTarget - firstEnd).Normalize();

            using (var tx = new Transaction(doc, "Antigravity Parallelize Leaders"))
            {
                tx.Start();

                for (int i = 1; i < tags.Count; i++)
                {
                    var tag = tags[i];
                    try
                    {
                        if (!tag.HasLeader) { result.SkippedCount++; continue; }

                        var taggedRefs = tag.GetTaggedReferences();
                        if (taggedRefs == null || taggedRefs.Count == 0) { result.SkippedCount++; continue; }

                        Reference taggedRef = taggedRefs.First();
                        XYZ headPos = tag.TagHeadPosition;
                        XYZ leaderEnd = tag.GetLeaderEnd(taggedRef);
                        if (leaderEnd == null) { result.SkippedCount++; continue; }

                        // Tính elbow mới sao cho leader song song với reference
                        double dist = leaderEnd.DistanceTo(headPos) * 0.6;
                        XYZ newElbow = leaderEnd + leaderDir * dist;

                        tag.LeaderEndCondition = LeaderEndCondition.Free;
                        tag.SetLeaderElbow(taggedRef, newElbow);

                        // Đặt TagHead ngang với elbow (landing ngang)
                        tag.TagHeadPosition = new XYZ(headPos.X, newElbow.Y, headPos.Z);

                        result.MovedCount++;
                    }
                    catch (Exception ex)
                    {
                        result.SkippedCount++;
                        result.LogMessages.Add($"  Error {tag.Id}: {ex.Message}");
                    }
                    result.ProcessedCount++;
                }

                tx.Commit();
            }

            result.Message = $"Parallel Leaders: Đã chỉnh {result.MovedCount}/{tags.Count - 1} tag song song với tag đầu tiên.";
            return result;
        }

        /// <summary>
        /// Định dạng các leader line của các tag được chọn thành dạng trực giao (Landing nằm ngang).
        /// </summary>
        public static ArrangeResult FormatOrthogonalLeaders(Document doc, View view, ICollection<ElementId> selectedIds)
        {
            var result = new ArrangeResult();
            var tags = CollectTags(doc, view, selectedIds);

            if (tags.Count == 0)
            {
                result.Message = "Không tìm thấy Tag nào có Leader.";
                return result;
            }

            using (var tx = new Transaction(doc, "Antigravity Format Orthogonal Leaders"))
            {
                tx.Start();

                foreach (var tag in tags)
                {
                    try
                    {
                        if (!tag.HasLeader) { result.SkippedCount++; continue; }

                        var taggedRefs = tag.GetTaggedReferences();
                        if (taggedRefs == null || taggedRefs.Count == 0) { result.SkippedCount++; continue; }

                        Dictionary<Reference, XYZ> savedEnds = new Dictionary<Reference, XYZ>();
                        bool updated = false;
                        foreach (Reference r in taggedRefs)
                        {
                            XYZ leaderEnd = null;
                            try
                            {
                                leaderEnd = tag.GetLeaderEnd(r);
                            }
                            catch
                            {
                                var elem = doc.GetElement(r.ElementId);
                                if (elem != null) leaderEnd = AutoTagService.GetDefaultTagPosition(elem, view);
                            }
                            if (leaderEnd != null) savedEnds[r] = leaderEnd;
                        }

                        tag.LeaderEndCondition = LeaderEndCondition.Free;
                        XYZ headPos = tag.TagHeadPosition;

                        foreach (var kvp in savedEnds)
                        {
                            var r = kvp.Key;
                            var leaderEnd = kvp.Value;

                            tag.SetLeaderEnd(r, leaderEnd); // Bắt buộc Revit gán lại đúng điểm trên tường

                            double dx = Math.Abs(leaderEnd.X - headPos.X);
                            double dy = Math.Abs(leaderEnd.Y - headPos.Y);
                            
                            XYZ elbowPos;
                            if (dx > dy)
                            {
                                elbowPos = new XYZ(leaderEnd.X, headPos.Y, headPos.Z);
                            }
                            else
                            {
                                elbowPos = new XYZ(headPos.X, leaderEnd.Y, headPos.Z);
                            }
                            tag.SetLeaderElbow(r, elbowPos);
                            updated = true;
                        }

                        if (updated) result.MovedCount++;
                    }
                    catch (Exception ex)
                    {
                        result.SkippedCount++;
                        result.LogMessages.Add($"  Error {tag.Id}: {ex.Message}");
                    }
                    result.ProcessedCount++;
                }

                tx.Commit();
            }

            result.Message = $"Format Orthogonal Leaders: Đã chỉnh {result.MovedCount}/{tags.Count} tag.";
            return result;
        }

        /// <summary>
        /// Thu thập tất cả IndependentTag có leader từ selection hoặc view.
        /// </summary>
        private static List<IndependentTag> CollectTags(Document doc, View view, ICollection<ElementId> selectedIds)
        {
            IEnumerable<Element> elements;

            if (selectedIds != null && selectedIds.Count > 0)
            {
                elements = selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null);
            }
            else
            {
                elements = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType();
            }

            return elements
                .OfType<IndependentTag>()
                .Where(t => t.HasLeader)
                .ToList();
        }
    }
}
