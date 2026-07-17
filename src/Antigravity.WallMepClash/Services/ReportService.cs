using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Antigravity.WallMepClash.Models;

namespace Antigravity.WallMepClash.Services
{
    public class ReportService
    {
        public string ExportHtml(
            IList<ClashResult> results,
            ClashCheckSettings settings,
            string hostDocumentTitle,
            string linkName,
            string outputPath = null)
        {
            string path = outputPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Antigravity",
                    "WallMepClash",
                    "Reports");

                Directory.CreateDirectory(directory);

                string fileName = "WallMepClash_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".html";
                path = Path.Combine(directory, fileName);
            }
            else
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, BuildHtml(results ?? new List<ClashResult>(), settings, hostDocumentTitle, linkName), Encoding.UTF8);
            return path;
        }

        private string BuildHtml(
            IList<ClashResult> results,
            ClashCheckSettings settings,
            string hostDocumentTitle,
            string linkName)
        {
            int clashCount = results.Count;

            var html = new StringBuilder();
            html.AppendLine("<!doctype html>");
            html.AppendLine("<html><head><meta charset=\"utf-8\"><title>Wall MEP Parallel Clash Report</title>");
            html.AppendLine("<style>");
            html.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;background:#0f172a;color:#e2e8f0;margin:24px;}");
            html.AppendLine("h1{font-size:24px;margin:0 0 16px;color:#f87171;} .meta{color:#94a3b8;margin-bottom:20px;}");
            html.AppendLine(".summary{display:flex;gap:12px;margin:18px 0}.card{background:#1e293b;border:1px solid #334155;border-radius:8px;padding:12px 16px;min-width:110px}.num{font-size:24px;font-weight:700;color:#f87171;}");
            html.AppendLine("table{width:100%;border-collapse:collapse;background:#111827;border:1px solid #334155} th,td{padding:9px 10px;border-bottom:1px solid #334155;text-align:left;font-size:13px} th{background:#1e293b;color:#cbd5e1} tr.clash{background:#3f1111;color:#fecaca}.right{text-align:right}");
            html.AppendLine("</style></head><body>");
            html.AppendLine("<h1>Wall MEP Parallel Clash Report</h1>");
            html.AppendLine("<div class=\"meta\">");
            html.AppendLine("<div>Host Model: " + Encode(hostDocumentTitle) + "</div>");
            html.AppendLine("<div>Linked MEP Model: " + Encode(linkName) + "</div>");
            html.AppendLine("<div>Parallel Angle Threshold: " + Format(settings == null ? 10.0 : settings.ParallelThresholdDeg) + " °</div>");
            html.AppendLine("<div>Generated: " + Encode(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)) + "</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"summary\">");
            html.AppendLine("<div class=\"card\"><div>Clashes Found</div><div class=\"num\">" + clashCount + "</div></div>");
            html.AppendLine("</div>");
            html.AppendLine("<table><thead><tr><th>Wall ID (Host)</th><th>MEP ID (Link)</th><th>Wall Type Name</th><th>MEP Name</th><th>Level</th><th class=\"right\">Angle (°)</th><th>Status</th></tr></thead><tbody>");

            foreach (ClashResult result in results)
            {
                html.AppendLine("<tr class=\"clash\">");
                html.AppendLine("<td>" + result.HostWallId + "</td>");
                html.AppendLine("<td>" + result.LinkMepId + "</td>");
                html.AppendLine("<td>" + Encode(result.WallTypeName) + "</td>");
                html.AppendLine("<td>" + Encode(result.MepName) + "</td>");
                html.AppendLine("<td>" + Encode(result.LevelName) + "</td>");
                html.AppendLine("<td class=\"right\">" + Format(result.AngleDeg) + "</td>");
                html.AppendLine("<td>● Clash</td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("</tbody></table></body></html>");
            return html.ToString();
        }

        private static string Format(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
