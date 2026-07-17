using Autodesk.Revit.DB;
using Antigravity.TagArranger.Core;

namespace Antigravity.TagArranger.Models
{
    /// <summary>
    /// Đại diện cho một annotation element (Tag hoặc Dimension) cùng bounding box 2D xấp xỉ.
    /// </summary>
    public class AnnotationBox
    {
        /// <summary>ElementId của annotation gốc trong Revit.</summary>
        public ElementId ElementId { get; set; }

        /// <summary>Loại annotation.</summary>
        public AnnotationType Type { get; set; }

        /// <summary>Vị trí đầu tag (TagHeadPosition) hoặc TextPosition.</summary>
        public XYZ HeadPosition { get; set; }

        /// <summary>Bounding box 2D xấp xỉ (trong tọa độ view).</summary>
        public BoundingBox2D Box { get; set; }

        /// <summary>Segment index (chỉ dùng cho DimensionSegment, -1 nếu không phải).</summary>
        public int SegmentIndex { get; set; } = -1;

        /// <summary>Tag có leader hay không.</summary>
        public bool HasLeader { get; set; }

        /// <summary>Đánh dấu text position có adjustable không (cho DimensionSegment).</summary>
        public bool IsPositionAdjustable { get; set; } = true;
    }
}
