using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.ZoneSplit.Models;
using Antigravity.ZoneSplit.Services;
using Antigravity.ZoneSplit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Antigravity.ZoneSplit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ZoneProcessCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = commandData.Application.ActiveUIDocument;
            var doc = uiDoc?.Document;
            if (doc == null) return Result.Failed;

            try
            {
                var mainWindow = new MainWindow();
                if (mainWindow.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                var selectedCategories = mainWindow.SelectedCategories;
                var parameterCategories = selectedCategories.ToList();
                if (mainWindow.IsCreateParts && !parameterCategories.Contains(BuiltInCategory.OST_Parts))
                {
                    parameterCategories.Add(BuiltInCategory.OST_Parts);
                }

                ParameterSetupService.EnsureZoneParametersExist(doc, parameterCategories);

                var zones = ZoneVolumeReader.ReadFromDocument(doc);
                var linkedZoneIds = ZoneVolumeReader.ReadZoneIdsFromLinkedDocuments(doc);

                ParameterSetupService.EnsureDynamicZoneVolumeParameters(
                    doc,
                    zones.Select(z => z.ZoneId).Concat(linkedZoneIds),
                    parameterCategories);

                if (zones.Count == 0)
                {
                    if (linkedZoneIds.Count > 0)
                    {
                        TaskDialog.Show(
                            "ZoneSplit",
                            "Khong tim thay Zone trong host model.\n" +
                            $"Da cap nhat {linkedZoneIds.Count} BIM_Zone volume parameters tu linked models de dung cho schedule Include elements in links.\n\n" +
                            "Luu y: Volume van phai duoc tinh/ghi trong file link nguon.");
                        return Result.Succeeded;
                    }

                    TaskDialog.Show(
                        "ZoneSplit",
                        "Khong tim thay khoi Zone nao (Generic Model co Mark hoac BIM_ZoneID).");
                    return Result.Cancelled;
                }

                if (mainWindow.IsDataOnly)
                {
                    var processor = new ZoneVolumeProcessor(doc);
                    var result = processor.Process(zones, null, selectedCategories);

                    string reportPath = ZoneReportWriter.WriteReport(result);

                    TaskDialog.Show(
                        "ZoneSplit - Hoan thanh",
                        $"Da xu ly Data-Only cho {result.AssignedCount} cau kien.\n" +
                        $"Bao cao da luu tai:\n{reportPath}");

                    TryOpenReport(reportPath);
                }
                else if (mainWindow.IsCreateParts)
                {
                    var elementIds = CollectTargetElements(doc, selectedCategories);

                    var partSplitService = new PartSplitService(doc);
                    var partIds = partSplitService.CreateAndDivideParts(elementIds, zones);

                    var paramService = new ParameterAssignService(doc);
                    int sourceAssignedCount = paramService.AssignParametersToSubElements(elementIds, zones);
                    int partAssignedCount = paramService.AssignParametersToSubElements(partIds, zones);

                    TaskDialog.Show(
                        "ZoneSplit - Hoan thanh",
                        $"Da tao/thu thap {partIds.Count} Parts.\n" +
                        $"Da gan Zone cho {partAssignedCount} Parts.\n" +
                        $"Da gan Zone cho {sourceAssignedCount} cau kien goc nam trong Zone.");
                }
                else if (mainWindow.IsPhysicalSplit)
                {
                    var physicalCategories = selectedCategories
                        .Where(c => c == BuiltInCategory.OST_Walls || c == BuiltInCategory.OST_StructuralFraming)
                        .ToList();

                    if (!physicalCategories.Any())
                    {
                        TaskDialog.Show(
                            "ZoneSplit - Physical Split",
                            "Che do cat vat ly hien chi ho tro Dam ket cau (Structural Framing) va Vach/Tuong (Walls).");
                        return Result.Cancelled;
                    }

                    var elementIds = CollectTargetElements(doc, physicalCategories);
                    var physicalSplitService = new PhysicalSplitService(doc);
                    var paramService = new ParameterAssignService(doc);

                    int totalSplit = 0;
                    int totalAssignedOnly = 0;
                    int totalSkipped = 0;
                    int totalOutsideZones = 0;
                    var warnings = new List<string>();
                    var singleZoneAssignments = new List<(ElementId ElementId, string ZoneId, string ZoneName)>();
                    var splitAssignments = new List<(ElementId ElementId, string ZoneId, string ZoneName)>();

                    using (var tx = new Transaction(doc, "ZoneSplit - Physical Split"))
                    {
                        tx.Start();
                        foreach (var elemId in elementIds)
                        {
                            var elem = doc.GetElement(elemId);
                            if (elem == null)
                            {
                                totalSkipped++;
                                continue;
                            }

                            if (!physicalSplitService.TryBuildSegmentsForZones(elem, zones, out var segments, out string reason))
                            {
                                if (IsOutsideZoneReason(reason))
                                {
                                    totalOutsideZones++;
                                }
                                else
                                {
                                    totalSkipped++;
                                    if (!string.IsNullOrWhiteSpace(reason))
                                    {
                                        warnings.Add($"{elemId.Value}: {reason}");
                                    }
                                }
                                continue;
                            }

                            if (segments.Count == 1)
                            {
                                // Dam/Tuong chi nam trong 1 Zone -> khong can cat, chi gan Parameter
                                singleZoneAssignments.Add((elemId, segments[0].ZoneId, segments[0].ZoneName));
                                totalAssignedOnly++;
                                continue;
                            }

                            // Dam/Tuong giao voi >= 2 Zones -> cat vat ly
                            var splitResult = physicalSplitService.Split(elem, segments);
                            if (splitResult.Succeeded)
                            {
                                totalSplit++;
                                splitAssignments.AddRange(splitResult.Assignments);
                            }
                            else
                            {
                                totalSkipped++;
                                if (!string.IsNullOrWhiteSpace(splitResult.Reason))
                                {
                                    warnings.Add($"{elemId.Value}: {splitResult.Reason}");
                                }
                            }
                        }
                        tx.Commit();
                    }

                    // Gan Parameter sau khi transaction cat vat ly da commit.
                    var allAssignments = splitAssignments.Concat(singleZoneAssignments).ToList();
                    if (allAssignments.Any())
                    {
                        paramService.AssignKnownZoneParameters(allAssignments);
                    }

                    TaskDialog.Show("ZoneSplit - Hoan thanh",
                        $"Da cat vat ly: {totalSplit} cau kien.\n" +
                        $"Da gan Zone (khong cat): {totalAssignedOnly} cau kien.\n" +
                        $"Nam ngoai Zone: {totalOutsideZones} cau kien.\n" +
                        $"Bo qua: {totalSkipped} cau kien.\n" +
                        $"Tong: {totalSplit + totalAssignedOnly} cau kien xu ly." +
                        FormatWarnings(warnings));
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Loi ZoneSplit", ex.ToString());
                return Result.Failed;
            }
        }

        private static void TryOpenReport(string reportPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not open ZoneSplit report: {ex}");
            }
        }

        private static List<ElementId> CollectTargetElements(Document doc, List<BuiltInCategory> categories)
        {
            var filters = categories.Distinct().Select(c => new ElementCategoryFilter(c)).Cast<ElementFilter>().ToList();
            if (!filters.Any()) return new List<ElementId>();

            return new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(new LogicalOrFilter(filters))
                .ToElementIds()
                .ToList();
        }

        private static string FormatWarnings(List<string> warnings)
        {
            if (warnings == null || warnings.Count == 0) return string.Empty;

            var preview = warnings.Take(8).ToList();
            string suffix = warnings.Count > preview.Count
                ? $"\n... va {warnings.Count - preview.Count} canh bao khac."
                : string.Empty;

            return "\n\nCanh bao:\n" + string.Join("\n", preview) + suffix;
        }

        private static bool IsOutsideZoneReason(string reason)
        {
            return string.Equals(reason, "Element does not intersect any zone.", StringComparison.OrdinalIgnoreCase);
        }

    }
}
