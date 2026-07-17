using System;
using System.Diagnostics;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.WallMepClash.UI;
using Antigravity.Core.Services;

namespace Antigravity.WallMepClash
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class WallMepClashCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDoc = commandData.Application.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document == null)
                {
                    message = "No active Revit document is open.";
                    return Result.Failed;
                }

                var dialog = new WallMepClashDialog(uiDoc);
                IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
                new WindowInteropHelper(dialog).Owner = handle;
                dialog.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "[WallMepClash] Command failed");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
