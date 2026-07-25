using System;
using System.Linq;
using Antigravity.Core.Services;
using Antigravity.SharedParamMapper.ViewModels;
using Antigravity.SharedParamMapper.Views;
using Antigravity.SharedParamMapper.Services;
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
                var handler = new ActionEventHandler();
                var viewModel = new ParamMapperViewModel(uiapp, selectedIds, handler);
                var window = new ParamMapperWindow(viewModel);

                var interop = new System.Windows.Interop.WindowInteropHelper(window)
                {
                    Owner = uiapp.MainWindowHandle
                };

                window.Show();

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
