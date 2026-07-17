using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Antigravity.CheckFloorElevation.Models;

namespace Antigravity.CheckFloorElevation.Services
{
    public class ReportService
    {
        public string ExportHtml(
            IList<FloorCheckResult> results,
            CheckSettings settings,
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
                    "CheckFloorElevation",
                    "Reports");

                Directory.CreateDirectory(directory);

                string fileName = "FloorElevationCheck_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".html";
                path = Path.Combine(directory, fileName);
            }
            else
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, BuildHtml(results ?? new List<FloorCheckResult>(), settings, hostDocumentTitle, linkName), Encoding.UTF8);
            return path;
        }

        private string BuildHtml(
            IList<FloorCheckResult> results,
            CheckSettings settings,
            string hostDocumentTitle,
            string linkName)
        {
            int errorCount = results.Count(r => r.IsError);
            int noMatchCount = results.Count(r => r.IsNoMatch);
            int okCount = results.Count - errorCount - noMatchCount;

            var html = new StringBuilder();
            html.AppendLine("<!doctype html>");
            html.AppendLine("<html><head><meta charset=\"utf-8\"><title>Floor Elevation Check</title>");
            html.AppendLine("<style>");
            html.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;background:#0f172a;color:#e2e8f0;margin:24px;}");
            html.AppendLine("h1{font-size:24px;margin:0 0 16px;} .meta{color:#94a3b8;margin-bottom:20px;}");
            html.AppendLine(".summary{display:flex;gap:12px;margin:18px 0}.card{background:#1e293b;border:1px solid #334155;border-radius:8px;padding:12px 16px;min-width:110px}.num{font-size:24px;font-weight:700}");
            html.AppendLine("table{width:100%;border-collapse:collapse;background:#111827;border:1px solid #334155} th,td{padding:9px 10px;border-bottom:1px solid #334155;text-align:left;font-size:13px} th{background:#1e293b;color:#cbd5e1} tr.error{background:#3f1111;color:#fecaca} tr.nomatch{background:#3a2608;color:#fde68a}.right{text-align:right}");
            html.AppendLine("</style></head><body>");
            html.AppendLine("<h1>Floor Elevation Check</h1>");
            html.AppendLine("<div class=\"meta\">");
            html.AppendLine("<div>Host: " + Encode(hostDocumentTitle) + "</div>");
            html.AppendLine("<div>Link: " + Encode(linkName) + "</div>");
            html.AppendLine("<div>Tolerance: " + Format(settings == null ? 20.0 : settings.ToleranceMm) + " mm</div>");
            html.AppendLine("<div>Generated: " + Encode(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)) + "</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"summary\">");
            html.AppendLine("<div class=\"card\"><div>OK</div><div class=\"num\">" + okCount + "</div></div>");
            html.AppendLine("<div class=\"card\"><div>Error</div><div class=\"num\">" + errorCount + "</div></div>");
            html.AppendLine("<div class=\"card\"><div>No Match</div><div class=\"num\">" + noMatchCount + "</div></div>");
            html.AppendLine("<div class=\"card\"><div>Total</div><div class=\"num\">" + results.Count + "</div></div>");
            html.AppendLine("</div>");
            html.AppendLine("<table><thead><tr><th>Host Floor ID</th><th>Link Floor ID</th><th>Host Type</th><th>Link Type</th><th>Level</th><th class=\"right\">ZTop Host (mm)</th><th class=\"right\">ZTop Link (mm)</th><th class=\"right\">Delta Z (mm)</th><th>Status</th><th>Message</th></tr></thead><tbody>");

            foreach (FloorCheckResult result in results)
            {
                string rowClass = result.IsError ? " class=\"error\"" : (result.IsNoMatch ? " class=\"nomatch\"" : string.Empty);
                html.AppendLine("<tr" + rowClass + ">");
                html.AppendLine("<td>" + result.HostFloorId + "</td>");
                html.AppendLine("<td>" + (result.LinkFloorId >= 0 ? result.LinkFloorId.ToString(CultureInfo.InvariantCulture) : "-") + "</td>");
                html.AppendLine("<td>" + Encode(result.HostTypeName) + "</td>");
                html.AppendLine("<td>" + (string.IsNullOrWhiteSpace(result.LinkTypeName) ? "-" : Encode(result.LinkTypeName)) + "</td>");
                html.AppendLine("<td>" + Encode(result.LevelName) + "</td>");
                html.AppendLine("<td class=\"right\">" + Format(result.ZTopHostMm) + "</td>");
                html.AppendLine("<td class=\"right\">" + (result.IsNoMatch ? "-" : Format(result.ZTopLinkMm)) + "</td>");
                html.AppendLine("<td class=\"right\">" + (result.IsNoMatch ? "-" : Format(result.DeltaZMm)) + "</td>");
                html.AppendLine("<td>" + GetStatus(result) + "</td>");
                html.AppendLine("<td>" + Encode(result.ErrorMessage) + "</td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("</tbody></table></body></html>");
            return html.ToString();
        }

        private static string GetStatus(FloorCheckResult result)
        {
            if (result.IsNoMatch)
                return "No Match";

            return result.IsError ? "Error" : "OK";
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
