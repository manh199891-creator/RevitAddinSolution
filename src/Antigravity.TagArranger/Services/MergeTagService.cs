using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Antigravity.TagArranger.Models;

namespace Antigravity.TagArranger.Services
{
    public enum MergePointMode
    {
        Center,
        PickPoint,
        SmartStack
    }

    public enum MergeScope
    {
        SelectedElements,
        ActiveView
    }

    /// <summary>
    /// Service gộp nhiều IndependentTag cùng loại thành 1 Tag có nhiều đường dẫn (multi-leader).
    /// Hỗ trợ từ Revit 2022+ (qua phương thức AddReferences).
    /// </summary>
    public static class MergeTagService
    {
        public static ArrangeResult Execute(UIApplication app, ICollection<ElementId> selectedIds, MergePointMode mode, MergeScope scope, double spacingFeet = 0)
        {
            var result = new ArrangeResult();
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Lọc ra các IndependentTag
            List<IndependentTag> tags = new List<IndependentTag>();
            if (scope == MergeScope.ActiveView)
            {
                tags = new FilteredElementCollector(doc, doc.ActiveView.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType()
                    .Cast<IndependentTag>()
                    .ToList();
            }
            else
            {
                // Tags được chọn trực tiếp
                var explicitTags = selectedIds
                    .Select(id => doc.GetElement(id))
                    .OfType<IndependentTag>()
                    .ToList();

                // Các đối tượng Host (ví dụ Tường, Cửa,...) được chọn
                var hostIds = selectedIds
                    .Select(id => doc.GetElement(id))
                    .Where(e => e != null && !(e is IndependentTag))
                    .Select(e => e.Id)
                    .ToHashSet();

                if (hostIds.Count > 0)
                {
                    // Tìm tất cả các tag trong view hiện tại đang trỏ vào các Host này
                    var viewTags = new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .OfClass(typeof(IndependentTag))
                        .WhereElementIsNotElementType()
                        .Cast<IndependentTag>();

                    foreach (var tag in viewTags)
                    {
                        var taggedRefs = tag.GetTaggedReferences();
                        if (taggedRefs != null && taggedRefs.Any(r => hostIds.Contains(r.ElementId)))
                        {
                            if (!explicitTags.Any(t => t.Id == tag.Id))
                            {
                                explicitTags.Add(tag);
                            }
                        }
                    }
                }

                tags = explicitTags;
            }

            if (tags.Count < 2)
            {
                result.Message = "Cần chọn ít nhất 2 Tags để gộp.";
                return result;
            }

            // Nhóm theo Text của Tag (để tránh bị <varies> khi khác parameter) và hướng của Tag
            var tagGroups = tags.GroupBy(t => GetTagGroupKey(doc, uidoc.ActiveView, t))
                                .OrderBy(g => g.Key)
                                .ToList();

            XYZ startPointHorizontal = null;
            XYZ startPointVertical = null;

            if (mode == MergePointMode.SmartStack)
            {
                bool hasHorizontal = tagGroups.Any(g => g.Key.Contains("_H_"));
                bool hasVertical = tagGroups.Any(g => g.Key.Contains("_V_"));

                if (hasHorizontal)
                {
                    try
                    {
                        startPointHorizontal = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Click chọn vị trí điểm gốc cho cụm Tag chiều NGANG (Bấm ESC để bỏ qua chiều này)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                }

                if (hasVertical)
                {
                    try
                    {
                        startPointVertical = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Click chọn vị trí điểm gốc cho cụm Tag chiều DỌC (Bấm ESC để bỏ qua chiều này)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                }

                if (startPointHorizontal == null && startPointVertical == null)
                {
                    result.Message = "Đã hủy chọn cả 2 điểm Smart Stack.";
                    return result;
                }
            }

            using (var tx = new Transaction(doc, "Antigravity Merge Tags"))
            {
                tx.Start();

                int groupIndexHorizontal = 0;
                int groupIndexVertical = 0;
                foreach (var group in tagGroups)
                {
                    var groupTags = group.ToList();
                    if (groupTags.Count < 2) continue; // Bỏ qua nếu nhóm chỉ có 1 tag

                    // Lấy Tag đầu tiên làm Master
                    var masterTag = groupTags[0];
                    XYZ masterPos = masterTag.TagHeadPosition;

                    // Thu thập tất cả các references từ các tag khác
                    List<Reference> allRefsToAdd = new List<Reference>();
                    List<ElementId> tagsToDelete = new List<ElementId>();
                    List<XYZ> allPositions = new List<XYZ> { masterPos };
                    Dictionary<ElementId, XYZ> savedLeaderEnds = new Dictionary<ElementId, XYZ>();

                    // Lưu lại leader end của masterTag
                    var masterRefs = masterTag.GetTaggedReferences();
                    if (masterRefs != null)
                    {
                        foreach (var r in masterRefs)
                        {
                            if (masterTag.HasLeader)
                            {
                                try { savedLeaderEnds[r.ElementId] = masterTag.GetLeaderEnd(r); } catch { }
                            }
                            else
                            {
                                // Fix Root Cause: Nếu tag không có Leader, vị trí của chữ chính là điểm đặt trên cấu kiện
                                savedLeaderEnds[r.ElementId] = masterTag.TagHeadPosition;
                            }
                        }
                    }

                    for (int i = 1; i < groupTags.Count; i++)
                    {
                        var tag = groupTags[i];
                        allPositions.Add(tag.TagHeadPosition);
                        
                        var refs = tag.GetTaggedReferences();
                        if (refs != null)
                        {
                            foreach (var r in refs)
                            {
                                allRefsToAdd.Add(r);
                                if (tag.HasLeader)
                                {
                                    try { savedLeaderEnds[r.ElementId] = tag.GetLeaderEnd(r); } catch { }
                                }
                                else
                                {
                                    // Fix Root Cause: Nếu tag không có Leader, vị trí của chữ chính là điểm đặt trên cấu kiện
                                    savedLeaderEnds[r.ElementId] = tag.TagHeadPosition;
                                }
                            }
                        }
                        
                        tagsToDelete.Add(tag.Id);
                    }

                    // Xóa các tag cũ
                    foreach (var id in tagsToDelete)
                    {
                        try { doc.Delete(id); } catch { }
                    }

                    // Xác định điểm đặt MasterTag
                    XYZ targetPoint = masterPos;
                    if (mode == MergePointMode.Center)
                    {
                        double avgX = allPositions.Average(p => p.X);
                        double avgY = allPositions.Average(p => p.Y);
                        targetPoint = new XYZ(avgX, avgY, masterPos.Z);
                    }
                    else if (mode == MergePointMode.PickPoint)
                    {
                        try
                        {
                            targetPoint = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Click chọn vị trí điểm tụ cho các Tag được gộp (Bấm ESC để bỏ qua)");
                            targetPoint = new XYZ(targetPoint.X, targetPoint.Y, masterPos.Z);
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            result.LogMessages.Add("Hủy chọn điểm, giữ nguyên vị trí tag chính.");
                        }
                    }
                    else if (mode == MergePointMode.SmartStack)
                    {
                        bool isHorizontal = group.Key.Contains("_H_");
                        if (isHorizontal && startPointHorizontal != null)
                        {
                            double offsetY = spacingFeet * groupIndexHorizontal;
                            targetPoint = new XYZ(startPointHorizontal.X, startPointHorizontal.Y - offsetY, masterPos.Z);
                            groupIndexHorizontal++;
                        }
                        else if (!isHorizontal && startPointVertical != null)
                        {
                            double offsetY = spacingFeet * groupIndexVertical;
                            targetPoint = new XYZ(startPointVertical.X, startPointVertical.Y - offsetY, masterPos.Z);
                            groupIndexVertical++;
                        }
                        // Nếu user nhấn ESC cho chiều này, targetPoint vẫn giữ nguyên là masterPos (Merge tại chỗ)
                    }

                    // Di chuyển MasterTag đến điểm mới TRƯỚC khi add references và set leader end
                    masterTag.TagHeadPosition = targetPoint;

                    // Thêm references vào MasterTag và khôi phục LeaderEnd
                    if (allRefsToAdd.Count > 0)
                    {
                        try
                        {
                            masterTag.AddReferences(allRefsToAdd);
                        }
                        catch (Exception ex)
                        {
                            result.LogMessages.Add($"Lỗi khi gộp tag {masterTag.Id}: {ex.Message}");
                        }
                    }

                    masterTag.HasLeader = true; // Bật hiển thị Leader
                    masterTag.LeaderEndCondition = LeaderEndCondition.Free;
                    
                    // Cực kỳ quan trọng: Phải Regenerate sau khi thêm references thì SetLeaderEnd mới không bị Revit ghi đè
                    doc.Regenerate();

                    // Phục hồi lại toàn bộ LeaderEnd để trỏ đúng vị trí ban đầu
                    var allCurrentRefs = masterTag.GetTaggedReferences();
                    if (allCurrentRefs != null)
                    {
                        foreach (var r in allCurrentRefs)
                        {
                            if (savedLeaderEnds.TryGetValue(r.ElementId, out XYZ endPos))
                            {
                                try 
                                { 
                                    masterTag.SetLeaderEnd(r, endPos); 

                                    // Fix: Revit ném lỗi nếu Elbow nằm CHÍNH XÁC trên đường thẳng (vì nó cho rằng như vậy là xoá Elbow)
                                    // Cách giải quyết: Tính trung điểm và lệch đi một khoảng siêu nhỏ (0.001 feet ~ 0.3mm)
                                    XYZ mid = (targetPoint + endPos) / 2.0;
                                    XYZ dir = (endPos - targetPoint).Normalize();
                                    XYZ perp = new XYZ(-dir.Y, dir.X, 0).Normalize();
                                    XYZ newElbow = mid + perp * 0.001;

                                    try { masterTag.SetLeaderElbow(r, newElbow); } 
                                    catch (Exception ex) { result.LogMessages.Add($"Lỗi SetElbow {r.ElementId}: {ex.Message}"); }
                                } 
                                catch (Exception ex) 
                                { 
                                    result.LogMessages.Add($"Lỗi phục hồi Leader {r.ElementId}: {ex.Message}");
                                }
                            }
                        }
                    }

                    result.MovedCount += tagsToDelete.Count + 1;
                    result.ProcessedCount++;
                }

                tx.Commit();
            }

            result.Message = $"Merge Tags: Gộp thành công {result.MovedCount} tags thành {result.ProcessedCount} multi-leader tags.";
            return result;
        }

        private static string GetTagGroupKey(Document doc, View view, IndependentTag tag)
        {
            string text = tag.TagText ?? "Unknown";
            string direction = "UnknownDir";
            
            try
            {
                var refs = tag.GetTaggedReferences();
                if (refs != null && refs.Count > 0)
                {
                    var elem = doc.GetElement(refs.FirstOrDefault().ElementId);
                    if (elem is Wall wall && wall.Location is LocationCurve lc)
                    {
                        var curveDir = (lc.Curve.GetEndPoint(1) - lc.Curve.GetEndPoint(0)).Normalize();
                        // Tường ngang -> X lớn hơn Y
                        bool isHorizontalWall = Math.Abs(curveDir.X) > Math.Abs(curveDir.Y);
                        direction = isHorizontalWall ? "H_Wall" : "V_Wall";
                    }
                    else
                    {
                        XYZ head = tag.TagHeadPosition;
                        XYZ target = head;
                        if (tag.HasLeader)
                        {
                            target = tag.GetLeaderEnd(refs.FirstOrDefault());
                        }
                        else if (elem != null)
                        {
                            var box = elem.get_BoundingBox(view);
                            if (box != null) target = (box.Min + box.Max) / 2;
                        }
                        
                        XYZ dir = target - head;
                        bool isVerticalLeader = Math.Abs(dir.Y) > Math.Abs(dir.X);
                        direction = isVerticalLeader ? "V_Leader" : "H_Leader";
                    }
                }
            }
            catch { }

            return $"{text}_{direction}";
        }
    }
}
