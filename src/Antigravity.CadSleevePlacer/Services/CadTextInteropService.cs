using Antigravity.CadSleevePlacer.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Antigravity.CadSleevePlacer.Services
{
    /// <summary>
    /// Communicates with a live AutoCAD instance via COM automation.
    ///
    /// Block-centric scan logic (v3):
    ///   1. Collect all Block Attributes and Text/MText entities as "text sources"
    ///      using their WCS InsertionPoint / TextPosition.
    ///   2. Collect all closed Polylines and Circles on the sleeve layer as geometry.
    ///      Polyline center is computed from OCS coords (≈ WCS for flat 2D plans).
    ///   3. For each text source that contains valid DN/BxH/COP data:
    ///      → find the nearest geometry within SNAP_RADIUS_MM
    ///      → place sleeve at that geometry's center with its rotation.
    ///
    /// AcDbLeader / AcDbLine / AcDbMLeader are intentionally IGNORED —
    /// their Coordinate() API returns OCS values that cannot be directly compared
    /// with WCS positions from InsertionPoint/TextPosition.
    /// </summary>
    public class CadTextInteropService
    {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        // ─────────────────────────────────────────────────────────────────────
        // Tuning constants
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Max distance (mm, WCS) from a Block/Text annotation to its sleeve geometry.
        /// 50 m is intentionally wide so the scan works even when annotations are
        /// placed far from the physical sleeve outline on the plan.
        /// </summary>
        private const double SNAP_RADIUS_MM = 50000.0;

        // ─────────────────────────────────────────────────────────────────────
        // Public helpers (unchanged)
        // ─────────────────────────────────────────────────────────────────────

        public static double[] PickCadPoint()
        {
            try
            {
                dynamic acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                acadApp.Visible = true;
                try { SetForegroundWindow(new IntPtr((int)acadApp.HWND)); } catch { }

                dynamic utility = acadApp.ActiveDocument.Utility;
                dynamic cadPt = utility.GetPoint(Type.Missing, "\nPick Reference Point in AutoCAD: ");
                return new double[] { (double)cadPt[0], (double)cadPt[1], (double)cadPt[2] };
            }
            catch { throw new Exception("Đã huỷ chọn điểm trên CAD."); }
        }

        public static string PickCadLayer()
        {
            try
            {
                dynamic acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                acadApp.Visible = true;
                try { SetForegroundWindow(new IntPtr((int)acadApp.HWND)); } catch { }

                dynamic utility = acadApp.ActiveDocument.Utility;
                object entityObj = null, pickPt = null;
                utility.GetEntity(out entityObj, out pickPt, "\nChọn một đối tượng trên AutoCAD để lấy Layer mẫu: ");
                if (entityObj != null) return (string)((dynamic)entityObj).Layer;
            }
            catch { }
            return null;
        }

        public static (List<CadSleeveInfo> Sleeves, string FilePath)? SelectEntities(
            string layerName, string regexDN, string regexRect, string regexCOP)
        {
            try
            {
                dynamic acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                dynamic acadDoc = acadApp.ActiveDocument;
                string  docPath = acadDoc.FullName;
                dynamic utility = acadDoc.Utility;

                try { acadDoc.SelectionSets.Item("CSP_SEL").Delete(); } catch { }
                dynamic selSet = acadDoc.SelectionSets.Add("CSP_SEL");

                acadApp.Visible = true;
                try { SetForegroundWindow(new IntPtr((int)acadApp.HWND)); } catch { }

                utility.Prompt(string.IsNullOrEmpty(layerName) || layerName == "<Tất cả Layer>"
                    ? "\n[CVP] Quét chọn phạm vi làm việc trên CAD, ấn Enter để hoàn tất: "
                    : $"\n[CVP] Quét chọn phạm vi làm việc (layer hình học: '{layerName}'), ấn Enter để hoàn tất: ");

                selSet.SelectOnScreen();

                var sleeves = ProcessEntities(selSet, utility, layerName, regexDN, regexRect, regexCOP);

                try { selSet.Delete(); } catch { }

                utility.Prompt(sleeves.Count == 0
                    ? "\n[CVP] Không tìm thấy sleeve hợp lệ trong vùng chọn."
                    : $"\n[CVP] Đã nhận diện được {sleeves.Count} sleeve.");

                return (sleeves, docPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Không thể kết nối đến AutoCAD.\n" + ex.Message);
            }
        }

        public static List<string> GetLayers()
        {
            var layers = new List<string>();
            try
            {
                dynamic acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                dynamic acadDoc = acadApp.ActiveDocument;
                foreach (dynamic layer in acadDoc.Layers)
                    layers.Add((string)layer.Name);
                layers.Sort();
            }
            catch { }
            return layers;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Core: Block-centric scan
        // ─────────────────────────────────────────────────────────────────────

        private static List<CadSleeveInfo> ProcessEntities(
            dynamic selSet, dynamic utility,
            string layerName,
            string regexDN, string regexRect, string regexCOP)
        {
            var sleeves     = new List<CadSleeveInfo>();
            var rawTexts    = new List<CadTextSourceInfo>(); // WCS positions
            var rawGeoms    = new List<CadPolylineInfo>();   // WCS positions
            var rawCircles  = new List<CadCircleInfo>();     // WCS positions
            var rawLeaders  = new List<CadLeaderInfo>();     // OCS translated to WCS

            utility.Prompt($"\n[CVP] Đang xử lý {selSet.Count} đối tượng...");

            Regex rgxDN   = TryCreateRegex(regexDN);
            Regex rgxRect = TryCreateRegex(regexRect);
            Regex rgxCOP  = TryCreateRegex(regexCOP);

            // ── SCAN PASS ────────────────────────────────────────────────────
            foreach (dynamic ent in selSet)
            {
                string etype = "";
                try { etype = (string)ent.EntityName; } catch { continue; }

                switch (etype)
                {
                    // ── Block with Attributes → text source (WCS via InsertionPoint) ──
                    case "AcDbBlockReference":
                        try
                        {
                            if ((bool)ent.HasAttributes)
                            {
                                string blockText = "";
                                double[] firstAttrPos = null;
                                foreach (dynamic attr in ent.GetAttributes())
                                {
                                    blockText += attr.TextString + " \n";
                                    if (firstAttrPos == null)
                                    {
                                        try
                                        {
                                            double[] aPos = (double[])attr.InsertionPoint;
                                            firstAttrPos = new double[] { aPos[0], aPos[1], aPos[2] };
                                        }
                                        catch { }
                                    }
                                }

                                double[] pos = firstAttrPos ?? (double[])ent.InsertionPoint; // WCS
                                rawTexts.Add(new CadTextSourceInfo
                                {
                                    Position    = new double[] { pos[0], pos[1], pos[2] },
                                    TextContent = blockText,
                                    Entity      = ent,
                                    IsResolved  = false
                                });
                            }
                        }
                        catch { }
                        break;

                    // ── MText / Text → text source (WCS) ─────────────────────
                    case "AcDbMText":
                        try
                        {
                            string txt = CleanMText((string)ent.TextString);
                            double[] pos = (double[])ent.InsertionPoint; // WCS
                            rawTexts.Add(new CadTextSourceInfo
                            {
                                Position    = new double[] { pos[0], pos[1], 0 },
                                TextContent = txt,
                                Entity      = ent,
                                IsResolved  = false
                            });
                        }
                        catch { }
                        break;

                    case "AcDbText":
                        try
                        {
                            string txt = CleanMText((string)ent.TextString);
                            double[] pos = (double[])ent.TextPosition; // WCS
                            rawTexts.Add(new CadTextSourceInfo
                            {
                                Position    = new double[] { pos[0], pos[1], 0 },
                                TextContent = txt,
                                Entity      = ent,
                                IsResolved  = false
                            });
                        }
                        catch { }
                        break;

                    // ── Polyline → geometry for snap (OCS, translated to WCS) ─
                    case "AcDbPolyline":
                    case "AcDb2dPolyline":
                        try
                        {
                            bool onSleeveLayer = IsOnSleeveLayer((string)ent.Layer, layerName);
                            if (!onSleeveLayer) break;

                            object coordsObj = ent.Coordinates;
                            double[] coords = null;
                            if (coordsObj is double[])
                            {
                                coords = (double[])coordsObj;
                            }
                            else
                            {
                                var arr = (Array)coordsObj;
                                coords = new double[arr.Length];
                                for (int i = 0; i < arr.Length; i++)
                                    coords[i] = Convert.ToDouble(arr.GetValue(i));
                            }

                            int step = (etype == "AcDb2dPolyline") ? 3 : 2;
                            int minLen = (etype == "AcDb2dPolyline") ? 12 : 8;
                            if (coords.Length < minLen) break;

                            // Build unique vertex list
                            var verts = new List<double[]>();
                            for (int i = 0; i < coords.Length; i += step)
                            {
                                if (i + 1 < coords.Length)
                                    verts.Add(new double[] { coords[i], coords[i + 1] });
                            }

                            var uVerts = new List<double[]>();
                            foreach (var v in verts)
                            {
                                if (!uVerts.Exists(u =>
                                    Math.Abs(u[0] - v[0]) < 1.0 && Math.Abs(u[1] - v[1]) < 1.0))
                                    uVerts.Add(v);
                            }

                            if (uVerts.Count < 4) break;

                            // Center of polyline in OCS
                            double cx = 0, cy = 0;
                            foreach (var v in uVerts) { cx += v[0]; cy += v[1]; }
                            cx /= uVerts.Count;
                            cy /= uVerts.Count;

                            // Elevation offset for WCS Z
                            double elev = 0;
                            try { elev = (double)ent.Elevation; } catch { }

                            // Translate OCS center to WCS center
                            double[] wcsCenter = new double[] { cx, cy, elev };
                            try
                            {
                                double[] ocsPt = new double[] { cx, cy, elev };
                                dynamic normal = ent.Normal;
                                // Translate from OCS (3) to World (0)
                                dynamic translated = utility.TranslateCoordinates(ocsPt, 3, 0, 0, normal);
                                wcsCenter = new double[] { (double)translated[0], (double)translated[1], (double)translated[2] };
                            }
                            catch { }

                            // Rotation from longest edge
                            double d01 = Dist2D(uVerts[0], uVerts[1]);
                            double d12 = Dist2D(uVerts[1], uVerts[2]);
                            double w   = Math.Min(d01, d12);
                            double h   = Math.Max(d01, d12);

                            double[] dirLong = d01 > d12
                                ? new double[] { uVerts[1][0] - uVerts[0][0], uVerts[1][1] - uVerts[0][1] }
                                : new double[] { uVerts[2][0] - uVerts[1][0], uVerts[2][1] - uVerts[1][1] };

                            double[] dirShort = d01 <= d12
                                ? new double[] { uVerts[1][0] - uVerts[0][0], uVerts[1][1] - uVerts[0][1] }
                                : new double[] { uVerts[2][0] - uVerts[1][0], uVerts[2][1] - uVerts[1][1] };

                            rawGeoms.Add(new CadPolylineInfo
                            {
                                Center      = wcsCenter,
                                Width       = w,
                                Height      = h,
                                RotationRad = Math.Atan2(dirLong[1], dirLong[0]),
                                ShortEdgeAngleRad = Math.Atan2(dirShort[1], dirShort[0]),
                                Entity      = ent,
                                IsResolved  = false
                            });
                        }
                        catch (Exception ex)
                        {
                            utility.Prompt($"\n[CVP] Lỗi phân tích Polyline: {ex.Message}");
                        }
                        break;

                    // ── Circle → geometry for snap (Center is WCS) ────────────
                    case "AcDbCircle":
                        try
                        {
                            bool onSleeveLayer = IsOnSleeveLayer((string)ent.Layer, layerName);
                            if (!onSleeveLayer) break;

                            double[] center = (double[])ent.Center; // WCS
                            double   radius = (double)ent.Radius;
                            rawCircles.Add(new CadCircleInfo
                            {
                                Center     = new double[] { center[0], center[1], center[2] },
                                Radius     = radius,
                                Entity     = ent,
                                IsResolved = false
                            });
                        }
                        catch (Exception ex)
                        {
                            utility.Prompt($"\n[CVP] Lỗi phân tích Circle: {ex.Message}");
                        }
                        break;

                    // ── Leader → connects annotation to target (OCS 3D) ───────
                    case "AcDbLeader":
                        try
                        {
                            bool onSleeveLayer = IsOnSleeveLayer((string)ent.Layer, layerName);
                            if (!onSleeveLayer) break;

                            object coordsObj = ent.Coordinates; // OCS 3D flat array
                            double[] coords = null;
                            if (coordsObj is double[])
                            {
                                coords = (double[])coordsObj;
                            }
                            else
                            {
                                var arr = (Array)coordsObj;
                                coords = new double[arr.Length];
                                for (int i = 0; i < arr.Length; i++)
                                    coords[i] = Convert.ToDouble(arr.GetValue(i));
                            }

                            if (coords.Length < 6) break; // Need at least 2 vertices (6 doubles)

                            int len = coords.Length;
                            double[] p1_ocs = new double[] { coords[0], coords[1], coords[2] }; // Arrowhead
                            double[] p2_ocs = new double[] { coords[len - 3], coords[len - 2], coords[len - 1] }; // Anchor

                            // AutoCAD COM Leader Coordinates are natively in WCS.
                            // We do not need translation, and calling TranslateCoordinates or ent.Normal
                            // often throws DISP_E_EXCEPTION (0x80020009) on modern AutoCAD COM environments.
                            rawLeaders.Add(new CadLeaderInfo
                            {
                                P1_WCS = p1_ocs,
                                P2_WCS = p2_ocs,
                                Entity = ent
                            });
                        }
                        catch (Exception ex)
                        {
                            utility.Prompt($"\n[CVP] Lỗi phân tích Leader: {ex.Message}");
                        }
                        break;
                }
            }

            utility.Prompt($"\n[CVP] Thu thập: {rawTexts.Count} text/block, {rawGeoms.Count} polyline, {rawCircles.Count} circle, {rawLeaders.Count} leader.");

            // ── STEP 1: RESOLVE VIA LEADERS (1-1 MATCH) ──────────────────────
            int resolvedLeaderCount = 0;
            int leaderIdx = 0;
            foreach (var leader in rawLeaders)
            {
                leaderIdx++;
                string tag = $"[Leader {leaderIdx}]";
                try
                {
                    utility.Prompt($"\n[CVP] {tag} Xét Leader: Đầu=({leader.P1_WCS[0]:F0},{leader.P1_WCS[1]:F0}), Đuôi=({leader.P2_WCS[0]:F0},{leader.P2_WCS[1]:F0})");

                    // 1. Find the closest Text source near leader tail (P2_WCS) within 30.0 meters
                    // We allow already-resolved texts to support shared annotations (multiple leaders pointing to the same text)
                    CadTextSourceInfo bestText = null;
                    double bestTextDist = double.MaxValue;
                    foreach (var txt in rawTexts)
                    {
                        double d = Dist2D(leader.P2_WCS, txt.Position);
                        if (d < 30000.0 && d < bestTextDist)
                        {
                            bestTextDist = d;
                            bestText = txt;
                        }
                    }

                    if (bestText == null)
                    {
                        utility.Prompt($"\n[CVP] {tag} SKIP: Không có Text/Block ghi chú nào gần đuôi Leader (trong bán kính 30m).");
                        continue;
                    }

                    // 2. Parse sleeve values from the found text
                    string txtStr = bestText.TextContent;
                    double dn  = ExtractValue(txtStr, rgxDN);
                    (double w, double h) = ExtractRectValues(txtStr, rgxRect);
                    (double cop, string elevType) = ExtractElevationWithType(txtStr, rgxCOP);

                    utility.Prompt($"\n[CVP] {tag} Thấy Text: [{txtStr.Replace("\n","|").Trim()}] cách đuôi {bestTextDist:F0}mm.");

                    bool hasSize = dn > 0 || (w > 0 && h > 0);
                    bool hasCop  = cop != double.MinValue;
                    if (!hasSize || !hasCop)
                    {
                        utility.Prompt($"\n[CVP] {tag} SKIP: Text thiếu kích thước (DN={dn}, W={w}, H={h}) hoặc thiếu cao độ {elevType}={cop}.");
                        continue;
                    }

                    // 3. Find closest geometry near leader arrowhead (P1_WCS) within 3.0 meters
                    double textW = (dn > 0) ? dn : w;
                    double textH = (dn > 0) ? 0 : h;
                    var match = FindBestGeometryMatch(leader.P1_WCS, textW, textH, rawCircles, rawGeoms);

                    double bestGeomDist = double.MaxValue;
                    double[] targetPt = null;
                    CadCircleInfo bestCircle = match.circle;
                    CadPolylineInfo bestPoly = match.poly;
                    double rotRad = match.rotRad;
                    dynamic matchedEnt = match.matchedEnt;

                    if (bestCircle != null)
                    {
                        targetPt = bestCircle.Center;
                        bestGeomDist = Dist2D(leader.P1_WCS, targetPt);
                    }
                    else if (bestPoly != null)
                    {
                        targetPt = bestPoly.Center;
                        bestGeomDist = Dist2D(leader.P1_WCS, targetPt);
                    }

                    // 4. Resolve and build sleeve info
                    bestText.IsResolved = true;
                    if (bestPoly != null) bestPoly.IsResolved = true;
                    if (bestCircle != null) bestCircle.IsResolved = true;

                    // Color success entities RED (1)
                    try { bestText.Entity.Color = 1; bestText.Entity.Update(); } catch { }
                    try { leader.Entity.Color = 1; leader.Entity.Update(); } catch { }

                    if (targetPt != null)
                    {
                        // Geometry found: use geom dimensions & rotation, but size can be adjusted by Revit
                        try { matchedEnt.Color = 1; matchedEnt.Update(); } catch { }
                        utility.Prompt($"\n[CVP] {tag} OK: Khớp thành công hình học cách đầu Leader {bestGeomDist:F0}mm. W={w} H={h} DN={dn} {elevType}={cop}");
                        
                        if (w > 0 && h > 0)
                            sleeves.Add(new CadSleeveInfo(targetPt, w, h, cop, elevType, "Block Sleeve Rect",  "", rotRad));
                        else
                            sleeves.Add(new CadSleeveInfo(targetPt, dn,    cop, elevType, "Block Sleeve Round", "", rotRad));
                    }
                    else
                    {
                        // No geometry block found: fallback to placing directly at arrowhead tip (P1_WCS)
                        utility.Prompt($"\n[CVP] {tag} OK: Không có ô vẽ sleeve, đặt trực tiếp tại đầu mũi tên. W={w} H={h} DN={dn} {elevType}={cop}");
                        if (w > 0 && h > 0)
                            sleeves.Add(new CadSleeveInfo(leader.P1_WCS, w, h, cop, elevType, "Block Sleeve Rect",  "", 0.0));
                        else
                            sleeves.Add(new CadSleeveInfo(leader.P1_WCS, dn,    cop, elevType, "Block Sleeve Round", "", 0.0));
                    }

                    resolvedLeaderCount++;
                }
                catch (Exception ex)
                {
                    utility.Prompt($"\n[CVP] {tag} LỖI xử lý: {ex.Message}");
                }
            }

            utility.Prompt($"\n[CVP] Khớp thành công {resolvedLeaderCount}/{rawLeaders.Count} sleeve từ Leader.");

            // ── STEP 2: LEADERLESS FALLBACK (PROXIMITY MATCH) ────────────────
            int fallbackCount = 0;
            int textIdx = 0;
            foreach (var txt in rawTexts)
            {
                if (txt.IsResolved) continue;
                textIdx++;
                string tag = $"[TextFallback {textIdx}]";

                try
                {
                    string txtStr = txt.TextContent;
                    double dn  = ExtractValue(txtStr, rgxDN);
                    (double w, double h) = ExtractRectValues(txtStr, rgxRect);
                    (double cop, string elevType) = ExtractElevationWithType(txtStr, rgxCOP);

                    bool hasSize = dn > 0 || (w > 0 && h > 0);
                    bool hasCop  = cop != double.MinValue;
                    if (!hasSize || !hasCop) continue;

                    // Find closest geometry near text position within 3.0 meters
                    double textW = (dn > 0) ? dn : w;
                    double textH = (dn > 0) ? 0 : h;
                    var match = FindBestGeometryMatch(txt.Position, textW, textH, rawCircles, rawGeoms);

                    double bestGeomDist = double.MaxValue;
                    double[] targetPt = null;
                    CadCircleInfo bestCircle = match.circle;
                    CadPolylineInfo bestPoly = match.poly;
                    double rotRad = match.rotRad;
                    dynamic matchedEnt = match.matchedEnt;

                    if (bestCircle != null)
                    {
                        targetPt = bestCircle.Center;
                        bestGeomDist = Dist2D(txt.Position, targetPt);
                    }
                    else if (bestPoly != null)
                    {
                        targetPt = bestPoly.Center;
                        bestGeomDist = Dist2D(txt.Position, targetPt);
                    }

                    if (targetPt != null)
                    {
                        txt.IsResolved = true;
                        if (bestPoly != null) bestPoly.IsResolved = true;
                        if (bestCircle != null) bestCircle.IsResolved = true;

                        try { txt.Entity.Color = 1; txt.Entity.Update(); } catch { }
                        try { matchedEnt.Color = 1; matchedEnt.Update(); } catch { }

                        if (w > 0 && h > 0)
                            sleeves.Add(new CadSleeveInfo(targetPt, w, h, cop, elevType, "Block Sleeve Rect",  "", rotRad));
                        else
                            sleeves.Add(new CadSleeveInfo(targetPt, dn,    cop, elevType, "Block Sleeve Round", "", rotRad));

                        fallbackCount++;
                    }
                }
                catch { }
            }

            utility.Prompt($"\n[CVP] Khớp bổ sung {fallbackCount} sleeve không cần Leader.");
            utility.Prompt($"\n[CVP] Hoàn tất: Tổng cộng đã nhận diện {sleeves.Count} sleeve.");
            return sleeves;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static (CadCircleInfo circle, CadPolylineInfo poly, double rotRad, dynamic matchedEnt) FindBestGeometryMatch(
            double[] centerPt,
            double textW,
            double textH,
            List<CadCircleInfo> rawCircles,
            List<CadPolylineInfo> rawGeoms)
        {
            double bestMatchDist = double.MaxValue;
            double bestFallbackDist = double.MaxValue;
            
            CadCircleInfo bestMatchCircle = null;
            CadPolylineInfo bestMatchPoly = null;
            CadCircleInfo bestFallbackCircle = null;
            CadPolylineInfo bestFallbackPoly = null;

            foreach (var circ in rawCircles)
            {
                if (circ.IsResolved) continue;
                double d = Dist2D(centerPt, circ.Center);
                if (d > 3000.0) continue;
                
                bool isDimMatch = Math.Abs((circ.Radius * 2) - textW) <= 50.0;
                if (isDimMatch)
                {
                    if (d < bestMatchDist)
                    {
                        bestMatchDist = d;
                        bestMatchCircle = circ;
                        bestMatchPoly = null;
                    }
                }
                else
                {
                    if (d < bestFallbackDist)
                    {
                        bestFallbackDist = d;
                        bestFallbackCircle = circ;
                        bestFallbackPoly = null;
                    }
                }
            }

            foreach (var poly in rawGeoms)
            {
                if (poly.IsResolved) continue;
                double d = Dist2D(centerPt, poly.Center);
                if (d > 3000.0) continue;
                
                double diffW_W = Math.Abs(poly.Width - textW);
                double diffH_W = Math.Abs(poly.Height - textW);
                double diffW_H = textH > 0 ? Math.Abs(poly.Width - textH) : double.MaxValue;
                double diffH_H = textH > 0 ? Math.Abs(poly.Height - textH) : double.MaxValue;
                
                double minDiffW = Math.Min(diffW_W, diffW_H);
                double minDiffH = Math.Min(diffH_W, diffH_H);
                
                bool isDimMatch = (Math.Min(minDiffW, minDiffH) <= 50.0);
                
                if (isDimMatch)
                {
                    if (d < bestMatchDist)
                    {
                        bestMatchDist = d;
                        bestMatchPoly = poly;
                        bestMatchCircle = null;
                    }
                }
                else
                {
                    if (d < bestFallbackDist)
                    {
                        bestFallbackDist = d;
                        bestFallbackPoly = poly;
                        bestFallbackCircle = null;
                    }
                }
            }

            CadCircleInfo selectedCircle = null;
            CadPolylineInfo selectedPoly = null;
            
            if (bestMatchCircle != null || bestMatchPoly != null)
            {
                selectedCircle = bestMatchCircle;
                selectedPoly = bestMatchPoly;
            }
            else
            {
                selectedCircle = bestFallbackCircle;
                selectedPoly = bestFallbackPoly;
            }

            double rotRad = 0.0;
            dynamic matchedEnt = null;

            if (selectedCircle != null)
            {
                matchedEnt = selectedCircle.Entity;
            }
            else if (selectedPoly != null)
            {
                matchedEnt = selectedPoly.Entity;
                double diffW_W = Math.Abs(selectedPoly.Width - textW);
                double diffH_W = Math.Abs(selectedPoly.Height - textW);
                double diffW_H = textH > 0 ? Math.Abs(selectedPoly.Width - textH) : double.MaxValue;
                double diffH_H = textH > 0 ? Math.Abs(selectedPoly.Height - textH) : double.MaxValue;
                
                double minDiffW = Math.Min(diffW_W, diffW_H);
                double minDiffH = Math.Min(diffH_W, diffH_H);
                
                if (minDiffW < minDiffH)
                    rotRad = selectedPoly.ShortEdgeAngleRad;
                else
                    rotRad = selectedPoly.RotationRad;
            }

            return (selectedCircle, selectedPoly, rotRad, matchedEnt);
        }

        private static bool IsOnSleeveLayer(string entLayer, string targetLayer)
        {
            if (string.IsNullOrEmpty(targetLayer) || targetLayer.Trim() == "<Tất cả Layer>")
                return true;
            return string.Equals(entLayer?.Trim(), targetLayer.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static Regex TryCreateRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return null;
            try { return new Regex(pattern, RegexOptions.IgnoreCase); }
            catch { return null; }
        }

        private static double Dist2D(double[] p1, double[] p2)
            => Math.Sqrt(Math.Pow(p1[0] - p2[0], 2) + Math.Pow(p1[1] - p2[1], 2));

        private static double ExtractValue(string text, Regex rgx)
        {
            if (rgx == null || string.IsNullOrWhiteSpace(text)) return double.MinValue;
            var m = rgx.Match(text);
            if (m.Success && m.Groups.Count > 1
                && double.TryParse(m.Groups[1].Value, out double val))
                return val;
            return double.MinValue;
        }

        /// <summary>
        /// Parses elevation value AND type prefix from CAD annotation text.
        /// Detects prefixes: BOO (Bottom of Opening), BOD (Bottom of Duct),
        /// BOP (Bottom of Pipe), COP (Center of Pipe), TOP (Top of Opening).
        /// Falls back to the user-supplied regex for the numeric part.
        /// </summary>
        /// <returns>(elevationMm, elevationType) where elevationType is e.g. "BOO", "COP", ""</returns>
        private static (double elevMm, string elevType) ExtractElevationWithType(string text, Regex rgxCOP)
        {
            if (string.IsNullOrWhiteSpace(text)) return (double.MinValue, "");

            // Try to find a known prefix before the FL value, e.g. "BOO=FL-700" or "BOP=FL+3050"
            // Pattern: optional prefix = (BOO|BOD|BOP|COP|TOP), then "=FL" then sign+number
            var prefixMatch = Regex.Match(text,
                @"(?<type>BOO|BOD|BOP|COP|TOP)[=:\s]*FL(?<sign>[+-]?)(?<num>\d+)",
                RegexOptions.IgnoreCase);

            if (prefixMatch.Success)
            {
                string prefix = prefixMatch.Groups["type"].Value.ToUpperInvariant();
                string sign   = prefixMatch.Groups["sign"].Value;
                string numStr = prefixMatch.Groups["num"].Value;
                if (double.TryParse(numStr, out double num))
                {
                    double elev = (sign == "-") ? -num : num;
                    return (elev, prefix);
                }
            }

            // Fallback: use user-supplied regex (no prefix differentiation)
            double val = ExtractValue(text, rgxCOP);
            return (val, "");
        }

        private static (double, double) ExtractRectValues(string text, Regex rgx)
        {
            if (rgx == null || string.IsNullOrWhiteSpace(text)) return (0, 0);
            var m = rgx.Match(text);
            if (m.Success && m.Groups.Count > 2
                && double.TryParse(m.Groups[1].Value, out double w)
                && double.TryParse(m.Groups[2].Value, out double h))
                return (w, h);
            return (0, 0);
        }

        private static string CleanMText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = raw;
            s = Regex.Replace(s, @"\\[A-Za-z0-9]+[^;]*;", "");
            s = s.Replace("\\P", "\n").Replace("\\p", "\n");
            s = s.Replace("{", "").Replace("}", "");
            return s;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private data classes
        // ─────────────────────────────────────────────────────────────────────

        private class CadTextSourceInfo
        {
            public double[] Position    { get; set; } // WCS
            public string   TextContent { get; set; }
            public dynamic  Entity      { get; set; }
            public bool     IsResolved  { get; set; }
        }

        private class CadPolylineInfo
        {
            public double[] Center      { get; set; } // WCS
            public double   Width       { get; set; }
            public double   Height      { get; set; }
            public double   RotationRad { get; set; }
            public double   ShortEdgeAngleRad { get; set; }
            public dynamic  Entity      { get; set; }
            public bool     IsResolved  { get; set; }
        }

        private class CadCircleInfo
        {
            public double[] Center      { get; set; } // WCS
            public double   Radius      { get; set; }
            public dynamic  Entity      { get; set; }
            public bool     IsResolved  { get; set; }
        }

        private class CadLeaderInfo
        {
            public double[] P1_WCS      { get; set; } // WCS Arrowhead
            public double[] P2_WCS      { get; set; } // WCS Tail / Anchor
            public dynamic  Entity      { get; set; }
        }
    }
}
