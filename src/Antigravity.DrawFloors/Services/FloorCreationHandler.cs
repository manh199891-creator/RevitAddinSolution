using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Antigravity.DrawFloors.Services
{
    /// <summary>
    /// Data cho một sàn cần tạo (dùng trong batch mode từ Hatch).
    /// </summary>
    public class FloorCreationItem
    {
        public ElementId LevelId { get; set; }
        public ElementId FloorTypeId { get; set; }
        public double OffsetMm { get; set; }
        
        // Chứa danh sách các đỉnh hoặc CurveLoop dạng dữ liệu thô
        public List<List<XYZ>> ProfilePoints { get; set; }
        public List<List<Curve>> ProfileCurves { get; set; }
    }

    /// <summary>
    /// ExternalEventHandler để chạy Revit Transaction trong đúng API context.
    /// Được raise từ button click trong WPF modeless window.
    /// </summary>
    public class FloorCreationHandler : IExternalEventHandler
    {
        // ── Input: single floor ────────────────────────────────
        public ElementId TargetLevelId { get; set; }
        public ElementId TargetFloorTypeId { get; set; }
        public double OffsetMm { get; set; }
        public List<List<XYZ>> ProfilePoints { get; set; }
        public List<List<Curve>> ProfileCurves { get; set; }
        public string TransactionName { get; set; } = "Create floor from CAD";

        // ── Input: batch mode (AutoHatch) ──────────────────────
        public bool IsBatchMode { get; set; }
        public List<FloorCreationItem> BatchItems { get; set; }

        // ── Output ─────────────────────────────────────────────
        public bool Success { get; private set; }
        public string ResultMessage { get; private set; }

        // ── Callback để notify WPF window sau khi hoàn tất ────
        public int TotalScannedHatches { get; set; } = 0;
        public Action<bool, string> OnComplete { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null) return;

            try
            {
                using (var tx = new Transaction(doc, TransactionName))
                {
                    tx.Start();
                    var builder = new RevitFloorBuilder(doc);
                    int count = 0;
                    var errors = new List<string>();

                    if (IsBatchMode && BatchItems != null)
                    {
                        foreach (var item in BatchItems)
                        {
                            try
                            {
                                List<string> loopErrors;
                                var loops = item.ProfileCurves != null && item.ProfileCurves.Count > 0
                                    ? BuildCurveLoops(item.ProfileCurves, out loopErrors)
                                    : BuildCurveLoops(item.ProfilePoints, out loopErrors);
                                
                                if (loops.Count == 0)
                                {
                                    if (loopErrors.Count > 0) errors.AddRange(loopErrors.Select(e => $"Item (FloorType={item.FloorTypeId}): " + e));
                                    else errors.Add($"Item (FloorType={item.FloorTypeId}): Không tìm thấy đường bao khép kín.");
                                    continue;
                                }

                                var level = doc.GetElement(item.LevelId) as Level;
                                var fType = doc.GetElement(item.FloorTypeId) as FloorType;

                                if (level != null && fType != null)
                                {
                                    builder.CreateFloor(level, fType, item.OffsetMm, loops);
                                    count++;
                                }
                            }
                            catch (Exception ex) { errors.Add(ex.Message); }
                        }
                        if (TotalScannedHatches > 0)
                            ResultMessage = $"✅ Đã quét được {TotalScannedHatches} hatch.\n✅ Vẽ thành công {count} sàn.";
                        else
                            ResultMessage = $"✅ Successfully created {count} floor elements.";
                        
                        if (errors.Count > 0) 
                        {
                            var uniqueErrors = errors.Distinct().Take(5);
                            ResultMessage += $"\n⚠️ {errors.Count} floors failed. Chi tiết lỗi:\n" + string.Join("\n", uniqueErrors);
                        }
                    }
                    else if ((ProfileCurves != null && ProfileCurves.Count > 0) || (ProfilePoints != null && ProfilePoints.Count > 0))
                    {
                        List<string> loopErrors;
                        var loops = ProfileCurves != null && ProfileCurves.Count > 0
                            ? BuildCurveLoops(ProfileCurves, out loopErrors)
                            : BuildCurveLoops(ProfilePoints, out loopErrors);
                        var level = doc.GetElement(TargetLevelId) as Level;
                        var fType = doc.GetElement(TargetFloorTypeId) as FloorType;

                        if (loops.Count > 0 && level != null && fType != null)
                        {
                            builder.CreateFloor(level, fType, OffsetMm, loops);
                            ResultMessage = "✅ Floor created successfully!";
                        }
                        else
                        {
                            tx.RollBack();
                            string reason = loopErrors.Count > 0 ? string.Join("\n", loopErrors) : "Insufficient data or Level/FloorType not found.";
                            OnComplete?.Invoke(false, reason);
                            return;
                        }
                    }
                    else
                    {
                        tx.RollBack();
                        OnComplete?.Invoke(false, "No valid profile data found.");
                        return;
                    }

                    tx.Commit();
                    Success = true;
                }
            }
            catch (Exception ex)
            {
                Success = false;
                ResultMessage = "Error creating floor:\n" + ex.Message;
            }

            OnComplete?.Invoke(Success, ResultMessage);
        }

        /// <summary>
        /// Tạo CurveLoops từ danh sách tập hợp các điểm XYZ ngay trong API Context.
        /// </summary>
        private List<CurveLoop> BuildCurveLoops(List<List<XYZ>> allLoopsPoints, out List<string> errors)
        {
            var result = new List<CurveLoop>();
            errors = new List<string>();
            if (allLoopsPoints == null) return result;

            foreach (var pts in allLoopsPoints.OrderByDescending(GetPointsArea))
            {
                if (pts.Count < 3)
                {
                    errors.Add("Quá ít điểm (yêu cầu ít nhất 3 điểm).");
                    continue;
                }
                var curves = new List<Curve>();
                for (int i = 0; i < pts.Count; i++)
                {
                    XYZ p1 = pts[i];
                    XYZ p2 = pts[(i + 1) % pts.Count];
                    if (p1.DistanceTo(p2) >= 0.003)
                    {
                        curves.Add(Line.CreateBound(p1, p2));
                    }
                }
                if (curves.Count >= 3)
                {
                    try
                    {
                        CurveLoop loop = CurveLoop.Create(curves);
                        bool shouldBeCcw = (result.Count == 0);
                        if (loop.IsCounterclockwise(XYZ.BasisZ) != shouldBeCcw)
                            loop.Flip();
                        result.Add(loop);
                    }
                    catch (Exception ex) { errors.Add($"CurveLoop.Create error: {ex.Message}"); }
                }
                else
                {
                    errors.Add("Số lượng Curve hợp lệ (>= 0.003 feet) ít hơn 3.");
                }
            }
            return result;
        }

        private List<CurveLoop> BuildCurveLoops(List<List<Curve>> allLoopsCurves, out List<string> errors)
        {
            var result = new List<CurveLoop>();
            errors = new List<string>();
            if (allLoopsCurves == null) return result;

            foreach (var sourceCurves in allLoopsCurves.OrderByDescending(GetCurvesArea))
            {
                var curves = CleanAndConnectCurves(sourceCurves);
                if (curves.Count < 3)
                {
                    errors.Add("Số lượng Curve hợp lệ sau khi clean ít hơn 3.");
                    continue;
                }
                try 
                { 
                    CurveLoop loop = CurveLoop.Create(curves); 
                    bool shouldBeCcw = (result.Count == 0); // First loop is outer
                    if (loop.IsCounterclockwise(XYZ.BasisZ) != shouldBeCcw)
                    {
                        loop.Flip();
                    }
                    result.Add(loop);
                }
                catch (Exception ex) { errors.Add($"CurveLoop.Create error: {ex.Message}"); }
            }
            return result;
        }

        private List<Curve> CleanAndConnectCurves(List<Curve> sourceCurves)
        {
            var result = new List<Curve>();
            if (sourceCurves == null || sourceCurves.Count == 0) return result;

            const double tol = 0.003;
            Curve previous = null;
            foreach (var curve in sourceCurves)
            {
                if (curve == null || curve.Length < tol) continue;
                Curve current = curve;

                if (previous != null)
                {
                    XYZ prevEnd = previous.GetEndPoint(1);
                    XYZ start = current.GetEndPoint(0);
                    XYZ end = current.GetEndPoint(1);

                    if (prevEnd.DistanceTo(end) < prevEnd.DistanceTo(start))
                    {
                        current = current.CreateReversed();
                        start = current.GetEndPoint(0);
                    }

                    double gap = prevEnd.DistanceTo(start);
                    if (gap > 1e-4)
                    {
                        if (gap >= tol && gap < 0.05)
                        {
                            result.Add(Line.CreateBound(prevEnd, start));
                        }
                        else if (gap < tol)
                        {
                            // Lấp lỗ hổng quá nhỏ bằng cách kéo dài/dời đỉnh nếu là Line
                            Curve adjustedCurrent = RebuildCurveWithStart(current, prevEnd, tol);
                            if (adjustedCurrent != null)
                            {
                                current = adjustedCurrent;
                            }
                            else if (previous is Line)
                            {
                                // Kéo dài previous line để chạm start
                                result.RemoveAt(result.Count - 1);
                                if (result.Count > 0)
                                {
                                    XYZ prevPrevEnd = result.Last().GetEndPoint(1);
                                    if (prevPrevEnd.DistanceTo(start) > tol)
                                        result.Add(Line.CreateBound(prevPrevEnd, start));
                                }
                                else
                                {
                                    XYZ prevStart = previous.GetEndPoint(0);
                                    if (prevStart.DistanceTo(start) > tol)
                                        result.Add(Line.CreateBound(prevStart, start));
                                }
                            }
                            else if (current is Arc arc)
                            {
                                // Kéo giãn Arc
                                if (prevEnd.DistanceTo(arc.GetEndPoint(1)) > tol)
                                    current = Arc.Create(prevEnd, arc.GetEndPoint(1), arc.Evaluate(0.5, true));
                            }
                        }
                    }
                }

                result.Add(current);
                previous = current;
            }

            if (result.Count > 2)
            {
                XYZ last = result.Last().GetEndPoint(1);
                XYZ first = result.First().GetEndPoint(0);
                double gap = last.DistanceTo(first);
                if (gap > 1e-4)
                {
                    if (gap >= tol && gap < 0.05)
                        result.Add(Line.CreateBound(last, first));
                    else if (gap < tol)
                    {
                        Curve adjustedLast = RebuildCurveWithEnd(result.Last(), first, tol);
                        if (adjustedLast != null)
                            result[result.Count - 1] = adjustedLast;
                    }
                }
            }

            return result;
        }

        private Curve RebuildCurveWithStart(Curve curve, XYZ start, double tol)
        {
            if (curve == null || start == null) return null;

            XYZ end = curve.GetEndPoint(1);
            if (start.DistanceTo(end) <= tol) return null;

            if (curve is Line)
                return Line.CreateBound(start, end);

            if (curve is Arc arc)
                return Arc.Create(start, end, arc.Evaluate(0.5, true));

            return null;
        }

        private Curve RebuildCurveWithEnd(Curve curve, XYZ end, double tol)
        {
            if (curve == null || end == null) return null;

            XYZ start = curve.GetEndPoint(0);
            if (start.DistanceTo(end) <= tol) return null;

            if (curve is Line)
                return Line.CreateBound(start, end);

            if (curve is Arc arc)
                return Arc.Create(start, end, arc.Evaluate(0.5, true));

            return null;
        }

        private double GetPointsArea(List<XYZ> pts)
        {
            try
            {
                if (pts == null || pts.Count < 3) return 0;

                double area = 0;
                for (int i = 0; i < pts.Count; i++)
                {
                    XYZ a = pts[i];
                    XYZ b = pts[(i + 1) % pts.Count];
                    area += a.X * b.Y - b.X * a.Y;
                }
                return Math.Abs(area) * 0.5;
            }
            catch { return 0; }
        }

        private double GetCurvesArea(List<Curve> curves)
        {
            try
            {
                var pts = new List<XYZ>();
                foreach (var curve in curves ?? new List<Curve>())
                {
                    if (curve == null) continue;
                    foreach (var pt in curve.Tessellate())
                    {
                        if (pts.Count == 0 || pt.DistanceTo(pts.Last()) > 0.001)
                            pts.Add(pt);
                    }
                }

                if (pts.Count > 2 && pts.First().DistanceTo(pts.Last()) < 0.001)
                    pts.RemoveAt(pts.Count - 1);

                return GetPointsArea(pts);
            }
            catch { return 0; }
        }

        public string GetName() => "FloorCreationHandler";
    }
}
