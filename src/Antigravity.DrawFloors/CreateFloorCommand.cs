using System;
using System.Diagnostics;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.DrawFloors.Services;
using Antigravity.DrawFloors.UI;

namespace Antigravity.DrawFloors
{
    [Transaction(TransactionMode.Manual)]
    public class CreateFloorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Tạo handler và ExternalEvent
                var handler = new FloorCreationHandler();
                ExternalEvent exEvent = ExternalEvent.Create(handler);

                // Tạo cửa sổ dạng modeless (Show, không dùng ShowDialog)
                // để ExternalEvent có thể được raise và execute đúng cách
                MainWindow window = new MainWindow(commandData, handler, exEvent);

                IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
                new WindowInteropHelper(window).Owner = handle;

                window.Show();
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
