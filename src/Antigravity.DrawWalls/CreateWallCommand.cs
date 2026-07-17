using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.DrawWalls.UI;
using System;

namespace Antigravity.DrawWalls
{
    [Transaction(TransactionMode.Manual)]
    public class CreateWallCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                MainWindow window = new MainWindow(commandData);
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = "Lỗi khi chạy lệnh: " + ex.Message;
                return Result.Failed;
            }
        }
    }
}
