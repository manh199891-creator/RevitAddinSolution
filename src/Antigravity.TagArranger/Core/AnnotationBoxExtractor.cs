using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Antigravity.TagArranger.Models;

namespace Antigravity.TagArranger.Core
{
    /// <summary>
    /// Trích xuất bounding box 2D cho các annotation element trong Revit view.
    /// B1-FIX: Ưu tiên dùng get_BoundingBox(view) thực tế từ Revit API (chính xác).
    /// Chỉ fallback về ước lượng khi BoundingBox không khả dụng (tag chưa được regenerate).
    /// </summary>
    public static class AnnotationBoxExtractor
    {
        // Hệ số ước lượng chiều rộng ký tự (tỷ lệ với TEXT_SIZE)
        private const double CharWidthRatio = 0.55;
        // Chiều rộng mặc định nếu không đọc được (feet) ~25mm
        private const double DefaultTextWidth = 0.082;
        // Chiều cao mặc định nếu không đọc được (feet) ~2.5mm
        private const double DefaultTextHeight = 0.008;

        /// <summary>
        /// Thu thập tất cả AnnotationBox từ các element đã chọn hoặc toàn bộ view.
        /// </summary>
        public static List<AnnotationBox> Extract(Document doc, View view, ICollection<ElementId> selectedIds, ArrangeOptions options)
        {
            var result = new List<AnnotationBox>();

            IEnumerable<Element> elements;
            if (selectedIds != null && selectedIds.Count > 0)
            {
                elements = selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null);
            }
            else
            {
                elements = new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType()
                    .ToElements();
            }

            foreach (Element elem in elements)
            {
                if (options.IncludeTags && elem is IndependentTag tag)
                {
                    var box = ExtractFromTag(tag, view);
                    if (box != null) result.Add(box);
                }
                else if (options.IncludeDimensions && elem is Dimension dim)
                {
                    var boxes = ExtractFromDimension(dim, view);
                    result.AddRange(boxes);
                }
            }

            return result;
        }

