using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.AutoFoundation.Services;
using Antigravity.AutoFoundation.UI;

namespace Antigravity.AutoFoundation.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoFoundationCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                message = "No active Revit document.";
                return Result.Failed;
            }

            var doc = uidoc.Document;

            try
            {
                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(level => level.Elevation)
                    .ToList();
                var foundationTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                    .Cast<FamilySymbol>()
                    .Where(HasWritableDimensions)
                    .OrderBy(symbol => symbol.FamilyName)
                    .ThenBy(symbol => symbol.Name)
                    .ToList();

                if (levels.Count == 0 || foundationTypes.Count == 0)
                {
                    message = levels.Count == 0
                        ? "The project contains no levels."
                        : "No structural foundation type has writable Length and Width parameters.";
                    TaskDialog.Show("Auto Foundation", message);
                    return Result.Failed;
                }

                var viewModel = new AutoFoundationViewModel
                {
                    Levels = levels,
                    FoundationFamilies = foundationTypes,
                    SelectedLevel = levels[0],
                    SelectedFamily = foundationTypes[0]
                };
                var handler = new AutoFoundationRevitEventHandler(doc, viewModel);
                var externalEvent = ExternalEvent.Create(handler);
                new AutoFoundationWindow(viewModel, externalEvent).Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static bool HasWritableDimensions(FamilySymbol symbol)
        {
            var length = symbol.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH)
                ?? symbol.LookupParameter("Length");
            var width = symbol.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH)
                ?? symbol.LookupParameter("Width");
            return length != null && width != null
                && !length.IsReadOnly && !width.IsReadOnly
                && length.StorageType == StorageType.Double
                && width.StorageType == StorageType.Double;
        }
    }
}
