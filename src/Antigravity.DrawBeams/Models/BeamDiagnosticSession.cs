using System;
using System.Collections.Generic;

namespace Antigravity.DrawBeams.Models
{
    public class CadSelectionSummary
    {
        public int TotalEntities { get; set; }
        public int LineCount { get; set; }
        public int PolylineCount { get; set; }
        public int TextCount { get; set; }
        public int MTextCount { get; set; }
        public int SkippedCount { get; set; }
        public Dictionary<string, int> SkippedReasons { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public class PipelineSummary
    {
        public int RawCandidatesCount { get; set; }
        public int ContinuityChainsCount { get; set; }
        public int JunctionSplitsCount { get; set; }
        public int DimensionSplitsCount { get; set; }
        public int BeforeOverlapCount { get; set; }
        public int SuppressedOverlapsCount { get; set; }
        public int AfterOverlapCount { get; set; }
        public List<string> SharedDimensionTextWarnings { get; set; } = new List<string>();
    }

    public class RevitSummary
    {
        public int ExistingDuplicatesCount { get; set; }
        public int CreatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
    }

    public class BeamDiagnosticSession
    {
        public string SessionId { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }

        public CadSelectionSummary CadSelectionSummary { get; set; } = new CadSelectionSummary();
        public PipelineSummary PipelineSummary { get; set; } = new PipelineSummary();
        public RevitSummary RevitSummary { get; set; } = new RevitSummary();

        public List<BeamDiagnosticEntry> Entries { get; set; } = new List<BeamDiagnosticEntry>();
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
