using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace LOQN1_Location_element
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        private const string TARGET_PARAM_NAME = "LOQN1_Location";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;

            if (uiDoc == null || uiDoc.Document == null)
            {
                message = "Không tìm thấy tài liệu Revit active.";
                return Result.Failed;
            }

            Document doc = uiDoc.Document;

            try
            {
                // ========================================================
                // BƯỚC 1: Lấy danh sách Level & Hiển thị Form cấu hình (UI mới)
                // ========================================================
                
                // Lấy đối tượng đã chọn sẵn
                ICollection<ElementId> preSelectedIds = uiDoc.Selection.GetElementIds();
                List<Element> preSelectedElements = new List<Element>();

                if (preSelectedIds != null && preSelectedIds.Count > 0)
                {
                    foreach (ElementId id in preSelectedIds)
                    {
                        Element elem = doc.GetElement(id);
                        if (elem != null && elem.Category != null)
                        {
                            preSelectedElements.Add(elem);
                        }
                    }
                }
                
                bool hasPreSelection = preSelectedElements.Count > 0;

                // Lấy danh sách Level
                List<Level> sortedLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .WhereElementIsNotElementType()
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                List<Tuple<string, double>> levelDataForForm = sortedLevels
                    .Select(l => new Tuple<string, double>(l.Name, RevitUnitUtils.FeetToMeters(l.Elevation)))
                    .ToList();

                // Hiển thị Form mới (Đã tích hợp Filter Category/Type)
                LevelMappingWindow mappingWindow = new LevelMappingWindow(doc, hasPreSelection, levelDataForForm, null);
                bool? dialogResult = mappingWindow.ShowDialog();

                if (dialogResult != true || !mappingWindow.IsConfirmed)
                {
                    return Result.Cancelled;
                }

                Dictionary<string, string> finalMapping = mappingWindow.LevelMapping;
                bool useAllInstances = mappingWindow.UseAllInstances;
                ElementId filterCategoryId = mappingWindow.SelectedCategoryId;
                ElementId filterFamilyTypeId = mappingWindow.SelectedFamilyTypeId;

                // ========================================================
                // BƯỚC 2: Xác định & Lọc đối tượng cần xử lý
                // ========================================================
                List<Element> targetElements = new List<Element>();

                if (useAllInstances)
                {
                    FilteredElementCollector collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();
                    
                    if (filterCategoryId != null && filterCategoryId != ElementId.InvalidElementId)
                    {
                        collector.OfCategoryId(filterCategoryId);
                    }
                    
                    targetElements = collector.ToElements().ToList();
                    
                    if (filterFamilyTypeId != null && filterFamilyTypeId != ElementId.InvalidElementId)
                    {
                        targetElements = targetElements.Where(e => e.GetTypeId() == filterFamilyTypeId).ToList();
                    }
                }
                else
                {
                    // Chọn đối tượng thủ công (hoặc dùng pre-selection)
                    if (hasPreSelection)
                    {
                        targetElements = preSelectedElements;
                    }
                    else
                    {
                        targetElements = PickNewElements(uiDoc, doc);
                        if (targetElements == null) return Result.Cancelled;
                    }

                    // Lọc những đối tượng user chọn theo cấu hình form
                    if (filterCategoryId != null && filterCategoryId != ElementId.InvalidElementId)
                    {
                        targetElements = targetElements.Where(e => e.Category != null && e.Category.Id == filterCategoryId).ToList();
                    }
                    if (filterFamilyTypeId != null && filterFamilyTypeId != ElementId.InvalidElementId)
                    {
                        targetElements = targetElements.Where(e => e.GetTypeId() == filterFamilyTypeId).ToList();
                    }
                }

                if (targetElements.Count == 0)
                {
                    TaskDialog.Show("VILAIVIET-LOQN1-Location", "No valid elements found matching the current filter.");
                    return Result.Succeeded;
                }

                // ========================================================
                // BƯỚC 3: Kiểm tra Shared Parameter
                // ========================================================
                bool parameterExists = false;
                foreach (Element elem in targetElements)
                {
                    Parameter testParam = elem.LookupParameter(TARGET_PARAM_NAME);
                    if (testParam != null)
                    {
                        parameterExists = true;
                        break;
                    }
                }

                if (!parameterExists)
                {
                    TaskDialog td = new TaskDialog("VILAIVIET-LOQN1-Location - Missing Shared Parameter");
                    td.MainInstruction = "Missing Shared Parameter \"" + TARGET_PARAM_NAME + "\"!";
                    td.MainContent =
                        "The selected elements do not have the Shared Parameter \"" + TARGET_PARAM_NAME + "\".\n\n" +
                        "Please perform the following steps before running this tool again:\n\n" +
                        "1. Go to Manage → Shared Parameters → Create Parameter \"" + TARGET_PARAM_NAME + "\" (Type: Text)\n" +
                        "2. Go to Manage → Project Parameters → Add → Shared Parameter\n" +
                        "3. Select \"" + TARGET_PARAM_NAME + "\" → check the required Categories\n" +
                        "   (Structural Framing, Columns, Walls, Floors, MEP Equipment...)\n" +
                        "4. Check Instance → OK\n" +
                        "5. Run the VILAIVIET-LOQN1-Location tool again.";
                    td.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
                    td.Show();
                    return Result.Succeeded;
                }

                // ========================================================
                // BƯỚC 4: Xử lý Group
                // ========================================================
                HashSet<ElementId> groupIdsToUngroup = new HashSet<ElementId>();
                List<Element> elementsInGroups = new List<Element>();
                List<Element> elementsNotInGroups = new List<Element>();

                foreach (Element elem in targetElements)
                {
                    if (elem.GroupId != null && elem.GroupId != ElementId.InvalidElementId)
                    {
                        elementsInGroups.Add(elem);
                        groupIdsToUngroup.Add(elem.GroupId);
                    }
                    else
                    {
                        elementsNotInGroups.Add(elem);
                    }
                }

                bool userChoseUngroup = false;
                bool shouldRegroup = false;
                if (elementsInGroups.Count > 0)
                {
                    TaskDialog groupDlg = new TaskDialog("VILAIVIET-LOQN1-Location - Groups Detected");
                    groupDlg.MainInstruction = string.Format("Found {0} elements contained in {1} Groups!",
                        elementsInGroups.Count, groupIdsToUngroup.Count);
                    groupDlg.MainContent =
                        "Revit does not allow modifying Parameters of elements within a Group.\n" +
                        "How would you like to proceed?";
                    groupDlg.MainIcon = TaskDialogIcon.TaskDialogIconWarning;

                    groupDlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                        "Auto Ungroup → Update → Regroup",
                        "The tool will temporarily ungroup, update the parameters, and recreate the group.");

                    groupDlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink2,
                        "Auto Ungroup → Update (Do not Regroup)",
                        "The tool will permanently ungroup the elements and update their parameters.");

                    groupDlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink3,
                        "Skip elements in Groups",
                        "Only update elements that are NOT in a Group.");

                    groupDlg.CommonButtons = TaskDialogCommonButtons.Cancel;
                    groupDlg.DefaultButton = TaskDialogResult.CommandLink1;

                    TaskDialogResult groupChoice = groupDlg.Show();

                    if (groupChoice == TaskDialogResult.Cancel)
                    {
                        return Result.Cancelled;
                    }
                    else if (groupChoice == TaskDialogResult.CommandLink1)
                    {
                        userChoseUngroup = true;
                        shouldRegroup = true;
                    }
                    else if (groupChoice == TaskDialogResult.CommandLink2)
                    {
                        userChoseUngroup = true;
                        shouldRegroup = false;
                    }
                }

                // ========================================================
                // BƯỚC 5: Ghi giá trị (Transaction)
                // ========================================================
                GridLocationHelper gridHelper = new GridLocationHelper(doc);
                
                int updatedCount = 0;
                int skippedNoParamCount = 0;
                int skippedReadOnlyCount = 0;
                int skippedGroupCount = 0;
                int skippedErrorCount = 0;
                int ungroupedCount = 0;
                List<string> updatedDetails = new List<string>();
                List<ElementId> missingParamIds = new List<ElementId>();
                Dictionary<string, int> skippedCategories = new Dictionary<string, int>();

                HashSet<string> ignoredCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Model Groups", "Grids", "Levels", "Constraints", "Array", 
                    "Views", "Cameras", "Dimensions", "Text Notes", "Lines", 
                    "Model Lines", "Detail Items", "Materials", "Rooms", "Spaces", 
                    "Areas", "Scope Boxes", "Section Boxes", "Reference Planes", 
                    "Reference Lines", "Project Information", "No Category"
                };

                using (Transaction trans = new Transaction(doc, "VILAIVIET-LOQN1-Location: Cập nhật dữ liệu"))
                {
                    trans.Start();

                    FailureHandlingOptions failOpts = trans.GetFailureHandlingOptions();
                    failOpts.SetFailuresPreprocessor(new GroupWarningSwallower());
                    trans.SetFailureHandlingOptions(failOpts);

                    List<Element> ungroupedElements = new List<Element>();
                    List<List<ElementId>> groupsToRegroup = new List<List<ElementId>>();

                    if (userChoseUngroup && groupIdsToUngroup.Count > 0)
                    {
                        foreach (ElementId gid in groupIdsToUngroup)
                        {
                            try
                            {
                                Group grp = doc.GetElement(gid) as Group;
                                if (grp != null)
                                {
                                    ICollection<ElementId> memberIds = grp.UngroupMembers();
                                    ungroupedCount++;

                                    List<ElementId> currentGroupMembers = new List<ElementId>();
                                    foreach (ElementId mid in memberIds)
                                    {
                                        Element memberElem = doc.GetElement(mid);
                                        if (memberElem != null && memberElem.Category != null)
                                        {
                                            // Vẫn phải kiểm tra lại filter sau khi Ungroup
                                            bool passFilter = true;
                                            if (filterCategoryId != null && filterCategoryId != ElementId.InvalidElementId)
                                            {
                                                if (memberElem.Category.Id != filterCategoryId) passFilter = false;
                                            }
                                            if (filterFamilyTypeId != null && filterFamilyTypeId != ElementId.InvalidElementId)
                                            {
                                                if (memberElem.GetTypeId() != filterFamilyTypeId) passFilter = false;
                                            }
                                            
                                            if (passFilter)
                                            {
                                                ungroupedElements.Add(memberElem);
                                            }
                                            currentGroupMembers.Add(mid);
                                        }
                                    }
                                    if (currentGroupMembers.Count > 0)
                                    {
                                        groupsToRegroup.Add(currentGroupMembers);
                                    }
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }

                    List<Element> allProcessElements = new List<Element>(elementsNotInGroups);
                    if (userChoseUngroup)
                    {
                        allProcessElements.AddRange(ungroupedElements);
                    }

                    foreach (Element elem in allProcessElements)
                    {
                        try
                        {
                            if (elem.GroupId != null && elem.GroupId != ElementId.InvalidElementId)
                            {
                                skippedGroupCount++;
                                continue;
                            }

                            Parameter param = elem.LookupParameter(TARGET_PARAM_NAME);

                            if (param == null)
                            {
                                string cName = elem.Category != null ? elem.Category.Name : "No Category";
                                
                                if (elem is BaseArray || ignoredCategories.Contains(cName))
                                {
                                    continue;
                                }

                                if (elem is FamilyInstance fi && fi.SuperComponent != null)
                                {
                                    continue;
                                }

                                missingParamIds.Add(elem.Id);
                                skippedNoParamCount++;
                                if (skippedCategories.ContainsKey(cName)) skippedCategories[cName]++;
                                else skippedCategories[cName] = 1;
                                continue;
                            }

                            if (param.IsReadOnly)
                            {
                                skippedReadOnlyCount++;
                                continue;
                            }

                            ElementPositionData posData = ElementPositionHelper.ExtractPositionData(elem, doc, sortedLevels);

                            var gridX = gridHelper.GetGridXRange(posData.MinX, posData.MaxX);
                            var gridY = gridHelper.GetGridYRange(posData.MinY, posData.MaxY);

                            string originalLevelName = posData.ReferenceLevel != null ? posData.ReferenceLevel.Name : "N/A";
                            string levelName = originalLevelName;
                            if (finalMapping.ContainsKey(originalLevelName))
                            {
                                levelName = finalMapping[originalLevelName];
                            }
                            string offsetStr = RevitUnitUtils.FormatElevationOffset(posData.ElevationOffsetInFeet);

                            string locationValue = string.Format("{0}, ({1}-{2}), ({3}-{4}), {5}",
                                levelName, gridX.StartGrid, gridX.EndGrid, gridY.StartGrid, gridY.EndGrid, offsetStr);

                            param.Set(locationValue);
                            updatedCount++;

                            if (updatedDetails.Count < 10)
                            {
                                string elemName = elem.Name ?? "";
                                string catName = elem.Category != null ? elem.Category.Name : "";
                                updatedDetails.Add(string.Format("  • [{0}] {1} (Id:{2}) → {3}",
                                    catName, elemName, elem.Id.Value, locationValue));
                            }
                        }
                        catch (Exception)
                        {
                            skippedErrorCount++;
                        }
                    }

                    if (shouldRegroup && groupsToRegroup.Count > 0)
                    {
                        foreach (var regroupIds in groupsToRegroup)
                        {
                            try
                            {
                                doc.Create.NewGroup(regroupIds);
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }

                    trans.Commit();
                }

                if (!userChoseUngroup)
                {
                    skippedGroupCount += elementsInGroups.Count;
                }

                // ========================================================
                // BƯỚC 6: Báo cáo
                // ========================================================
                TaskDialog resultDlg = new TaskDialog("VILAIVIET-LOQN1-Location - Complete");

                if (updatedCount > 0)
                {
                    resultDlg.MainInstruction = string.Format("Successfully updated {0} elements!", updatedCount);
                    resultDlg.MainIcon = TaskDialogIcon.TaskDialogIconNone;
                }
                else
                {
                    resultDlg.MainInstruction = "No elements were updated.";
                    resultDlg.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
                }

                string detailContent = "";

                if (updatedDetails.Count > 0)
                {
                    detailContent += "Update details:\n" + string.Join("\n", updatedDetails);
                    if (updatedCount > 10)
                    {
                        detailContent += string.Format("\n  ... and {0} other elements.", updatedCount - 10);
                    }
                    detailContent += "\n\n";
                }

                if (ungroupedCount > 0)
                {
                    string regroupNote = shouldRegroup ? " (regrouped)" : " (permanently ungrouped)";
                    detailContent += string.Format("→ Ungrouped {0} Groups{1}.\n\n", ungroupedCount, regroupNote);
                }

                if (skippedGroupCount > 0)
                {
                    detailContent += string.Format("⚠ Skipped {0} elements (in Groups).\n\n", skippedGroupCount);
                }

                if (skippedNoParamCount > 0)
                {
                    detailContent += string.Format("⚠ Skipped {0} elements (missing Parameter \"{1}\").\n",
                        skippedNoParamCount, TARGET_PARAM_NAME);
                    detailContent += "  → Go to Manage → Project Parameters to assign it to the respective Categories.\n\n";
                }

                if (skippedReadOnlyCount > 0)
                {
                    detailContent += string.Format("⚠ Skipped {0} elements (Read-Only Parameter).\n\n", skippedReadOnlyCount);
                }

                if (skippedErrorCount > 0)
                {
                    detailContent += string.Format("⚠ Skipped {0} elements (unknown error).\n", skippedErrorCount);
                }

                resultDlg.MainContent = detailContent;

                if (missingParamIds.Count > 0)
                {
                    resultDlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                        "Select elements with missing Parameter",
                        "The tool will select these " + missingParamIds.Count + " elements in the view.");
                    resultDlg.CommonButtons = TaskDialogCommonButtons.Close;
                    resultDlg.DefaultButton = TaskDialogResult.CommandLink1;
                }
                else
                {
                    resultDlg.CommonButtons = TaskDialogCommonButtons.Ok;
                }

                TaskDialogResult finalChoice = resultDlg.Show();

                if (missingParamIds.Count > 0 && finalChoice == TaskDialogResult.CommandLink1)
                {
                    uiDoc.Selection.SetElementIds(missingParamIds);
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = "Execution error: " + ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private List<Element> PickNewElements(UIDocument uiDoc, Document doc)
        {
            IList<Reference> pickedRefs;
            try
            {
                pickedRefs = uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    "VILAIVIET-LOQN1-Location: Select elements, then click Finish.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }

            if (pickedRefs == null || pickedRefs.Count == 0)
            {
                return new List<Element>();
            }

            List<Element> result = new List<Element>();
            foreach (Reference r in pickedRefs)
            {
                Element elem = doc.GetElement(r);
                if (elem != null && elem.Category != null)
                {
                    result.Add(elem);
                }
            }
            return result;
        }
    }

    internal class GroupWarningSwallower : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            IList<FailureMessageAccessor> failMessages = failuresAccessor.GetFailureMessages();

            foreach (FailureMessageAccessor fma in failMessages)
            {
                if (fma.GetSeverity() == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(fma);
                }
            }

            return FailureProcessingResult.Continue;
        }
    }
}
