using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace Antigravity.HoanThien.Services
{
    public class RoomCheckerService
    {
        public static void CheckUnplacedRooms(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var view = doc.ActiveView;

            if (view.GenLevel == null)
            {
                TaskDialog.Show("Error", "Lệnh này chỉ chạy được trên mặt bằng (Plan View) có Level.");
                return;
            }

            var phaseId = view.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId();
            Phase phase = null;
            if (phaseId != null && phaseId != ElementId.InvalidElementId)
            {
                phase = doc.GetElement(phaseId) as Phase;
            }

            if (phase == null)
            {
                TaskDialog.Show("Error", "Không tìm thấy Phase của View hiện tại.");
                return;
            }

            List<XYZ> unplacedPoints = new List<XYZ>();

            using (TransactionGroup tg = new TransactionGroup(doc, "Check Unplaced Rooms"))
            {
                tg.Start();

                using (Transaction tx = new Transaction(doc, "Temp Rooms"))
                {
                    tx.Start();
                    
                    // Create rooms in all unplaced enclosed regions
                    var newRoomIds = doc.Create.NewRooms2(view.GenLevel, phase);
                    
                    foreach (ElementId id in newRoomIds)
                    {
                        if (doc.GetElement(id) is Room room && room.Location is LocationPoint locPt)
                        {
                            unplacedPoints.Add(locPt.Point);
                        }
                    }

                    // Rollback to undo room creation
                    tx.RollBack();
                }

                if (unplacedPoints.Count > 0)
                {
                    using (Transaction tx2 = new Transaction(doc, "Mark Unplaced Rooms"))
                    {
                        tx2.Start();

                        foreach (var pt in unplacedPoints)
                        {
                            // Draw an X marker of size 1000mm
                            double size = 1000.0 / 304.8; // Convert to feet
                            XYZ p1 = pt + new XYZ(-size, -size, 0);
                            XYZ p2 = pt + new XYZ(size, size, 0);
                            XYZ p3 = pt + new XYZ(-size, size, 0);
                            XYZ p4 = pt + new XYZ(size, -size, 0);

                            Line line1 = Line.CreateBound(p1, p2);
                            Line line2 = Line.CreateBound(p3, p4);

                            var curve1 = doc.Create.NewDetailCurve(view, line1);
                            var curve2 = doc.Create.NewDetailCurve(view, line2);
                        }

                        tx2.Commit();
                    }
                    TaskDialog.Show("Kết quả", $"Phát hiện {unplacedPoints.Count} khu vực kín chưa được đặt Room. Đã đánh dấu 'X' trên mặt bằng.");
                }
                else
                {
                    TaskDialog.Show("Kết quả", "Không phát hiện khu vực kín nào bị bỏ sót (tất cả đều đã có Room).");
                }

                tg.Commit();
            }
        }
    }
}
