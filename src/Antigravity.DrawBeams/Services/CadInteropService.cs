using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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
        // PHASE 1: CAD SCENE EXTRACTION & ISOLATION (CLOSED COM BOUNDARY)
        // ================================================================

        /// <summary>
        /// Public API to capture CAD scene from AutoCAD selection set.
        /// Selection set cleanup is guaranteed in finally block.
        /// Pure C# DTO return without COM references.
        /// </summary>
        public CadScene CaptureCadScene(string textLayer = null)
        {
            if (_acadDoc == null && !Connect())
            {
                throw new InvalidOperationException("AutoCAD connection unavailable.");
            }

            dynamic ssets = _acadDoc.SelectionSets;
            string ssetName = "BeamsSet_" + DateTime.Now.Ticks;
            dynamic sset = null;

            try
            {
                try { sset = ssets.Add(ssetName); }
                catch { sset = ssets.Item(ssetName); }

                _acadDoc.Utility.Prompt("\nV12: Quét chọn vùng dầm cần vẽ... ");
                sset.SelectOnScreen();

                return ExtractSceneFromSelectionSet(sset, textLayer);
            }
            finally
            {
                if (sset != null)
                {
                    try { sset.Delete(); }
                    catch { /* Safe selection set cleanup */ }
                }
            }
        }

        /// <summary>
        /// Private COM interop function extracting entities into CadScene DTO.
        /// </summary>
        private CadScene ExtractSceneFromSelectionSet(dynamic sset, string textLayer)
        {
            var scene = new CadScene();
            if (sset == null) return scene;

            for (int i = 0; i < sset.Count; i++)
            {
                dynamic entity = sset.Item(i);
                string objName = entity.ObjectName;
                string entLayer = entity.Layer;

                if (objName == "AcDbLine")
                {
                    double[] startPt = entity.StartPoint;
                    double[] endPt = entity.EndPoint;

                    scene.Segments.Add(new CadSegment
                    {
                        StartX = startPt[0],
                        StartY = startPt[1],
                        EndX = endPt[0],
                        EndY = endPt[1],
                        Id = entity.Handle,
                        Layer = entLayer,
                        Color = GetEntityColor(entity)
                    });
                }
                else if (objName == "AcDbPolyline" || objName == "AcDb2dPolyline")
                {
                    var segments = ExtractSegmentsFromPolyline(entity);
                    scene.Segments.AddRange(segments);
                }
                else if (objName == "AcDbHatch")
                {
                    var segments = ExtractSegmentsFromHatch(entity);
                    scene.Segments.AddRange(segments);
                }
                else if (objName == "AcDbText" || objName == "AcDbMText")
                {
                    if (string.IsNullOrEmpty(textLayer) || string.Equals(entLayer, textLayer, StringComparison.OrdinalIgnoreCase))
                    {
                        double rot = 0;
                        try { rot = (double)entity.Rotation; } catch { }

                        double textHeight = 0;
                        try { textHeight = (double)entity.Height; }
                        catch
                        {
                            try { textHeight = (double)entity.TextHeight; }
                            catch { textHeight = 0; }
                        }

                        double[] insPt = entity.InsertionPoint;

                        scene.Texts.Add(new CadText
                        {
                            Id = entity.Handle,
                            TextString = entity.TextString,
                            X = insPt[0],
                            Y = insPt[1],
                            Rotation = rot,
                            TextHeight = textHeight,
                            Layer = entLayer,
                            ObjectName = objName
                        });
                    }
                }
            }

            return scene;
        }

        /// <summary>
        /// Entry point for CAD beam processing. Captures scene and processes it.
        /// </summary>
        public List<CadBeamData> GetCadBeams(string beamLayer = null, string textLayer = null)
        {
            try
            {
                CadScene scene = CaptureCadScene(textLayer);
                return ProcessScene(scene, beamLayer, textLayer);
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi V12: " + ex.Message);
            }
        }

        // ================================================================
        // V12: CẤU TRÚC DỮ LIỆU LINH HOẠT & THUẬT TOÁN BEAM PROCESSING
        // ================================================================

        private class LegacyBeamCandidate
        {
            public CadSegment MainLine { get; set; }
            public CadSegment SubLine { get; set; }
            public double MeasuredWidth { get; set; }
            public double TextWidth { get; set; }
            public double TextHeight { get; set; }
            public string TextContent { get; set; }
            public double OverlapLength { get; set; }
            public double Confidence { get; set; }
        }

        /// <summary>
        /// Pure scene processing API operating strictly on CadScene DTOs.
        /// </summary>
        public List<CadBeamData> ProcessScene(CadScene scene, string beamLayer = null, string textLayer = null)
        {
            List<CadBeamData> beams = new List<CadBeamData>();
            if (scene == null || scene.Segments == null) return beams;

            List<CadSegment> allSegments = scene.Segments;
            List<CadText> allTexts = scene.Texts ?? new List<CadText>();

            if (!string.IsNullOrEmpty(textLayer))
            {
                allTexts = allTexts
                    .Where(t => string.Equals(t.Layer, textLayer, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            allSegments = PreProcessSegments(allSegments);

            List<CadSegment> anchorLines;
            List<CadSegment> potentialPartners;

            if (!string.IsNullOrEmpty(beamLayer))
            {
                anchorLines = allSegments.Where(s => string.Equals(s.Layer, beamLayer, StringComparison.OrdinalIgnoreCase)).ToList();
                if (anchorLines.Count == 0)
                    anchorLines = allSegments.Where(s => s.Layer == "0").ToList();
                potentialPartners = allSegments;
            }
            else
            {
                anchorLines = allSegments;
                potentialPartners = allSegments;
            }

            HashSet<string> usedIds = new HashSet<string>();
            var confirmedCandidates = new List<LegacyBeamCandidate>();
            var commonWidths = GetCommonBeamWidths(allTexts);

            foreach (var anchor in anchorLines)
            {
                if (usedIds.Contains(anchor.Id)) continue;
                if (anchor.Length < 500) continue;

                if (anchor.PolylineWidth >= 100 && anchor.PolylineWidth < 3000)
                {
                    var plTexts = FindParallelTexts(anchor, allTexts);
                    double beamB = anchor.PolylineWidth;
                    double beamH = 500;
                    string beamContent = "";
                    if (plTexts.Count > 0)
                    {
                        beamB = plTexts[0].Width;
                        beamH = plTexts[0].Height;
                        beamContent = plTexts[0].Content;
                    }
                    var plBeam = new CadBeamData
                    {
                        StartX = anchor.StartX,
                        StartY = anchor.StartY,
                        EndX = anchor.EndX,
                        EndY = anchor.EndY,
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

                if (anchor.GroupId != null)
                {
                    CadSegment groupPartner = potentialPartners
                        .FirstOrDefault(p => p.GroupId == anchor.GroupId && p.Id != anchor.Id && !usedIds.Contains(p.Id));
                    if (groupPartner != null)
                    {
                        double overlapLen = GetSegmentOverlapLength(anchor, groupPartner);
                        double measuredW = GetPerpendicularDistance(anchor.StartX, anchor.StartY, anchor.EndX, anchor.EndY, groupPartner.StartX, groupPartner.StartY);
                        if (overlapLen > 200 && measuredW > 50)
                        {
                            var plTexts2 = FindParallelTexts(anchor, allTexts);
                            if (plTexts2.Count > 0)
                            {
                                double gB = plTexts2[0].Width, gH = plTexts2[0].Height;
                                string gContent = plTexts2[0].Content;
                                confirmedCandidates.Add(new LegacyBeamCandidate
                                {
                                    MainLine = anchor,
                                    SubLine = groupPartner,
                                    MeasuredWidth = measuredW,
                                    TextWidth = gB,
                                    TextHeight = gH,
                                    TextContent = gContent,
                                    OverlapLength = overlapLen,
                                    Confidence = overlapLen + 800
                                });
                            }
                        }
                    }
                    continue;
                }

                var nearbyTexts = FindParallelTexts(anchor, allTexts);
                if (nearbyTexts.Count == 0)
                {
                    if (commonWidths.Count > 0)
                    {
                        CadSegment partner = FindPartnerWithoutText(anchor, potentialPartners, usedIds, commonWidths);
                        if (partner != null)
                        {
                            double overlapLen = GetSegmentOverlapLength(anchor, partner);
                            double measuredWidth = GetPerpendicularDistance(anchor.StartX, anchor.StartY, anchor.EndX, anchor.EndY, partner.StartX, partner.StartY);
                            double matchedWidth = commonWidths
                                .Where(w => Math.Abs(w - measuredWidth) / w < 0.15)
                                .OrderBy(w => Math.Abs(w - measuredWidth))
                                .FirstOrDefault();
                            if (matchedWidth > 0 && overlapLen > 200 && IsProjectionWithinRange(anchor, partner))
                            {
                                confirmedCandidates.Add(new LegacyBeamCandidate
                                {
                                    MainLine = anchor,
                                    SubLine = partner,
                                    MeasuredWidth = measuredWidth,
                                    TextWidth = matchedWidth,
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

                foreach (var textInfo in nearbyTexts)
                {
                    double expectedWidth = textInfo.Width;
                    CadSegment partner = FindParallelPartner(anchor, potentialPartners, expectedWidth, usedIds);

                    if (partner != null)
                    {
                        double overlapLen = GetSegmentOverlapLength(anchor, partner);
                        if (overlapLen > 200 && IsProjectionWithinRange(anchor, partner))
                        {
                            double measuredWidth = GetPerpendicularDistance(anchor.StartX, anchor.StartY, anchor.EndX, anchor.EndY, partner.StartX, partner.StartY);
                            if (measuredWidth > 1200 || anchor.Length < measuredWidth * 1.2) continue;

                            confirmedCandidates.Add(new LegacyBeamCandidate
                            {
                                MainLine = anchor,
                                SubLine = partner,
                                MeasuredWidth = measuredWidth,
                                TextWidth = textInfo.Width,
                                TextHeight = textInfo.Height,
                                TextContent = textInfo.Content,
                                OverlapLength = overlapLen,
                                Confidence = overlapLen + (string.Equals(anchor.Layer, partner.Layer, StringComparison.OrdinalIgnoreCase) ? 500 : 200)
                            });
                        }
                    }
                }
            }

            foreach (var candidate in confirmedCandidates.OrderByDescending(c => c.Confidence))
            {
                if (usedIds.Contains(candidate.MainLine.Id) || usedIds.Contains(candidate.SubLine.Id)) continue;

                var beam = new CadBeamData();
                beam.TextContent = candidate.TextContent;
                beam.Width = candidate.TextWidth;
                beam.Height = candidate.TextHeight;
                beam.IsPaired = true;

                SetupBeamCenterline(beam, candidate.MainLine, candidate.SubLine);
                beam.MeasuredWidth = candidate.MeasuredWidth;

                ExtractMark(beam);

                if (beam.IsValid)
                {
                    beams.Add(beam);
                    usedIds.Add(candidate.MainLine.Id);
                    usedIds.Add(candidate.SubLine.Id);
                }
            }

            if (!string.IsNullOrEmpty(beamLayer))
            {
                foreach (var anchor in anchorLines)
                {
                    if (usedIds.Contains(anchor.Id)) continue;
                    if (anchor.Length < 1000) continue;

                    var nearbyTexts = FindParallelTexts(anchor, allTexts);
                    if (nearbyTexts.Count > 0)
                    {
                        var bestText = nearbyTexts.First();
                        var beam = new CadBeamData();
                        beam.TextContent = bestText.Content;
                        beam.Width = bestText.Width;
                        beam.Height = bestText.Height;
                        beam.StartX = anchor.StartX;
                        beam.StartY = anchor.StartY;
                        beam.EndX = anchor.EndX;
                        beam.EndY = anchor.EndY;
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

            var mergedBeams = MergeCollinearBeams(beams);
            AssignMarksToBeams(mergedBeams, allTexts);
            foreach (var beam in mergedBeams)
                NormalizeBeamGeometry(beam);
            return mergedBeams;
        }

        // ================================================================
        // V12: CÁC HÀM CON - Thuật toán hình học & Text matching
        // ================================================================

        private class TextInfo
        {
            public double Width { get; set; }
            public double Height { get; set; }
            public string Content { get; set; }
            public double Distance { get; set; }
        }

        private List<TextInfo> FindParallelTexts(CadSegment anchor, List<CadText> allTexts)
        {
            var result = new List<TextInfo>();
            double searchRadius = 5000.0;
            double anchorAngle = anchor.Angle;

            foreach (var txt in allTexts)
            {
                string content = txt.CleanText;

                if (!Regex.IsMatch(content, @"(\d+[\.,]?\d*)\s*[xX\*\-\/]\s*(\d+[\.,]?\d*)", RegexOptions.IgnoreCase))
                    continue;

                var tempBeam = new CadBeamData { TextContent = content };
                ParseDimensionsV12(tempBeam);
                if (tempBeam.Width <= 0 || tempBeam.Height <= 0) continue;

                double distToLine = GetPerpendicularDistance(anchor.StartX, anchor.StartY, anchor.EndX, anchor.EndY, txt.X, txt.Y);
                double dynamicRadius = Math.Max(searchRadius, tempBeam.Width * 3.0);
                if (distToLine > dynamicRadius) continue;

                double textRotation = txt.Rotation;
                while (textRotation < 0) textRotation += Math.PI;
                while (textRotation >= Math.PI) textRotation -= Math.PI;

                double angleDiff = Math.Abs(anchorAngle - textRotation);
                if (angleDiff > Math.PI / 2.0) angleDiff = Math.PI - angleDiff;
                if (angleDiff >= Math.PI / 9.0) continue;

                double dx = anchor.DirectionX, dy = anchor.DirectionY;
                double len = anchor.Length;
                double ux = dx / len, uy = dy / len;
                double proj = ((txt.X - anchor.StartX) * ux + (txt.Y - anchor.StartY) * uy) / len;
                if (proj < -0.5 || proj > 1.5) continue;

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

        private List<double> GetCommonBeamWidths(List<CadText> allTexts)
        {
            var widths = new List<double>();
            foreach (var txt in allTexts)
            {
                string content = txt.CleanText;
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

        private double CalculatePartnerScore(CadSegment anchor, CadSegment candidate, double expectedWidth)
        {
            double anchorLen = anchor.Length;
            double candidateLen = candidate.Length;
            if (anchorLen < 10 || candidateLen < 200 || expectedWidth <= 0) return double.NegativeInfinity;

            double dot = Math.Abs((anchor.DirectionX * candidate.DirectionX + anchor.DirectionY * candidate.DirectionY) / (anchorLen * candidateLen));
            if (dot < 0.999) return double.NegativeInfinity;

            double measuredWidth = GetPerpendicularDistance(anchor.StartX, anchor.StartY, anchor.EndX, anchor.EndY, candidate.StartX, candidate.StartY);
            if (measuredWidth < 50 || measuredWidth > 3000) return double.NegativeInfinity;

            double widthScore = 1.0 - (Math.Abs(measuredWidth - expectedWidth) / expectedWidth);
            if (widthScore < 0.7) return double.NegativeInfinity;

            double overlap = GetSegmentOverlapLength(anchor, candidate);
            if (overlap < 200) return double.NegativeInfinity;

            double overlapScore = Math.Min(1.0, overlap / Math.Max(anchorLen, candidateLen));
            double layerScore = string.Equals(anchor.Layer, candidate.Layer, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
            double colorScore = anchor.Color >= 0 && anchor.Color == candidate.Color ? 0.5 : 0.0;

            return widthScore * 40.0 + overlapScore * 30.0 + dot * 20.0 + layerScore * 10.0 + colorScore * 5.0;
        }

        private CadSegment FindParallelPartner(CadSegment anchor, List<CadSegment> allLines, double widthMm, HashSet<string> usedIds)
        {
            CadSegment bestPartner = null;
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

                double dist = GetPerpendicularDistance(anchor.StartX, anchor.StartY, anchor.EndX, anchor.EndY, line.StartX, line.StartY);
                if (dist > 2000.0) continue;

                double overlap = GetSegmentOverlapLength(anchor, line);
                if (overlap < 200) continue;

                if (!IsProjectionWithinRange(anchor, line)) continue;

                double score = CalculatePartnerScore(anchor, line, widthMm);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPartner = line;
                }
            }

            return bestPartner;
        }

        private CadSegment FindPartnerWithoutText(CadSegment anchor, List<CadSegment> allLines, HashSet<string> usedIds, List<double> expectedWidths)
        {
            CadSegment bestPartner = null;
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

                double dist = GetPerpendicularDistance(anchor.StartX, anchor.StartY, anchor.EndX, anchor.EndY, line.StartX, line.StartY);
                if (dist < 50.0 || dist > 3000.0) continue;

                double overlap = GetSegmentOverlapLength(anchor, line);
                if (overlap < 200) continue;
                if (!IsProjectionWithinRange(anchor, line)) continue;

                double score = expectedWidths
                    .Select(width => CalculatePartnerScore(anchor, line, width))
                    .DefaultIfEmpty(double.NegativeInfinity)
                    .Max();

                if (score <= bestScore) continue;

                if (score > 40)
                {
                    bestScore = score;
                    bestPartner = line;
                }
            }

            return bestPartner;
        }

        private bool IsProjectionWithinRange(CadSegment anchor, CadSegment partner)
        {
            double dx = anchor.DirectionX, dy = anchor.DirectionY;
            double len = anchor.Length;
            if (len < 10) return false;
            double ux = dx / len, uy = dy / len;

            double proj = ((partner.MidX - anchor.StartX) * ux + (partner.MidY - anchor.StartY) * uy) / len;
            return proj > -0.3 && proj < 1.3;
        }

        private void SetupBeamCenterline(CadBeamData beam, CadSegment main, CadSegment sub)
        {
            double s1x = main.StartX, s1y = main.StartY, e1x = main.EndX, e1y = main.EndY;
            double s2x = sub.StartX, s2y = sub.StartY, e2x = sub.EndX, e2y = sub.EndY;

            double d1 = Math.Sqrt(Math.Pow(s1x - s2x, 2) + Math.Pow(s1y - s2y, 2));
            double d2 = Math.Sqrt(Math.Pow(s1x - e2x, 2) + Math.Pow(s1y - e2y, 2));

            if (d1 < d2)
            {
                beam.StartX = (s1x + s2x) / 2.0;
                beam.StartY = (s1y + s2y) / 2.0;
                beam.EndX = (e1x + e2x) / 2.0;
                beam.EndY = (e1y + e2y) / 2.0;
            }
            else
            {
                beam.StartX = (s1x + e2x) / 2.0;
                beam.StartY = (s1y + e2y) / 2.0;
                beam.EndX = (e1x + s2x) / 2.0;
                beam.EndY = (e1y + s2y) / 2.0;
            }
        }

        private void ParseDimensionsV12(CadBeamData beam)
        {
            if (string.IsNullOrEmpty(beam.TextContent)) return;
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
        // HÀM TIỆN ÍCH - Geometry & COM Helper
        // ================================================================

        private List<CadSegment> ExtractSegmentsFromPolyline(dynamic pline)
        {
            List<CadSegment> result = new List<CadSegment>();
            try
            {
                int count = pline.NumberOfVertices;
                bool isClosed = pline.Closed;
                string parentHandle = pline.Handle;
                string layer = pline.Layer;

                double polyWidth = 0;
                try { polyWidth = (double)pline.GlobalWidth; } catch { }
                if (polyWidth <= 0)
                    try { polyWidth = (double)pline.ConstantWidth; } catch { }

                for (int i = 0; i < (isClosed ? count : count - 1); i++)
                {
                    double[] p1 = pline.Coordinate[i];
                    double[] p2 = pline.Coordinate[(i + 1) % count];

                    double segmentWidth = polyWidth;
                    if (segmentWidth <= 0)
                    {
                        try { segmentWidth = (double)pline.GetWidthInfoAt(i, out double startW, out double endW); segmentWidth = startW; } catch { }
                        if (segmentWidth <= 0) try { segmentWidth = (double)pline.GetStartWidthAt(i); } catch { }
                    }

                    double sX = p1[0], sY = p1[1];
                    double eX = p2[0], eY = p2[1];

                    double len = Math.Sqrt(Math.Pow(sX - eX, 2) + Math.Pow(sY - eY, 2));
                    if (len > 50)
                    {
                        result.Add(new CadSegment
                        {
                            StartX = sX,
                            StartY = sY,
                            EndX = eX,
                            EndY = eY,
                            Id = $"{parentHandle}_{i}",
                            Layer = layer,
                            Color = GetEntityColor(pline),
                            PolylineWidth = segmentWidth
                        });
                    }
                }

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

        private List<CadSegment> ExtractSegmentsFromHatch(dynamic hatch)
        {
            List<CadSegment> result = new List<CadSegment>();
            try
            {
                string parentHandle = hatch.Handle;
                string layer = hatch.Layer;
                int color = GetEntityColor(hatch);
                int loopCount = 0;
                try { loopCount = hatch.NumberOfLoops; } catch { }

                bool loopExtracted = false;
                if (loopCount > 0)
                {
                    for (int i = 0; i < loopCount; i++)
                    {
                        try
                        {
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
                                    double[] sPt = ent.StartPoint;
                                    double[] ePt = ent.EndPoint;

                                    result.Add(new CadSegment
                                    {
                                        StartX = sPt[0],
                                        StartY = sPt[1],
                                        EndX = ePt[0],
                                        EndY = ePt[1],
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

                if (!loopExtracted)
                {
                    object minPt, maxPt;
                    hatch.GetBoundingBox(out minPt, out maxPt);
                    double[] min = (double[])minPt;
                    double[] max = (double[])maxPt;
                    double[][] pts = new double[][] {
                        new double[] { min[0], min[1] },
                        new double[] { max[0], min[1] },
                        new double[] { max[0], max[1] },
                        new double[] { min[0], max[1] }
                    };
                    for (int i = 0; i < 4; i++)
                    {
                        int next = (i + 1) % 4;
                        result.Add(new CadSegment
                        {
                            StartX = pts[i][0],
                            StartY = pts[i][1],
                            EndX = pts[next][0],
                            EndY = pts[next][1],
                            Id = $"{parentHandle}_fb{i}",
                            Layer = layer,
                            Color = color,
                            GroupId = parentHandle + (i % 2 == 0 ? "_pairA" : "_pairB")
                        });
                    }
                }
            }
            catch { }
            return result;
        }

        private double GetPerpendicularDistance(double startX, double startY, double endX, double endY, double pointX, double pointY)
        {
            double dx = endX - startX;
            double dy = endY - startY;
            double L2 = dx * dx + dy * dy;
            if (L2 == 0) return 0;
            return Math.Abs(dy * pointX - dx * pointY + endX * startY - endY * startX) / Math.Sqrt(L2);
        }

        private double GetSegmentOverlapLength(CadSegment s1, CadSegment s2)
        {
            double dx = s1.EndX - s1.StartX, dy = s1.EndY - s1.StartY;
            double L = Math.Sqrt(dx * dx + dy * dy);
            if (L < 1) return 0;
            double ux = dx / L, uy = dy / L;
            double t1 = ((s2.StartX - s1.StartX) * ux + (s2.StartY - s1.StartY) * uy) / L;
            double t2 = ((s2.EndX - s1.StartX) * ux + (s2.EndY - s1.StartY) * uy) / L;
            double start = Math.Max(0, Math.Min(t1, t2)), end = Math.Min(1, Math.Max(t1, t2));
            if (start < end) return (end - start) * L;
            return 0;
        }

        private int GetEntityColor(dynamic entity)
        {
            try { return (int)entity.Color; }
            catch { return -1; }
        }

        private List<CadSegment> PreProcessSegments(List<CadSegment> source)
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
                double distanceKey = Math.Round(((s.StartX * normalX) + (s.StartY * normalY)) / 20.0);
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
                        double t1 = s.StartX * ux + s.StartY * uy;
                        double t2 = s.EndX * ux + s.EndY * uy;
                        return new
                        {
                            Segment = s,
                            Min = Math.Min(t1, t2),
                            Max = Math.Max(t1, t2)
                        };
                    })
                    .OrderBy(i => i.Min)
                    .ToList();

                var cluster = new List<CadSegment> { intervals[0].Segment };
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
                        cluster = new List<CadSegment> { intervals[i].Segment };
                        clusterMin = intervals[i].Min;
                        clusterMax = intervals[i].Max;
                    }
                }

                result.Add(CreateMergedSegment(cluster, clusterMin, clusterMax, ux, uy));
            }

            return result;
        }

        private CadSegment CreateMergedSegment(List<CadSegment> cluster, double minT, double maxT, double ux, double uy)
        {
            var first = cluster[0];
            double nx = -uy;
            double ny = ux;
            double offset = cluster.Average(s => s.StartX * nx + s.StartY * ny);

            return new CadSegment
            {
                StartX = minT * ux + offset * nx,
                StartY = minT * uy + offset * ny,
                EndX = maxT * ux + offset * nx,
                EndY = maxT * uy + offset * ny,
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

            var finalBeams = new List<CadBeamData>();
            var sorted = merged.OrderBy(b => b.Width).ToList();
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

                    double midX1 = (b1.StartX + b1.EndX) / 2, midY1 = (b1.StartY + b1.EndY) / 2;
                    double midX2 = (b2.StartX + b2.EndX) / 2, midY2 = (b2.StartY + b2.EndY) / 2;
                    double dist = Math.Sqrt(Math.Pow(midX1 - midX2, 2) + Math.Pow(midY1 - midY2, 2));

                    if (dist < 300)
                    {
                        finalUsed.Add(j);
                    }
                }
            }

            return finalBeams;
        }

        private void AssignMarksToBeams(List<CadBeamData> beams, List<CadText> allTexts)
        {
            foreach (var beam in beams)
            {
                if (!string.IsNullOrEmpty(beam.Mark)) continue;

                double midX = (beam.StartX + beam.EndX) / 2.0;
                double midY = (beam.StartY + beam.EndY) / 2.0;
                double beamAngle = Math.Atan2(beam.EndY - beam.StartY, beam.EndX - beam.StartX);
                while (beamAngle < 0) beamAngle += Math.PI;
                while (beamAngle >= Math.PI) beamAngle -= Math.PI;

                CadText closestMarkText = null;
                double minDist = 5000;

                foreach (var txt in allTexts)
                {
                    string content = txt.CleanText;
                    if (string.IsNullOrWhiteSpace(content)) continue;

                    double dist = Math.Sqrt(Math.Pow(txt.X - midX, 2) + Math.Pow(txt.Y - midY, 2));
                    if (dist < minDist)
                    {
                        double txtRot = txt.Rotation;
                        double angleDiff = Math.Abs(beamAngle - txtRot);
                        while (angleDiff > Math.PI / 2) angleDiff = Math.PI - angleDiff;
                        if (angleDiff > 0.5) continue;

                        minDist = dist;
                        closestMarkText = txt;
                    }
                }

                if (closestMarkText != null)
                {
                    string content = closestMarkText.CleanText;
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
