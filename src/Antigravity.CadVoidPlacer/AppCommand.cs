using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.CadVoidPlacer.Services;
using System;

namespace Antigravity.CadVoidPlacer
{
    [Transaction(TransactionMode.Manual)]
    public class AppCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                var mainWindow = new UI.MainWindow(doc, uidoc);
                bool? result = mainWindow.ShowDialog();

                if (result != true) return Result.Cancelled;

                // Phase 05: Place voids as DirectShape — no family required
                int placedCount = PlacementService.PlaceVoids(
                    doc,
                    mainWindow.ExtractedOpenings,
                    mainWindow.GridMapper,
                    mainWindow.DepthMm,
                    mainWindow.SelectedLevelId);

                TaskDialog.Show("Placement Complete",
                    $"Successfully placed {placedCount} out of {mainWindow.ExtractedOpenings.Count} void(s).\n\n" +
                    $"Depth: {mainWindow.DepthMm:F0} mm\n" +
                    $"Category: Generic Models (DirectShape)");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
