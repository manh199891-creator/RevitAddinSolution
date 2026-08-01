using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antigravity.DrawBeams.Services
{
    public class BeamCandidateGenerator
    {
        private readonly BeamGeometryService _geometryService;
        private readonly BeamTextMatcher _textMatcher;

        public BeamCandidateGenerator(
            BeamGeometryService geometryService = null,
            BeamTextMatcher textMatcher = null)
        {
            _geometryService = geometryService ?? new BeamGeometryService();
            _textMatcher = textMatcher ?? new BeamTextMatcher(_geometryService);
        }

        public List<BeamCandidate> GenerateCandidates(
            CadScene scene,
            string beamLayer = null,
            string textLayer = null)
        {
            var result = new List<BeamCandidate>();
            if (scene == null || scene.Segments == null) return result;

            List<CadSegment> allSegments = scene.Segments;
            List<CadText> allTexts = scene.Texts ?? new List<CadText>();

            if (!string.IsNullOrEmpty(textLayer))
            {
                allTexts = allTexts
                    .Where(t => string.Equals(t.Layer, textLayer, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            allSegments = _geometryService.PreProcessSegments(allSegments);

            List<CadSegment> anchorLines;
            List<CadSegment> potentialPartners;

            if (!string.IsNullOrEmpty(beamLayer))
            {
                anchorLines = allSegments
                    .Where(s => string.Equals(s.Layer, beamLayer, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (anchorLines.Count == 0)
                {
                    anchorLines = allSegments.Where(s => s.Layer == "0").ToList();
                }
                potentialPartners = allSegments;
            }
            else
            {
                anchorLines = allSegments;
                potentialPartners = allSegments;
            }

            var commonWidths = GetCommonBeamWidths(allTexts);
            var candidates = new List<BeamCandidate>();

            foreach (var anchor in anchorLines)
            {
                if (anchor == null) continue;
                if (anchor.Length < 500) continue;

                // 1. PolylineWidth Candidate
                if (anchor.PolylineWidth >= 100 && anchor.PolylineWidth < 3000)
                {
                    var plMatches = _textMatcher.FindMatches(anchor, allTexts);
                    var nearestMatch = plMatches.FirstOrDefault();

                    string textId = nearestMatch?.Text?.Id ?? "NONE";
                    string candidateId = $"WIDTH:{anchor.Id}:{textId}";

                    candidates.Add(new BeamCandidate
                    {
                        Id = candidateId,
                        Kind = BeamCandidateKind.PolylineWidth,
                        MainSegment = anchor,
                        PartnerSegment = null,
                        TextMatch = nearestMatch,
                        MeasuredWidth = anchor.PolylineWidth,
                        ParsedWidth = nearestMatch != null ? nearestMatch.ParsedWidth : anchor.PolylineWidth,
                        ParsedHeight = nearestMatch != null ? nearestMatch.ParsedHeight : 500.0,
                        TextContent = nearestMatch != null ? nearestMatch.Content : string.Empty,
                        Mark = nearestMatch != null ? nearestMatch.Mark : string.Empty
                    });

                    continue;
                }

                // 2. ClosedPolylinePair Candidate
                if (anchor.GroupId != null)
                {
                    var groupPartners = potentialPartners
                        .Where(p => p.GroupId == anchor.GroupId && p.Id != anchor.Id)
                        .ToList();

                    foreach (var groupPartner in groupPartners)
                    {
                        double overlapLen = _geometryService.GetOverlapLength(anchor, groupPartner);
                        double measuredW = _geometryService.GetPerpendicularDistance(anchor, groupPartner);

                        if (overlapLen > 200 && measuredW > 50 && _geometryService.AreParallel(anchor, groupPartner, 0.95))
                        {
                            var plMatches2 = _textMatcher.FindMatches(anchor, allTexts);
                            var nearestMatch = plMatches2.FirstOrDefault();

                            string smallerId = string.CompareOrdinal(anchor.Id, groupPartner.Id) <= 0 ? anchor.Id : groupPartner.Id;
                            string largerId = string.CompareOrdinal(anchor.Id, groupPartner.Id) <= 0 ? groupPartner.Id : anchor.Id;
                            string textId = nearestMatch?.Text?.Id ?? "NONE";
                            string candidateId = $"GROUP:{smallerId}:{largerId}:{textId}";

                            candidates.Add(new BeamCandidate
                            {
                                Id = candidateId,
                                Kind = BeamCandidateKind.ClosedPolylinePair,
                                MainSegment = anchor,
                                PartnerSegment = groupPartner,
                                TextMatch = nearestMatch,
                                MeasuredWidth = measuredW,
                                ParsedWidth = nearestMatch != null ? nearestMatch.ParsedWidth : measuredW,
                                ParsedHeight = nearestMatch != null ? nearestMatch.ParsedHeight : 500.0,
                                TextContent = nearestMatch != null ? nearestMatch.Content : string.Empty,
                                Mark = nearestMatch != null ? nearestMatch.Mark : string.Empty,
                                OverlapLength = overlapLen,
                                OverlapRatio = _geometryService.GetOverlapRatio(anchor, groupPartner),
                                AngleDifference = _geometryService.GetAngleDifference(anchor, groupPartner),
                                SourceGroupId = anchor.GroupId
                            });
                        }
                    }

                    continue;
                }

                var textMatches = _textMatcher.FindMatches(anchor, allTexts);

                // 3. SingleLineWithText Candidates
                foreach (var textMatch in textMatches)
                {
                    string candidateId = $"SINGLE:{anchor.Id}:{textMatch.Text.Id}";
                    candidates.Add(new BeamCandidate
                    {
                        Id = candidateId,
                        Kind = BeamCandidateKind.SingleLineWithText,
                        MainSegment = anchor,
                        PartnerSegment = null,
                        TextMatch = textMatch,
                        MeasuredWidth = 0,
                        ParsedWidth = textMatch.ParsedWidth,
                        ParsedHeight = textMatch.ParsedHeight,
                        TextContent = textMatch.Content,
                        Mark = textMatch.Mark,
                        AngleDifference = textMatch.AngleDifference
                    });
                }

                // 4. PairedEdges Candidates
                if (textMatches.Count > 0)
                {
                    foreach (var textMatch in textMatches)
                    {
                        double expectedWidth = textMatch.ParsedWidth;

                        foreach (var partner in potentialPartners)
                        {
                            if (partner.Id == anchor.Id) continue;
                            if (partner.Length < 200) continue;
                            if (!_geometryService.AreParallel(anchor, partner, 0.999)) continue;

                            double measuredWidth = _geometryService.GetPerpendicularDistance(anchor, partner);
                            if (measuredWidth < 50 || measuredWidth > 1200) continue;
                            if (anchor.Length < measuredWidth * 1.2) continue;

                            double widthScore = 1.0 - (Math.Abs(measuredWidth - expectedWidth) / expectedWidth);
                            if (widthScore < 0.7) continue;

                            double overlap = _geometryService.GetOverlapLength(anchor, partner);
                            if (overlap < 200) continue;

                            if (!_geometryService.IsProjectionWithinRange(anchor, partner)) continue;

                            string smallerId = string.CompareOrdinal(anchor.Id, partner.Id) <= 0 ? anchor.Id : partner.Id;
                            string largerId = string.CompareOrdinal(anchor.Id, partner.Id) <= 0 ? partner.Id : anchor.Id;
                            string textId = textMatch.Text.Id;
                            string candidateId = $"PAIR:{smallerId}:{largerId}:{textId}";

                            candidates.Add(new BeamCandidate
                            {
                                Id = candidateId,
                                Kind = BeamCandidateKind.PairedEdges,
                                MainSegment = anchor,
                                PartnerSegment = partner,
                                TextMatch = textMatch,
                                MeasuredWidth = measuredWidth,
                                ParsedWidth = textMatch.ParsedWidth,
                                ParsedHeight = textMatch.ParsedHeight,
                                OverlapLength = overlap,
                                OverlapRatio = _geometryService.GetOverlapRatio(anchor, partner),
                                AngleDifference = _geometryService.GetAngleDifference(anchor, partner),
                                TextContent = textMatch.Content,
                                Mark = textMatch.Mark
                            });
                        }
                    }
                }
                else
                {
                    // 5. CommonWidthFallback Candidates
                    if (commonWidths.Count > 0)
                    {
                        foreach (var partner in potentialPartners)
                        {
                            if (partner.Id == anchor.Id) continue;
                            if (partner.Length < 200) continue;
                            if (!_geometryService.AreParallel(anchor, partner, 0.999)) continue;

                            double measuredWidth = _geometryService.GetPerpendicularDistance(anchor, partner);
                            if (measuredWidth < 50.0 || measuredWidth > 3000.0) continue;

                            double overlap = _geometryService.GetOverlapLength(anchor, partner);
                            if (overlap < 200) continue;
                            if (!_geometryService.IsProjectionWithinRange(anchor, partner)) continue;

                            double matchedWidth = commonWidths
                                .Where(w => Math.Abs(w - measuredWidth) / w < 0.15)
                                .OrderBy(w => Math.Abs(w - measuredWidth))
                                .FirstOrDefault();

                            if (matchedWidth > 0)
                            {
                                string smallerId = string.CompareOrdinal(anchor.Id, partner.Id) <= 0 ? anchor.Id : partner.Id;
                                string largerId = string.CompareOrdinal(anchor.Id, partner.Id) <= 0 ? partner.Id : anchor.Id;
                                string candidateId = $"COMMON:{smallerId}:{largerId}:{matchedWidth}";

                                candidates.Add(new BeamCandidate
                                {
                                    Id = candidateId,
                                    Kind = BeamCandidateKind.CommonWidthFallback,
                                    MainSegment = anchor,
                                    PartnerSegment = partner,
                                    TextMatch = null,
                                    MeasuredWidth = measuredWidth,
                                    ParsedWidth = matchedWidth,
                                    ParsedHeight = 500,
                                    OverlapLength = overlap,
                                    OverlapRatio = _geometryService.GetOverlapRatio(anchor, partner),
                                    AngleDifference = _geometryService.GetAngleDifference(anchor, partner),
                                    TextContent = string.Empty,
                                    Mark = string.Empty
                                });
                            }
                        }
                    }
                }
            }

            // Deduplicate by Candidate Id and sort deterministically by Id
            var deduplicated = candidates
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .OrderBy(c => c.Id, StringComparer.Ordinal)
                .ToList();

            return deduplicated;
        }

        private List<double> GetCommonBeamWidths(List<CadText> allTexts)
        {
            var widths = new List<double>();
            foreach (var txt in allTexts)
            {
                if (txt == null) continue;
                string content = txt.CleanText;
                if (string.IsNullOrEmpty(content)) continue;

                var dimMatch = System.Text.RegularExpressions.Regex.Match(content, @"(\d+[\.,]?\d*)\s*[xX\*\-\/]\s*(\d+[\.,]?\d*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!dimMatch.Success) continue;

                string val1 = dimMatch.Groups[1].Value.Replace(',', '.');
                if (double.TryParse(val1, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double b))
                {
                    if (b < 100) b *= 10;
                    if (b >= 100 && b <= 2000)
                        widths.Add(Math.Round(b / 10.0) * 10.0);
                }
            }

            var common = widths
                .GroupBy(w => w)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            if (common.Count == 0)
            {
                common.AddRange(new[] { 200.0, 300.0, 400.0 });
            }

            return common;
        }
    }
}
