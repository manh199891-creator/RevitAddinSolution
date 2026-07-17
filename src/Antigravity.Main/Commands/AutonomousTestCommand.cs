using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Antigravity.Core.Services;

namespace Antigravity.Main.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutonomousTestCommand : IExternalCommand
    {
        // 1. Giao tiếp với UI (External Command Pattern)
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Failed;

            return Execute2(uidoc.Document);
        }

        // 2. Logic lõi (Không dính tới UI, dễ dàng gọi từ Automation Hook)
        public static Result Execute2(Document doc)
        {
            AutomationLogger.Write("AutonomousTestCommand", "Bắt đầu thực thi Execute2");
            
            try
            {
                using (Transaction t = new Transaction(doc, "Test Automation"))
                {
                    t.Start();
                    
                    // Logic test ví dụ: Lấy số lượng Wall trong mô hình
                    FilteredElementCollector collector = new FilteredElementCollector(doc);
                    int wallCount = collector.OfClass(typeof(Wall)).GetElementCount();
                    
                    AutomationLogger.Write("AutonomousTestCommand", $"Mô hình hiện có {wallCount} bức tường.");
                    
                    t.Commit();
                }

                AutomationLogger.Write("AutonomousTestCommand", "Thực thi thành công");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                AutomationLogger.WriteError("AutonomousTestCommand", ex);
                return Result.Failed;
            }
        }
    }
}
