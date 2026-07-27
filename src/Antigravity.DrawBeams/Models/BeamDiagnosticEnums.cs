namespace Antigravity.DrawBeams.Models
{
    public enum BeamDiagnosticStage
    {
        RawCadEntity,
        RawBeamCandidate,
        ContinuityInput,
        ContinuityOutput,
        JunctionSplit,
        DimensionAssignment,
        DimensionSplit,
        OverlapResolverInput,
        OverlapResolverOutput,
        RevitGuardCheck,
        RevitCreateResult
    }

    public enum BeamDiagnosticAction
    {
        Created,
        Kept,
        Merged,
        Split,
        Suppressed,
        SkippedDuplicate,
        Rejected,
        Failed
    }
}
