using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.TagArranger.Core;
using Antigravity.TagArranger.Models;

namespace Antigravity.TagArranger.Services
{
    /// <summary>
    /// Loại thao tác mà EventHandler sẽ thực hiện.
    /// </summary>
    public enum ArrangeAction
    {
        AlignTop,
        AlignBottom,
        AlignLeft,
        AlignRight,
        AlignCenterH,
        AlignCenterV,
        AntiOverlap,
        DistributeH,
        DistributeV,
        StraightenLeaders,
        SetLeaderAngle,
        ParallelizeLeaders,
        OrthogonalLeaders,
        MergeTagsCenter,
        MergeTagsPickPoint,
        MergeTagsSmartStack,
        AutoTag,
        SmartStackLeft,
        SmartStackRight
    }

    /// <summary>
    /// IExternalEventHandler cho phép gọi Revit API từ WPF modeless window.
    /// </summary>
    public class ArrangerEventHandler : IExternalEventHandler
    {
        /// <summary>Hành động sẽ thực hiện khi event được raise.</summary>
        public ArrangeAction CurrentAction { get; set; }

        /// <summary>Tùy chọn cấu hình.</summary>
        public ArrangeOptions Options { get; set; } = new ArrangeOptions();

        /// <summary>Góc leader (độ) cho SetLeaderAngle.</summary>
        public double LeaderAngleDegrees { get; set; } = 45.0;

        /// <summary>Khoảng cách offset (feet) cho dóng hàng.</summary>
        public double AlignOffsetFeet { get; set; } = 0.0;

        /// <summary>Khoảng cách offset ngang (feet) cho Smart Stack.</summary>
        public double HorizontalOffsetFeet { get; set; } = 0.656; // ~200mm

        /// <summary>Chiều dài landing line (feet) cho StraightenLeaders.</summary>
        public double LandingDistanceFeet { get; set; } = 0.0328; // ~10mm

        /// <summary>Danh mục (Category) cho AutoTag.</summary>
        public BuiltInCategory AutoTagCategory { get; set; } = BuiltInCategory.OST_Walls;

        /// <summary>Loại (Type/Symbol) cho AutoTag.</summary>
        public ElementId AutoTagTypeId { get; set; } = ElementId.InvalidElementId;

        /// <summary>Tự động chống đè sau khi AutoTag.</summary>
        public bool AutoUntangle { get; set; } = true;

        /// <summary>Phạm vi cho Merge Tags.</summary>
        public MergeScope MergeScope { get; set; } = MergeScope.SelectedElements;

        /// <summary>Phạm vi (Scope) cho AutoTag.</summary>
        public AutoTagScope AutoTagScope { get; set; } = AutoTagScope.ActiveView;

        /// <summary>Khoảng cách (feet) đẩy chữ Tag ra xa tim đối tượng dọc theo pháp tuyến.</summary>
        public double AutoTagOffsetFeet { get; set; } = 3.0;

        /// <summary>Có tạo Leader cho AutoTag không.</summary>
        public bool AutoTagHasLeader { get; set; } = false;

        /// <summary>Sử dụng chế độ Pick Point cho AutoTag.</summary>
        public bool AutoTagPickPoint { get; set; } = false;

        /// <summary>Callback để trả kết quả về UI.</summary>
        public Action<ArrangeResult> OnCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                UIDocument uiDoc = app.ActiveUIDocument;
                if (uiDoc == null) return;

                Document doc = uiDoc.Document;
                View view = doc.ActiveView;

                if (view == null || view.IsTemplate || view.ViewType == ViewType.ThreeD)
                {
                    OnCompleted?.Invoke(new ArrangeResult { Message = "Vui lòng mở một View 2D (Plan/Section/Elevation)." });
                    return;
                }

                ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
                ArrangeResult result;
                
                // Quy đổi MinSpacingFeet (kích thước giấy) sang kích thước thực trong Model Space
                double modelSpacing = Options.MinSpacingFeet * view.Scale;
                // Nếu AlignOffsetFeet là khoảng cách thực trong model thì không cần nhân view.Scale.
                // Tuy nhiên, để cho chuẩn xác, Options truyền vào AntiOverlapService cần là modelSpacing
                
                // Tạm thời update Options cho AutoTagService (nếu AutoTagService dùng Options.MinSpacingFeet trực tiếp)
                double originalMinSpacing = Options.MinSpacingFeet;
                Options.MinSpacingFeet = modelSpacing;

