using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.TagArranger.Models;
using Antigravity.TagArranger.Core;

namespace Antigravity.TagArranger.Services
{
    public enum AutoTagScope
    {
        ActiveView,
        SelectedElements,
        EntireProject
    }

    public static class AutoTagService
    {
        public static ArrangeResult Execute(Document doc, View view, ICollection<ElementId> selectedIds, AutoTagScope scope, BuiltInCategory category, ElementId tagTypeId, bool hasLeader, bool autoUntangle, double tagOffsetFeet, ArrangeOptions options, XYZ pickedPoint = null)
        {
            var result = new ArrangeResult();
            
            IList<Element> elements = new List<Element>();

            // Lấy các element theo Scope
            if (scope == AutoTagScope.SelectedElements)
            {
                if (selectedIds != null && selectedIds.Count > 0)
                {
                    elements = selectedIds.Select(id => doc.GetElement(id))
                        .Where(e => e != null && e.Category != null && (BuiltInCategory)e.Category.Id.Value == category)
                        .ToList();
                }
                
                if (elements.Count == 0)
                {
                    result.Message = "Không có đối tượng nào thuộc Category đã chọn trong danh sách bạn quét chọn.";
                    return result;
                }
            }
            else if (scope == AutoTagScope.EntireProject)
            {
                elements = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .OfCategory(category)
                    .ToElements();
            }
            else // ActiveView
            {
                elements = new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType()
                    .OfCategory(category)
                    .ToElements();
            }

            if (elements.Count == 0)
            {
                result.Message = "Không tìm thấy đối tượng nào thuộc category đã chọn trong View hiện tại.";
                return result;
            }

            // Tìm các element đã được tag trong view này
            var existingTags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            var taggedElementIds = new HashSet<ElementId>();
            foreach (var tag in existingTags)
            {
                try
                {
                    var refs = tag.GetTaggedReferences();
                    if (refs != null)
                    {
                        foreach (var r in refs)
                        {
                            taggedElementIds.Add(r.ElementId);
                        }
                    }
                }
                catch
                {
                    // Ignore lỗi nếu tag có vấn đề
                }
            }

            // Lọc ra các element chưa có tag
            var untaggedElements = elements.Where(e => !taggedElementIds.Contains(e.Id)).ToList();

            if (untaggedElements.Count == 0)
            {
                result.Message = "Tất cả đối tượng đã được tag.";
                return result;
            }

            // Sắp xếp các đối tượng theo toạ độ Y từ trên xuống dưới để tránh dính chéo Leader
            untaggedElements = untaggedElements.OrderByDescending(e => GetDefaultTagPosition(e, view).Y)
                                               .ThenBy(e => GetDefaultTagPosition(e, view).X)
                                               .ToList();

            List<ElementId> newTagIds = new List<ElementId>();
            int successCount = 0;

            using (var tx = new Transaction(doc, "Antigravity Auto Tag"))
            {
                tx.Start();

                List<IndependentTag> createdTags = new List<IndependentTag>();

                foreach (var element in untaggedElements)
                {
                    try
                    {
                        XYZ elementCenter;
                        var box = element.get_BoundingBox(view);
                        if (box != null)
                        {
                            elementCenter = (box.Min + box.Max) / 2;
                        }
                        else
                        {
                            var loc = element.Location as LocationPoint;
                            elementCenter = loc != null ? loc.Point : XYZ.Zero;
                        }

                        XYZ targetPosition;
                        if (pickedPoint != null)
                        {
                            // Tạm thời để ở XYZ.Zero, sẽ set lại vị trí sau khi gộp
                            targetPosition = XYZ.Zero;
                        }
                        else
                        {
                            if (element is Wall wall)
                            {
                                XYZ wallNormal = WallReferenceResolver.GetWallNormal(wall);
                                elementCenter = elementCenter + wallNormal * tagOffsetFeet;
                            }
                            targetPosition = elementCenter;
                        }

                        Reference reference = null;

                        if (element is Wall w)
                        {
                            reference = WallReferenceResolver.GetWallFaceReference(w, view, wantExterior: true);
                        }

                        if (reference == null)
                        {
                            reference = new Reference(element);
                        }

                        IndependentTag tag = null;
                        try
                        {
                            tag = IndependentTag.Create(doc, view.Id, reference, hasLeader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, targetPosition);
                        }
                        catch (Exception)
                        {
                            var fallbackRef = new Reference(element);
                            tag = IndependentTag.Create(doc, view.Id, fallbackRef, hasLeader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, targetPosition);
                        }

                        if (tag != null && tagTypeId != null && tagTypeId != ElementId.InvalidElementId)
                        {
                            tag.ChangeTypeId(tagTypeId);
                        }
                        
                        if (tag != null)
                        {
                            createdTags.Add(tag);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.LogMessages.Add($"Lỗi khi tag element {element.Id}: {ex.Message}");
                    }
                }

                if (pickedPoint != null && createdTags.Count > 0)
                {
                    // Quan trọng: Regenerate để đọc được TagText
                    doc.Regenerate();

                    var textGroupedTags = createdTags.GroupBy(t => t.TagText ?? Guid.NewGuid().ToString()).ToList();
                    List<List<IndependentTag>> finalClusters = new List<List<IndependentTag>>();

                    if (options.MaxMergeDistanceFeet <= 0.001)
                    {
                        foreach (var group in textGroupedTags)
                        {
                            finalClusters.Add(group.ToList());
                        }
                    }
                    else
                    {
                        foreach (var textGroup in textGroupedTags)
                        {
                            var tagsList = textGroup.ToList();
                            List<List<IndependentTag>> textClusters = new List<List<IndependentTag>>();
                            
                            foreach (var tag in tagsList)
                            {
                                var r = tag.GetTaggedReferences()?.FirstOrDefault();
                                if (r == null) continue;
                                var elem = doc.GetElement(r.ElementId);
                                if (elem == null) continue;
                                XYZ elemPos = GetDefaultTagPosition(elem, view);

                                bool foundCluster = false;
                                foreach (var cluster in textClusters)
                                {
                                    var firstTag = cluster.First();
                                    var firstRef = firstTag.GetTaggedReferences()?.FirstOrDefault();
                                    if (firstRef != null)
                                    {
                                        var firstElem = doc.GetElement(firstRef.ElementId);
                                        if (firstElem != null)
                                        {
                                            XYZ firstPos = GetDefaultTagPosition(firstElem, view);
                                            if (elemPos.DistanceTo(firstPos) <= options.MaxMergeDistanceFeet)
                                            {
                                                cluster.Add(tag);
                                                foundCluster = true;
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (!foundCluster)
                                {
                                    textClusters.Add(new List<IndependentTag> { tag });
                                }
                            }
                            finalClusters.AddRange(textClusters);
                        }
                    }

                    // Giảm estimatedTagHeight từ 8.0 xuống 4.0 (khoảng 4mm trên giấy ~ 400mm thực tế ở tỉ lệ 1:100) để các tag xếp gần nhau hơn
                    double estimatedTagHeight = (4.0 / 304.8) * view.Scale;
                    double step = options.MinSpacingFeet + estimatedTagHeight;

                    // Calculate centers for each cluster
                    var clusterCenters = new Dictionary<List<IndependentTag>, XYZ>();
                    double minX = double.MaxValue, maxX = double.MinValue;
                    double minY = double.MaxValue, maxY = double.MinValue;

                    foreach (var cluster in finalClusters)
                    {
                        if (cluster.Count == 0) continue;
                        double sumX = 0, sumY = 0, sumZ = 0;
                        int validCount = 0;
                        foreach (var tag in cluster)
                        {
                            var r = tag.GetTaggedReferences()?.FirstOrDefault();
                            if (r != null)
                            {
                                var elem = doc.GetElement(r.ElementId);
                                if (elem != null)
                                {
                                    XYZ pos = GetDefaultTagPosition(elem, view);
                                    sumX += pos.X;
                                    sumY += pos.Y;
                                    sumZ += pos.Z;
                                    validCount++;
                                }
                            }
                        }
                        
                        XYZ center = validCount > 0 
                            ? new XYZ(sumX / validCount, sumY / validCount, sumZ / validCount)
                            : pickedPoint;
                            
                        clusterCenters[cluster] = center;

                        if (center.X < minX) minX = center.X;
                        if (center.X > maxX) maxX = center.X;
                        if (center.Y < minY) minY = center.Y;
                        if (center.Y > maxY) maxY = center.Y;
                    }

                    bool isHorizontal = (maxX - minX) > (maxY - minY);

                    // Group clusters by proximity along the distribution axis to stack them if they overlap
                    List<List<List<IndependentTag>>> stackedGroups = new List<List<List<IndependentTag>>>();
                    foreach (var cluster in finalClusters)
                    {
                        if (cluster.Count == 0) continue;
                        var center = clusterCenters[cluster];
                        bool foundGroup = false;

                        foreach (var group in stackedGroups)
                        {
                            var groupCenter = clusterCenters[group.First()];
                            if (isHorizontal)
                            {
                                if (Math.Abs(center.X - groupCenter.X) < 3.0) // 3 feet ~ 900mm tolerance for stacking
                                {
                                    group.Add(cluster);
                                    foundGroup = true;
                                    break;
                                }
                            }
                            else
                            {
                                if (Math.Abs(center.Y - groupCenter.Y) < 3.0)
                                {
                                    group.Add(cluster);
                                    foundGroup = true;
                                    break;
                                }
                            }
                        }

                        if (!foundGroup)
                        {
                            stackedGroups.Add(new List<List<IndependentTag>> { cluster });
                        }
                    }

                    // Place tags
                    foreach (var group in stackedGroups)
                    {
                        double avgX = group.Average(c => clusterCenters[c].X);
                        double avgY = group.Average(c => clusterCenters[c].Y);

                        for (int i = 0; i < group.Count; i++)
                        {
                            var cluster = group[i];
                            var masterTag = cluster.First();

                            List<Reference> additionalRefs = new List<Reference>();
                            for (int j = 1; j < cluster.Count; j++)
                            {
                                var r = cluster[j].GetTaggedReferences()?.FirstOrDefault();
                                if (r != null) additionalRefs.Add(r);
                                doc.Delete(cluster[j].Id);
                            }

                            if (additionalRefs.Count > 0)
                            {
                                masterTag.AddReferences(additionalRefs);
                            }

                            XYZ finalPosition;
                            if (isHorizontal)
                            {
                                finalPosition = new XYZ(avgX, pickedPoint.Y - (i * step), pickedPoint.Z);
                            }
                            else
                            {
                                finalPosition = new XYZ(pickedPoint.X + (i * step * 2), avgY, pickedPoint.Z);
                            }

                            masterTag.TagHeadPosition = finalPosition;
                            newTagIds.Add(masterTag.Id);
                            successCount++;
                        }
                    }
                }
                else
                {
                    // Nếu không Pick Point, giữ nguyên
                    newTagIds.AddRange(createdTags.Select(t => t.Id));
                    successCount = createdTags.Count;
                }

                tx.Commit();
            }

            result.Message = $"Đã tạo {successCount} tags mới.";
            if (successCount == 0 && result.LogMessages.Count > 0)
            {
                result.Message += $"\nChi tiết lỗi: {result.LogMessages.First()}";
            }

            // Tự né nhau sau khi tag
            // Bỏ qua Anti-overlap nếu đã dùng Pick Point vì các tag đã được xếp chồng (stack) thẳng hàng
            if (autoUntangle && newTagIds.Count > 1 && pickedPoint == null)
            {
                // Cần Regenerate để Revit tính toán chính xác TagText và BoundingBox cho các Tag mới tạo
                using (var txRegen = new Transaction(doc, "Regenerate for AutoTag"))
                {
                    txRegen.Start();
                    doc.Regenerate();
                    txRegen.Commit();
                }
                
                // Gọi module Core để trích xuất AnnotationBox
                var boxes = AnnotationBoxExtractor.Extract(doc, view, newTagIds, options);
                var untangleResult = AntiOverlapService.Execute(doc, boxes, options.MinSpacingFeet);
                result.Message += $"\nĐã chạy Anti-overlap: di chuyển {untangleResult.MovedCount} tags đè nhau.";
                result.LogMessages.AddRange(untangleResult.LogMessages);
            }

            // Tự động bẻ góc vuông cho Leader (Orthogonal) nếu dùng Pick Point
            if (pickedPoint != null && hasLeader && newTagIds.Count > 0)
            {
                using (var txOrthogonal = new Transaction(doc, "Orthogonal Leaders"))
                {
                    txOrthogonal.Start();
                    doc.Regenerate(); // Cần cập nhật hình học để lấy chính xác LeaderEnd

                    foreach (var tagId in newTagIds)
                    {
                        try
                        {
                            if (doc.GetElement(tagId) is IndependentTag tag && tag.HasLeader)
                            {
                                var refs = tag.GetTaggedReferences();
                                if (refs != null && refs.Count > 0)
                                {
                                    Dictionary<Reference, XYZ> savedEnds = new Dictionary<Reference, XYZ>();
                                    foreach (var r in refs)
                                    {
                                        XYZ leaderEnd = null;
                                        try
                                        {
                                            leaderEnd = tag.GetLeaderEnd(r);
                                        }
                                        catch
                                        {
                                            var elem = doc.GetElement(r.ElementId);
                                            if (elem != null) leaderEnd = GetDefaultTagPosition(elem, view);
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

                                        if (options.AutoTagLeaderStyle == LeaderFormatStyle.RevitDefault)
                                        {
                                            // Do nothing, let Revit draw a straight diagonal line
                                            continue;
                                        }

                                        double dx = Math.Abs(leaderEnd.X - headPos.X);
                                        double dy = Math.Abs(leaderEnd.Y - headPos.Y);
                                        XYZ elbow;

                                        if (options.AutoTagLeaderStyle == LeaderFormatStyle.Angled)
                                        {
                                            double angleRad = (options.LeaderAngleDegrees * Math.PI) / 180.0;
                                            double dx_required = 0;
                                            if (Math.Tan(angleRad) > 0.001)
                                            {
                                                dx_required = dy / Math.Tan(angleRad);
                                            }

                                            if (dx_required > dx) dx_required = dx; // Cap it so it doesn't overshoot

                                            double elbowX = leaderEnd.X + Math.Sign(headPos.X - leaderEnd.X) * dx_required;
                                            elbow = new XYZ(elbowX, headPos.Y, headPos.Z);
                                        }
                                        else // Orthogonal
                                        {
                                            if (dx > dy)
                                            {
                                                elbow = new XYZ(leaderEnd.X, headPos.Y, headPos.Z);
                                            }
                                            else
                                            {
                                                elbow = new XYZ(headPos.X, leaderEnd.Y, headPos.Z);
                                            }
                                        }
                                        
                                        tag.SetLeaderElbow(r, elbow);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            result.LogMessages.Add($"Lỗi bẻ góc Tag {tagId}: {ex.Message}");
                        }
                    }
                    txOrthogonal.Commit();
                }
            }

            return result;
        }

        public static XYZ GetDefaultTagPosition(Element elem, View view)
        {
            // Thử lấy tâm BoundingBox trước, đây là vị trí an toàn nhất trên Plan view
            BoundingBoxXYZ bbox = elem.get_BoundingBox(view);
            if (bbox != null)
            {
                XYZ center = (bbox.Min + bbox.Max) / 2.0;
                // Giữ nguyên cao độ Z của view
                return new XYZ(center.X, center.Y, view.Origin.Z);
            }
            
            // Fallback: dùng Location
            Location loc = elem.Location;
            if (loc is LocationPoint lp)
            {
                return lp.Point;
            }
            if (loc is LocationCurve lc)
            {
                return lc.Curve.Evaluate(0.5, true);
            }

            return XYZ.Zero;
        }
    }
}
