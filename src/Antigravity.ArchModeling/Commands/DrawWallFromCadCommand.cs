using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.ArchModeling.UI;

namespace Antigravity.ArchModeling.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DrawWallFromCadCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var window = new ArchModelingWindow(commandData.Application, ArchModelingMode.Wall);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
