using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.TagArranger.UI;
using Antigravity.TagArranger.Services;

namespace Antigravity.TagArranger
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class TagArrangeCommand : IExternalCommand
    {
        private static ArrangerWindow _activeWindow = null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDoc = commandData.Application.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document == null)
                {
                    TaskDialog.Show("Tag Arranger", "Mở một dự án Revit trước khi chạy Tag Arranger.");
                    return Result.Cancelled;
                }

                if (_activeWindow != null && _activeWindow.IsLoaded)
                {
                    _activeWindow.Activate();
                    return Result.Succeeded;
                }

                var handler = new ArrangerEventHandler();
                var externalEvent = ExternalEvent.Create(handler);

                _activeWindow = new ArrangerWindow(uiDoc, externalEvent, handler);
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
