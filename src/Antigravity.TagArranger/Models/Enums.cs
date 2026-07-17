namespace Antigravity.TagArranger.Models
{
    /// <summary>
    /// Loại annotation được hỗ trợ.
    /// </summary>
    public enum AnnotationType
    {
        Tag,
        DimensionSegment,
        TextNote
    }

    /// <summary>
    /// Hướng align.
    /// </summary>
    public enum AlignMode
    {
        Top,
        Bottom,
        Left,
        Right,
        CenterHorizontal,
        CenterVertical
    }

    /// <summary>
    /// Hướng distribute.
    /// </summary>
    public enum DistributeMode
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Phạm vi xử lý.
    /// </summary>
    public enum ArrangeScope
    {
        Selection,
        EntireView
    }
}
