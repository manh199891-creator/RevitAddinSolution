using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;
using System.Globalization;

namespace Antigravity.DrawBeams.Services
{
    public class CadInteropService
    {
        private dynamic _acadApp;
        private dynamic _acadDoc;

        public bool Connect()
        {
            try
            {
                _acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                _acadDoc = _acadApp.ActiveDocument;
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Không thể kết nối với AutoCAD. Đảm bảo AutoCAD đang mở.", ex);
            }
        }

        public double[] GetPointCAD(string prompt)
        {
            try
            {
                _acadApp.Visible = true;
                dynamic utility = _acadDoc.Utility;
                var pt = utility.GetPoint(Type.Missing, "\n" + prompt);
                return pt as double[];
            }
            catch
            {
                throw new Exception("Hủy chọn điểm.");
            }
        }

        public void SetOriginFromRevitPoint(Autodesk.Revit.DB.XYZ revitOriginFeet)
        {
            try
            {
                _acadApp.Visible = true;
                dynamic utility = _acadDoc.Utility;
                utility.Prompt("\nClick the corresponding origin point on the AutoCAD drawing: ");
                dynamic cadPt = utility.GetPoint(Type.Missing, "\nSelect CAD origin: ");
                
                double cadX = (double)cadPt[0]; // mm
                double cadY = (double)cadPt[1]; // mm

                Autodesk.Revit.DB.XYZ offset = new Autodesk.Revit.DB.XYZ(
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

        public Dictionary<string, string> GetEntityInfo()
        {
            try
            {
                _acadApp.Visible = true;
                dynamic utility = _acadDoc.Utility;
                object entityObj = null;
                object pickPt = null;
                
                utility.GetEntity(out entityObj, out pickPt, "\nChọn đối tượng mẫu (Line hoặc Text)... ");
                
                dynamic entity = entityObj;
                return new Dictionary<string, string>
                {
                    { "Layer", entity.Layer },
                    { "ObjectName", entity.ObjectName }
                };
            }
            catch { return null; }
        }

        public List<string> GetLayers()
        {
            List<string> layers = new List<string>();
            try
            {
                foreach (dynamic layer in _acadDoc.Layers)
                {
                    layers.Add(layer.Name);
                }
            }
            catch { }
            return layers.OrderBy(n => n).ToList();
        }

        // ================================================================
        // V12: CẤU TRÚC DỮ LIỆU LINH HOẠT
        // ================================================================

        /// <summary>Cặp cạnh dầm: Nét chuẩn (Layer A) + Nét song song (Layer bất kỳ)</summary>
        private class BeamCandidate
        {
            public CadLineSegment MainLine { get; set; }    // Nét thuộc Layer chuẩn (beam layer)
            public CadLineSegment SubLine { get; set; }     // Nét song song tìm được (layer bất kỳ)
            public double MeasuredWidth { get; set; }       // Khoảng cách hình học giữa 2 nét
            public double TextWidth { get; set; }           // Giá trị B lấy từ Text (ưu tiên)
            public double TextHeight { get; set; }          // Giá trị H lấy từ Text
            public string TextContent { get; set; }         // Nội dung text gốc
            public double OverlapLength { get; set; }       // Chiều dài chồng lấn
            public double Confidence { get; set; }          // Điểm tin cậy
        }

        private class CadLineSegment
        {
            public double[] StartPoint { get; set; }
            public double[] EndPoint { get; set; }
            public string Id { get; set; }
            public string Layer { get; set; }
            public int Color { get; set; } = -1;
            /// <summary>Bề dày polyline (mm). 0 = Line thường hoặc Polyline không có width.</summary>
            public double PolylineWidth { get; set; } = 0;
            /// <summary>ID nhóm cặp song song (cho Closed Polyline hình chữ nhật).</summary>
            public string GroupId { get; set; } = null;

            // Vector hướng chuẩn hóa
            public double DirectionX => EndPoint[0] - StartPoint[0];
            public double DirectionY => EndPoint[1] - StartPoint[1];
            public double Length => Math.Sqrt(DirectionX * DirectionX + DirectionY * DirectionY);

            // Trung điểm
            public double MidX => (StartPoint[0] + EndPoint[0]) / 2.0;
            public double MidY => (StartPoint[1] + EndPoint[1]) / 2.0;

            // Góc hướng chuẩn hóa [0, π)
            public double Angle
            {
                get
                {
                    double a = Math.Atan2(DirectionY, DirectionX);
                    while (a < 0) a += Math.PI;
                    while (a >= Math.PI) a -= Math.PI;
                    return a;
                }
            }

            // Vector pháp tuyến (vuông góc với hướng)
            public double NormalX => -DirectionY / Length;
            public double NormalY => DirectionX / Length;
        }

        // ================================================================
        // V12: THUẬT TOÁN CHÍNH - Anchor → Text → Partner
        // ================================================================

        public List<CadBeamData> GetCadBeams(string beamLayer = null, string textLayer = null)
        {
            List<CadBeamData> beams = new List<CadBeamData>();
            try
            {
                dynamic utility = _acadDoc.Utility;
                dynamic ssets = _acadDoc.SelectionSets;

                string ssetName = "BeamsSet_" + DateTime.Now.Ticks;
                dynamic sset = null;
                try { sset = ssets.Add(ssetName); }
                catch { sset = ssets.Item(ssetName); }

                utility.Prompt("\nV12: Quét chọn vùng dầm cần vẽ... ");
                sset.SelectOnScreen();

                // ── Bước 1: Thu thập TẤT CẢ đối tượng ──
                List<CadLineSegment> allSegments = new List<CadLineSegment>();
                List<dynamic> allTexts = new List<dynamic>();

                for (int i = 0; i < sset.Count; i++)
                {
                    dynamic entity = sset.Item(i);
                    string objName = entity.ObjectName;
                    string entLayer = entity.Layer;

                    if (objName == "AcDbLine")
                    {
                        allSegments.Add(new CadLineSegment
                        {
                            StartPoint = entity.StartPoint,
                            EndPoint = entity.EndPoint,
                            Id = entity.Handle,
                            Layer = entLayer,
                            Color = GetEntityColor(entity)
                        });
                    }
                    else if (objName == "AcDbPolyline" || objName == "AcDb2dPolyline")
                    {
                        var segments = ExtractSegmentsFromPolyline(entity);
                        allSegments.AddRange(segments);
                    }
                    else if (objName == "AcDbHatch")
                    {
                        var segments = ExtractSegmentsFromHatch(entity);
                        allSegments.AddRange(segments);
                    }
                    else if (objName == "AcDbText" || objName == "AcDbMText")
                    {
                        if (string.IsNullOrEmpty(textLayer) || string.Equals(entLayer, textLayer, StringComparison.OrdinalIgnoreCase))
                        {
                            allTexts.Add(entity);
                        }
                    }
                }

                // ── Bước 2: Phân loại Anchor Lines (nét trên beam layer) vs All Lines ──
                allSegments = PreProcessSegments(allSegments);

                List<CadLineSegment> anchorLines;
                List<CadLineSegment> potentialPartners;

                if (!string.IsNullOrEmpty(beamLayer))
                {
                    anchorLines = allSegments.Where(s => string.Equals(s.Layer, beamLayer, StringComparison.OrdinalIgnoreCase)).ToList();
                    // Fallback: nếu không có nét nào trên beamLayer, thử nhận Layer "0"
                    if (anchorLines.Count == 0)
                        anchorLines = allSegments.Where(s => s.Layer == "0").ToList();
                    potentialPartners = allSegments; // Tất cả nét đều có thể là partner
                }
                else
                {
                    // Nếu không chọn beam layer → tất cả đều là anchor
                    anchorLines = allSegments;
                    potentialPartners = allSegments;
                }

                // ── Bước 3: Duyệt từng Anchor Line → Tìm Text → Tìm Partner ──
                HashSet<string> usedIds = new HashSet<string>();
                var confirmedCandidates = new List<BeamCandidate>();
                var commonWidths = GetCommonBeamWidths(allTexts);

                foreach (var anchor in anchorLines)
                {
                    if (usedIds.Contains(anchor.Id)) continue;
                    if (anchor.Length < 500) continue; // Bỏ nét quá ngắn

                    // ── Fast-path: Polyline có bề dày thực sự (>=100mm) → tạo dầm ngay từ 1 nét ──
                    if (anchor.PolylineWidth >= 100 && anchor.PolylineWidth < 3000)
                    {
                        var plTexts = FindParallelTexts(anchor, allTexts);
                        double beamB = anchor.PolylineWidth;
                        double beamH = 500; // fallback mặc định
                        string beamContent = "";
                        if (plTexts.Count > 0)
                        {
                            beamB = plTexts[0].Width;
                            beamH = plTexts[0].Height;
                            beamContent = plTexts[0].Content;
                        }
                        var plBeam = new CadBeamData
                        {
                            StartX = anchor.StartPoint[0],
                            StartY = anchor.StartPoint[1],
                            EndX = anchor.EndPoint[0],
                            EndY = anchor.EndPoint[1],
                            Width = beamB,
                            Height = beamH,
                            TextContent = beamContent,
                            IsPaired = false
                        };
                        ExtractMark(plBeam);
                        if (plBeam.IsValid)
                        {
                            beams.Add(plBeam);
                            usedIds.Add(anchor.Id);
                        }
                        continue;
                    }

                    // ── Fast-path: Closed Rect Polyline – cặp segment theo GroupId ──
                    if (anchor.GroupId != null)
                    {
                        CadLineSegment groupPartner = potentialPartners
                            .FirstOrDefault(p => p.GroupId == anchor.GroupId && p.Id != anchor.Id && !usedIds.Contains(p.Id));
                        if (groupPartner != null)
                        {
                            double overlapLen = GetSegmentOverlapLength(anchor, groupPartner);
                            double measuredW = GetPerpendicularDistance(anchor.StartPoint, anchor.EndPoint, groupPartner.StartPoint);
                            if (overlapLen > 200 && measuredW > 50)
                            {
                                var plTexts2 = FindParallelTexts(anchor, allTexts);
                                // Root cause fix: PHẢI có Text kích thước hợp lệ, không dùng measuredW làm Width
                                if (plTexts2.Count == 0) { } // Bỏ qua nếu không có text
                                else
                                {
                                    double gB = plTexts2[0].Width, gH = plTexts2[0].Height;
                                    string gContent = plTexts2[0].Content;
                                    confirmedCandidates.Add(new BeamCandidate
                                    {
                                        MainLine = anchor,
                                        SubLine = groupPartner,
                                        MeasuredWidth = measuredW,
                                        TextWidth = gB,
                                        TextHeight = gH,
                                        TextContent = gContent,
                                        OverlapLength = overlapLen,
                                        Confidence = overlapLen + 800 // Ưu tiên cao nhất
                                    });
                                }
                            }
                        }
                        continue;
                    }

                    // 3a. Tìm Text song song gần nhất (bán kính R = width * 1.5, tối thiểu 1500mm)
                    var nearbyTexts = FindParallelTexts(anchor, allTexts);
                    if (nearbyTexts.Count == 0)
                    {
                        // Root cause fix: Nếu không có Text và commonWidths rỗng → bỏ qua hoàn toàn
                        // Không bao giờ dùng measuredWidth làm TextWidth → nguồn gốc của kích thước rác
                        if (commonWidths.Count > 0)
                        {
                            CadLineSegment partner = FindPartnerWithoutText(anchor, potentialPartners, usedIds, commonWidths);
                            if (partner != null)
                            {
                                double overlapLen = GetSegmentOverlapLength(anchor, partner);
                                double measuredWidth = GetPerpendicularDistance(anchor.StartPoint, anchor.EndPoint, partner.StartPoint);
                                // Chỉ chấp nhận nếu measuredWidth khớp với ít nhất 1 common width ±15%
                                double matchedWidth = commonWidths
                                    .Where(w => Math.Abs(w - measuredWidth) / w < 0.15)
                                    .OrderBy(w => Math.Abs(w - measuredWidth))
                                    .FirstOrDefault();
                                if (matchedWidth > 0 && overlapLen > 200 && IsProjectionWithinRange(anchor, partner))
                                {
                                    confirmedCandidates.Add(new BeamCandidate
                                    {
                                        MainLine = anchor,
                                        SubLine = partner,
                                        MeasuredWidth = measuredWidth,
                                        TextWidth = matchedWidth, // Dùng kích thước chuẩn từ text, không dùng số đo thô
                                        TextHeight = 500,
                                        TextContent = "",
                                        OverlapLength = overlapLen,
                                        Confidence = overlapLen + 100
                                    });
                                }
                            }
                        }
                        continue;
                    }

                    // 3b. Với mỗi Text, lấy giá trị B → Tìm nét Partner cách anchor đúng B (±20mm)
                    foreach (var textInfo in nearbyTexts)
                    {
                        double expectedWidth = textInfo.Width;

                        CadLineSegment partner = FindParallelPartner(anchor, potentialPartners, expectedWidth, usedIds);

                        if (partner != null)
                        {
                            double overlapLen = GetSegmentOverlapLength(anchor, partner);

                            // 3c. Double-Check: Trung điểm partner chiếu xuống anchor phải nằm trong phạm vi
                            if (overlapLen > 200 && IsProjectionWithinRange(anchor, partner))
                            {
                                 double measuredWidth = GetPerpendicularDistance(anchor.StartPoint, anchor.EndPoint, partner.StartPoint);
                                
                                // Anti-False-Positive: Dầm không được rộng quá 1200mm và phải dài hơn rộng
                                if (measuredWidth > 1200 || anchor.Length < measuredWidth * 1.2) continue;

                                confirmedCandidates.Add(new BeamCandidate
                                {
                                    MainLine = anchor,
                                    SubLine = partner,
                                    MeasuredWidth = measuredWidth,
                                    TextWidth = textInfo.Width,
                                    TextHeight = textInfo.Height,
                                    TextContent = textInfo.Content,
                                    OverlapLength = overlapLen,
                                    Confidence = (overlapLen) + (string.Equals(anchor.Layer, partner.Layer, StringComparison.OrdinalIgnoreCase) ? 500 : 200)
                                });
                            }
                        }
                    }
                }

                // ── Bước 4: Xử lý & loại trùng ──
                foreach (var candidate in confirmedCandidates.OrderByDescending(c => c.Confidence))
                {
                    if (usedIds.Contains(candidate.MainLine.Id) || usedIds.Contains(candidate.SubLine.Id)) continue;

                    var beam = new CadBeamData();
                    beam.TextContent = candidate.TextContent;
                    beam.Width = candidate.TextWidth;   // ƯU TIÊN TEXT
                    beam.Height = candidate.TextHeight;
                    beam.IsPaired = true;

                    // Tính đường tâm dầm (midpoint của 2 nét)
                    SetupBeamCenterline(beam, candidate.MainLine, candidate.SubLine);
                    beam.MeasuredWidth = candidate.MeasuredWidth; // Fix Low: Gán giá trị đo được thực tế

                    // Parse Mark từ text
                    ExtractMark(beam);

                    if (beam.IsValid)
                    {
                        beams.Add(beam);
                        usedIds.Add(candidate.MainLine.Id);
                        usedIds.Add(candidate.SubLine.Id);
                    }
                }

                // ── Bước 5 (Fallback): Xử lý nét Anchor chưa có partner ──
                // Nếu không có beam layer → bỏ qua (tránh false positive)
                if (!string.IsNullOrEmpty(beamLayer))
                {
                    foreach (var anchor in anchorLines)
                    {
                        if (usedIds.Contains(anchor.Id)) continue;
                        if (anchor.Length < 1000) continue;

                        // Tìm Text phù hợp để tạo beam single-line
                        var nearbyTexts = FindParallelTexts(anchor, allTexts);
                        if (nearbyTexts.Count > 0)
                        {
                            var bestText = nearbyTexts.First();
                            var beam = new CadBeamData();
                            beam.TextContent = bestText.Content;
                            beam.Width = bestText.Width;
                            beam.Height = bestText.Height;
                            beam.StartX = anchor.StartPoint[0];
                            beam.StartY = anchor.StartPoint[1];
                            beam.EndX = anchor.EndPoint[0];
                            beam.EndY = anchor.EndPoint[1];
                            beam.IsPaired = false;
                            ExtractMark(beam);

                            if (beam.IsValid)
                            {
                                beams.Add(beam);
                                usedIds.Add(anchor.Id);
                            }
                        }
                    }
                }

                sset.Delete();

                var mergedBeams = MergeCollinearBeams(beams);
                AssignMarksToBeams(mergedBeams, allTexts);
                foreach (var beam in mergedBeams)
                    NormalizeBeamGeometry(beam);
                return mergedBeams;
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi V12: " + ex.Message);
            }
        }

        // ================================================================
        // V12: CÁC HÀM CON - Thuật toán hình học
        // ================================================================

        /// <summary>Text info chứa giá trị đã parse</summary>
        private class TextInfo
        {
            public double Width { get; set; }
            public double Height { get; set; }
            public string Content { get; set; }
            public double Distance { get; set; }    // Khoảng cách tới anchor
        }

        /// <summary>Tìm tất cả Text song song với anchor line, sắp xếp theo khoảng cách gần nhất</summary>
        private List<TextInfo> FindParallelTexts(CadLineSegment anchor, List<dynamic> allTexts)
        {
            var result = new List<TextInfo>();
            double searchRadius = 5000.0; // R = 5000mm mặc định

            double anchorAngle = anchor.Angle;

            foreach (var txt in allTexts)
            {
                double[] p = txt.InsertionPoint;
                string content = GetCleanText(txt);

                // Kiểm tra text có chứa kích thước không
                if (!Regex.IsMatch(content, @"(\d+[\.,]?\d*)\s*[xX\*\-\/]\s*(\d+[\.,]?\d*)", RegexOptions.IgnoreCase))
                    continue;

                // Parse kích thước
                var tempBeam = new CadBeamData { TextContent = content };
                ParseDimensionsV12(tempBeam);
                if (tempBeam.Width <= 0 || tempBeam.Height <= 0) continue;

                // Tính khoảng cách text tới anchor line
                double distToLine = GetPerpendicularDistance(anchor.StartPoint, anchor.EndPoint, p);
                
                // Bán kính quét linh hoạt: R = width * 1.5, tối thiểu 1500mm  
                double dynamicRadius = Math.Max(searchRadius, tempBeam.Width * 3.0);
                if (distToLine > dynamicRadius) continue;

                // V11.2: Kiểm tra góc xoay text SONG SONG với hướng dầm
                double textRotation = 0;
                try { textRotation = (double)txt.Rotation; } catch { }
                while (textRotation < 0) textRotation += Math.PI;
                while (textRotation >= Math.PI) textRotation -= Math.PI;

                double angleDiff = Math.Abs(anchorAngle - textRotation);
                if (angleDiff > Math.PI / 2.0) angleDiff = Math.PI - angleDiff;
                if (angleDiff >= Math.PI / 9.0) continue; // ±20° tolerance

                // Kiểm tra text nằm trong phạm vi chiều dài anchor (chiếu lên trục anchor)
                double dx = anchor.DirectionX, dy = anchor.DirectionY;
                double len = anchor.Length;
                double ux = dx / len, uy = dy / len;
                double proj = ((p[0] - anchor.StartPoint[0]) * ux + (p[1] - anchor.StartPoint[1]) * uy) / len;
                if (proj < -0.5 || proj > 1.5) continue; // Cho phép lệch 50% ngoài biên

                result.Add(new TextInfo
                {
                    Width = tempBeam.Width,
                    Height = tempBeam.Height,
                    Content = content,
                    Distance = distToLine
                });
            }

            return result.OrderBy(t => t.Distance).ToList();
        }

        /// <summary>Tìm nét song song cách anchor đúng widthMm (±20mm), không quan tâm layer</summary>
        private List<double> GetCommonBeamWidths(List<dynamic> allTexts)
        {
            var widths = new List<double>();
            foreach (var txt in allTexts)
            {
                string content = GetCleanText(txt);
                if (!Regex.IsMatch(content, @"(\d+[\.,]?\d*)\s*[xX\*\-\/]\s*(\d+[\.,]?\d*)", RegexOptions.IgnoreCase))
                    continue;

                var beam = new CadBeamData { TextContent = content };
                ParseDimensionsV12(beam);
                if (beam.Width >= 100 && beam.Width <= 2000)
                    widths.Add(Math.Round(beam.Width / 10.0) * 10.0);
            }

            var common = widths
                .GroupBy(w => w)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            if (common.Count == 0)
                common.AddRange(new[] { 200.0, 300.0, 400.0 });

            return common;
        }

        private double CalculatePartnerScore(CadLineSegment anchor, CadLineSegment candidate, double expectedWidth)
        {
            double anchorLen = anchor.Length;
            double candidateLen = candidate.Length;
            if (anchorLen < 10 || candidateLen < 200 || expectedWidth <= 0) return double.NegativeInfinity;

            double dot = Math.Abs((anchor.DirectionX * candidate.DirectionX + anchor.DirectionY * candidate.DirectionY) / (anchorLen * candidateLen));
            if (dot < 0.999) return double.NegativeInfinity;

            double measuredWidth = GetPerpendicularDistance(anchor.StartPoint, anchor.EndPoint, candidate.StartPoint);
            if (measuredWidth < 50 || measuredWidth > 3000) return double.NegativeInfinity;

            double widthScore = 1.0 - (Math.Abs(measuredWidth - expectedWidth) / expectedWidth);
            if (widthScore < 0.7) return double.NegativeInfinity; // Chỉ cho phép sai số tối đa 30%

            double overlap = GetSegmentOverlapLength(anchor, candidate);
            if (overlap < 200) return double.NegativeInfinity;

            double overlapScore = Math.Min(1.0, overlap / Math.Max(anchorLen, candidateLen));
            double layerScore = string.Equals(anchor.Layer, candidate.Layer, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
            double colorScore = anchor.Color >= 0 && anchor.Color == candidate.Color ? 0.5 : 0.0;

            return widthScore * 40.0 + overlapScore * 30.0 + dot * 20.0 + layerScore * 10.0 + colorScore * 5.0;
        }

        private CadLineSegment FindParallelPartner(CadLineSegment anchor, List<CadLineSegment> allLines, double widthMm, HashSet<string> usedIds)
        {
            CadLineSegment bestPartner = null;
            double bestScore = double.NegativeInfinity;

            double anchorDx = anchor.DirectionX, anchorDy = anchor.DirectionY;
            double anchorLen = anchor.Length;
            if (anchorLen < 10) return null;

            foreach (var line in allLines)
            {
                if (line.Id == anchor.Id) continue;
                if (usedIds.Contains(line.Id)) continue;

                // 1. Kiểm tra song song (dot product ~1 hoặc ~-1)
                double lineDx = line.DirectionX, lineDy = line.DirectionY;
                double lineLen = line.Length;
                if (lineLen < 200) continue;

                double dot = Math.Abs((anchorDx * lineDx + anchorDy * lineDy) / (anchorLen * lineLen));
                if (dot < 0.999) continue; // Gần song song tuyệt đối

                // 2. Kiểm tra khoảng cách hình học ≈ widthMm (±20mm)
                double dist = GetPerpendicularDistance(anchor.StartPoint, anchor.EndPoint, line.StartPoint);
                if (dist > 2000.0) continue;

                // 3. Kiểm tra overlap
                double overlap = GetSegmentOverlapLength(anchor, line);
                if (overlap < 200) continue;

                // 4. Double-check: Trung điểm line B chiếu lên anchor A phải nằm trong phạm vi
                if (!IsProjectionWithinRange(anchor, line)) continue;

                // Chọn partner có overlap lớn nhất
                double score = CalculatePartnerScore(anchor, line, widthMm);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPartner = line;
                }
            }

            return bestPartner;
        }

        /// <summary>Kiểm tra projection ngược: trung điểm B nằm trong phạm vi chiều dài A</summary>
        /// <summary>Tìm nét song song theo khoảng cách hình học khi chưa tìm thấy text kích thước</summary>
        private CadLineSegment FindPartnerWithoutText(CadLineSegment anchor, List<CadLineSegment> allLines, HashSet<string> usedIds, List<double> expectedWidths)
        {
            CadLineSegment bestPartner = null;
            double bestScore = double.NegativeInfinity;

            double anchorDx = anchor.DirectionX, anchorDy = anchor.DirectionY;
            double anchorLen = anchor.Length;
            if (anchorLen < 10) return null;

            foreach (var line in allLines)
            {
                if (line.Id == anchor.Id) continue;
                if (usedIds.Contains(line.Id)) continue;

                double lineDx = line.DirectionX, lineDy = line.DirectionY;
                double lineLen = line.Length;
                if (lineLen < 200) continue;

                double dot = Math.Abs((anchorDx * lineDx + anchorDy * lineDy) / (anchorLen * lineLen));
                if (dot < 0.999) continue;

                double dist = GetPerpendicularDistance(anchor.StartPoint, anchor.EndPoint, line.StartPoint);
                if (dist < 50.0 || dist > 3000.0) continue; // Lọc khoảng cách bất hợp lý

                double overlap = GetSegmentOverlapLength(anchor, line);
                if (overlap < 200) continue;
                if (!IsProjectionWithinRange(anchor, line)) continue;

                double score = expectedWidths
                    .Select(width => CalculatePartnerScore(anchor, line, width))
                    .DefaultIfEmpty(double.NegativeInfinity)
                    .Max();

                if (score <= bestScore) continue;

                if (score > 40) // Chỉ lấy nếu điểm tin cậy đủ cao (khớp với commonWidths)
                {
                    bestScore = score;
                    bestPartner = line;
                }
            }

            return bestPartner;
        }

        private bool IsProjectionWithinRange(CadLineSegment anchor, CadLineSegment partner)
        {
            double dx = anchor.DirectionX, dy = anchor.DirectionY;
            double len = anchor.Length;
            if (len < 10) return false;
            double ux = dx / len, uy = dy / len;

            double proj = ((partner.MidX - anchor.StartPoint[0]) * ux + (partner.MidY - anchor.StartPoint[1]) * uy) / len;
            return proj > -0.3 && proj < 1.3;
        }

        /// <summary>Tính đường tâm dầm từ 2 nét song song</summary>
        private void SetupBeamCenterline(CadBeamData beam, CadLineSegment main, CadLineSegment sub)
        {
            double[] s1 = main.StartPoint, e1 = main.EndPoint;
            double[] s2 = sub.StartPoint, e2 = sub.EndPoint;

            // Xác định hướng phù hợp của sub so với main
            double d1 = Math.Sqrt(Math.Pow(s1[0] - s2[0], 2) + Math.Pow(s1[1] - s2[1], 2));
            double d2 = Math.Sqrt(Math.Pow(s1[0] - e2[0], 2) + Math.Pow(s1[1] - e2[1], 2));

            if (d1 < d2)
            {
                beam.StartX = (s1[0] + s2[0]) / 2.0;
                beam.StartY = (s1[1] + s2[1]) / 2.0;
                beam.EndX = (e1[0] + e2[0]) / 2.0;
                beam.EndY = (e1[1] + e2[1]) / 2.0;
            }
            else
            {
                beam.StartX = (s1[0] + e2[0]) / 2.0;
                beam.StartY = (s1[1] + e2[1]) / 2.0;
                beam.EndX = (e1[0] + s2[0]) / 2.0;
                beam.EndY = (e1[1] + s2[1]) / 2.0;
            }
        }

        // ================================================================
        // V12: PARSE DIMENSIONS & MARK
        // ================================================================

        private void ParseDimensionsV12(CadBeamData beam)
        {
            var match = Regex.Match(beam.TextContent, @"(\d+[\.,]?\d*)\s*[xX\*\-\/]\s*(\d+[\.,]?\d*)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string val1 = match.Groups[1].Value.Replace(',', '.');
                string val2 = match.Groups[2].Value.Replace(',', '.');

                try
                {
                    double b = double.Parse(val1, CultureInfo.InvariantCulture);
                    double h = double.Parse(val2, CultureInfo.InvariantCulture);

                    if (b < 100) b *= 10;
                    if (h < 100) h *= 10;

                    beam.Width = b;
                    beam.Height = h;
                }
                catch { }
            }
        }

        private void ExtractMark(CadBeamData beam)
        {
            if (string.IsNullOrEmpty(beam.TextContent)) return;

            var match = Regex.Match(beam.TextContent, @"(\d+[\.,]?\d*)\s*[xX\*\-\/]\s*(\d+[\.,]?\d*)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string mark = Regex.Replace(beam.TextContent, Regex.Escape(match.Value), "");
                mark = Regex.Replace(mark, @"[()\[\]]", "").Trim();

                // Giữ prefix PT, RC, D làm một phần Mark
                if (mark.Length > 0)
                {
                    beam.Mark = mark.Trim();
                }
                else
                {
                    var prefixMatch = Regex.Match(beam.TextContent, @"^(PT|RC|D\d*)\b", RegexOptions.IgnoreCase);
                    if (prefixMatch.Success) beam.Mark = prefixMatch.Value;
                }
            }
        }

        // ================================================================
        // HÀM TIỆN ÍCH - Geometry
        // ================================================================

        private List<CadLineSegment> ExtractSegmentsFromPolyline(dynamic pline)
        {
            List<CadLineSegment> result = new List<CadLineSegment>();
            try
            {
                int count = pline.NumberOfVertices;
                bool isClosed = pline.Closed;
                string parentHandle = pline.Handle;
                string layer = pline.Layer;

                // Đọc bề dày (GlobalWidth) của Polyline
                double polyWidth = 0;
                try { polyWidth = (double)pline.GlobalWidth; } catch { }
                if (polyWidth <= 0)
                    try { polyWidth = (double)pline.ConstantWidth; } catch { }

                for (int i = 0; i < (isClosed ? count : count - 1); i++)
                {
                    double[] p1 = pline.Coordinate[i];
                    double[] p2 = pline.Coordinate[(i + 1) % count];

                    // Nếu GlobalWidth = 0, thử lấy Width của từng phân đoạn (dành cho LWPolyline)
                    double segmentWidth = polyWidth;
                    if (segmentWidth <= 0)
                    {
                        try { segmentWidth = (double)pline.GetWidthInfoAt(i, out double startW, out double endW); segmentWidth = startW; } catch { }
                        if (segmentWidth <= 0) try { segmentWidth = (double)pline.GetStartWidthAt(i); } catch { }
                    }

                    double[] s = new double[] { p1[0], p1[1], 0 };
                    double[] e = new double[] { p2[0], p2[1], 0 };

                    double len = Math.Sqrt(Math.Pow(s[0] - e[0], 2) + Math.Pow(s[1] - e[1], 2));
                    if (len > 50) // Hạ xuống 50mm để nhận diện được cạnh ngắn dầm 200mm
                    {
                        result.Add(new CadLineSegment
                        {
                            StartPoint = s,
                            EndPoint = e,
                            Id = $"{parentHandle}_{i}",
                            Layer = layer,
                            Color = GetEntityColor(pline),
                            PolylineWidth = segmentWidth
                        });
                    }
                }

                // Gán GroupId cho Closed Polyline hình chữ nhật (4 cạnh)
                // Cặp song song: (0,2) và (1,3)
                if (isClosed && result.Count == 4)
                {
                    result[0].GroupId = parentHandle + "_pairA";
                    result[2].GroupId = parentHandle + "_pairA";
                    result[1].GroupId = parentHandle + "_pairB";
                    result[3].GroupId = parentHandle + "_pairB";
                }
            }
            catch { }
            return result;
        }

        private List<CadLineSegment> ExtractSegmentsFromHatch(dynamic hatch)
        {
            List<CadLineSegment> result = new List<CadLineSegment>();
            try
            {
                string parentHandle = hatch.Handle;
                string layer = hatch.Layer;
                int color = GetEntityColor(hatch);
                // V13.3: Sửa lỗi GetLoopAt (cần 2 tham số out) và thêm Fallback BoundingBox
                int loopCount = 0;
                try { loopCount = hatch.NumberOfLoops; } catch { }
                
                bool loopExtracted = false;
                if (loopCount > 0)
                {
                    for (int i = 0; i < loopCount; i++)
                    {
                        try
                        {
                            // GetLoopAt trong AutoCAD COM cần 2 tham số out: LoopType và Entities
                            hatch.GetLoopAt(i, out int loopType, out object loopEntities);
                            if (loopEntities == null) continue;

                            object[] entities = (object[])loopEntities;
                            if (entities.Length > 0) loopExtracted = true;

                            for (int j = 0; j < entities.Length; j++)
                            {
                                dynamic ent = entities[j];
                                string objName = "";
                                try { objName = ent.ObjectName; } catch { continue; }

                                if (objName == "AcDbLine")
                                {
                                    result.Add(new CadLineSegment
                                    {
                                        StartPoint = ent.StartPoint,
                                        EndPoint = ent.EndPoint,
                                        Id = $"{parentHandle}_h{i}_{j}",
                                        Layer = layer,
                                        Color = color,
                                        GroupId = parentHandle + (j % 2 == 0 ? "_pairA" : "_pairB")
                                    });
                                }
                                else if (objName == "AcDbPolyline" || objName == "AcDb2dPolyline")
                                {
                                    var plSegments = ExtractSegmentsFromPolyline(ent);
                                    foreach (var seg in plSegments)
                                    {
                                        // Giữ nguyên GroupId của Polyline nếu nó đã được phân cặp (count=4)
                                        if (string.IsNullOrEmpty(seg.GroupId))
                                            seg.GroupId = parentHandle + "_loop" + i;
                                        result.Add(seg);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                // FALLBACK: Nếu không lấy được loop (do lỗi API), dùng lại BoundingBox để không bị mất dầm
                if (!loopExtracted)
                {
                    object minPt, maxPt;
                    hatch.GetBoundingBox(out minPt, out maxPt);
                    double[] min = (double[])minPt;
                    double[] max = (double[])maxPt;
                    double[][] pts = new double[][] {
                        new double[] { min[0], min[1], 0 },
                        new double[] { max[0], min[1], 0 },
                        new double[] { max[0], max[1], 0 },
                        new double[] { min[0], max[1], 0 }
                    };
                    for (int i = 0; i < 4; i++) {
                        result.Add(new CadLineSegment {
                            StartPoint = pts[i], EndPoint = pts[(i + 1) % 4],
                            Id = $"{parentHandle}_fb{i}", Layer = layer, Color = color,
                            GroupId = parentHandle + (i % 2 == 0 ? "_pairA" : "_pairB")
                        });
                    }
                }
            }
            catch { }
            return result;
        }

        private double GetPerpendicularDistance(double[] s, double[] e, double[] p)
        {
            double dx = e[0] - s[0];
            double dy = e[1] - s[1];
            double L2 = dx * dx + dy * dy;
            if (L2 == 0) return 0;
            return Math.Abs(dy * p[0] - dx * p[1] + e[0] * s[1] - e[1] * s[0]) / Math.Sqrt(L2);
        }

        private double GetSegmentOverlapLength(CadLineSegment s1, CadLineSegment s2)
        {
            double dx = s1.EndPoint[0] - s1.StartPoint[0], dy = s1.EndPoint[1] - s1.StartPoint[1];
            double L = Math.Sqrt(dx * dx + dy * dy);
            if (L < 1) return 0;
            double ux = dx / L, uy = dy / L;
            double t1 = ((s2.StartPoint[0] - s1.StartPoint[0]) * ux + (s2.StartPoint[1] - s1.StartPoint[1]) * uy) / L;
            double t2 = ((s2.EndPoint[0] - s1.StartPoint[0]) * ux + (s2.EndPoint[1] - s1.StartPoint[1]) * uy) / L;
            double start = Math.Max(0, Math.Min(t1, t2)), end = Math.Min(1, Math.Max(t1, t2));
            if (start < end) return (end - start) * L;
            return 0;
        }

        // ================================================================
        // HÀM TIỆN ÍCH - Text & Mark
        // ================================================================

        private dynamic FindNearestText(double x, double y, List<dynamic> texts)
        {
            dynamic nearest = null;
            double minDist = 3000.0;

            foreach (var txt in texts)
            {
                var insPt = txt.InsertionPoint;
                double dist = Math.Sqrt(Math.Pow(insPt[0] - x, 2) + Math.Pow(insPt[1] - y, 2));
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = txt;
                }
            }
            return nearest;
        }

        private string GetCleanText(dynamic textObj)
        {
            string txt = textObj.TextString;

            if (textObj.ObjectName == "AcDbMText")
            {
                txt = Regex.Replace(txt, @"\{[^;]*;", "");
                txt = txt.Replace("}", "");
                txt = Regex.Replace(txt, @"\\[PfgCLHKTQW].*?;", "");
                txt = Regex.Replace(txt, @"\\[PfgCLHKTQW]", " ");
            }

            return txt.Trim();
        }

        // ================================================================
        // HÀM TIỆN ÍCH - Merge & Assign
        // ================================================================

        private int GetEntityColor(dynamic entity)
        {
            try { return (int)entity.Color; }
            catch { return -1; }
        }

        private List<CadLineSegment> PreProcessSegments(List<CadLineSegment> source)
        {
            if (source == null || source.Count <= 1) return source;

            var result = source
                .Where(s => s.GroupId != null || s.PolylineWidth > 0)
                .ToList();

            var normalSegments = source
                .Where(s => s.GroupId == null && s.PolylineWidth <= 0 && s.Length > 50)
                .ToList();

            var groups = normalSegments.GroupBy(s =>
            {
                double angleKey = Math.Round(s.Angle / 0.01);
                double normalX = -Math.Sin(s.Angle);
                double normalY = Math.Cos(s.Angle);
                double distanceKey = Math.Round(((s.StartPoint[0] * normalX) + (s.StartPoint[1] * normalY)) / 20.0);
                return $"{s.Layer}|{s.Color}|{angleKey}|{distanceKey}";
            });

            foreach (var group in groups)
            {
                var items = group.ToList();
                if (items.Count == 1)
                {
                    result.Add(items[0]);
                    continue;
                }

                double angle = items[0].Angle;
                double ux = Math.Cos(angle);
                double uy = Math.Sin(angle);

                var intervals = items
                    .Select(s =>
                    {
                        double t1 = s.StartPoint[0] * ux + s.StartPoint[1] * uy;
                        double t2 = s.EndPoint[0] * ux + s.EndPoint[1] * uy;
                        return new
                        {
                            Segment = s,
                            Min = Math.Min(t1, t2),
                            Max = Math.Max(t1, t2)
                        };
                    })
                    .OrderBy(i => i.Min)
                    .ToList();

                var cluster = new List<CadLineSegment> { intervals[0].Segment };
                double clusterMin = intervals[0].Min;
                double clusterMax = intervals[0].Max;

                for (int i = 1; i < intervals.Count; i++)
                {
                    if (intervals[i].Min - clusterMax <= 200.0)
                    {
                        cluster.Add(intervals[i].Segment);
                        clusterMax = Math.Max(clusterMax, intervals[i].Max);
                    }
                    else
                    {
                        result.Add(CreateMergedSegment(cluster, clusterMin, clusterMax, ux, uy));
                        cluster = new List<CadLineSegment> { intervals[i].Segment };
                        clusterMin = intervals[i].Min;
                        clusterMax = intervals[i].Max;
                    }
                }

                result.Add(CreateMergedSegment(cluster, clusterMin, clusterMax, ux, uy));
            }

            return result;
        }

        private CadLineSegment CreateMergedSegment(List<CadLineSegment> cluster, double minT, double maxT, double ux, double uy)
        {
            var first = cluster[0];
            double nx = -uy;
            double ny = ux;
            double offset = cluster.Average(s => s.StartPoint[0] * nx + s.StartPoint[1] * ny);

            return new CadLineSegment
            {
                StartPoint = new[] { minT * ux + offset * nx, minT * uy + offset * ny, 0.0 },
                EndPoint = new[] { maxT * ux + offset * nx, maxT * uy + offset * ny, 0.0 },
                Id = string.Join("+", cluster.Select(s => s.Id)),
                Layer = first.Layer,
                Color = first.Color
            };
        }

        private void NormalizeBeamGeometry(CadBeamData beam)
        {
            double dx = beam.EndX - beam.StartX;
            double dy = beam.EndY - beam.StartY;
            double angle = Math.Abs(Math.Atan2(dy, dx));
            while (angle >= Math.PI) angle -= Math.PI;

            if (angle < 0.02 || Math.Abs(angle - Math.PI) < 0.02)
            {
                double y = (beam.StartY + beam.EndY) / 2.0;
                beam.StartY = y;
                beam.EndY = y;
            }
            else if (Math.Abs(angle - Math.PI / 2.0) < 0.02)
            {
                double x = (beam.StartX + beam.EndX) / 2.0;
                beam.StartX = x;
                beam.EndX = x;
            }

            beam.StartX = Math.Round(beam.StartX, 4);
            beam.StartY = Math.Round(beam.StartY, 4);
            beam.EndX = Math.Round(beam.EndX, 4);
            beam.EndY = Math.Round(beam.EndY, 4);
        }

        private List<CadBeamData> MergeCollinearBeams(List<CadBeamData> beams)
        {
            if (beams == null || beams.Count <= 1) return beams;

            var merged = new List<CadBeamData>();
            var used = new HashSet<int>();

            for (int i = 0; i < beams.Count; i++)
            {
                if (used.Contains(i)) continue;

                var current = beams[i];
                bool hasMerged;

                do
                {
                    hasMerged = false;
                    for (int j = i + 1; j < beams.Count; j++)
                    {
                        if (used.Contains(j)) continue;
                        var other = beams[j];

                        if (Math.Abs(current.Width - other.Width) > 5 || Math.Abs(current.Height - other.Height) > 5) continue;

                        double dx1 = current.EndX - current.StartX, dy1 = current.EndY - current.StartY;
                        double len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                        double dx2 = other.EndX - other.StartX, dy2 = other.EndY - other.StartY;
                        double len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                        if (len1 < 10 || len2 < 10) continue;

                        double ux1 = dx1 / len1, uy1 = dy1 / len1;
                        double dot = (dx1 * dx2 + dy1 * dy2) / (len1 * len2);
                        if (Math.Abs(Math.Abs(dot) - 1.0) > 0.05) continue;

                        double dist = Math.Abs(dy1 * other.StartX - dx1 * other.StartY + current.EndX * current.StartY - current.EndY * current.StartX) / (len1 > 0 ? len1 : 1);
                        if (dist > 50) continue;

                        double p3 = (other.StartX - current.StartX) * ux1 + (other.StartY - current.StartY) * uy1;
                        double p4 = (other.EndX - current.StartX) * ux1 + (other.EndY - current.StartY) * uy1;
                        double minOther = Math.Min(p3, p4), maxOther = Math.Max(p3, p4);
                        if (minOther > len1 + 2000 || maxOther < -2000) continue;

                        var pts = new List<double[]> {
                            new double[] { 0.0, current.StartX, current.StartY },
                            new double[] { len1, current.EndX, current.EndY },
                            new double[] { p3, other.StartX, other.StartY },
                            new double[] { p4, other.EndX, other.EndY }
                        };
                        pts = pts.OrderBy(p => p[0]).ToList();

                        current.StartX = pts.First()[1]; current.StartY = pts.First()[2];
                        current.EndX = pts.Last()[1]; current.EndY = pts.Last()[2];

                        if (string.IsNullOrEmpty(current.Mark) && !string.IsNullOrEmpty(other.Mark))
                            current.Mark = other.Mark;

                        used.Add(j);
                        hasMerged = true;
                        break;
                    }
                } while (hasMerged);

                merged.Add(current);
            }

            // Phase 2: Spatial Deduplication - Loại bỏ các dầm chồng lấn nhau
            var finalBeams = new List<CadBeamData>();
            var sorted = merged.OrderBy(b => b.Width).ToList(); // Ưu tiên dầm mảnh hơn (thường là dầm thật)
            var finalUsed = new HashSet<int>();

            for (int i = 0; i < sorted.Count; i++)
            {
                if (finalUsed.Contains(i)) continue;
                var b1 = sorted[i];
                finalBeams.Add(b1);

                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (finalUsed.Contains(j)) continue;
                    var b2 = sorted[j];

                    double dx1 = b1.EndX - b1.StartX, dy1 = b1.EndY - b1.StartY;
                    double dx2 = b2.EndX - b2.StartX, dy2 = b2.EndY - b2.StartY;
                    double len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                    double len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                    if (len1 < 10 || len2 < 10) continue;

                    double dirDot = Math.Abs((dx1 * dx2 + dy1 * dy2) / (len1 * len2));
                    if (dirDot < 0.95) continue;

                    // Kiểm tra nếu 2 dầm gần như trùng tâm và cùng hướng
                    double midX1 = (b1.StartX + b1.EndX) / 2, midY1 = (b1.StartY + b1.EndY) / 2;
                    double midX2 = (b2.StartX + b2.EndX) / 2, midY2 = (b2.StartY + b2.EndY) / 2;
                    double dist = Math.Sqrt(Math.Pow(midX1 - midX2, 2) + Math.Pow(midY1 - midY2, 2));

                    if (dist < 300) // Nếu tâm cách nhau < 300mm -> coi là trùng
                    {
                        finalUsed.Add(j);
                    }
                }
            }

            return finalBeams;
        }

        private void AssignMarksToBeams(List<CadBeamData> beams, List<dynamic> allTexts)
        {
            // Pass 1: Gán Mark text gần nhất cho beam chưa có Mark
            foreach (var beam in beams)
            {
                if (!string.IsNullOrEmpty(beam.Mark)) continue;

                double midX = (beam.StartX + beam.EndX) / 2.0;
                double midY = (beam.StartY + beam.EndY) / 2.0;
                double beamAngle = Math.Atan2(beam.EndY - beam.StartY, beam.EndX - beam.StartX);
                while (beamAngle < 0) beamAngle += Math.PI;
                while (beamAngle >= Math.PI) beamAngle -= Math.PI;

                dynamic closestMarkText = null;
                double minDist = 5000;

                foreach (var txt in allTexts)
                {
                    string content = GetCleanText(txt);
                    if (string.IsNullOrWhiteSpace(content)) continue;

                    double[] p = txt.InsertionPoint;
                    double dist = Math.Sqrt(Math.Pow(p[0] - midX, 2) + Math.Pow(p[1] - midY, 2));
                    if (dist < minDist)
                    {
                        // Fix High: Kiểm tra góc xoay của Text phải tương đối song song với dầm
                        double txtRot = 0;
                        try { txtRot = (double)txt.Rotation; } catch { }
                        double angleDiff = Math.Abs(beamAngle - txtRot);
                        while (angleDiff > Math.PI / 2) angleDiff = Math.PI - angleDiff;
                        if (angleDiff > 0.5) continue; // Lệch quá ~30 độ thì bỏ qua

                        minDist = dist;
                        closestMarkText = txt;
                    }
                }

                if (closestMarkText != null)
                {
                    string content = GetCleanText(closestMarkText);
                    var dimensionMatch = Regex.Match(content, @"(\d+[\.,]?\d*)\s*[xX\*\-\/]\s*(\d+[\.,]?\d*)");

                    if (dimensionMatch.Success)
                    {
                        bool canUpdateDimensions = string.IsNullOrEmpty(beam.TextContent) || beam.Width <= 0 || beam.Height <= 0;
                        beam.TextContent = content;
                        if (canUpdateDimensions)
                            ParseDimensionsV12(beam);
                        ExtractMark(beam);
                    }
                    else
                    {
                        beam.Mark = content;
                    }
                }
            }

            // Pass 2: Truyền Mark cho các dầm collinear cùng nhóm
            var used = new HashSet<int>();
            for (int i = 0; i < beams.Count; i++)
            {
                if (used.Contains(i)) continue;
                var group = new List<CadBeamData> { beams[i] };
                used.Add(i);

                bool addedNew;
                do
                {
                    addedNew = false;
                    for (int j = 0; j < beams.Count; j++)
                    {
                        if (used.Contains(j)) continue;
                        foreach (var member in group)
                        {
                            if (AreCollinearAndConnected(member, beams[j]))
                            {
                                group.Add(beams[j]);
                                used.Add(j);
                                addedNew = true;
                                break;
                            }
                        }
                    }
                } while (addedNew);

                string sharedMark = group.FirstOrDefault(b => !string.IsNullOrEmpty(b.Mark))?.Mark;
                if (!string.IsNullOrEmpty(sharedMark))
                    foreach (var b in group) b.Mark = sharedMark;
            }
        }

        private bool AreCollinearAndConnected(CadBeamData b1, CadBeamData b2)
        {
            double dx1 = b1.EndX - b1.StartX, dy1 = b1.EndY - b1.StartY;
            double len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
            double dx2 = b2.EndX - b2.StartX, dy2 = b2.EndY - b2.StartY;
            double len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
            if (len1 < 10 || len2 < 10) return false;

            double ux1 = dx1 / len1, uy1 = dy1 / len1;
            double dot = (dx1 * dx2 + dy1 * dy2) / (len1 * len2);
            if (Math.Abs(Math.Abs(dot) - 1.0) > 0.05) return false;

            double dist = Math.Abs(dy1 * b2.StartX - dx1 * b2.StartY + b1.EndX * b1.StartY - b1.EndY * b1.StartX) / len1;
            if (dist > 50) return false;

            double p3 = (b2.StartX - b1.StartX) * ux1 + (b2.StartY - b1.StartY) * uy1;
            double p4 = (b2.EndX - b1.StartX) * ux1 + (b2.EndY - b1.StartY) * uy1;
            double minO = Math.Min(p3, p4), maxO = Math.Max(p3, p4);
            if (minO > len1 + 2000 || maxO < -2000) return false;

            return true;
        }

        public void DrawRedLine(double x1, double y1, double x2, double y2)
        {
            try
            {
                double[] start = new double[] { x1, y1, 0 };
                double[] end = new double[] { x2, y2, 0 };
                dynamic line = _acadDoc.ModelSpace.AddLine(start, end);
                line.Color = 1;
                line.Update();
            }
            catch { }
        }
    }
}