                // Leader actions work directly with tags, not annotation boxes
                switch (CurrentAction)
                {
                    case ArrangeAction.StraightenLeaders:
                        result = LeaderService.StraightenLeaders(doc, view, selectedIds, LandingDistanceFeet);
                        break;
                    case ArrangeAction.SetLeaderAngle:
                        result = LeaderService.SetLeaderAngle(doc, view, selectedIds, LeaderAngleDegrees);
                        break;
                    case ArrangeAction.ParallelizeLeaders:
                        result = LeaderService.ParallelizeLeaders(doc, view, selectedIds);
                        break;
                    case ArrangeAction.OrthogonalLeaders:
                        result = LeaderService.FormatOrthogonalLeaders(doc, view, selectedIds);
                        break;
                    case ArrangeAction.MergeTagsCenter:
                        result = MergeTagService.Execute(app, selectedIds, MergePointMode.Center, MergeScope);
                        break;
                    case ArrangeAction.MergeTagsPickPoint:
                        result = MergeTagService.Execute(app, selectedIds, MergePointMode.PickPoint, MergeScope);
                        break;
                    case ArrangeAction.MergeTagsSmartStack:
                        result = MergeTagService.Execute(app, selectedIds, MergePointMode.SmartStack, MergeScope, Options.MinSpacingFeet);
                        break;
                    case ArrangeAction.AutoTag:
                        XYZ pickedPoint = null;
                        if (AutoTagPickPoint)
                        {
                            try
                            {
                                pickedPoint = uiDoc.Selection.PickPoint("Pick a starting point to place tags");
                            }
                            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                            {
                                OnCompleted?.Invoke(new ArrangeResult { Message = "Đã hủy chọn điểm." });
                                return;
                            }
                        }
                        result = AutoTagService.Execute(doc, view, selectedIds, AutoTagScope, AutoTagCategory, AutoTagTypeId, AutoTagHasLeader, AutoUntangle, AutoTagOffsetFeet, Options, pickedPoint);
                        break;
                    case ArrangeAction.SmartStackLeft:
                        result = SmartStackService.ExecuteSmartStack(doc, view, selectedIds, HorizontalOffsetFeet, false);
                        break;
                    case ArrangeAction.SmartStackRight:
                        result = SmartStackService.ExecuteSmartStack(doc, view, selectedIds, HorizontalOffsetFeet, true);
                        break;
                    default:
                    {
                        // Align/Distribute/AntiOverlap work with annotation boxes
                        var boxes = AnnotationBoxExtractor.Extract(doc, view, selectedIds, Options);
                        if (boxes.Count == 0)
                        {
                            OnCompleted?.Invoke(new ArrangeResult { Message = "Không tìm thấy Tag hoặc Dimension nào. Hãy chọn annotation trước." });
                            return;
                        }

                        switch (CurrentAction)
                        {
                            case ArrangeAction.AlignTop:
                                result = AlignService.Execute(doc, boxes, AlignMode.Top, AlignOffsetFeet);
                                break;
                            case ArrangeAction.AlignBottom:
                                result = AlignService.Execute(doc, boxes, AlignMode.Bottom, AlignOffsetFeet);
                                break;
                            case ArrangeAction.AlignLeft:
                                result = AlignService.Execute(doc, boxes, AlignMode.Left, AlignOffsetFeet);
                                break;
                            case ArrangeAction.AlignRight:
                                result = AlignService.Execute(doc, boxes, AlignMode.Right, AlignOffsetFeet);
                                break;
                            case ArrangeAction.AlignCenterH:
                                result = AlignService.Execute(doc, boxes, AlignMode.CenterHorizontal, AlignOffsetFeet);
                                break;
                            case ArrangeAction.AlignCenterV:
                                result = AlignService.Execute(doc, boxes, AlignMode.CenterVertical, AlignOffsetFeet);
                                break;
                            case ArrangeAction.AntiOverlap:
                                result = AntiOverlapService.Execute(doc, boxes, modelSpacing);
                                break;
                            case ArrangeAction.DistributeH:
                                result = DistributeService.Execute(doc, boxes, DistributeMode.Horizontal);
                                break;
                            case ArrangeAction.DistributeV:
                                result = DistributeService.Execute(doc, boxes, DistributeMode.Vertical);
                                break;
                            default:
                                result = new ArrangeResult { Message = "Unknown action." };
                                break;
                        }
                        break;
                    }
                }

                // Restore original spacing
                Options.MinSpacingFeet = originalMinSpacing;

                OnCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                OnCompleted?.Invoke(new ArrangeResult { Message = $"Lỗi: {ex.Message}" });
            }
        }

        public string GetName() => "Antigravity.TagArranger.EventHandler";
    }
}
