using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DoorClearanceBox.UI;

namespace DoorClearanceBox.Commands
{
    /// <summary>
    /// IExternalCommand — Clash Control Command
    /// Launches the ClashControlWindow WPF interface.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class ClashControlCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                              ref string message,
                              ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument    uiDoc = uiApp.ActiveUIDocument;
            
            if (uiDoc == null || uiDoc.Document == null)
            {
                TaskDialog.Show("Kiểm Soát Xung Đột", "Vui lòng mở một dự án Revit trước khi chạy công cụ này.");
                return Result.Cancelled;
            }

            // Launch WPF window
            var window = new ClashControlWindow(uiDoc);

            // Bind window owner to Revit main window handle
            var wih = new System.Windows.Interop.WindowInteropHelper(window);
            wih.Owner = uiApp.MainWindowHandle;

            try
            {
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Lỗi khi mở giao diện Kiểm Soát Xung Đột:\n{ex.Message}";
                return Result.Failed;
            }
        }
    }
}
