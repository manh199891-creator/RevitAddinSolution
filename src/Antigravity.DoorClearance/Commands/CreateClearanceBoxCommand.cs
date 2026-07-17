using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DoorClearanceBox.Core;
using DoorClearanceBox.UI;

namespace DoorClearanceBox.Commands
{
    /// <summary>
    /// IExternalCommand — Create Clearance Boxes
    ///
    /// Execution flow:
    ///   1. Show ClearanceBoxWindow → collect scope / options.
    ///   2. Gather door instances.
    ///   3. (Optional) Run inline clash pre-check via ClashChecker.
    ///   4. Create W×2W×H DirectShapes in a single named transaction.
    ///   5. Report results via TaskDialog.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class CreateClearanceBoxCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                              ref string message,
                              ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument    uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                TaskDialog.Show(Constants.AddInName, "Please open a document first before using this tool.");
                return Result.Cancelled;
            }
            Document      doc   = uiDoc.Document;
            View          view  = uiDoc.ActiveView;

            // ── 1. Show settings dialog ──────────────────────────────────────────
            var window = new ClearanceBoxWindow(doc, view);

            // ShowDialog requires the WPF window to be owned by the Revit process window
            var wih = new System.Windows.Interop.WindowInteropHelper(window);
            wih.Owner = uiApp.MainWindowHandle;
            
            bool? dialogResult = null;
            try
            {
                dialogResult = window.ShowDialog();
            }
            catch (Exception ex)
            {
                message = $"Dialog error: {ex.Message}";
                return Result.Failed;
            }

            if (dialogResult != true || !window.Confirmed)
                return Result.Cancelled;

            // ── 2. Collect elements ──────────────────────────────────────────────
            var foundElements = new List<Element>();
            var elementsWithTransform = new List<(Element, Transform)>();

            if (window.UseActiveView)
            {
                var els = DoorCollector.CollectFromView(doc, view, window.IncludeDoors, window.IncludeWindows, window.IncludeCurtainPanels, window.IncludeCurtainWalls);
                foreach (var el in els) elementsWithTransform.Add((el, Transform.Identity));
            }
            else if (window.UseEntireModel)
            {
                var els = DoorCollector.CollectFromDocument(doc, window.IncludeDoors, window.IncludeWindows, window.IncludeCurtainPanels, window.IncludeCurtainWalls);
                foreach (var el in els) elementsWithTransform.Add((el, Transform.Identity));
            }
            else
            {
                // Combined Custom Scope
                if (window.UseLevel)
                {
                    var els = DoorCollector.CollectFromLevels(doc, window.SelectedLevels, window.IncludeDoors, window.IncludeWindows, window.IncludeCurtainPanels, window.IncludeCurtainWalls);
                    foreach (var el in els) elementsWithTransform.Add((el, Transform.Identity));
                }

                if (window.UseLink && window.SelectedLink != null)
                {
                    var linkTransform = window.SelectedLink.GetTotalTransform();
                    var els = DoorCollector.CollectFromLink(window.SelectedLink, window.SelectedLinkLevels, window.IncludeDoors, window.IncludeWindows, window.IncludeCurtainPanels, window.IncludeCurtainWalls);
                    foreach (var el in els) elementsWithTransform.Add((el, linkTransform));
                }
            }

            if (elementsWithTransform.Count == 0)
            {
                TaskDialog.Show(Constants.AddInName, "No valid elements found in the selected scope.");
                return Result.Cancelled;
            }

            // ── Apply Type filtering ─────────────────────────────────────────────
            if (window.IncludeDoors || window.IncludeWindows || window.IncludeCurtainPanels || window.IncludeCurtainWalls)
            {
                elementsWithTransform = elementsWithTransform.Where(x => 
                {
                    if (x.Item1.Category.Id.Value == (long)BuiltInCategory.OST_Doors)
                        return window.SelectedDoorTypeIds.Contains(x.Item1.GetTypeId());
                    if (x.Item1.Category.Id.Value == (long)BuiltInCategory.OST_Windows)
                        return window.SelectedWindowTypeIds.Contains(x.Item1.GetTypeId());
                    if (x.Item1.Category.Id.Value == (long)BuiltInCategory.OST_CurtainWallPanels)
                        return window.SelectedCurtainPanelTypeIds.Contains(x.Item1.GetTypeId());
                    if (x.Item1.Category.Id.Value == (long)BuiltInCategory.OST_Walls)
                        return window.SelectedCurtainWallTypeIds.Contains(x.Item1.GetTypeId());
                    return true;
                }).ToList();
            }

            if (elementsWithTransform.Count == 0)
            {
                TaskDialog.Show(Constants.AddInName, "All elements were filtered out by category types.");
                return Result.Cancelled;
            }

            // ── UNDRAW MODE: delete all clearance boxes for collected elements ───
            if (window.UndrawMode)
            {
                var idsToUndraw = elementsWithTransform.Select(x => x.Item1.Id).ToList();
                int deleted = 0;
                try
                {
                    TransactionWrapper.Execute(doc, "Undraw Clearance Boxes", _ =>
                    {
                        deleted = ReserveSpaceGeometry.BatchDeleteForElements(doc, idsToUndraw);
                    });
                }
                catch (Exception ex)
                {
                    message = $"Undraw failed: {ex.Message}";
                    return Result.Failed;
                }

                TaskDialog.Show(Constants.AddInName,
                    deleted > 0
                        ? $"Removed {deleted} clearance box(es) successfully."
                        : "No clearance boxes found to remove.");
                return deleted > 0 ? Result.Succeeded : Result.Cancelled;
            }

            // ── SKIP EXISTING: filter out elements that already have a clearance box
            if (window.SkipExisting)
            {
                var existingMap = ReserveSpaceGeometry.FindForElements(doc,
                    elementsWithTransform.Select(x => x.Item1.Id));
                int skipped = elementsWithTransform.Count;
                elementsWithTransform = elementsWithTransform
                    .Where(x => !existingMap.ContainsKey(x.Item1.Id))
                    .ToList();
                skipped -= elementsWithTransform.Count;

                if (elementsWithTransform.Count == 0)
                {
                    TaskDialog.Show(Constants.AddInName,
                        $"All {skipped} element(s) already have clearance boxes. Nothing to create.\n\n" +
                        "Tip: Uncheck 'Skip if clearance box already exists' to replace them.");
                    return Result.Cancelled;
                }
            }

            // ── 3. Pre-cleanup: batch delete existing clearance shapes ───────────
            //    One single DB scan instead of 675 individual scans (O(n) vs O(n²))
            var allElementIds = elementsWithTransform.Select(x => x.Item1.Id).ToList();
            try
            {
                TransactionWrapper.Execute(doc, "Delete old Clearance Boxes", _ =>
                {
                    ReserveSpaceGeometry.BatchDeleteForElements(doc, allElementIds);
                });
            }
            catch { /* Old shapes may not exist — safe to ignore */ }

            // ── 4. Pre-cache material ID (1 lookup instead of 675) ───────────────
            ElementId cachedMatId = null;
            try
            {
                TransactionWrapper.Execute(doc, "Prepare Clearance Material", _ =>
                {
                    cachedMatId = ReserveSpaceGeometry.GetOrCreateMaterial(doc);
                });
            }
            catch { /* Material will be created per-element as fallback */ }

            // ── 5. Create DirectShapes in batches of 100 ─────────────────────────
            int created  = 0;
            int failed   = 0;
            var failedIds = new List<ElementId>();
            var failReasons = new List<string>();
            const int batchSize = 100;

            for (int batchStart = 0; batchStart < elementsWithTransform.Count; batchStart += batchSize)
            {
                var batch = elementsWithTransform.Skip(batchStart).Take(batchSize).ToList();
                int batchNum = (batchStart / batchSize) + 1;

                try
                {
                    TransactionWrapper.Execute(doc,
                        string.Format("Create Clearance Boxes (batch {0}, {1} items)", batchNum, batch.Count),
                        _ =>
                        {
                            foreach (var item in batch)
                            {
                                Element el = item.Item1;
                                Transform tr = item.Item2;

                                try
                                {
                                    var ds = ReserveSpaceGeometry.CreateOrReplace(
                                        doc, el, window.TagIfc, window.SelectedGeometry, tr, cachedMatId);

                                    if (ds != null) created++;
                                    else           { failed++; failedIds.Add(el.Id); }
                                }
                                catch (Exception ex)
                                {
                                    failed++;
                                    failedIds.Add(el.Id);
                                    string reason = ex.Message;
                                    if (reason.Length > 100) reason = reason.Substring(0, 97) + "...";
                                    
                                    if (failReasons.Count < 10)
                                        failReasons.Add(string.Format("ID {0}: {1}", el.Id.Value, reason));
                                }
                            }
                        });
                }
                catch (Exception ex)
                {
                    // Log batch failure but continue with remaining batches
                    failed += batch.Count;
                    if (failReasons.Count < 10)
                        failReasons.Add(string.Format("Batch {0} failed: {1}", batchNum, ex.Message));
                }
            }

            // ── 5. Result report ────────────────────────────────────────────────
            string report = BuildReport(created, failed, failedIds, failReasons, window);
            TaskDialog.Show(Constants.AddInName + " — Done", report);

            return created > 0 ? Result.Succeeded : Result.Failed;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static string BuildReport(
            int created,
            int failed,
            IList<ElementId> failedIds,
            IList<string> failReasons,
            ClearanceBoxWindow settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"✅ Created: {created} clearance DirectShape(s)");

            if (failed > 0)
            {
                sb.AppendLine($"⚠ Failed:  {failed} element(s) skipped (invalid geometry)");
                
                if (failReasons.Count > 0)
                {
                    sb.AppendLine("\nReasons (first 10):");
                    foreach (var r in failReasons) sb.AppendLine(" • " + r);
                }

                // Show first 15 IDs
                var idsStrings = failedIds.Take(15).Select(id => id.ToString());
                sb.AppendLine("\nIDs: " + string.Join(", ", idsStrings) + (failed > 15 ? "..." : ""));
            }

            if (settings.TagIfc)
                sb.AppendLine("🏷 IFC tag: IfcSpace applied");

            sb.AppendLine();
            sb.AppendLine("Tip: Clearance volumes are tracked by door ID.");
            sb.AppendLine("If you resize a door, the linked volume is flagged as STALE.");
            sb.AppendLine("Re-run this command to regenerate updated volumes.");

            return sb.ToString();
        }
    }
}
