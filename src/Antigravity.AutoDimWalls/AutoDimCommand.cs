using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.AutoDimWalls.UI;
using Antigravity.AutoDimWalls.Services;

namespace Antigravity.AutoDimWalls
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class AutoDimCommand : IExternalCommand
    {
        private static AutoDimWindow _activeWindow = null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDoc = commandData.Application.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document == null)
                {
                    TaskDialog.Show("AutoDim Walls", "Open a Revit project before running AutoDim Walls.");
                    return Result.Cancelled;
                }

                if (_activeWindow != null && _activeWindow.IsLoaded)
                {
                    _activeWindow.Activate();
                    return Result.Succeeded;
                }

                var handler = new AutoDimEventHandler();
                var externalEvent = ExternalEvent.Create(handler);

                _activeWindow = new AutoDimWindow(uiDoc, externalEvent, handler);
                var helper = new System.Windows.Interop.WindowInteropHelper(_activeWindow)
                {
                    Owner = commandData.Application.MainWindowHandle
                };

                _activeWindow.Closed += (s, e) => { _activeWindow = null; };
                _activeWindow.Show();

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
