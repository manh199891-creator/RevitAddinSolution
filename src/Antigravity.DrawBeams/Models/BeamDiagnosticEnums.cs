namespace Antigravity.DrawBeams.Models
{
    public enum BeamDiagnosticStage
    {
        RawCadEntity,
        RawBeamCandidate,
        ContinuityInput,
        ContinuityEvaluation,
        ContinuityOutput,
        ContinuityChainCreated,
        JunctionEvaluation,
        JunctionSplit,
        DimensionAssignment,
        DimensionTextEvaluation,
        DimensionTextAssigned,
        DimensionSplit,
        OverlapResolverInput,
        OverlapResolverOutput,
        OverlapEvaluation,
        OverlapDecision,
        FinalCandidate,
        RevitGuardCheck,
        RevitGuardEvaluation,
        RevitGuardDecision,
        RevitCreateResult
    }

    public enum BeamDiagnosticAction
    {
        Created,
        Kept,
        Merged,
        Split,
        Assigned,
        Suppressed,
        SkippedDuplicate,
        Rejected,
        Evaluated,
        Warning,
        Failed
    }
}
