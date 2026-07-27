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

        /// <summary>beamLayers: danh sách layer nét dầm (hỗ trợ nhiều layer). Truyền null/empty = không lọc layer.</summary>
        public List<CadBeamData> GetCadBeams(IReadOnlyList<string> beamLayers = null, IReadOnlyList<string> textLayers = null)
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

                // ── Bước 0: Thống kê & Khởi tạo Diagnostics Session ──
                BeamDiagnosticCollector.Instance.StartSession();
                var diagSession = BeamDiagnosticCollector.Instance.CurrentSession;
                var cadSummary = diagSession.CadSelectionSummary;
                cadSummary.TotalEntities = sset.Count;

                // ── Bước 1: Thu thập TẤT CẢ đối tượng ──
                List<CadLineSegment> allSegments = new List<CadLineSegment>();
                List<dynamic> allTexts = new List<dynamic>();

                var activeTextLayers = (textLayers ?? new List<string>())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < sset.Count; i++)
                {
                    dynamic entity = sset.Item(i);
                    string objName = entity.ObjectName;
                    string entLayer = entity.Layer;

                    if (objName == "AcDbLine")
                    {
                        cadSummary.LineCount++;
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
                        cadSummary.PolylineCount++;
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
                        if (objName == "AcDbText") cadSummary.TextCount++;
                        else cadSummary.MTextCount++;

                        if (activeTextLayers.Count == 0 || activeTextLayers.Contains(entLayer))
                        {
                            allTexts.Add(entity);
                        }
                        else
                        {
                            cadSummary.SkippedCount++;
                            string reasonKey = $"TextLayerNotActive:{entLayer}";
                            if (!cadSummary.SkippedReasons.ContainsKey(reasonKey)) cadSummary.SkippedReasons[reasonKey] = 0;
                            cadSummary.SkippedReasons[reasonKey]++;
                        }
                    }
                    else
                    {
                        cadSummary.SkippedCount++;
                        string reasonKey = $"UnsupportedType:{objName}";
                        if (!cadSummary.SkippedReasons.ContainsKey(reasonKey)) cadSummary.SkippedReasons[reasonKey] = 0;
                        cadSummary.SkippedReasons[reasonKey]++;
                    }
                }

                // ── Bước 2: Phân loại Anchor Lines (nét trên beam layer) vs All Lines ──
                allSegments = PreProcessSegments(allSegments);

                // Chuẩn hoá danh sách beam layers (hỗ trợ nhiều layer)
                var activeBeamLayers = (beamLayers ?? new List<string>())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                List<CadLineSegment> anchorLines;
                List<CadLineSegment> potentialPartners;

                if (activeBeamLayers.Count > 0)
                {
                    // Anchor = nét thuộc BẤT KỲ layer nào trong danh sách
                    anchorLines = allSegments.Where(s => activeBeamLayers.Contains(s.Layer)).ToList();
                    // Fallback: nếu không tìm được nét nào → thử Layer "0"
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
                var rawSegments = new List<CadBeamSegment>();

                foreach (var anchor in anchorLines)
                {
                    if (usedIds.Contains(anchor.Id)) continue;
                    if (anchor.Length < 500) continue; // Bỏ nét quá ngắn

                    // ── Fast-path: Polyline có bề dày thực sự (>=100mm) → tạo dầm ngay từ 1 nét ──
                    if (anchor.PolylineWidth >= 100 && anchor.PolylineWidth < 3000)
                    {
                        var plTexts = FindParallelTexts(anchor, allTexts);
                        double beamB = anchor.PolylineWidth;
                        double beamH = 0; // Để trống Height để AssignMarksToBeams tự điền
                        string beamContent = "";
                        if (plTexts.Count > 0)
                        {
                            beamB = plTexts[0].Width;
                            beamH = plTexts[0].Height;
                            beamContent = plTexts[0].Content;
                        }
                        var tempBeam = new CadBeamData { TextContent = beamContent };
                        ExtractMark(tempBeam);
                        rawSegments.Add(new CadBeamSegment
                        {
                            StartX = anchor.StartPoint[0],
                            StartY = anchor.StartPoint[1],
                            EndX = anchor.EndPoint[0],
                            EndY = anchor.EndPoint[1],
                            Width = beamB,
                            Height = beamH,
                            MeasuredWidth = anchor.PolylineWidth,
                            Mark = tempBeam.Mark,
                            TextContent = beamContent,
                            IsPaired = false,
                            SourceLineIds = new List<string> { anchor.Id },
                            Confidence = 900,
                            Layer = anchor.Layer,
                            DetectionMethod = BeamDetectionMethod.PolylineWidth
                        });
                        usedIds.Add(anchor.Id);
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
                                var plTexts2 = FindTextsInBeamBoundingBox(anchor, groupPartner, allTexts);
                                
                                double gB = 0, gH = 0;
                                string gContent = "";
                                
                                if (plTexts2.Count > 0)
                                {
                                    var dimTexts = plTexts2
                                        .Where(t => t.Width > 0 && t.Height > 0)
                                        .OrderBy(t => Math.Abs(t.Width - measuredW))
                                        .ToList();

                                    if (dimTexts.Count > 0)
                                    {
                                        gB = dimTexts[0].Width;
                                        gH = dimTexts[0].Height;
                                        gContent = dimTexts[0].Content;
                                    }
                                }

                                // Validation
                                if (gB > 0 && Math.Abs(gB - measuredW) / gB > 0.30)
                                {
                                    gB = 0; gH = 0; gContent = "";
                                }

                                if (gB > 0)
                                {
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

                    // 3. Tìm tất cả nét song song tiềm năng (đã lọc length > 200)
                    foreach (var partner in potentialPartners)
                    {
                        if (partner.Id == anchor.Id || usedIds.Contains(partner.Id)) continue;
                        
                        // Kiểm tra góc song song
                        double anchorDx = anchor.DirectionX, anchorDy = anchor.DirectionY, anchorLen = anchor.Length;
                        double partnerDx = partner.DirectionX, partnerDy = partner.DirectionY, partnerLen = partner.Length;
                        if (partnerLen < 200) continue;

                        double dot = Math.Abs((anchorDx * partnerDx + anchorDy * partnerDy) / (anchorLen * partnerLen));
                        if (dot < 0.999) continue; // Phải song song

                        // Kiểm tra khoảng cách đo được hợp lý (50mm - 2000mm)
                        double measuredWidth = GetPerpendicularDistance(anchor.StartPoint, anchor.EndPoint, partner.StartPoint);
                        if (measuredWidth < 50 || measuredWidth > 2000) continue;

                        // Kiểm tra overlap
                        double overlapLen = GetSegmentOverlapLength(anchor, partner);
                        if (overlapLen < 200) continue;
                        
                        // Anti-False-Positive: Dầm không được dài ngắn hơn rộng (trừ khi overlap rất lớn)
                        if (anchor.Length < measuredWidth * 1.2 && overlapLen < measuredWidth * 1.5) continue;

                        // TẠO BOUNDING BOX và QUÉT TEXT
                        var textsInBox = FindTextsInBeamBoundingBox(anchor, partner, allTexts);

                        double expectedB = 0, expectedH = 0;
                        string textContent = "";
                        double confidence = overlapLen;
                        
                        if (textsInBox.Count > 0)
                        {
                            // Lọc ra text kích thước và ưu tiên text có Width gần với measuredWidth nhất
                            var dimTexts = textsInBox
                                .Where(t => t.Width > 0 && t.Height > 0)
                                .OrderBy(t => Math.Abs(t.Width - measuredWidth))
                                .ToList();

                            if (dimTexts.Count > 0)
                            {
                                expectedB = dimTexts[0].Width;
                                expectedH = dimTexts[0].Height;
                                textContent = dimTexts[0].Content;
                                confidence += 500; // Ưu tiên có text
                            }
                        }

                        // VALIDATION: Reject nếu text nói B = 500 nhưng đo thực tế = 300 (lệch > 30%)
                        // (Nghĩa là bắt nhầm text của dầm bên cạnh)
                        if (expectedB > 0 && Math.Abs(expectedB - measuredWidth) / expectedB > 0.30)
                        {
                            expectedB = 0;
                            expectedH = 0;
                            textContent = "";
                            confidence -= 500;
                        }

                        // Nếu không có text -> fallback check commonWidths
                        if (expectedB == 0 && commonWidths.Count > 0)
                        {
                            double matchedWidth = commonWidths
                                .Where(w => Math.Abs(w - measuredWidth) / w < 0.30)
                                .OrderBy(w => Math.Abs(w - measuredWidth))
                                .FirstOrDefault();
                            if (matchedWidth > 0)
                            {
                                expectedB = matchedWidth;
                                expectedH = 0; // Để trống Height để AssignMarksToBeams tự điền
                                confidence += 100;
                            }
                        }

                        if (expectedB > 0)
                        {
                            // Thêm candidate
                            confirmedCandidates.Add(new BeamCandidate
                            {
                                MainLine = anchor,
                                SubLine = partner,
                                MeasuredWidth = measuredWidth,
                                TextWidth = expectedB,
                                TextHeight = expectedH,
                                TextContent = textContent,
                                OverlapLength = overlapLen,
                                Confidence = confidence + (string.Equals(anchor.Layer, partner.Layer, StringComparison.OrdinalIgnoreCase) ? 200 : 0)
                            });
                        }
                    }
                }

                // ── Bước 4: Xử lý & loại trùng ──
                foreach (var candidate in confirmedCandidates.OrderByDescending(c => c.Confidence))
                {
                    if (usedIds.Contains(candidate.MainLine.Id) || usedIds.Contains(candidate.SubLine.Id)) continue;

                    var tempBeam = new CadBeamData();
                    tempBeam.TextContent = candidate.TextContent;
                    SetupBeamCenterline(tempBeam, candidate.MainLine, candidate.SubLine);
                    ExtractMark(tempBeam);

                    rawSegments.Add(new CadBeamSegment
                    {
                        StartX = tempBeam.StartX,
                        StartY = tempBeam.StartY,
                        EndX = tempBeam.EndX,
                        EndY = tempBeam.EndY,
                        Width = candidate.TextWidth,
                        Height = candidate.TextHeight,
                        MeasuredWidth = candidate.MeasuredWidth,
                        Mark = tempBeam.Mark,
                        TextContent = candidate.TextContent,
                        IsPaired = true,
                        SourceLineIds = new List<string> { candidate.MainLine.Id, candidate.SubLine.Id },
                        Confidence = candidate.Confidence,
                        Layer = candidate.MainLine.Layer,
                        DetectionMethod = BeamDetectionMethod.PairedLines
                    });

                    usedIds.Add(candidate.MainLine.Id);
                    usedIds.Add(candidate.SubLine.Id);
                }

                // ── Bước 5 (Fallback): Xử lý nét Anchor chưa có partner ──
                // Nếu không có beam layer → bỏ qua (tránh false positive)
                if (activeBeamLayers.Count > 0)
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
                            var tempBeam = new CadBeamData { TextContent = bestText.Content };
                            ExtractMark(tempBeam);

                            rawSegments.Add(new CadBeamSegment
                            {
                                StartX = anchor.StartPoint[0],
                                StartY = anchor.StartPoint[1],
                                EndX = anchor.EndPoint[0],
                                EndY = anchor.EndPoint[1],
                                Width = bestText.Width,
                                Height = bestText.Height,
                                MeasuredWidth = 0,
                                Mark = tempBeam.Mark,
                                TextContent = bestText.Content,
                                IsPaired = false,
                                SourceLineIds = new List<string> { anchor.Id },
                                Confidence = 100,
                                Layer = anchor.Layer,
                                DetectionMethod = BeamDetectionMethod.SingleLineFallback
                            });

                            usedIds.Add(anchor.Id);
                        }
                    }
                }

                sset.Delete();

                // ── Bước 6: BeamCadPipeline Integration ──
                var dimTextDTOs = new List<CadDimensionText>();
                int skippedTextCount = 0;

                foreach (dynamic txt in allTexts)
                {
                    try
                    {
                        object rawPoint = txt.InsertionPoint;
                        double posX = 0, posY = 0;
                        bool hasValidPoint = false;

                        if (rawPoint is Array arr && arr.Length >= 2)
                        {
                            posX = Convert.ToDouble(arr.GetValue(0));
                            posY = Convert.ToDouble(arr.GetValue(1));
                            hasValidPoint = true;
                        }

                        if (!hasValidPoint)
                        {
                            skippedTextCount++;
                            System.Diagnostics.Debug.WriteLine("[DrawBeams Diagnostics] Skipped text entity: InsertionPoint is null or invalid.");
                            continue;
                        }

                        string content = GetCleanText(txt);
                        if (string.IsNullOrWhiteSpace(content))
                        {
                            skippedTextCount++;
                            System.Diagnostics.Debug.WriteLine("[DrawBeams Diagnostics] Skipped text entity: Content is empty.");
                            continue;
                        }

                        var tempBeam = new CadBeamData { TextContent = content };
                        ParseDimensionsV12(tempBeam);

                        double rot = 0;
                        try
                        {
                            rot = Convert.ToDouble(txt.Rotation);
                        }
                        catch (Exception rotEx)
                        {
                            rot = 0;
                            System.Diagnostics.Debug.WriteLine($"[DrawBeams Diagnostics] Optional Rotation read failed for text '{content}': {rotEx.Message}. Defaulted to 0.");
                        }

                        dimTextDTOs.Add(new CadDimensionText
                        {
                            X = posX,
                            Y = posY,
                            Rotation = rot,
                            Width = tempBeam.Width,
                            Height = tempBeam.Height,
                            Content = content,
                            Confidence = 500
                        });
                    }
                    catch (Exception ex)
                    {
                        skippedTextCount++;
                        System.Diagnostics.Debug.WriteLine($"[DrawBeams Diagnostics] Failed to process COM text entity: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[DrawBeams Diagnostics] Text extraction summary: Total COM texts={allTexts.Count}, Valid DTOs={dimTextDTOs.Count}, Skipped={skippedTextCount}");

                if (allTexts.Count > 0 && dimTextDTOs.Count == 0)
                {
                    BeamDiagnosticCollector.Instance.RecordWarning($"{allTexts.Count} COM text entities were scanned but 0 valid dimension DTOs were produced.");
                    System.Diagnostics.Debug.WriteLine($"[DrawBeams Diagnostics] Warning: {allTexts.Count} COM text entities were scanned but 0 valid dimension DTOs were produced.");
                }

                // Record Raw Candidates in Diagnostic Collector
                diagSession.PipelineSummary.RawCandidatesCount = rawSegments.Count;
                int candIdx = 0;
                var lineUsage = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var seg in rawSegments)
                {
                    string candId = $"RAW_{++candIdx:D3}";
                    BeamDiagnosticCollector.Instance.Record(new BeamDiagnosticEntry
                    {
                        CandidateId = candId,
                        Stage = BeamDiagnosticStage.RawBeamCandidate,
                        Action = BeamDiagnosticAction.Kept,
                        Reason = $"Extracted via {seg.DetectionMethod}",
                        DetectionMethod = seg.DetectionMethod,
                        Confidence = seg.Confidence,
                        StartX = seg.StartX,
                        StartY = seg.StartY,
                        EndX = seg.EndX,
                        EndY = seg.EndY,
                        Length = seg.Length,
                        AngleDegrees = seg.Angle * 180.0 / Math.PI,
                        Width = seg.Width,
                        Height = seg.Height,
                        MeasuredWidth = seg.MeasuredWidth,
                        Mark = seg.Mark,
                        TextContent = seg.TextContent,
                        HasDimensionText = seg.HasDimensionText,
                        SourceLayer = seg.Layer,
                        SourceLineIds = seg.SourceLineIds != null ? new List<string>(seg.SourceLineIds) : new List<string>(),
                        IsPaired = seg.IsPaired
                    });

                    if (seg.SourceLineIds != null)
                    {
                        foreach (var lid in seg.SourceLineIds)
                        {
                            if (string.IsNullOrEmpty(lid)) continue;
                            if (!lineUsage.TryGetValue(lid, out var list))
                            {
                                list = new List<string>();
                                lineUsage[lid] = list;
                            }
                            list.Add(candId);
                        }
                    }
                }

                foreach (var kvp in lineUsage.Where(k => k.Value.Count > 1))
                {
                    BeamDiagnosticCollector.Instance.RecordWarning($"CAD Line ID '{kvp.Key}' was used by multiple raw candidates: {string.Join(", ", kvp.Value)}");
                }

                try
                {
                    var pipeline = new BeamCadPipeline();
                    var pipelineBeams = pipeline.ProcessPipeline(rawSegments, dimTextDTOs);

                    beams.Clear();
                    beams.AddRange(pipelineBeams);

                    AssignMarksToBeams(beams, allTexts);

                    var finalBeams = beams.Where(b => b.IsValid).ToList();
                    foreach (var beam in finalBeams)
                        NormalizeBeamGeometry(beam);

                    return finalBeams;
                }
                finally
                {
                    BeamDiagnosticCollector.Instance.CompleteSession();
                }
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

        private bool IsPointInRotatedRect(
            double px, double py,
            double[] rectCenter, double ux, double uy,
            double halfAlong, double halfAcross)
        {
            double dx = px - rectCenter[0];
            double dy = py - rectCenter[1];

            // Project onto the length axis (ux, uy)
            double projAlong = dx * ux + dy * uy;
            if (Math.Abs(projAlong) > halfAlong) return false;

            // Project onto the width axis (-uy, ux)
            double nx = -uy;
            double ny = ux;
            double projAcross = dx * nx + dy * ny;
            if (Math.Abs(projAcross) > halfAcross) return false;

            return true;
        }

        private List<TextInfo> FindTextsInBeamBoundingBox(
            CadLineSegment mainLine, CadLineSegment subLine, 
            List<dynamic> allTexts,
            double extendAlong = 500.0, double extendAcross = 200.0)
        {
            var result = new List<TextInfo>();

            // 1. Tính unit vector hướng dầm (theo mainLine)
            double dx = mainLine.DirectionX;
            double dy = mainLine.DirectionY;
            double len = mainLine.Length;
            if (len < 1) return result;
            double ux = dx / len;
            double uy = dy / len;

            // 2. Tính trung điểm của cả khối dầm (midpoint của main và sub)
            double midX = (mainLine.MidX + subLine.MidX) / 2.0;
            double midY = (mainLine.MidY + subLine.MidY) / 2.0;
            double[] center = new double[] { midX, midY };

            // 3. Tính kích thước bounding box
            // Chiều dài = max(mainLen, subLen) / 2 + extendAlong
            double halfAlong = Math.Max(mainLine.Length, subLine.Length) / 2.0 + extendAlong;
            
            // Chiều rộng = khoảng cách giữa 2 nét / 2 + extendAcross
            double measuredWidth = GetPerpendicularDistance(mainLine.StartPoint, mainLine.EndPoint, subLine.StartPoint);
            double halfAcross = (measuredWidth / 2.0) + extendAcross;

            // 4. Lọc text
            foreach (var txt in allTexts)
            {
                double[] p = txt.InsertionPoint;
                if (IsPointInRotatedRect(p[0], p[1], center, ux, uy, halfAlong, halfAcross))
                {
                    string content = GetCleanText(txt);
                    if (string.IsNullOrWhiteSpace(content)) continue;

                    var info = new TextInfo { Content = content };
                    
                    // Thử parse kích thước
                    var tempBeam = new CadBeamData { TextContent = content };
                    ParseDimensionsV12(tempBeam);
                    info.Width = tempBeam.Width;
                    info.Height = tempBeam.Height;
                    
                    // Tính khoảng cách tới tâm để sort
                    info.Distance = Math.Sqrt(Math.Pow(p[0] - midX, 2) + Math.Pow(p[1] - midY, 2));
                    
                    result.Add(info);
                }
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
            if (widthScore < 0.0) return double.NegativeInfinity; // Cho phép sai số tối đa 100% (ví dụ: vẽ nét 200, text 400)

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

            // V14: Bỏ Layer+Color khỏi group key → nét đồng tuyến trên các layer khác nhau
            // vẫn được merge thành 1 anchor, giữ nguyên Layer của segment đầu trong cluster.
            var groups = normalSegments.GroupBy(s =>
            {
                double angleKey = Math.Round(s.Angle / 0.01);
                double normalX = -Math.Sin(s.Angle);
                double normalY = Math.Cos(s.Angle);
                double distanceKey = Math.Round(((s.StartPoint[0] * normalX) + (s.StartPoint[1] * normalY)) / 20.0);
                return $"{angleKey}|{distanceKey}";
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
            // Pass 1: Gán Mark (và Dimension nếu chưa có) cho beam từ text nằm trong vùng bounding box
            foreach (var beam in beams)
            {
                double midX = (beam.StartX + beam.EndX) / 2.0;
                double midY = (beam.StartY + beam.EndY) / 2.0;
                
                double dx = beam.EndX - beam.StartX;
                double dy = beam.EndY - beam.StartY;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1) continue;
                
                double ux = dx / len;
                double uy = dy / len;
                
                // Mở rộng bounding box
                double halfAlong = len / 2.0 + 500.0;
                double halfAcross = (beam.Width > 0 ? beam.Width : 400.0) / 2.0 + 200.0;
                double[] center = new double[] { midX, midY };

                var nearbyTexts = new List<Tuple<double, string>>();

                foreach (var txt in allTexts)
                {
                    double[] p = txt.InsertionPoint;
                    
                    // Kiểm tra điểm có nằm trong Bounding Box của dầm không
                    if (IsPointInRotatedRect(p[0], p[1], center, ux, uy, halfAlong, halfAcross))
                    {
                        string content = GetCleanText(txt);
                        if (string.IsNullOrWhiteSpace(content)) continue;

                        double dist = Math.Sqrt(Math.Pow(p[0] - midX, 2) + Math.Pow(p[1] - midY, 2));
                        nearbyTexts.Add(new Tuple<double, string>(dist, content));
                    }
                }

                // Sắp xếp text theo khoảng cách tăng dần
                nearbyTexts = nearbyTexts.OrderBy(t => t.Item1).ToList();

                // KHÔNG ghi đè dimension nếu dầm đã có Width, Height hợp lệ
                bool foundDim = (beam.Width > 0 && beam.Height > 0);
                bool foundMark = !string.IsNullOrEmpty(beam.Mark);

                foreach (var txtTuple in nearbyTexts)
                {
                    if (foundDim && foundMark) break;

                    string content = txtTuple.Item2;
                    var dimensionMatch = Regex.Match(content, @"(\d+[\.,]?\d*)\s*[xX\*\-\/]\s*(\d+[\.,]?\d*)");

                    if (dimensionMatch.Success)
                    {
                        if (!foundDim)
                        {
                            beam.TextContent = content;
                            ParseDimensionsV12(beam);
                            
                            // Nếu text có chứa cả mark (ví dụ: D1 220x400)
                            if (!foundMark)
                            {
                                ExtractMark(beam);
                                if (!string.IsNullOrEmpty(beam.Mark))
                                    foundMark = true;
                            }
                            foundDim = true;
                        }
                    }
                    else
                    {
                        if (!foundMark)
                        {
                            // Text không chứa số đo -> đây là Mark riêng biệt (vd: D1, B3)
                            beam.Mark = content;
                            foundMark = true;
                        }
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
