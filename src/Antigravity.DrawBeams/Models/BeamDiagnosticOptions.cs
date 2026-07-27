using System;
using System.IO;

namespace Antigravity.DrawBeams.Models
{
    public class BeamDiagnosticOptions
    {
        public bool Enabled { get; set; } = true;
        public string ExportDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Antigravity", "DrawBeams", "Diagnostics");
        public bool AutoExport { get; set; } = true;
    }
}
