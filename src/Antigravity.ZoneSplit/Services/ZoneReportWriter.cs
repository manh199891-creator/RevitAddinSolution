using Antigravity.ZoneSplit.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Antigravity.ZoneSplit.Services
{
    public static class ZoneReportWriter
    {
        public static string WriteReport(ZoneProcessResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ZoneSplit QTO Report");
            sb.AppendLine($"Run time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"- Total candidate elements: {result.TotalProcessed}");
            sb.AppendLine($"- Assigned elements: {result.AssignedCount}");
            sb.AppendLine($"- Multi-zone elements: {result.MultiZoneCount}");
            sb.AppendLine($"- Physically split elements: {result.PhysicallySplitCount}");
            sb.AppendLine($"- Split segments assigned: {result.SplitSegmentCount}");
            sb.AppendLine($"- Physical split skipped: {result.SplitSkippedCount}");
            sb.AppendLine($"- Skipped without solid: {result.SkippedNoSolid}");
            sb.AppendLine();

            sb.AppendLine("## Zone Summary");
            sb.AppendLine("| Zone ID | Total Volume (m3) | Element Count |");
            sb.AppendLine("|---------|-------------------|---------------|");

            var summary = result.Contributions
                .GroupBy(c => c.ZoneId)
                .Select(g => new
                {
                    ZoneId = g.Key,
                    TotalVol = g.Sum(x => x.VolumeM3),
                    Count = g.Select(x => x.ElementId).Distinct().Count()
                })
                .OrderBy(x => x.ZoneId);

            foreach (var item in summary)
            {
                sb.AppendLine($"| {item.ZoneId} | {Math.Round(item.TotalVol, 3)} | {item.Count} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Element Details (Top 50)");
            sb.AppendLine("| Element ID | Category | Zone | Volume (m3) | Length (m) |");
            sb.AppendLine("|------------|----------|------|-------------|------------|");

            foreach (var c in result.Contributions.Take(50))
            {
                sb.AppendLine($"| {c.ElementId} | {c.Category} | {c.ZoneId} | {Math.Round(c.VolumeM3, 4)} | {Math.Round(c.LengthM, 3)} |");
            }

            if (result.Contributions.Count > 50)
            {
                sb.AppendLine($"*... and {result.Contributions.Count - 50} more contribution rows.*");
            }

            if (result.Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Warnings");
                foreach (var warning in result.Warnings.Take(100))
                {
                    sb.AppendLine($"- {warning}");
                }
            }

            string fileName = $"ZoneSplit_Report_{DateTime.Now:yyyyMMdd_HHmmss}.md";
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AntigravityReports");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, fileName);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

            result.ReportPath = path;
            return path;
        }
    }
}
