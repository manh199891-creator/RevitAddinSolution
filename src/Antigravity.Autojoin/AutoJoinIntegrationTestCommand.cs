using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.Autojoin.Services;

namespace Antigravity.Autojoin
{
    [Transaction(TransactionMode.Manual)]
    public class AutoJoinIntegrationTestCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var result = AutoJoinIntegrationTestService.RunSelectionJoinTest(uidoc);

            TaskDialog.Show(
                "AutoJoin Integration Test",
                $"{result.Message}\n\nStatus: {result.StatusMessage}");

            if (result.Passed) return Result.Succeeded;

            message = result.Message;
            return Result.Failed;
        }
    }
}
