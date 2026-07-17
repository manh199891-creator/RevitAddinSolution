using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DoorClearanceBox.Core;

namespace DoorClearanceBox.Commands
{
    /// <summary>
    /// IExternalCommand — Clear all Door Clearance DirectShapes from the active document.
    /// Shows a confirmation TaskDialog before deleting.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class ClearClearanceBoxCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                              ref string message,
                              ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                TaskDialog.Show(Constants.AddInName, "Please open a document first before using this tool.");
                return Result.Cancelled;
            }
            Document doc = uiDoc.Document;

            // Count shapes before asking
            var existing = ReserveSpaceGeometry.GetAll(doc);

            if (existing.Count == 0)
            {
                TaskDialog.Show(Constants.AddInName,
                    "No clearance DirectShapes found in this document.");
                return Result.Cancelled;
            }

            // Confirmation
            var confirm = new TaskDialog(Constants.AddInName + " — Clear Clearance Boxes")
            {
                MainInstruction = $"Delete {existing.Count} clearance DirectShape(s)?",
                MainContent     = "This action cannot be undone outside the undo stack.\n\n" +
                                  "The door families themselves are NOT affected.",
                CommonButtons   = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton   = TaskDialogResult.No,
                MainIcon        = TaskDialogIcon.TaskDialogIconWarning,
            };

            if (confirm.Show() != TaskDialogResult.Yes)
                return Result.Cancelled;

            // Delete
            int deleted = 0;
            try
            {
                TransactionWrapper.Execute(doc, "Clear Door Clearance Boxes", _ =>
                {
                    deleted = ReserveSpaceGeometry.DeleteAll(doc);
                });
            }
            catch (System.Exception ex)
            {
                message = $"Failed to delete clearance boxes:\n{ex.Message}";
                return Result.Failed;
            }

            TaskDialog.Show(Constants.AddInName,
                $"✅ Removed {deleted} clearance DirectShape(s).");

            return Result.Succeeded;
        }
    }
}
