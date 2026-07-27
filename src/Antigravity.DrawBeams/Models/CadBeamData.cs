using System;
using System.Collections.Generic;

namespace Antigravity.DrawBeams.Models
{
    public class CadBeamData
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }

        public string TextContent { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Mark { get; set; }

        // Measured data from parallel lines
        public double MeasuredWidth { get; set; }
        public bool IsPaired { get; set; }

        // Provenance properties
        public double Confidence { get; set; }
        public HashSet<string> SourceLineIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string SourceLayer { get; set; }
        public bool HasDimensionText { get; set; }
        public BeamDetectionMethod DetectionMethod { get; set; } = BeamDetectionMethod.PairedLines;

        // Lineage & Traceability Properties
        public string DiagnosticId { get; set; }
        public List<string> ParentDiagnosticIds { get; set; } = new List<string>();
        public List<string> RootRawCandidateIds { get; set; } = new List<string>();

        public bool IsValid => (Width > 0) || (IsPaired && MeasuredWidth > 0);
    }
}
