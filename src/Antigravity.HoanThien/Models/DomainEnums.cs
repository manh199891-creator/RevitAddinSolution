namespace Antigravity.HoanThien.Models
{
    public enum SubstrateClass
    {
        Unknown,
        Masonry,
        Concrete,
        Gypsum,
        Glazing,
        Column,
        FreeBoundary
    }

    public enum FinishLayerKind
    {
        Plaster,
        Waterproofing,
        Tile,
        Putty,
        Paint,
        Other
    }

    public enum TopConstraintMode
    {
        FixedHeight,
        RoomUpperBoundary,
        CeilingPlusOffset
    }

    public enum ProcessingMode
    {
        AuditOnly,
        CreateMissing,
        UpdateManaged
    }

    public enum PlanStatus
    {
        Ready,
        Skipped,
        Warning,
        RequiresReview,
        Error
    }
}
