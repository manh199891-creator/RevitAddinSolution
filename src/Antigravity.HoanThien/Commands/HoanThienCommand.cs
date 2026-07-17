using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.HoanThien.UI;

namespace Antigravity.HoanThien.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class HoanThienCommand : IExternalCommand
    {
        private static UI.HoanThienWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (_window != null && _window.IsLoaded)
            {
                _window.Focus();
                return Result.Succeeded;
            }

            var handler = new Handlers.HoanThienRequestHandler();
            var exEvent = ExternalEvent.Create(handler);

            _window = new UI.HoanThienWindow(commandData.Application, exEvent, handler);
            _window.Show();

            return Result.Succeeded;
        }
    }
}
