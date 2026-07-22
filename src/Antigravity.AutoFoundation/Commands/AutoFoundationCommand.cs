using Antigravity.AutoFoundation.Utils;
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

                if (levels.Count == 0)
                {
                    message = "The project contains no levels.";
                    TaskDialog.Show("Auto Foundation", message);
                    return Result.Failed;
                }

                var viewModel = new AutoFoundationViewModel
                {
                    Levels = levels,
                    FoundationFamilies = foundationTypes,
                    SelectedLevel = levels[0],
                    SelectedFamily = foundationTypes.FirstOrDefault()
                };
                new AutoFoundationWindow(viewModel, uiapp).ShowDialog();
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
            return FoundationParameterHelper.HasWritableDimensions(symbol);
        }
    }
}

