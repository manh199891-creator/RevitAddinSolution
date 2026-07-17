using System;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using Antigravity.ZoneSplit.Services;

namespace Antigravity.ZoneSplit.Commands
{
    /// <summary>
    /// Export ZoneID data sang Navisworks Selection Sets XML.
    /// Prompt 9 — Optional extension.
    /// Thêm button "Export to Navisworks" vào ribbon trong App.cs.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument?.Document;
                if (doc == null) { message = "Không có active document."; return Result.Failed; }

                // FileSaveDialog để chọn output path
                var dlg = new SaveFileDialog
                {
                    Title      = "Xuất Navisworks Selection Sets",
                    Filter     = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                    DefaultExt = ".xml",
                    FileName   = $"ZoneSplit_{DateTime.Now:yyyyMMdd_HHmm}.xml"
                };

                if (dlg.ShowDialog() != true)
                    return Result.Cancelled;

                string outputPath = dlg.FileName;
                var exporter      = new NavisworksExporter();
                exporter.ExportSelectionSets(doc, outputPath);

                // Đếm selection sets (zones) từ file
                int setsCount = CountSelectionSets(outputPath);

                TaskDialog.Show("ZoneSplit — Export",
                    $"✅ Đã xuất {setsCount} selection sets ra:\n{outputPath}");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Export lỗi: {ex.Message}";
                return Result.Failed;
            }
        }

        private static int CountSelectionSets(string path)
        {
            try
            {
                var xml = System.Xml.Linq.XDocument.Load(path);
                return xml.Root?.Element("selectionsets")?.Elements("selectionset")
                    is { } sets ? System.Linq.Enumerable.Count(sets) : 0;
            }
            catch { return 0; }
        }
    }
}
