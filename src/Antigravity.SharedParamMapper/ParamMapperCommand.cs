using System;
using System.Linq;
using Antigravity.Core.Services;
using Antigravity.SharedParamMapper.ViewModels;
using Antigravity.SharedParamMapper.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Antigravity.SharedParamMapper
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ParamMapperCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                if (!SecurityService.IsAuthorized(uiapp.Application, out bool _))
                {
                    TaskDialog.Show("Security", "Not authorized to run this command.");
                    return Result.Failed;
                }

                var selectedIds = uidoc.Selection.GetElementIds();
                var viewModel = new ParamMapperViewModel(doc, selectedIds);
                var window = new ParamMapperWindow(viewModel);

                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                AppLogger.Error(ex, "ParamMapperCommand Error");
                return Result.Failed;
            }
        }
    }
}
