namespace Antigravity.TagArranger.Models
{
    /// <summary>
    /// Các tùy chọn cho thao tác sắp xếp annotation.
    /// </summary>
    public class ArrangeOptions
    {
        /// <summary>Phạm vi xử lý: Selection hoặc Entire View.</summary>
        public ArrangeScope Scope { get; set; } = ArrangeScope.Selection;

        /// <summary>Khoảng cách tối thiểu giữa 2 annotation (đơn vị feet).</summary>
        public double MinSpacingFeet { get; set; } = 0.0164; // ~5mm

        /// <summary>Có giữ leader line khi di chuyển tag không.</summary>
        public bool MaintainLeader { get; set; } = true;

        /// <summary>Hướng align (chỉ dùng cho Align).</summary>
        public AlignMode AlignMode { get; set; } = AlignMode.Top;

        /// <summary>Hướng distribute (chỉ dùng cho Distribute).</summary>
        public DistributeMode DistributeMode { get; set; } = DistributeMode.Horizontal;

        /// <summary>Có xử lý IndependentTag không.</summary>
        public bool IncludeTags { get; set; } = true;

        /// <summary>Có xử lý Dimension text không.</summary>
        public bool IncludeDimensions { get; set; } = true;

        /// <summary>Kiểu định dạng leader cho AutoTag.</summary>
        public LeaderFormatStyle AutoTagLeaderStyle { get; set; } = LeaderFormatStyle.Orthogonal;

        /// <summary>Góc leader (độ) cho AutoTag Angled.</summary>
        public double LeaderAngleDegrees { get; set; } = 45.0;

        /// <summary>Khoảng cách gộp Tag tối đa (feet). Nếu = 0 thì gộp không giới hạn.</summary>
        public double MaxMergeDistanceFeet { get; set; } = 32.8084; // ~10m
    }

    public enum LeaderFormatStyle
    {
        Orthogonal,
        Angled,
        RevitDefault
    }
}
