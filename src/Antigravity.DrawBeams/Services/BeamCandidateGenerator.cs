using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Antigravity.DrawBeams.Services
{
    public class BeamCandidateGenerator
    {
        private readonly BeamGeometryService _geometryService;
        private readonly BeamTextMatcher _textMatcher;
        private readonly BeamCandidateScoringOptions _options;
        private readonly BeamCandidateScorer _scorer;

        public BeamCandidateGenerator(
            BeamGeometryService geometryService = null,
            BeamTextMatcher textMatcher = null,
            BeamCandidateScoringOptions scoringOptions = null,
            BeamCandidateScorer scorer = null)
        {
            _geometryService = geometryService ?? new BeamGeometryService();
            _textMatcher = textMatcher ?? new BeamTextMatcher(_geometryService);
            _options = scoringOptions ?? new BeamCandidateScoringOptions();
            _scorer = scorer ?? new BeamCandidateScorer(_options);
        }

        public List<BeamCandidate> GenerateCandidates(
            CadScene scene,
            string beamLayer = null,
            string textLayer = null)
        {
            var result = new List<BeamCandidate>();
            if (scene == null || scene.Segments == null) return result;

            var orderedSourceSegments = scene.Segments
                .Where(s => s != null)
                .OrderBy(s => s.Id ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(s => s.StartX)
                .ThenBy(s => s.StartY)
                .ToList();

            List<CadSegment> allSegments = _geometryService.PreProcessSegments(orderedSourceSegments)
                .Where(s => s != null)
                .OrderBy(s => s.Id ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(s => s.StartX)
                .ThenBy(s => s.StartY)
                .ToList();

            List<CadText> allTexts = (scene.Texts ?? new List<CadText>())
                .Where(t => t != null)
                .OrderBy(t => t.Id ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(t => t.X)
                .ThenBy(t => t.Y)
                .ToList();

            if (!string.IsNullOrEmpty(textLayer))
            {
                allTexts = allTexts
                    .Where(t => string.Equals(t.Layer, textLayer, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            List<CadSegment> anchorLines;
            List<CadSegment> potentialPartners;

            if (!string.IsNullOrEmpty(beamLayer))
            {
                anchorLines = allSegments
                    .Where(s => string.Equals(s.Layer, beamLayer, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (anchorLines.Count == 0)
                    anchorLines = allSegments.Where(s => s.Layer == "0").ToList();

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
                if (anchor.Length < _options.MinimumAnchorLength) continue;

                if (anchor.PolylineWidth >= _options.MinimumPolylineWidth &&
                    anchor.PolylineWidth < _options.MaximumPolylineWidth)
                {
                    var nearestMatch = _textMatcher.FindMatches(anchor, allTexts).FirstOrDefault();
                    string textId = nearestMatch?.Text?.Id ?? "NONE";
                    AddCandidate(candidates, new BeamCandidate
                    {
                        Id = $"WIDTH:{anchor.Id}:{textId}",
                        Kind = BeamCandidateKind.PolylineWidth,
                        MainSegment = anchor,
                        TextMatch = nearestMatch,
                        MeasuredWidth = anchor.PolylineWidth,
                        ParsedWidth = nearestMatch != null ? nearestMatch.ParsedWidth : anchor.PolylineWidth,
                        ParsedHeight = nearestMatch != null ? nearestMatch.ParsedHeight : 500.0,
                        TextContent = nearestMatch?.Content ?? string.Empty,
                        Mark = nearestMatch?.Mark ?? string.Empty
                    });
                    continue;
                }

                if (anchor.GroupId != null)
                {
                    var groupPartners = potentialPartners
                        .Where(p => p.GroupId == anchor.GroupId && p.Id != anchor.Id)
                        .OrderBy(p => p.Id ?? string.Empty, StringComparer.Ordinal)
                        .ToList();

                    foreach (var groupPartner in groupPartners)
                    {
                        CanonicalizeSegments(anchor, groupPartner, out var canonicalMain, out var canonicalPartner);
                        var nearestMatch = _textMatcher.FindMatches(canonicalMain, allTexts).FirstOrDefault();
                        if (nearestMatch == null || nearestMatch.ParsedWidth <= 0) continue;

                        double angleDifference = _geometryService.GetAngleDifference(canonicalMain, canonicalPartner);
                        if (angleDifference > _options.MaximumPairAngleDifference) continue;

                        double measuredWidth = _geometryService.GetPerpendicularDistance(canonicalMain, canonicalPartner);
                        if (measuredWidth < _options.MinimumBeamWidth || measuredWidth > _options.MaximumPairedWidth) continue;
                        if (!_options.IsWidthAcceptable(measuredWidth, nearestMatch.ParsedWidth)) continue;

                        double overlap = _geometryService.GetOverlapLength(canonicalMain, canonicalPartner);
                        if (overlap <= _options.RequiredOverlap(canonicalMain.Length, canonicalPartner.Length)) continue;
                        if (!_geometryService.IsProjectionWithinRange(canonicalMain, canonicalPartner)) continue;

                        AddCandidate(candidates, new BeamCandidate
                        {
                            Id = $"GROUP:{canonicalMain.Id}:{canonicalPartner.Id}:{nearestMatch.Text.Id}",
                            Kind = BeamCandidateKind.ClosedPolylinePair,
                            MainSegment = canonicalMain,
                            PartnerSegment = canonicalPartner,
                            TextMatch = nearestMatch,
                            MeasuredWidth = measuredWidth,
                            ParsedWidth = nearestMatch.ParsedWidth,
                            ParsedHeight = nearestMatch.ParsedHeight,
                            TextContent = nearestMatch.Content,
                            Mark = nearestMatch.Mark,
                            OverlapLength = overlap,
                            OverlapRatio = _geometryService.GetOverlapRatio(canonicalMain, canonicalPartner),
                            AngleDifference = angleDifference,
                            SourceGroupId = canonicalMain.GroupId
                        });
                    }
                    continue;
                }

                var textMatches = _textMatcher.FindMatches(anchor, allTexts);

                foreach (var textMatch in textMatches)
                {
                    AddCandidate(candidates, new BeamCandidate
                    {
                        Id = $"SINGLE:{anchor.Id}:{textMatch.Text.Id}",
                        Kind = BeamCandidateKind.SingleLineWithText,
                        MainSegment = anchor,
                        TextMatch = textMatch,
                        ParsedWidth = textMatch.ParsedWidth,
                        ParsedHeight = textMatch.ParsedHeight,
                        TextContent = textMatch.Content,
                        Mark = textMatch.Mark,
                        AngleDifference = textMatch.AngleDifference
                    });
                }

                if (textMatches.Count > 0)
                {
                    foreach (var textMatch in textMatches)
                    {
                        if (textMatch?.Text == null || string.IsNullOrEmpty(textMatch.Text.Id)) continue;

                        foreach (var partner in potentialPartners)
                        {
                            if (partner.Id == anchor.Id || partner.Length < _options.MinimumPartnerLength) continue;
                            CanonicalizeSegments(anchor, partner, out var canonicalMain, out var canonicalPartner);

                            var matchOnMain = _textMatcher.FindMatches(canonicalMain, allTexts)
                                .FirstOrDefault(m => m.Text != null && string.Equals(m.Text.Id, textMatch.Text.Id, StringComparison.Ordinal));
                            if (matchOnMain == null || matchOnMain.ParsedWidth <= 0) continue;

                            double angleDifference = _geometryService.GetAngleDifference(canonicalMain, canonicalPartner);
                            if (angleDifference > _options.MaximumPairAngleDifference) continue;

                            double measuredWidth = _geometryService.GetPerpendicularDistance(canonicalMain, canonicalPartner);
                            if (measuredWidth < _options.MinimumBeamWidth || measuredWidth > _options.MaximumPairedWidth) continue;
                            if (canonicalMain.Length < measuredWidth * _options.MinimumLengthToWidthRatio &&
                                canonicalPartner.Length < measuredWidth * _options.MinimumLengthToWidthRatio) continue;
                            if (!_options.IsWidthAcceptable(measuredWidth, matchOnMain.ParsedWidth)) continue;

                            double overlap = _geometryService.GetOverlapLength(canonicalMain, canonicalPartner);
                            if (overlap < _options.RequiredOverlap(canonicalMain.Length, canonicalPartner.Length)) continue;
                            if (!_geometryService.IsProjectionWithinRange(canonicalMain, canonicalPartner)) continue;

                            AddCandidate(candidates, new BeamCandidate
                            {
                                Id = $"PAIR:{canonicalMain.Id}:{canonicalPartner.Id}:{matchOnMain.Text.Id}",
                                Kind = BeamCandidateKind.PairedEdges,
                                MainSegment = canonicalMain,
                                PartnerSegment = canonicalPartner,
                                TextMatch = matchOnMain,
                                MeasuredWidth = measuredWidth,
                                ParsedWidth = matchOnMain.ParsedWidth,
                                ParsedHeight = matchOnMain.ParsedHeight,
                                OverlapLength = overlap,
                                OverlapRatio = _geometryService.GetOverlapRatio(canonicalMain, canonicalPartner),
                                AngleDifference = angleDifference,
                                TextContent = matchOnMain.Content,
                                Mark = matchOnMain.Mark
                            });
                        }
                    }
                }
                else if (commonWidths.Count > 0)
                {
                    foreach (var partner in potentialPartners)
                    {
                        if (partner.Id == anchor.Id || partner.Length < _options.MinimumPartnerLength) continue;
                        CanonicalizeSegments(anchor, partner, out var canonicalMain, out var canonicalPartner);

                        double angleDifference = _geometryService.GetAngleDifference(canonicalMain, canonicalPartner);
                        if (angleDifference > _options.MaximumPairAngleDifference) continue;

                        double measuredWidth = _geometryService.GetPerpendicularDistance(canonicalMain, canonicalPartner);
                        if (measuredWidth < _options.MinimumBeamWidth || measuredWidth > _options.MaximumFallbackWidth) continue;

                        double overlap = _geometryService.GetOverlapLength(canonicalMain, canonicalPartner);
                        if (overlap < _options.RequiredOverlap(canonicalMain.Length, canonicalPartner.Length)) continue;
                        if (!_geometryService.IsProjectionWithinRange(canonicalMain, canonicalPartner)) continue;

                        double matchedWidth = commonWidths
                            .Where(w => w > 0 && Math.Abs(w - measuredWidth) / w < _options.CommonWidthRelativeError)
                            .OrderBy(w => Math.Abs(w - measuredWidth))
                            .ThenBy(w => w)
                            .FirstOrDefault();

                        if (matchedWidth <= 0) continue;

                        string widthId = matchedWidth.ToString("0.###", CultureInfo.InvariantCulture);
                        AddCandidate(candidates, new BeamCandidate
                        {
                            Id = $"COMMON:{canonicalMain.Id}:{canonicalPartner.Id}:{widthId}",
                            Kind = BeamCandidateKind.CommonWidthFallback,
                            MainSegment = canonicalMain,
                            PartnerSegment = canonicalPartner,
                            MeasuredWidth = measuredWidth,
                            ParsedWidth = matchedWidth,
                            ParsedHeight = 500,
                            OverlapLength = overlap,
                            OverlapRatio = _geometryService.GetOverlapRatio(canonicalMain, canonicalPartner),
                            AngleDifference = angleDifference,
                            TextContent = string.Empty,
                            Mark = string.Empty
                        });
                    }
                }
            }

            return candidates
                .GroupBy(c => c.Id, StringComparer.Ordinal)
                .Select(g => g
                    .OrderByDescending(c => c.Evidence?.FinalScore ?? 0.0)
                    .ThenBy(c => c.MainSegment?.Id, StringComparer.Ordinal)
                    .ThenBy(c => c.PartnerSegment?.Id, StringComparer.Ordinal)
                    .ThenBy(c => c.TextMatch?.Text?.Id, StringComparer.Ordinal)
                    .ThenBy(c => c.MeasuredWidth)
                    .First())
                .OrderBy(c => c.Id, StringComparer.Ordinal)
                .ToList();
        }

        private void AddCandidate(List<BeamCandidate> candidates, BeamCandidate candidate)
        {
            candidate.Evidence = _scorer.BuildEvidence(candidate);
            candidates.Add(candidate);
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

                var dimMatch = System.Text.RegularExpressions.Regex.Match(
                    content,
                    @"(\d+[\.,]?\d*)\s*[xX\*\-\/]\s*(\d+[\.,]?\d*)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!dimMatch.Success) continue;

                string val1 = dimMatch.Groups[1].Value.Replace(',', '.');
                if (double.TryParse(val1, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out double b))
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
                common.AddRange(new[] { 200.0, 300.0, 400.0 });

            return common;
        }
    }
}
