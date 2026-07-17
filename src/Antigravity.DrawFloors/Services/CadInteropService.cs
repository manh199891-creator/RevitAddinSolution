using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Autodesk.Revit.DB;
using Antigravity.DrawFloors.Models;

namespace Antigravity.DrawFloors.Services
{
    /// <summary>
    /// Dịch vụ giao tiếp với AutoCAD qua COM Interop.
    /// Đơn vị bản vẽ CAD: mm. Đơn vị Revit: decimal feet (1 foot = 304.8 mm).
    /// </summary>
    public class CadInteropService
    {
        private dynamic _acadApp;
        private dynamic _acadDoc;
        private dynamic _acadUtil;

        public CadInteropService()
        {
            ConnectToAutoCAD();
        }

        // ─────────────────────────────────────────────
        // KẾT NỐI AutoCAD
        // ─────────────────────────────────────────────

        private void ConnectToAutoCAD()
        {
            try
            {
                _acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                _acadDoc = _acadApp.ActiveDocument;
                _acadUtil = _acadDoc.Utility;
            }
            catch (Exception)
            {
                throw new Exception(
                    "Could not connect to AutoCAD.\n" +
                    "Please ensure AutoCAD is open and it is a Full version (2018 or newer).");
            }
        }

        // ─────────────────────────────────────────────
        // ĐẶT GỐC TỌA ĐỘ TƯƠNG ĐỐI
        // ─────────────────────────────────────────────

        /// <summary>
        /// Yêu cầu người dùng click chọn một điểm trên CAD làm gốc tọa độ,
        /// rồi nhập điểm tương ứng trong Revit để tính offset.
        /// </summary>
        /// <param name="revitOriginFeet">Điểm gốc đã pick trong Revit (đơn vị feet)</param>
        public void SetOriginFromRevitPoint(XYZ revitOriginFeet)
        {
            try
            {
                _acadApp.Visible = true;
                _acadUtil.Prompt("\nClick the corresponding origin point on the AutoCAD drawing: ");
                dynamic cadPt = _acadUtil.GetPoint(Type.Missing, "\nSelect CAD origin: ");
                double cadX = (double)cadPt[0]; // mm
                double cadY = (double)cadPt[1]; // mm

                // Offset = Revit_origin - CAD_origin_in_feet
                XYZ offset = new XYZ(
                    revitOriginFeet.X - cadX / 304.8,
                    revitOriginFeet.Y - cadY / 304.8,
                    0);
                    
                Antigravity.Core.Services.CoordinateService.SetOriginOffset(offset);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting origin point from CAD: " + ex.Message);
            }
        }

        /// <summary>Đặt offset thủ công (nếu không dùng điểm gốc)</summary>
        public void SetOriginOffset(XYZ offsetFeet)
        {
            Antigravity.Core.Services.CoordinateService.SetOriginOffset(offsetFeet);
        }

        // ─────────────────────────────────────────────
        // CHUYỂN ĐỔI TỌA ĐỘ
        // ─────────────────────────────────────────────

        private XYZ CadToRevit(double xMm, double yMm, double elevFeet = 0)
        {
            return Antigravity.Core.Services.CoordinateService.CadToRevit(xMm, yMm, elevFeet);
        }

