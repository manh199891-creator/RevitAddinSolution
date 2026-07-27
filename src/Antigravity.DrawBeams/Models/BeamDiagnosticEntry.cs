using System;
using System.Collections.Generic;

namespace Antigravity.DrawBeams.Models
{
    public class BeamDiagnosticEntry
    {
        public string SessionId { get; set; }
        public string CandidateId { get; set; }
        public string DiagnosticId { get; set; }
        public string ObjectType { get; set; }

        public List<string> ParentCandidateIds { get; set; } = new List<string>();
        public List<string> ParentDiagnosticIds { get; set; } = new List<string>();
        public List<string> RootRawCandidateIds { get; set; } = new List<string>();

        public BeamDiagnosticStage Stage { get; set; }
        public BeamDiagnosticAction Action { get; set; }
        public string Reason { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public BeamDetectionMethod? DetectionMethod { get; set; }
        public double Confidence { get; set; }
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double Length { get; set; }
        public double AngleDegrees { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double MeasuredWidth { get; set; }
        public string Mark { get; set; }
        public string TextContent { get; set; }
        public bool HasDimensionText { get; set; }
        public string SourceLayer { get; set; }
        public List<string> SourceLineIds { get; set; } = new List<string>();
        public bool IsPaired { get; set; }

        // Lineage & Relationship Properties
        public string RelatedCandidateId { get; set; }
        public string WinnerDiagnosticId { get; set; }
        public string LoserDiagnosticId { get; set; }
        public List<string> InputDiagnosticIds { get; set; } = new List<string>();
        public List<string> OutputDiagnosticIds { get; set; } = new List<string>();

        // Geometry Metrics
        public double AngularDifferenceDegrees { get; set; }
        public double CenterlineDistanceMm { get; set; }
        public double EndpointDistanceMm { get; set; }
        public double GapMm { get; set; }
        public double LateralOffsetMm { get; set; }
        public double OverlapLengthMm { get; set; }
        public double OverlapRatio { get; set; }
        public bool IsContained { get; set; }
        public int SharedSourceLineCount { get; set; }
        public double PriorityScore { get; set; }
        public double CompetingPriorityScore { get; set; }

        // Text Metrics
        public string DimensionTextId { get; set; }
        public double TextProjection { get; set; }
        public double TextLateralDistance { get; set; }
        public List<string> AssignedChainIds { get; set; } = new List<string>();

        // Revit & Exception Properties
        public string ExistingRevitElementId { get; set; }
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }
    }
}
