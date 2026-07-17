using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.Autojoin.UI;

namespace Antigravity.Autojoin
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoJoinCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("AutoJoin", "Vui lòng mở một tài liệu Revit trước.");
                return Result.Cancelled;
            }

            var win = new MainWindow(uidoc);
            win.Show();
            return Result.Succeeded;
        }
    }
}