        // ─────────────────────────────────────────────
        // VẼ POLYLINE TRỰC TIẾP TRÊN CAD (Draw Polyline)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Yêu cầu người dùng vẽ Polyline trực tiếp trên CAD rồi
        /// trả về danh sách các điểm đã đồ thành CurveLoop trong Revit.
        /// </summary>
        public List<List<XYZ>> DrawPolylineInteractive()
        {
            try
            {
                _acadApp.Visible = true;
                _acadUtil.Prompt("\nDraw floor boundary using Polyline. Press ENTER to finish.");

                // Dùng SendCommand để kích hoạt lệnh PLINE trong AutoCAD
                _acadDoc.SendCommand("_.PLINE ");

                // Thu thập điểm theo vòng lặp
                var points = new List<XYZ>();
                while (true)
                {
                    try
                    {
                        dynamic pt = _acadUtil.GetPoint(Type.Missing, "\nSelect next point (ENTER to finish): ");
                        points.Add(CadToRevit((double)pt[0], (double)pt[1]));
                    }
                    catch
                    {
                        // Người dùng nhấn ENTER hoặc ESC
                        break;
                    }
                }

                var result = new List<List<XYZ>>();
                if (points.Count >= 3) result.Add(points);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("Error drawing Polyline in CAD: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        // VẼ HÌNH CHỮ NHẬT (Draw Rectangle)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Yêu cầu người dùng chọn 2 góc đối diện trên CAD để tạo sàn hình chữ nhật.
        /// </summary>
        public List<List<XYZ>> DrawRectangleInteractive()
        {
            try
            {
                _acadApp.Visible = true;
                _acadUtil.Prompt("\nSelect the first corner of the rectangle: ");
                dynamic pt1 = _acadUtil.GetPoint(Type.Missing, "\nCorner 1: ");
                _acadUtil.Prompt("\nSelect the opposite corner: ");
                dynamic pt2 = _acadUtil.GetCorner(pt1, "\nCorner 2: ");

                double x1 = (double)pt1[0],  y1 = (double)pt1[1];
                double x2 = (double)pt2[0],  y2 = (double)pt2[1];

                // 4 góc của hình chữ nhật
                var points = new List<XYZ>
                {
                    CadToRevit(x1, y1),
                    CadToRevit(x2, y1),
                    CadToRevit(x2, y2),
                    CadToRevit(x1, y2)
                };

                return new List<List<XYZ>> { points };
            }
            catch (Exception ex)
            {
                throw new Exception("Error drawing rectangle on CAD: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        // CHỌN POLYLINE CÓ SẴN TRÊN CAD
        // ─────────────────────────────────────────────

        /// <summary>
        /// Người dùng quét chọn các đường Polyline khép kín có sẵn trên CAD.
        /// Trả về danh sách CurveLoop — phần tử đầu là outer boundary,
        /// phần tử tiếp theo (nếu có) là holes.
        /// </summary>
        public List<List<Curve>> SelectPolylineBoundaries()
        {
            var result = new List<List<Curve>>();
            try
            {
                _acadApp.Visible = true;

                // Tạo selection set tạm
                string ssetName = "PolySel_" + DateTime.Now.Ticks;
                dynamic ssets = _acadDoc.SelectionSets;
                dynamic sset = ssets.Add(ssetName);

                _acadUtil.Prompt(
                    "\nSelect (Window Selection) the floor boundary Polylines. " +
                    "Select outer boundary first, then holes. Press ENTER to confirm: ");
                sset.SelectOnScreen();

                for (int i = 0; i < sset.Count; i++)
                {
                    dynamic ent = sset.Item(i);
                    string objName = ent.ObjectName.ToString();
                    if (objName == "AcDbPolyline" ||
                        objName == "AcDb2dPolyline" ||
                        objName == "AcDb3dPolyline")
                    {
                        var curves = ExtractPolylineCurves(ent);
                        if (curves != null && curves.Count >= 3)
                            result.Add(curves);
                    }
                }

                sset.Delete();

                // Sắp xếp: loop lớn nhất (diện tích lớn nhất) là outer, còn lại là holes
                result.Sort((a, b) =>
                    GetCurvesArea(b).CompareTo(GetCurvesArea(a))); // giảm dần

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("Error selecting Polylines: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        // CHỌN VÀ PARSE HATCH
        // ─────────────────────────────────────────────

        /// <summary>
        /// Người dùng quét chọn các đối tượng Hatch. Trả về dict [PatternName -> list hatch objects].
        /// </summary>
        public Dictionary<string, List<object>> SelectAndParseHatches()
        {
            var dict = new Dictionary<string, List<object>>();
            try
            {
                _acadApp.Visible = true;
                string ssetName = "HatchSel_" + DateTime.Now.Ticks;
                dynamic ssets = _acadDoc.SelectionSets;
                dynamic sset = ssets.Add(ssetName);

                _acadUtil.Prompt("\nSelect Hatch regions for automatic floor creation... ");
                sset.SelectOnScreen();

                for (int i = 0; i < sset.Count; i++)
                {
                    dynamic entity = sset.Item(i);
                    string objName = entity.ObjectName.ToString();
                    if (objName == "AcDbHatch")
                    {
                        string pattern = entity.PatternName.ToString();
                        if (string.IsNullOrWhiteSpace(pattern))
                            pattern = "(Solid)";
                        if (!dict.ContainsKey(pattern))
                            dict[pattern] = new List<object>();
                        dict[pattern].Add(entity);
                    }
                }
                sset.Delete();
            }
            catch (Exception ex)
            {
                throw new Exception("Error selecting Hatches: " + ex.Message);
            }
            return dict;
        }

        // ─────────────────────────────────────────────
        // TRÍCH XUẤT BOUNDARY TỪ HATCH
        // ─────────────────────────────────────────────

        /// <summary>
        /// Trích xuất các vòng biên (CurveLoop) từ một đối tượng Hatch của AutoCAD.
        /// Sử dụng thuộc tính GetBoundingBox và quét các AssociatedObjects.
        /// </summary>
        public List<List<Curve>> ExtractHatchBoundaries(object hatchObj)
        {
            var result = new List<List<Curve>>();
            try
            {
                dynamic hatch = hatchObj;

                dynamic activeSpaceGenerated = _acadDoc.ActiveLayout.Block;
                int cBeforeGenerated = activeSpaceGenerated.Count;
                try
                {
                    // Pass the handle directly to the command with \n to simulate Enter
                    _acadDoc.SendCommand("._HATCHGENERATEBOUNDARY (handent \"" + hatch.Handle + "\")\n\n");
                }
                catch { }

                int cAfterGenerated = WaitForBoundaryEntities(activeSpaceGenerated, cBeforeGenerated);
                if (cAfterGenerated > cBeforeGenerated)
                {
                    var generatedLoops = new List<List<Curve>>();
                    var looseCurves = new List<Curve>();
                    for (int k = cBeforeGenerated; k < cAfterGenerated; k++)
                    {
                        dynamic tempEnt = activeSpaceGenerated.Item(k);
                        string objName = "";
                        try { objName = tempEnt.ObjectName.ToString(); } catch { }
                        
                        var curves = ExtractCurvesFromAnyEntity(tempEnt);
                        looseCurves.AddRange(curves);
                            
                        // try { tempEnt.Delete(); } catch { } // DEBUG: Giữ lại trên CAD để user soi lỗi
                    }

                    foreach (var loop in BuildLoopsFromLooseCurves(looseCurves))
                    {
                        if (loop.Count >= 3)
                            generatedLoops.Add(loop);
                    }

                    if (generatedLoops.Count > 0)
                    {
                        generatedLoops.Sort((a, b) => GetCurvesArea(b).CompareTo(GetCurvesArea(a)));
                        return generatedLoops;
                    }
                }

                // ── TẦNG 1: THỬ LẤY LOOP TRỰC TIẾP (DÀNH CHO HATCH CHUẨN) ────────────
                int numLoops = (int)hatch.NumberOfLoops;
                if (numLoops > 0)
                {
                    for (int i = 0; i < numLoops; i++)
                    {
                        var curves = new List<Curve>();
                        object loopObj = null;
                        try
                        {
                            object[] args = new object[] { i, null };
                            hatch.GetType().InvokeMember("GetLoopAt",
                                System.Reflection.BindingFlags.InvokeMethod, null, hatch, args);
                            loopObj = args[1];
                        }
                        catch { continue; }

                        if (loopObj != null)
                        {
                            double[] dArray = TryGetDoubleArray(loopObj);
                            if (dArray != null)
                            {
                                var polyCurves = ExtractPolylineLoopFromDoubles(dArray);
                                if (polyCurves != null && polyCurves.Count > 0)
                                    curves.AddRange(polyCurves);
                            }
                            else
                            {
                                System.Collections.IEnumerable entities = loopObj as System.Collections.IEnumerable;
                                if (entities != null)
                                {
                                    foreach (dynamic ent in entities) ExtractEntityCurves(ent, curves);
                                }
                            }
                        }
                        var cleanCurves = CleanConsecutiveDuplicateCurves(curves);
                        if (cleanCurves.Count >= 3) result.Add(cleanCurves);
                    }
                    if (result.Count > 0) return result;
                }
            }
            catch { }
            return result;
        }

        private List<Curve> ExtractPolylineLoopFromDoubles(double[] coords)
        {
            try
            {
                // Format: [X1, Y1, Bulge1, X2, Y2, Bulge2...]
                if (coords == null || coords.Length < 3) return null;
                
                var pts = new List<XYZ>();
                var bulges = new List<double>();
                
                // Read chunks of 3: X, Y, Bulge.
                for (int j = 0; j <= coords.Length - 3; j += 3)
                {
                    pts.Add(CadToRevit(coords[j], coords[j + 1]));
                    bulges.Add(coords[j + 2]);
                }
                
                if (pts.Count < 2) return null;

                var curves = new List<Curve>();
                for (int i = 0; i < pts.Count; i++)
                {
                    XYZ start = pts[i];
                    XYZ end = pts[(i + 1) % pts.Count];
                    if (start.DistanceTo(end) < 0.003) continue;

                    double bulge = bulges[i];
                    AddCurve(curves, CreateArcFromBulge(start, end, bulge));
                }
                return CleanConsecutiveDuplicateCurves(curves);
            }
            catch { return null; }
        }

        private double[] TryGetDoubleArray(object value)
        {
            try
            {
                if (value is double[] doubles) return doubles;

                var enumerable = value as System.Collections.IEnumerable;
                if (enumerable == null || value is string) return null;

                var result = new List<double>();
                foreach (object item in enumerable)
                    result.Add(Convert.ToDouble(item));

                return result.Count > 0 ? result.ToArray() : null;
            }
            catch { return null; }
        }

        private List<XYZ> ExtractPointsFromAnyEntity(dynamic ent)
        {
            var pts = new List<XYZ>();
            try
            {
                string objName = ent.ObjectName.ToString();
                if (objName.Contains("Polyline"))
                {
                    var polyPts = ExtractPolylinePoints(ent);
                    if (polyPts != null) pts.AddRange(polyPts);
                }
                else if (objName == "AcDbLine")
                {
                    pts.Add(CadToRevit((double)ent.StartPoint[0], (double)ent.StartPoint[1]));
                    pts.Add(CadToRevit((double)ent.EndPoint[0], (double)ent.EndPoint[1]));
                }
            }
            catch { }
            return CleanConsecutiveDuplicatePoints(pts);
        }

        private int WaitForBoundaryEntities(dynamic activeSpace, int countBefore)
        {
            int currentCount = countBefore;
            int stableTicks = 0;
            bool hasNewEntities = false;
            for (int i = 0; i < 60; i++)
            {
                int previousCount = currentCount;
                try { currentCount = (int)activeSpace.Count; } catch { }
                if (currentCount > countBefore)
                {
                    hasNewEntities = true;
                    stableTicks = currentCount == previousCount ? stableTicks + 1 : 0;
                    if (stableTicks >= 5) break;
                }
                else if (!hasNewEntities)
                {
                    stableTicks = 0;
                }
                Thread.Sleep(100);
            }
            return currentCount;
        }

        private List<Curve> ExtractCurvesFromAnyEntity(dynamic ent)
        {
            var curves = new List<Curve>();
            try
            {
                string objName = ent.ObjectName.ToString();
                if (objName.Contains("Polyline"))
                {
                    var polyCurves = ExtractPolylineCurves(ent);
                    if (polyCurves != null) curves.AddRange(polyCurves);
                }
                else
                {
                    ExtractEntityCurves(ent, curves);
                }
            }
            catch { }
            return CleanConsecutiveDuplicateCurves(curves);
        }

        private void ExtractEntityCurves(dynamic ent, List<Curve> curves)
        {
            try
            {
                string objName = ent.ObjectName.ToString();
                if (objName == "AcDbLine")
                {
                    XYZ p1 = CadToRevit((double)ent.StartPoint[0], (double)ent.StartPoint[1]);
                    XYZ p2 = CadToRevit((double)ent.EndPoint[0], (double)ent.EndPoint[1]);
                    AddCurve(curves, Line.CreateBound(p1, p2));
                }
                else if (objName == "AcDbArc")
                {
                    AddCurve(curves, CreateArcFromCadArc(ent));
                }
                else if (objName.Contains("Polyline"))
                {
                    var polyCurves = ExtractPolylineCurves(ent);
                    if (polyCurves != null) curves.AddRange(polyCurves);
                }
                else if (objName == "AcDbSpline")
                {
                    var splineCurves = ExtractSplineCurves(ent);
                    if (splineCurves != null) curves.AddRange(splineCurves);
                }
                else if (objName == "AcDbRegion")
                {
                    ExtractRegionCurves(ent, curves);
                }
            }
            catch { }
        }

        private void ExtractRegionCurves(dynamic regionEnt, List<Curve> curves)
        {
            try
            {
                object explodedObjectsObj = regionEnt.Explode();
                object[] explodedObjects = explodedObjectsObj as object[];
                if (explodedObjects == null) return;
                
                foreach (dynamic expEnt in explodedObjects)
                {
                    ExtractEntityCurves(expEnt, curves);
                    try { expEnt.Delete(); } catch { }
                }
            }
            catch { }
        }

        private List<Curve> ExtractSplineCurves(dynamic splineEnt)
        {
            var result = new List<Curve>();
            try
            {
                int numPts = (int)splineEnt.NumberOfControlPoints;
                if (numPts < 2) return result;
                
                XYZ prevPt = null;
                for (int i = 0; i < numPts; i++)
                {
                    dynamic pt = splineEnt.GetControlPoint(i);
                    XYZ current = CadToRevit((double)pt[0], (double)pt[1]);
                    if (prevPt != null && prevPt.DistanceTo(current) > 0.003)
                    {
                        result.Add(Line.CreateBound(prevPt, current));
                    }
                    if (prevPt == null || prevPt.DistanceTo(current) > 0.003)
                    {
                        prevPt = current;
                    }
                }
            }
            catch { }
            return result;
        }

        private void ExtractEntityPoints(dynamic ent, List<XYZ> pts)
        {
            try
            {
                string objName = ent.ObjectName.ToString();
                if (objName == "AcDbLine")
                {
                    pts.Add(CadToRevit((double)ent.StartPoint[0], (double)ent.StartPoint[1]));
                    pts.Add(CadToRevit((double)ent.EndPoint[0], (double)ent.EndPoint[1]));
                }
                else if (objName == "AcDbArc")
                {
                    pts.Add(CadToRevit((double)ent.StartPoint[0], (double)ent.StartPoint[1]));
                    pts.Add(CadToRevit((double)ent.EndPoint[0], (double)ent.EndPoint[1]));
                }
                else if (objName.Contains("Polyline"))
                {
                    var polyPts = ExtractPolylinePoints(ent);
                    if (polyPts != null) pts.AddRange(polyPts);
                }
            }
            catch { }
        }

        private List<XYZ> CleanConsecutiveDuplicatePoints(List<XYZ> pts)
        {
            var unique = new List<XYZ>();
            if (pts == null || pts.Count == 0) return unique;

            unique.Add(pts[0]);
            for (int i = 1; i < pts.Count; i++)
            {
                if (pts[i].DistanceTo(unique.Last()) > 0.001)
                    unique.Add(pts[i]);
            }

            if (unique.Count > 2 && unique.First().DistanceTo(unique.Last()) < 0.001)
                unique.RemoveAt(unique.Count - 1);

            return unique;
        }

        // ─────────────────────────────────────────────
        // HELPER: TRÍCH XUẤT POLYLINE ENTITY
        // ─────────────────────────────────────────────

        private List<XYZ> ExtractPolylinePoints(dynamic polyEnt)
        {
            try
            {
                dynamic coordsObj = polyEnt.Coordinates;
                double[] coords = coordsObj as double[];
                if (coords == null)
                {
                    object[] arr = coordsObj as object[];
                    if (arr != null)
                    {
                        coords = new double[arr.Length];
                        for (int k = 0; k < arr.Length; k++)
                            coords[k] = Convert.ToDouble(arr[k]);
                    }
                }
                if (coords == null || coords.Length < 4) return null;

                string objName = polyEnt.ObjectName.ToString();
                // 2D Polyline / Polyline: 2 tọa độ (X,Y) mỗi điểm. 3D Polyline: 3 tọa độ (X,Y,Z).
                int step = (objName == "AcDb3dPolyline") ? 3 : 2;

                var pts = new List<XYZ>();
                for (int j = 0; j <= coords.Length - step; j += step)
                {
                    pts.Add(CadToRevit(coords[j], coords[j + 1]));
                }
                return pts;
            }
            catch { return null; }
        }

        private List<Curve> ExtractPolylineCurves(dynamic polyEnt)
        {
            try
            {
                var pts = ExtractPolylinePoints(polyEnt);
                if (pts == null || pts.Count < 2) return null;

                bool isClosed = false;
                try { isClosed = (bool)polyEnt.Closed; } catch { }

                int segmentCount = isClosed ? pts.Count : pts.Count - 1;
                var curves = new List<Curve>();
                for (int i = 0; i < segmentCount; i++)
                {
                    XYZ start = pts[i];
                    XYZ end = pts[(i + 1) % pts.Count];
                    if (start.DistanceTo(end) < 0.003) continue;

                    double bulge = 0.0;
                    try { bulge = Convert.ToDouble(polyEnt.GetBulge(i)); }
                    catch
                    {
                        try { bulge = Convert.ToDouble(polyEnt.GetBulgeAt(i)); }
                        catch { bulge = 0.0; }
                    }

                    AddCurve(curves, CreateArcFromBulge(start, end, bulge));
                }

                return CleanConsecutiveDuplicateCurves(curves);
            }
            catch { return null; }
        }

        private Curve CreateArcFromBulge(XYZ start, XYZ end, double bulge)
        {
            if (Math.Abs(bulge) < 1e-6)
                return Line.CreateBound(start, end);

            XYZ chord = end - start;
            double chordLength = chord.GetLength();
            if (chordLength < 0.003)
                return null;

            double theta = 4.0 * Math.Atan(Math.Abs(bulge));
            double radius = chordLength / (2.0 * Math.Sin(theta / 2.0));
            double sagitta = Math.Abs(bulge) * chordLength / 2.0;
            double centerOffset = radius - sagitta;

            XYZ mid = (start + end) * 0.5;
            XYZ chordDir = chord.Normalize();
            XYZ leftNormal = new XYZ(-chordDir.Y, chordDir.X, 0);
            XYZ center = mid + leftNormal.Multiply(Math.Sign(bulge) * centerOffset);

            XYZ v1 = (start - center).Normalize();
            XYZ v2 = (end - center).Normalize();
            double signedAngle = Math.Atan2(v1.CrossProduct(v2).Z, v1.DotProduct(v2));
            if (bulge > 0 && signedAngle < 0) signedAngle += 2.0 * Math.PI;
            if (bulge < 0 && signedAngle > 0) signedAngle -= 2.0 * Math.PI;

            XYZ midOnArc = center + RotateVector(v1, signedAngle / 2.0).Multiply(radius);
            try { return Arc.Create(start, end, midOnArc); }
            catch { return Line.CreateBound(start, end); }
        }

        private Curve CreateArcFromCadArc(dynamic arcEnt)
        {
            XYZ start = CadToRevit((double)arcEnt.StartPoint[0], (double)arcEnt.StartPoint[1]);
            XYZ end = CadToRevit((double)arcEnt.EndPoint[0], (double)arcEnt.EndPoint[1]);
            XYZ center = CadToRevit((double)arcEnt.Center[0], (double)arcEnt.Center[1]);

            XYZ v1 = (start - center).Normalize();
            XYZ v2 = (end - center).Normalize();
            double radius = start.DistanceTo(center);
            double signedAngle = Math.Atan2(v1.CrossProduct(v2).Z, v1.DotProduct(v2));
            if (signedAngle < 0) signedAngle += 2.0 * Math.PI;

            XYZ mid = center + RotateVector(v1, signedAngle / 2.0).Multiply(radius);
            try { return Arc.Create(start, end, mid); }
            catch { return Line.CreateBound(start, end); }
        }

        private void AddCurve(List<Curve> curves, Curve curve)
        {
            if (curve == null || curve.Length < 0.003) return;

            if (curves.Count > 0)
            {
                XYZ previousEnd = curves.Last().GetEndPoint(1);
                XYZ start = curve.GetEndPoint(0);
                XYZ end = curve.GetEndPoint(1);
                if (previousEnd.DistanceTo(end) < previousEnd.DistanceTo(start))
                    curve = curve.CreateReversed();
            }

            curves.Add(curve);
        }

        private XYZ RotateVector(XYZ vector, double angle)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            return new XYZ(
                vector.X * cos - vector.Y * sin,
                vector.X * sin + vector.Y * cos,
                vector.Z);
        }

        private List<Curve> CleanConsecutiveDuplicateCurves(List<Curve> curves)
        {
            var result = new List<Curve>();
            if (curves == null) return result;

            foreach (var curve in curves)
            {
                if (curve == null || curve.Length < 0.003) continue;
                
                if (result.Count == 0)
                {
                    result.Add(curve);
                }
                else
                {
                    XYZ mid1 = curve.Evaluate(0.5, true);
                    XYZ mid2 = result.Last().Evaluate(0.5, true);
                    if (mid1.DistanceTo(mid2) > 0.001)
                        result.Add(curve);
                }
            }
            return result;
        }

        private List<List<Curve>> BuildLoopsFromLooseCurves(List<Curve> looseCurves)
        {
            var loops = new List<List<Curve>>();
            var remaining = looseCurves?
                .Where(c => c != null && c.Length >= 0.003)
                .ToList() ?? new List<Curve>();

            const double tol = 0.01;
            while (remaining.Count > 0)
            {
                var loop = new List<Curve> { remaining[0] };
                remaining.RemoveAt(0);

                bool extended;
                do
                {
                    extended = false;
                    XYZ loopEnd = loop.Last().GetEndPoint(1);

                    for (int i = 0; i < remaining.Count; i++)
                    {
                        Curve candidate = remaining[i];
                        XYZ start = candidate.GetEndPoint(0);
                        XYZ end = candidate.GetEndPoint(1);

                        if (loopEnd.DistanceTo(start) <= tol)
                        {
                            loop.Add(candidate);
                            remaining.RemoveAt(i);
                            extended = true;
                            break;
                        }

                        if (loopEnd.DistanceTo(end) <= tol)
                        {
                            loop.Add(candidate.CreateReversed());
                            remaining.RemoveAt(i);
                            extended = true;
                            break;
                        }
                    }
                } while (extended);

                if (loop.Count >= 3 && loop.Last().GetEndPoint(1).DistanceTo(loop.First().GetEndPoint(0)) <= tol)
                    loops.Add(loop);
            }

            loops.Sort((a, b) => GetCurvesArea(b).CompareTo(GetCurvesArea(a)));
            return loops;
        }

        private void ExtractBulgeVerticesToPoints(dynamic vertices, List<XYZ> pts)
        {
            try
            {
                int nVerts = (int)vertices.Count;
                for (int k = 0; k < nVerts; k++)
                {
                    dynamic v = vertices.Item(k);
                    pts.Add(CadToRevit((double)v.X, (double)v.Y));
                }
            }
            catch { }
        }

        private void ExtractSegmentToPoints(dynamic seg, List<XYZ> pts)
        {
            try
            {
                dynamic startPt = seg.StartPoint;
                dynamic endPt = seg.EndPoint;
                XYZ p1 = CadToRevit((double)startPt[0], (double)startPt[1]);
                XYZ p2 = CadToRevit((double)endPt[0], (double)endPt[1]);

                if (pts.Count == 0) pts.Add(p1);
                if (p1.DistanceTo(pts.Last()) < 0.001) pts.Add(p2);
                else { pts.Add(p1); pts.Add(p2); }
            }
            catch { }
        }

        private double GetPointsArea(List<XYZ> pts)
        {
            try
            {
                double area = 0;
                int n = pts.Count;
                for (int i = 0; i < n; i++)
                {
                    XYZ a = pts[i];
                    XYZ b = pts[(i + 1) % n];
                    area += (a.X * b.Y - b.X * a.Y);
                }
                return Math.Abs(area) / 2.0;
            }
            catch { return 0; }
        }

        private double GetCurvesArea(List<Curve> curves)
        {
            try
            {
                var pts = new List<XYZ>();
                foreach (var curve in curves)
                {
                    var tessellated = curve.Tessellate();
                    foreach (var pt in tessellated)
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
    }
}
