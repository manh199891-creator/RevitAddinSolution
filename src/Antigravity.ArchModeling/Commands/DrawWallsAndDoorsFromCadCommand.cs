using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.ArchModeling.UI;
using System;

namespace Antigravity.ArchModeling.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DrawWallsAndDoorsFromCadCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var wnd = new ArchModelingWindow(uiApp, ArchModelingMode.Combined);
                wnd.ShowDialog();
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
