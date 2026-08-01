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
                        CanonicalizeSegments(anchor, groupPartner, out var canonicalMain, out var canonicalPartner);

                        var matchesOnMain = _textMatcher.FindMatches(canonicalMain, allTexts);
                        var nearestMatch = matchesOnMain.FirstOrDefault();

                        // Section 5.1: Must have valid text
                        if (nearestMatch == null) continue;

                        // Section 5.3: Check parsed width > 0
                        if (nearestMatch.ParsedWidth <= 0) continue;

                        // Section 5.4: Parallel threshold 0.999
                        if (!_geometryService.AreParallel(canonicalMain, canonicalPartner, 0.999)) continue;

                        double measuredW = _geometryService.GetPerpendicularDistance(canonicalMain, canonicalPartner);

                        // Section 5.5: Measured width limits 50..1200
                        if (measuredW < 50 || measuredW > 1200) continue;

                        // Section 5.3: Width match score threshold 0.7
                        double widthScore = 1.0 - (Math.Abs(measuredW - nearestMatch.ParsedWidth) / nearestMatch.ParsedWidth);
                        if (widthScore < 0.7) continue;

                        // Section 5.6: Overlap & Projection
                        double overlapLen = _geometryService.GetOverlapLength(canonicalMain, canonicalPartner);
                        if (overlapLen <= 200) continue;
                        if (!_geometryService.IsProjectionWithinRange(canonicalMain, canonicalPartner)) continue;

                        string textId = nearestMatch.Text.Id;
                        string candidateId = $"GROUP:{canonicalMain.Id}:{canonicalPartner.Id}:{textId}";

                        candidates.Add(new BeamCandidate
                        {
                            Id = candidateId,
                            Kind = BeamCandidateKind.ClosedPolylinePair,
                            MainSegment = canonicalMain,
                            PartnerSegment = canonicalPartner,
                            TextMatch = nearestMatch,
                            MeasuredWidth = measuredW,
                            ParsedWidth = nearestMatch.ParsedWidth,
                            ParsedHeight = nearestMatch.ParsedHeight,
                            TextContent = nearestMatch.Content,
                            Mark = nearestMatch.Mark,
                            OverlapLength = overlapLen,
                            OverlapRatio = _geometryService.GetOverlapRatio(canonicalMain, canonicalPartner),
                            AngleDifference = _geometryService.GetAngleDifference(canonicalMain, canonicalPartner),
                            SourceGroupId = canonicalMain.GroupId
                        });
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

                            CanonicalizeSegments(anchor, partner, out var canonicalMain, out var canonicalPartner);

                            if (!_geometryService.AreParallel(canonicalMain, canonicalPartner, 0.999)) continue;

                            double measuredWidth = _geometryService.GetPerpendicularDistance(canonicalMain, canonicalPartner);
                            if (measuredWidth < 50 || measuredWidth > 1200) continue;
                            if (anchor.Length < measuredWidth * 1.2) continue;

                            double widthScore = 1.0 - (Math.Abs(measuredWidth - expectedWidth) / expectedWidth);
                            if (widthScore < 0.7) continue;

                            double overlap = _geometryService.GetOverlapLength(canonicalMain, canonicalPartner);
                            if (overlap < 200) continue;

                            if (!_geometryService.IsProjectionWithinRange(canonicalMain, canonicalPartner)) continue;

                            var mainMatches = _textMatcher.FindMatches(canonicalMain, allTexts);
                            var matchOnMain = mainMatches.FirstOrDefault(m => m.Text.Id == textMatch.Text.Id)
                                              ?? mainMatches.FirstOrDefault()
                                              ?? textMatch;

                            string textId = matchOnMain.Text.Id;
                            string candidateId = $"PAIR:{canonicalMain.Id}:{canonicalPartner.Id}:{textId}";

                            candidates.Add(new BeamCandidate
                            {
                                Id = candidateId,
                                Kind = BeamCandidateKind.PairedEdges,
                                MainSegment = canonicalMain,
                                PartnerSegment = canonicalPartner,
                                TextMatch = matchOnMain,
                                MeasuredWidth = measuredWidth,
                                ParsedWidth = matchOnMain.ParsedWidth,
                                ParsedHeight = matchOnMain.ParsedHeight,
                                OverlapLength = overlap,
                                OverlapRatio = _geometryService.GetOverlapRatio(canonicalMain, canonicalPartner),
                                AngleDifference = _geometryService.GetAngleDifference(canonicalMain, canonicalPartner),
                                TextContent = matchOnMain.Content,
                                Mark = matchOnMain.Mark
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

                            CanonicalizeSegments(anchor, partner, out var canonicalMain, out var canonicalPartner);

                            if (!_geometryService.AreParallel(canonicalMain, canonicalPartner, 0.999)) continue;

                            double measuredWidth = _geometryService.GetPerpendicularDistance(canonicalMain, canonicalPartner);
                            if (measuredWidth < 50.0 || measuredWidth > 3000.0) continue;

                            double overlap = _geometryService.GetOverlapLength(canonicalMain, canonicalPartner);
                            if (overlap < 200) continue;
                            if (!_geometryService.IsProjectionWithinRange(canonicalMain, canonicalPartner)) continue;

                            double matchedWidth = commonWidths
                                .Where(w => Math.Abs(w - measuredWidth) / w < 0.15)
                                .OrderBy(w => Math.Abs(w - measuredWidth))
                                .FirstOrDefault();

                            if (matchedWidth > 0)
                            {
                                string candidateId = $"COMMON:{canonicalMain.Id}:{canonicalPartner.Id}:{matchedWidth}";

                                candidates.Add(new BeamCandidate
                                {
                                    Id = candidateId,
                                    Kind = BeamCandidateKind.CommonWidthFallback,
                                    MainSegment = canonicalMain,
                                    PartnerSegment = canonicalPartner,
                                    TextMatch = null,
                                    MeasuredWidth = measuredWidth,
                                    ParsedWidth = matchedWidth,
                                    ParsedHeight = 500,
                                    OverlapLength = overlap,
                                    OverlapRatio = _geometryService.GetOverlapRatio(canonicalMain, canonicalPartner),
                                    AngleDifference = _geometryService.GetAngleDifference(canonicalMain, canonicalPartner),
                                    TextContent = string.Empty,
                                    Mark = string.Empty
                                });
                            }
                        }
                    }
                }
            }

            // Section 8: Deduplicate deterministically
            var deduplicated = candidates
                .GroupBy(c => c.Id, StringComparer.Ordinal)
                .Select(g => g
                    .OrderBy(c => c.MainSegment?.Id, StringComparer.Ordinal)
                    .ThenBy(c => c.PartnerSegment?.Id, StringComparer.Ordinal)
                    .ThenBy(c => c.TextMatch?.Text?.Id, StringComparer.Ordinal)
                    .ThenBy(c => c.MeasuredWidth)
                    .First())
                .OrderBy(c => c.Id, StringComparer.Ordinal)
                .ToList();

            return deduplicated;
        }

        private static void CanonicalizeSegments(
            CadSegment first,
            CadSegment second,
            out CadSegment main,
            out CadSegment partner)
        {
            if (string.CompareOrdinal(first.Id, second.Id) <= 0)
            {
                main = first;
                partner = second;
            }
            else
            {
                main = second;
                partner = first;
            }
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