        /// <summary>
        /// Trích xuất AnnotationBox từ một IndependentTag.
        /// B1-FIX: Ưu tiên dùng BoundingBox thực tế từ Revit (get_BoundingBox),
        /// chỉ fallback về ước lượng khi BoundingBox không available.
        /// </summary>
        public static AnnotationBox ExtractFromTag(IndependentTag tag, View view)
        {
            try
            {
                XYZ headPos = tag.TagHeadPosition;
                if (headPos == null) return null;

                BoundingBox2D box2d;

                // B1-FIX: Thử lấy BoundingBox thực tế từ Revit trước
                BoundingBoxXYZ revitBBox = tag.get_BoundingBox(view);
                if (revitBBox != null && IsValidBBox(revitBBox))
                {
                    // BoundingBox thực tế — chính xác tuyệt đối
                    box2d = new BoundingBox2D(
                        revitBBox.Min.X, revitBBox.Min.Y,
                        revitBBox.Max.X, revitBBox.Max.Y);
                }
                else
                {
                    // Fallback: ước lượng dựa trên text size với cải tiến B1
                    box2d = EstimateTagBoundingBox(tag, view, headPos);
                }

                return new AnnotationBox
                {
                    ElementId = tag.Id,
                    Type = AnnotationType.Tag,
                    HeadPosition = headPos,
                    Box = box2d,
                    HasLeader = tag.HasLeader,
                    IsPositionAdjustable = true
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Trích xuất AnnotationBox cho mỗi DimensionSegment có text.
        /// B1-FIX: Thêm đường dẫn ưu tiên BoundingBox thực tế cho single-segment dimension.
        /// </summary>
        public static List<AnnotationBox> ExtractFromDimension(Dimension dim, View view)
        {
            var result = new List<AnnotationBox>();

            try
            {
                if (dim.NumberOfSegments <= 0)
                {
                    // Single-segment dimension — thử BoundingBox thực tế trước
                    XYZ textPos = dim.TextPosition;
                    if (textPos == null) return result;

                    BoundingBox2D box2d;
                    BoundingBoxXYZ revitBBox = dim.get_BoundingBox(view);
                    if (revitBBox != null && IsValidBBox(revitBBox))
                    {
                        box2d = new BoundingBox2D(
                            revitBBox.Min.X, revitBBox.Min.Y,
                            revitBBox.Max.X, revitBBox.Max.Y);
                    }
                    else
                    {
                        string valueStr = dim.ValueString ?? "0";
                        double textSize = GetTextSize(dim.GetTypeId(), dim.Document);
                        double modelTextSize = textSize * view.Scale;
                        double widthScale = GetWidthScale(dim.GetTypeId(), dim.Document);
                        double halfWidth = EstimateHalfWidth(valueStr, modelTextSize, widthScale);
                        double halfHeight = modelTextSize * 0.6;
                        box2d = new BoundingBox2D(
                            textPos.X - halfWidth, textPos.Y - halfHeight,
                            textPos.X + halfWidth, textPos.Y + halfHeight);
                    }

                    result.Add(new AnnotationBox
                    {
                        ElementId = dim.Id,
                        Type = AnnotationType.DimensionSegment,
                        HeadPosition = textPos,
                        Box = box2d,
                        SegmentIndex = -1,
                        IsPositionAdjustable = true
                    });
                    return result;
                }

                int idx = 0;
                foreach (DimensionSegment seg in dim.Segments)
                {
                    if (seg.Value == null) { idx++; continue; }

                    bool adjustable = seg.IsTextPositionAdjustable();
                    XYZ textPos = adjustable ? seg.TextPosition : seg.Origin;
                    if (textPos == null) { idx++; continue; }

                    string valueStr = seg.ValueString ?? "0";
                    double textSize = GetTextSize(dim.GetTypeId(), dim.Document);
                    double modelTextSize = textSize * view.Scale;
                    double widthScale = GetWidthScale(dim.GetTypeId(), dim.Document);
                    double halfWidth = EstimateHalfWidth(valueStr, modelTextSize, widthScale);
                    double halfHeight = modelTextSize * 0.6;

                    result.Add(new AnnotationBox
                    {
                        ElementId = dim.Id,
                        Type = AnnotationType.DimensionSegment,
                        HeadPosition = textPos,
                        Box = new BoundingBox2D(
                            textPos.X - halfWidth, textPos.Y - halfHeight,
                            textPos.X + halfWidth, textPos.Y + halfHeight),
                        SegmentIndex = idx,
                        IsPositionAdjustable = adjustable
                    });
                    idx++;
                }
            }
            catch
            {
                // Silently skip problematic dimensions
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // B1-FIX: Private helpers — chính xác hơn, có WidthScale
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra BoundingBox từ Revit có hợp lệ không (đôi khi trả về box rỗng).
        /// Box phải có kích thước tối thiểu > 0.1mm trên cả 2 chiều để hợp lệ.
        /// </summary>
        private static bool IsValidBBox(BoundingBoxXYZ bbox)
        {
            if (bbox == null) return false;
            double w = bbox.Max.X - bbox.Min.X;
            double h = bbox.Max.Y - bbox.Min.Y;
            return w > 0.0003 && h > 0.0003;
        }

        /// <summary>
        /// B1-FIX: Ước lượng BoundingBox cho tag với cải tiến:
        /// - Đọc TEXT_WIDTH_SCALE từ TagType (Revit có tham số này)
        /// - Tăng halfHeight lên 0.65 để bao cả descender
        /// - Thêm padding 5% để tránh tags chạm sát nhau
        /// </summary>
        private static BoundingBox2D EstimateTagBoundingBox(IndependentTag tag, View view, XYZ headPos)
        {
            string tagText = "";
            try { tagText = tag.TagText ?? ""; } catch { }

            ElementId typeId = tag.GetTypeId();
            double textSize = GetTextSize(typeId, tag.Document);
            double widthScale = GetWidthScale(typeId, tag.Document);

            double modelTextSize = textSize * view.Scale;
            double halfWidth = EstimateHalfWidth(tagText, modelTextSize, widthScale);
            double halfHeight = modelTextSize * 0.65;

            // Padding nhỏ để tránh false negative khi detect overlap
            double padding = modelTextSize * 0.05;
            halfWidth += padding;
            halfHeight += padding;

            return new BoundingBox2D(
                headPos.X - halfWidth, headPos.Y - halfHeight,
                headPos.X + halfWidth, headPos.Y + halfHeight);
        }

        /// <summary>
        /// Lấy TEXT_SIZE từ DimensionType hoặc TagType.
        /// </summary>
        private static double GetTextSize(ElementId typeId, Document doc)
        {
            if (typeId == null || typeId == ElementId.InvalidElementId)
                return DefaultTextHeight;

            Element typeElem = doc.GetElement(typeId);
            if (typeElem == null) return DefaultTextHeight;

            Parameter textSizeParam = typeElem.get_Parameter(BuiltInParameter.TEXT_SIZE);
            if (textSizeParam != null && textSizeParam.HasValue)
            {
                double size = textSizeParam.AsDouble();
                if (size > 0) return size;
            }

            return DefaultTextHeight;
        }

        /// <summary>
        /// B1-FIX: Lấy TEXT_WIDTH_SCALE từ TagType/DimensionType.
        /// Revit lưu tham số này để co/giãn chiều rộng text (mặc định = 1.0).
        /// </summary>
        private static double GetWidthScale(ElementId typeId, Document doc)
        {
            if (typeId == null || typeId == ElementId.InvalidElementId)
                return 1.0;

            try
            {
                Element typeElem = doc.GetElement(typeId);
                if (typeElem == null) return 1.0;

                Parameter widthScaleParam = typeElem.get_Parameter(BuiltInParameter.TEXT_WIDTH_SCALE);
                if (widthScaleParam != null && widthScaleParam.HasValue)
                {
                    double scale = widthScaleParam.AsDouble();
                    if (scale > 0.1 && scale < 10.0) return scale;
                }
            }
            catch { }

            return 1.0;
        }

        /// <summary>
        /// B1-FIX: Ước lượng nửa chiều rộng text với widthScale từ TagType.
        /// </summary>
        private static double EstimateHalfWidth(string text, double textSize, double widthScale = 1.0)
        {
            if (string.IsNullOrEmpty(text)) return DefaultTextWidth;
            double charWidth = textSize * CharWidthRatio * widthScale;
            double totalWidth = text.Length * charWidth;
            return Math.Max(totalWidth * 0.5, DefaultTextWidth);
        }
    }
}
