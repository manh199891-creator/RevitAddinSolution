using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Autodesk.Revit.DB;

namespace Antigravity.ZoneSplit.Services
{
    /// <summary>
    /// Xuất Navisworks Selection Sets XML từ ZoneID shared parameter.
    /// Prompt 9 — Optional extension.
    /// Output format: Navisworks findspec XML (well-formed UTF-8).
    /// </summary>
    public sealed class NavisworksExporter
    {
        private const string DefaultParamName = "BIM_ZoneID";

        /// <summary>
        /// Xuất selection sets XML cho tất cả zones có BIM_ZoneID.
        /// </summary>
        /// <param name="doc">Revit document.</param>
        /// <param name="outputPath">Đường dẫn file .xml output (overwrite nếu tồn tại).</param>
        /// <param name="zoneParameterName">Tên shared parameter — default "BIM_ZoneID".</param>
        public void ExportSelectionSets(
            Document doc,
            string outputPath,
            string zoneParameterName = DefaultParamName)
        {
            if (doc == null)         throw new ArgumentNullException(nameof(doc));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("outputPath không được rỗng.", nameof(outputPath));

            try
            {
                // 1. Collect tất cả elements có BIM_ZoneID (non-null, non-empty)
                var grouped = CollectAndGroup(doc, zoneParameterName);

                if (grouped.Count == 0)
                {
                    Trace.WriteLine("[NavisworksExporter] Không có element nào có BIM_ZoneID — bỏ qua export.");
                    return;
                }

                // 2. Build XML
                XDocument xml = BuildXml(grouped, zoneParameterName);

                // 3. Ensure output folder tồn tại
                string folder = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                // 4. Write (overwrite nếu tồn tại)
                xml.Save(outputPath, System.Xml.Linq.SaveOptions.None);

                Trace.WriteLine($"[NavisworksExporter] Exported {grouped.Count} selection sets to: {outputPath}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[NavisworksExporter] Export error: {ex.Message}");
                throw;
            }
        }

        // ── Private helpers ───────────────────────────────────────────

        /// <summary>Collect tất cả elements và group theo ZoneID value.</summary>
        private static Dictionary<string, List<int>> CollectAndGroup(
            Document doc, string paramName)
        {
            var grouped = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            using (var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType())
            {
                foreach (Element el in collector)
                {
                    try
                    {
                        Parameter p = el.LookupParameter(paramName);
                        if (p == null) continue;

                        string val = p.AsString();
                        if (string.IsNullOrWhiteSpace(val)) continue;

                        if (!grouped.ContainsKey(val))
                            grouped[val] = new List<int>();

                        grouped[val].Add((int)el.Id.Value);
                    }
                    catch { /* Skip individual element errors */ }
                }
            }

            return grouped;
        }

        /// <summary>Build Navisworks Selection Sets XML document.</summary>
        private static XDocument BuildXml(
            Dictionary<string, List<int>> grouped,
            string paramName)
        {
            var selectionSets = new XElement("selectionsets");

            foreach (var kvp in grouped.OrderBy(k => k.Key))
            {
                var selSet = new XElement("selectionset",
                    new XAttribute("name", kvp.Key),
                    new XElement("findspec",
                        new XElement("conditions",
                            new XElement("condition",
                                new XAttribute("test", "equals"),
                                new XElement("category", "Item"),
                                new XElement("property",  paramName),
                                new XElement("value",     kvp.Key)))));

                selectionSets.Add(selSet);
            }

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("exchange", selectionSets));
        }
    }
}
