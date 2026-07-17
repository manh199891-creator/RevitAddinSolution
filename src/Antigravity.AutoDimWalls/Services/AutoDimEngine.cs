using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.AutoDimWalls.Core;
using Antigravity.AutoDimWalls.Models;

namespace Antigravity.AutoDimWalls.Services
{
    public sealed class AutoDimEngine
    {
        private const string CreatedByMarker = "Antigravity.AutoDimWalls.AutoDimWalls";

        private readonly UIDocument _uiDoc;
        private readonly Document _doc;

        public AutoDimEngine(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
            _doc = uiDoc.Document;
        }

        public AutoDimResult CreateDimensions(AutoDimOptions options)
        {
            var result = new AutoDimResult();
            View view = _doc.ActiveView;

            result.LogMessages.Add($"--- BẮT ĐẦU CHẨN ĐOÁN AUTODIM ---");
            result.LogMessages.Add($"Active View: '{view?.Name}' (Type: {view?.ViewType})");
            result.LogMessages.Add($"Scope: {options.Scope}");

            var selectedIds = _uiDoc.Selection.GetElementIds();
            result.LogMessages.Add($"Số lượng phần tử đang chọn trong Revit: {selectedIds.Count}");
            foreach (var id in selectedIds)
            {
                var el = _doc.GetElement(id);
                result.LogMessages.Add($"  - ID: {id.Value}, Type: {el?.GetType().Name}, Category: {el?.Category?.Name}, Name: '{el?.Name}'");
            }

            if (view == null || view.IsTemplate || view.ViewType == ViewType.ThreeD)
            {
                result.Message = "Run AutoDim in a 2D plan view or section view.";
                return result;
            }

            if (options.IsSectionView)
            {
                if (view.ViewType != ViewType.Section && view.ViewType != ViewType.Elevation)
                {
                    result.Message = "Chế độ Mặt cắt (Section View) chỉ hỗ trợ chạy trên View Mặt cắt/Mặt đứng (Section/Elevation).";
                    return result;
                }
                return CreateVerticalDimensions(options);
            }
            else
            {
                if (view.ViewType != ViewType.FloorPlan && 
                    view.ViewType != ViewType.CeilingPlan && 
                    view.ViewType != ViewType.EngineeringPlan && 
                    view.ViewType != ViewType.AreaPlan)
                {
                    result.Message = "Chế độ Mặt bằng (Plan View) chỉ hỗ trợ chạy trên View Mặt bằng (Floor/Structural/Reflected Ceiling Plan).\n\nĐể chạy trên Mặt cắt, vui lòng chuyển đổi sang 'Section View' trên giao diện!";
                    return result;
                }
            }

            IList<Wall> walls = CollectWalls(options.Scope, view, result.LogMessages);
            if (walls.Count == 0)
            {
                result.Message = "No straight walls found for the selected scope.";
                return result;
            }

            var groups = GroupCollinearWalls(walls, result.LogMessages);

            using (var transaction = new Transaction(_doc, "BimTools AutoDim Walls"))
            {
                transaction.Start();

                foreach (var group in groups)
                {
                    try
                    {
                        var firstInfo = group.WallInfos[0];
                        XYZ consensusDir = group.Direction;
                        XYZ consensusNormal = group.Normal;
                        XYZ origin = firstInfo.Start;

                        double minProj = double.MaxValue;
                        double maxProj = double.MinValue;

                        foreach (var wi in group.WallInfos)
                        {
                            double pStart = (wi.Start - origin).DotProduct(consensusDir);
                            double pEnd = (wi.End - origin).DotProduct(consensusDir);

                            minProj = Math.Min(minProj, Math.Min(pStart, pEnd));
                            maxProj = Math.Max(maxProj, Math.Max(pStart, pEnd));
                        }

                        XYZ groupStart = origin + minProj * consensusDir;
                        XYZ groupEnd = origin + maxProj * consensusDir;
                        Line combinedLine = Line.CreateBound(groupStart, groupEnd);

                        var groupInfo = new StraightWallInfo
                        {
                            Wall = firstInfo.Wall,
                            LocationLine = combinedLine,
                            Direction = consensusDir,
                            Normal = consensusNormal,
                            Start = groupStart,
                            End = groupEnd,
                            WidthFeet = group.WallInfos.Max(wi => wi.WidthFeet)
                        };

                        double offsetFeet = WallGeometryUtils.ResolveOffsetFeet(groupInfo, options.OffsetMm, options.PickedPoint);
                        int createdForGroup = CreateGroupDimensions(view, group, groupInfo, options, offsetFeet, result.LogMessages);

                        if (createdForGroup > 0)
                        {
                            result.WallsProcessed += group.Walls.Count;
                            result.DimensionsCreated += createdForGroup;
                        }
                        else
                        {
                            result.WallsSkipped += group.Walls.Count;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.WallsSkipped += group.Walls.Count;
                        result.LogMessages.Add($"[LỖI] Nhóm tường collinear bị lỗi khi tạo Dim: {ex.Message}");
                    }
                }

                transaction.Commit();
            }

            result.Message = $"Created {result.DimensionsCreated} dimensions for {result.WallsProcessed} walls. Skipped {result.WallsSkipped}.";
            return result;
        }

        public AutoDimResult DeleteCreatedDimensions()
        {
            var result = new AutoDimResult();
            View view = _doc.ActiveView;
            if (view == null)
                return result;

            var ids = new FilteredElementCollector(_doc, view.Id)
                .OfClass(typeof(Dimension))
                .OfType<Dimension>()
                .Where(IsCreatedByAutoDim)
                .Select(d => d.Id)
                .ToList();

            if (ids.Count == 0)
            {
                result.Message = "No AutoDim dimensions found in the active view.";
                return result;
            }

            using (var transaction = new Transaction(_doc, "Delete BimTools AutoDim Walls"))
            {
                transaction.Start();
                _doc.Delete(ids);
                transaction.Commit();
            }

            result.DimensionsDeleted = ids.Count;
            result.Message = $"Deleted {ids.Count} AutoDim dimensions from the active view.";
            return result;
        }

        private int CreateGroupDimensions(
            View view,
            CollinearWallGroup group,
            StraightWallInfo groupInfo,
            AutoDimOptions options,
            double offsetFeet,
            List<string> logs)
        {
            int created = 0;
            var references = new List<ReferenceInfo>();

            // 1. End faces of each wall in the group
            foreach (Wall w in group.Walls)
            {
                var wallInfoOfW = group.WallInfos.First(wi => wi.Wall.Id == w.Id);
                var endRefs = WallGeometryUtils.GetEndFaceReferences(w, groupInfo, view, logs);
                if (endRefs.Count == 2)
                {
                    double paramStart = (wallInfoOfW.Start - groupInfo.Start).DotProduct(groupInfo.Direction);
                    double paramEnd = (wallInfoOfW.End - groupInfo.Start).DotProduct(groupInfo.Direction);
                    
                    references.Add(new ReferenceInfo { Reference = endRefs[0], Parameter = paramStart });
                    references.Add(new ReferenceInfo { Reference = endRefs[1], Parameter = paramEnd });
                }
            }

            // 2. Openings in each wall
            if (options.IncludeOpenings)
            {
                foreach (Wall w in group.Walls)
                {
                    var wallInfoOfW = group.WallInfos.First(wi => wi.Wall.Id == w.Id);
                    var openingRefs = OpeningReferenceResolver.GetOpeningReferences(_doc, w, wallInfoOfW, view, options.OpeningMode, logs);
                    double wallOffsetInGroup = (wallInfoOfW.Start - groupInfo.Start).DotProduct(groupInfo.Direction);

                    foreach (var opRef in openingRefs)
                    {
                        references.Add(new ReferenceInfo 
                        { 
                            Reference = opRef.Reference, 
                            Parameter = opRef.Parameter + wallOffsetInGroup 
                        });
                    }
                }
            }

            // 3. Host columns (scanned along the entire group span, excluding collinear walls)
            if (options.IncludeHostStructural)
            {
                var excludeIds = group.Walls.Select(w => w.Id).ToList();
                var hostFaces = HostStructuralResolver.GetHostIntersectingFaces(_doc, view, groupInfo, excludeIds, logs);
                foreach (var hf in hostFaces)
                {
                    references.Add(new ReferenceInfo { Reference = hf.Reference, Parameter = hf.DistanceFromStart });
                }
            }

            // 4. Linked columns (scanned along the entire group span)
            if (options.IncludeLinks)
            {
                var linkFaces = LinkResolver.GetLinkedIntersectingFaces(_doc, view, groupInfo, options.SelectedLinkInstanceId, logs);
                foreach (var lf in linkFaces)
                {
                    references.Add(new ReferenceInfo { Reference = lf.Reference, Parameter = lf.DistanceFromStart });
                }
            }

            // 5. Grids (scanned along the entire group span)
            if (options.IncludeGrids)
            {
                references.AddRange(IntersectionResolver.GetGridReferences(_doc, view, groupInfo, options.IncludeLinks, options.SelectedLinkInstanceId, logs));
            }

            // Create combined dimension line
            if (references.Count > 1)
            {
                if (TryCreateDimension(view, references, WallGeometryUtils.CreateDimensionLine(groupInfo, offsetFeet), options.DimensionType, groupInfo, logs))
                    created++;
            }

            return created;
        }

        private bool TryCreateDimension(
            View view,
            IEnumerable<ReferenceInfo> references,
            Line line,
            DimensionType dimensionType,
            StraightWallInfo wallInfo,
            List<string> logs)
        {
            // 1. Sort references by their parameter (coordinate along the wall)
            var sorted = references
                .Where(r => r != null && r.Reference != null)
                .OrderBy(r => r.Parameter)
                .ToList();

            // 2. Eliminate duplicates closer than 0.01 feet (~3mm) to prevent 0-dim segments
            var unique = new List<ReferenceInfo>();
            foreach (var info in sorted)
            {
                if (unique.Count == 0)
                {
                    unique.Add(info);
                }
                else
                {
                    double diff = Math.Abs(info.Parameter - unique[unique.Count - 1].Parameter);
                    if (diff > 0.01) // > 3mm
                    {
                        unique.Add(info);
                    }
                    else
                    {
                        logs?.Add($"[AutoDimEngine] Removed coplanar/duplicate reference at {info.Parameter:F3}ft (diff: {diff:F4}ft)");
                    }
                }
            }

            if (unique.Count < 2)
                return false;

            var referenceArray = new ReferenceArray();
            foreach (ReferenceInfo info in unique)
                referenceArray.Append(info.Reference);

            try
            {
                Dimension dimension = dimensionType != null
                    ? _doc.Create.NewDimension(view, line, referenceArray, dimensionType)
                    : _doc.Create.NewDimension(view, line, referenceArray);

                if (dimension != null)
                {
                    MarkCreatedDimension(dimension);
                    // Regenerate the document so Revit fully computes the newly created dimension's segments, values, and origins!
                    _doc.Regenerate();
                    AdjustOverlappingSegmentTexts(dimension, wallInfo, false, logs);
                }

                return dimension != null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Revit NewDimension failed: {ex.Message}. Reference Count: {unique.Count}.", ex);
            }
        }

        private static void AdjustOverlappingSegmentTexts(
            Dimension dimension,
            StraightWallInfo wallInfo,
            bool isVertical,
            List<string> logs)
        {
            try
            {
                if (dimension.NumberOfSegments <= 1)
                    return;

                XYZ shiftDir = isVertical ? XYZ.BasisZ : wallInfo.Direction;
                double wallLength = isVertical ? 100.0 : wallInfo.LocationLine.Length;

                // Threshold for a narrow segment: 260mm (~0.85 feet)
                double narrowThreshold = 0.85; 
                double shiftDistance = 1.3; // ~400mm offset to place outside beautifully

                var segments = new List<DimensionSegment>();
                foreach (DimensionSegment seg in dimension.Segments)
                {
                    segments.Add(seg);
                }

                logs?.Add($"[AutoDimEngine] Adjusting texts for {segments.Count} segments (Vertical={isVertical})...");

                for (int i = 0; i < segments.Count; i++)
                {
                    DimensionSegment seg = segments[i];
                    if (seg.Value == null)
                    {
                        logs?.Add($"  Segment {i}: Value is null!");
                        continue;
                    }

                    double val = seg.Value.Value;
                    if (val < narrowThreshold)
                    {
                        if (seg.IsTextPositionAdjustable())
                        {
                            XYZ currentTextPos = seg.TextPosition;
                            XYZ centerPoint = seg.Origin;
                            
                            double directionSign = 1.0;
                            if (isVertical)
                            {
                                directionSign = (i % 2 == 0) ? -1.0 : 1.0;
                            }
                            else
                            {
                                double t_center = (centerPoint - wallInfo.Start).DotProduct(shiftDir);
                                directionSign = (t_center < wallLength * 0.5) ? -1.0 : 1.0;
                                if (i > 0 && segments[i-1].Value != null && segments[i-1].Value.Value < narrowThreshold)
                                {
                                    directionSign = -directionSign;
                                }
                            }

                            XYZ newTextPos = currentTextPos + shiftDir * (directionSign * shiftDistance);
                            seg.TextPosition = newTextPos;
                            logs?.Add($"  Segment {i} ({val*304.8:F1}mm): Shifted text position by {directionSign * shiftDistance:F2}ft along {(isVertical ? "Z" : "Wall")}");
                        }
                        else
                        {
                            logs?.Add($"  Segment {i} ({val*304.8:F1}mm): Text position is NOT adjustable!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logs?.Add($"[AutoDimEngine] AdjustOverlappingSegmentTexts failed: {ex.Message}");
            }
        }

        private AutoDimResult CreateVerticalDimensions(AutoDimOptions options)
        {
            var result = new AutoDimResult();
            View view = _doc.ActiveView;

            IList<Wall> walls = CollectWalls(options.Scope, view, result.LogMessages);
            if (walls.Count == 0)
            {
                result.Message = "No straight walls found for the selected scope in this section view.";
                return result;
            }

            using (var transaction = new Transaction(_doc, "BimTools AutoDim Section Walls"))
            {
                transaction.Start();

                foreach (Wall wall in walls)
                {
                    if (!WallGeometryUtils.TryGetStraightWallInfo(wall, result.LogMessages, out StraightWallInfo wallInfo))
                    {
                        result.WallsSkipped++;
                        continue;
                    }

                    try
                    {
                        int createdForWall = CreateVerticalWallDimensions(view, wall, wallInfo, options, result.LogMessages);

                        if (createdForWall > 0)
                        {
                            result.WallsProcessed++;
                            result.DimensionsCreated += createdForWall;
                        }
                        else
                        {
                            result.WallsSkipped++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.WallsSkipped++;
                        result.LogMessages.Add($"[LỖI] Tường ID={wall.Id.Value} bị lỗi khi tạo Dim mặt cắt: {ex.Message}");
                    }
                }

                transaction.Commit();
            }

            result.Message = $"Created {result.DimensionsCreated} vertical dimensions for {result.WallsProcessed} walls. Skipped {result.WallsSkipped}.";
            return result;
        }

        private int CreateVerticalWallDimensions(
            View view,
            Wall wall,
            StraightWallInfo wallInfo,
            AutoDimOptions options,
            List<string> logs)
        {
            int created = 0;
            var references = new List<ReferenceInfo>();

            // 1. Core wall height, sill, and lintel references
            var wallRefs = SectionGeometryResolver.GetVerticalReferences(wall, view, logs);
            references.AddRange(wallRefs);

            // 2. Intersecting structural framing (Beams/Lintels)
            if (options.IncludeHostStructural)
            {
                var beamRefs = SectionGeometryResolver.GetIntersectingBeamReferences(_doc, view, wallInfo, logs);
                references.AddRange(beamRefs);
            }

            // 3. Intersecting concrete slabs (Floors)
            if (options.IncludeGrids) // Reuse the grids checkbox for floors in section mode
            {
                var floorRefs = SectionGeometryResolver.GetIntersectingFloorReferences(_doc, view, wallInfo, logs);
                references.AddRange(floorRefs);
            }

            if (references.Count > 1)
            {
                XYZ wallCenter = (wallInfo.Start + wallInfo.End) * 0.5;
                double offsetFeet = UnitUtils.ConvertToInternalUnits(Math.Max(100.0, options.OffsetMm), UnitTypeId.Millimeters);

                if (options.PickedPoint != null)
                {
                    XYZ vector = options.PickedPoint - wallCenter;
                    double signed = vector.DotProduct(view.RightDirection);
                    if (Math.Abs(signed) > 0.01)
                        offsetFeet = signed;
                }

                XYZ dimLineOrigin = wallCenter + offsetFeet * view.RightDirection;
                Line line = Line.CreateBound(dimLineOrigin, dimLineOrigin + XYZ.BasisZ * 10.0);

                if (TryCreateVerticalDimension(view, references, line, options.DimensionType, wallInfo, logs))
                    created++;
            }

            return created;
        }

        private bool TryCreateVerticalDimension(
            View view,
            IEnumerable<ReferenceInfo> references,
            Line line,
            DimensionType dimensionType,
            StraightWallInfo wallInfo,
            List<string> logs)
        {
            var sorted = references
                .Where(r => r != null && r.Reference != null)
                .OrderBy(r => r.Parameter)
                .ToList();

            var unique = new List<ReferenceInfo>();
            foreach (var info in sorted)
            {
                if (unique.Count == 0)
                {
                    unique.Add(info);
                }
                else
                {
                    double diff = Math.Abs(info.Parameter - unique[unique.Count - 1].Parameter);
                    if (diff > 0.01)
                    {
                        unique.Add(info);
                    }
                    else
                    {
                        logs?.Add($"[AutoDimEngine] Removed coplanar/duplicate reference at Z={info.Parameter*304.8:F1}mm");
                    }
                }
            }

            if (unique.Count < 2)
                return false;

            var referenceArray = new ReferenceArray();
            foreach (ReferenceInfo info in unique)
                referenceArray.Append(info.Reference);

            try
            {
                Dimension dimension = dimensionType != null
                    ? _doc.Create.NewDimension(view, line, referenceArray, dimensionType)
                    : _doc.Create.NewDimension(view, line, referenceArray);

                if (dimension != null)
                {
                    MarkCreatedDimension(dimension);
                    _doc.Regenerate();
                    AdjustOverlappingSegmentTexts(dimension, wallInfo, true, logs);
                }

                return dimension != null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Revit NewDimension failed: {ex.Message}. Reference Count: {unique.Count}.", ex);
            }
        }

        private IList<Wall> CollectWalls(AutoDimScope scope, View view, List<string> logs)
        {
            IEnumerable<Element> elements;
            if (scope == AutoDimScope.Selection)
            {
                var selIds = _uiDoc.Selection.GetElementIds();
                logs?.Add($"[CollectWalls] Phạm vi: Selection (Chọn đối tượng). Số phần tử chọn: {selIds.Count}");
                if (selIds.Count > 0)
                {
                    elements = selIds
                        .Select(id => _doc.GetElement(id))
                        .Where(e => e != null);
                }
                else
                {
                    elements = Array.Empty<Element>();
                }
            }
            else
            {
                elements = new FilteredElementCollector(_doc, view.Id)
                    .OfClass(typeof(Wall))
                    .WhereElementIsNotElementType();
                logs?.Add($"[CollectWalls] Phạm vi: ActiveView (Toàn bộ view). Tìm thấy trong view: {elements.Count()} tường");
            }

            var wallsList = new List<Wall>();
            foreach (var el in elements)
            {
                if (el is Wall wall)
                {
                    if (WallGeometryUtils.TryGetStraightWallInfo(wall, logs, out _))
                    {
                        wallsList.Add(wall);
                    }
                    else
                    {
                        logs?.Add($"Tường ID={wall.Id.Value} bị loại bỏ trong TryGetStraightWallInfo");
                    }
                }
                else
                {
                    logs?.Add($"Đối tượng ID={el?.Id.Value} (Loại: {el?.GetType().Name}) không phải là Tường.");
                }
            }

            logs?.Add($"[CollectWalls] Kết quả: Thu thập được {wallsList.Count} tường thẳng hợp lệ.");
            return wallsList;
        }

        private static string GetStableKey(Document doc, Reference reference)
        {
            try
            {
                return reference.ConvertToStableRepresentation(doc);
            }
            catch
            {
                return reference.ElementId.Value + ":" + reference.ElementReferenceType;
            }
        }

        private static void MarkCreatedDimension(Dimension dimension)
        {
            Parameter comments = dimension?.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments != null && !comments.IsReadOnly)
                comments.Set(CreatedByMarker);
        }

        private static bool IsCreatedByAutoDim(Dimension dimension)
        {
            return dimension
                .get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
                ?.AsString() == CreatedByMarker;
        }

        private class CollinearWallGroup
        {
            public XYZ Direction { get; set; }
            public XYZ Normal { get; set; }
            public List<Wall> Walls { get; } = new List<Wall>();
            public List<StraightWallInfo> WallInfos { get; } = new List<StraightWallInfo>();
        }

        private List<CollinearWallGroup> GroupCollinearWalls(IList<Wall> walls, List<string> logs)
        {
            var groups = new List<CollinearWallGroup>();

            foreach (Wall wall in walls)
            {
                if (!WallGeometryUtils.TryGetStraightWallInfo(wall, logs, out StraightWallInfo wallInfo))
                    continue;

                bool added = false;
                foreach (var group in groups)
                {
                    // 1. Parallel check
                    double dot = Math.Abs(wallInfo.Direction.DotProduct(group.Direction));
                    if (dot > 0.999) // Parallel
                    {
                        // 2. Distance to line check
                        XYZ vec = wallInfo.Start - group.WallInfos[0].Start;
                        double perpDist = vec.CrossProduct(group.Direction).GetLength();
                        if (perpDist < 0.16) // Less than 50mm (~2 inches) perpendicular distance
                        {
                            group.Walls.Add(wall);
                            group.WallInfos.Add(wallInfo);
                            added = true;
                            break;
                        }
                    }
                }

                if (!added)
                {
                    var newGroup = new CollinearWallGroup();
                    newGroup.Direction = wallInfo.Direction;
                    newGroup.Normal = wallInfo.Normal;
                    newGroup.Walls.Add(wall);
                    newGroup.WallInfos.Add(wallInfo);
                    groups.Add(newGroup);
                }
            }

            logs?.Add($"[GroupCollinearWalls] Grouped {walls.Count} walls into {groups.Count} collinear groups.");
            return groups;
        }
    }
}
