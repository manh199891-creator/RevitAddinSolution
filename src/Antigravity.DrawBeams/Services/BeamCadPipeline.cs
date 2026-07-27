using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Antigravity.DrawBeams.Services
{
    public class BeamCadPipeline
    {
        private readonly BeamContinuityOptions _options;

        public BeamCadPipeline(BeamContinuityOptions options = null)
        {
            _options = options ?? new BeamContinuityOptions();
        }

        public List<CadBeamData> ProcessPipeline(
            IEnumerable<CadBeamSegment> rawSegments,
            IEnumerable<CadDimensionText> dimensionTexts = null,
            BeamContinuityOptions options = null)
        {
            var opt = options ?? _options;
            if (rawSegments == null) return new List<CadBeamData>();

            var textList = dimensionTexts != null ? dimensionTexts.ToList() : new List<CadDimensionText>();

            // Step 1: Build Geometry Chains with Junction Splitting
            var chainBuilder = new BeamChainBuilder(opt);
            var initialChains = chainBuilder.BuildChains(rawSegments);

            // Step 2: Dimension Resolver & Splitting along Chains
            var resolvedChains = ResolveDimensionsAndSplitChains(initialChains, textList, opt);

            // Step 3: Convert Chains to CadBeamData
            List<CadBeamData> result = new List<CadBeamData>();

            foreach (var chain in resolvedChains)
            {
                var beamData = ConvertChainToBeamData(chain);
                if (beamData != null && beamData.IsValid)
                {
                    result.Add(beamData);
                }
            }

            // Deduplicate CadBeamData output by geometry
            return DeduplicateBeamData(result);
        }

        private List<BeamChain> ResolveDimensionsAndSplitChains(
            List<BeamChain> chains,
            List<CadDimensionText> allTexts,
            BeamContinuityOptions opt)
        {
            var outputChains = new List<BeamChain>();

            foreach (var chain in chains)
            {
                if (chain.Segments == null || chain.Segments.Count == 0) continue;

                // Find all texts associated with this chain
                var assignedTexts = FindTextsForChain(chain, allTexts, opt);

                // Group assigned texts by dimension (Width, Height) where Width > 0
                var dimGroups = assignedTexts
                    .Where(t => t.Width > 0)
                    .GroupBy(t => new { t.Width, t.Height })
                    .ToList();

                if (dimGroups.Count <= 1)
                {
                    // 0 or 1 dimension group -> No size split needed
                    if (dimGroups.Count == 1)
                    {
                        var group = dimGroups[0];
                        var sampleText = group.First();
                        foreach (var seg in chain.Segments)
                        {
                            if (seg.Width <= 0) seg.Width = sampleText.Width;
                            if (seg.Height <= 0) seg.Height = sampleText.Height;
                            if (string.IsNullOrEmpty(seg.TextContent)) seg.TextContent = sampleText.Content;
                        }
                    }
                    outputChains.Add(chain);
                }
                else
                {
                    // Multiple DIFFERENT dimensions along the chain -> SPLIT chain at dimension boundary
                    var splitChains = SplitChainByDimensions(chain, assignedTexts, opt);
                    outputChains.AddRange(splitChains);
                }
            }

            return outputChains;
        }

        private List<CadDimensionText> FindTextsForChain(BeamChain chain, List<CadDimensionText> texts, BeamContinuityOptions opt)
        {
            var result = new List<CadDimensionText>();
            if (texts == null || texts.Count == 0) return result;

            double dx = chain.EndX - chain.StartX;
            double dy = chain.EndY - chain.StartY;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return result;

            double ux = dx / len;
            double uy = dy / len;
            double nx = -uy;
            double ny = ux;

            double minT = chain.StartX * ux + chain.StartY * uy;
            double maxT = chain.EndX * ux + chain.EndY * uy;

            double chainLat = chain.StartX * nx + chain.StartY * ny;
            double latMargin = opt.LateralOffsetToleranceMm + (chain.Width > 0 ? chain.Width / 2.0 : 200.0);

            foreach (var t in texts)
            {
                double textT = t.X * ux + t.Y * uy;
                double textLat = t.X * nx + t.Y * ny;

                if (textT >= minT - 100.0 && textT <= maxT + 100.0 && Math.Abs(textLat - chainLat) <= latMargin)
                {
                    result.Add(t);
                }
            }

            return result;
        }

        private List<BeamChain> SplitChainByDimensions(BeamChain chain, List<CadDimensionText> texts, BeamContinuityOptions opt)
        {
            // Project texts along chain axis
            double dx = chain.EndX - chain.StartX;
            double dy = chain.EndY - chain.StartY;
            double len = Math.Sqrt(dx * dx + dy * dy);
            double ux = dx / len;
            double uy = dy / len;

            var projectedTexts = texts
                .Where(t => t.Width > 0)
                .Select(t => new { Text = t, ProjectionT = t.X * ux + t.Y * uy })
                .OrderBy(p => p.ProjectionT)
                .ToList();

            if (projectedTexts.Count <= 1) return new List<BeamChain> { chain };

            // Find split boundaries between different dimension texts
            var splitProjections = new List<double>();
            for (int i = 0; i < projectedTexts.Count - 1; i++)
            {
                var t1 = projectedTexts[i];
                var t2 = projectedTexts[i + 1];

                if (Math.Abs(t1.Text.Width - t2.Text.Width) > 5.0 || Math.Abs(t1.Text.Height - t2.Text.Height) > 5.0)
                {
                    double midT = (t1.ProjectionT + t2.ProjectionT) / 2.0;

                    // Find nearest segment node near midT
                    double nearestNodeT = FindNearestSegmentNodeProjection(chain, ux, uy, midT);
                    splitProjections.Add(nearestNodeT);
                }
            }

            if (splitProjections.Count == 0) return new List<BeamChain> { chain };

            // Split chain segments into sub-chains at splitProjections
            var subChains = new List<BeamChain>();
            var sortedSplits = splitProjections.Distinct().OrderBy(t => t).ToList();

            double currentStartT = chain.StartX * ux + chain.StartY * uy;

            foreach (double splitT in sortedSplits)
            {
                var subSegs = chain.Segments.Where(s =>
                {
                    double segMidT = ((s.StartX + s.EndX) / 2.0) * ux + ((s.StartY + s.EndY) / 2.0) * uy;
                    return segMidT >= currentStartT - 1e-3 && segMidT < splitT;
                }).ToList();

                if (subSegs.Count > 0)
                {
                    var subChain = FinalizeSubChain(subSegs, texts, ux, uy);
                    subChains.Add(subChain);
                }
                currentStartT = splitT;
            }

            var lastSubSegs = chain.Segments.Where(s =>
            {
                double segMidT = ((s.StartX + s.EndX) / 2.0) * ux + ((s.StartY + s.EndY) / 2.0) * uy;
                return segMidT >= currentStartT - 1e-3;
            }).ToList();

            if (lastSubSegs.Count > 0)
            {
                var subChain = FinalizeSubChain(lastSubSegs, texts, ux, uy);
                subChains.Add(subChain);
            }

            return subChains.Count > 0 ? subChains : new List<BeamChain> { chain };
        }

        private double FindNearestSegmentNodeProjection(BeamChain chain, double ux, double uy, double targetT)
        {
            double nearestT = targetT;
            double minDiff = double.MaxValue;

            foreach (var s in chain.Segments)
            {
                double t1 = s.StartX * ux + s.StartY * uy;
                double t2 = s.EndX * ux + s.EndY * uy;

                double diff1 = Math.Abs(t1 - targetT);
                if (diff1 < minDiff) { minDiff = diff1; nearestT = t1; }

                double diff2 = Math.Abs(t2 - targetT);
                if (diff2 < minDiff) { minDiff = diff2; nearestT = t2; }
            }

            return nearestT;
        }

        private BeamChain FinalizeSubChain(List<CadBeamSegment> segments, List<CadDimensionText> texts, double ux, double uy)
        {
            var builder = new BeamChainBuilder(_options);
            var subChains = builder.BuildChains(segments);
            var subChain = subChains.FirstOrDefault() ?? new BeamChain { Segments = segments };

            // Find closest matching text for this sub-chain
            double midT = ((subChain.StartX + subChain.EndX) / 2.0) * ux + ((subChain.StartY + subChain.EndY) / 2.0) * uy;
            var closestText = texts
                .Where(t => t.Width > 0)
                .OrderBy(t => Math.Abs((t.X * ux + t.Y * uy) - midT))
                .FirstOrDefault();

            if (closestText != null)
            {
                foreach (var s in subChain.Segments)
                {
                    if (s.Width <= 0) s.Width = closestText.Width;
                    if (s.Height <= 0) s.Height = closestText.Height;
                    if (string.IsNullOrEmpty(s.TextContent)) s.TextContent = closestText.Content;
                }
            }

            return subChain;
        }

        private CadBeamData ConvertChainToBeamData(BeamChain chain)
        {
            if (chain == null || chain.Segments == null || chain.Segments.Count == 0) return null;

            // Primary segment selection by Confidence descending, then Length descending
            var primarySeg = chain.Segments
                .OrderByDescending(s => s.Confidence)
                .ThenByDescending(s => s.Length)
                .First();

            double width = chain.Width > 0 ? chain.Width : primarySeg.Width;
            double height = chain.Height > 0 ? chain.Height : primarySeg.Height;

            string textContent = chain.Segments.FirstOrDefault(s => !string.IsNullOrEmpty(s.TextContent))?.TextContent
                ?? primarySeg.TextContent;

            double measuredW = chain.Segments.FirstOrDefault(s => s.MeasuredWidth > 0)?.MeasuredWidth
                ?? primarySeg.MeasuredWidth;

            var beam = new CadBeamData
            {
                StartX = chain.StartX,
                StartY = chain.StartY,
                EndX = chain.EndX,
                EndY = chain.EndY,
                Width = width,
                Height = height,
                TextContent = textContent,
                IsPaired = chain.Segments.Any(s => s.IsPaired),
                MeasuredWidth = measuredW
            };

            // Extract Mark from textContent or primary segment's Mark
            ExtractMark(beam);
            if (string.IsNullOrEmpty(beam.Mark) && !string.IsNullOrEmpty(primarySeg.Mark))
            {
                beam.Mark = primarySeg.Mark;
            }

            return beam;
        }

        private void ExtractMark(CadBeamData beam)
        {
            if (string.IsNullOrEmpty(beam.TextContent)) return;

            // Pattern: D1, d1, B1, SB1, dầm D1...
            Match match = Regex.Match(beam.TextContent, @"\b([A-Z]{1,3}\d+)\b", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                beam.Mark = match.Groups[1].Value.ToUpper();
            }
        }

        private List<CadBeamData> DeduplicateBeamData(List<CadBeamData> beams)
        {
            var result = new List<CadBeamData>();
            foreach (var b in beams)
            {
                bool isDup = result.Any(existing =>
                    Math.Abs(existing.StartX - b.StartX) < 1e-3 &&
                    Math.Abs(existing.StartY - b.StartY) < 1e-3 &&
                    Math.Abs(existing.EndX - b.EndX) < 1e-3 &&
                    Math.Abs(existing.EndY - b.EndY) < 1e-3 &&
                    Math.Abs(existing.Width - b.Width) < 1e-3 &&
                    Math.Abs(existing.Height - b.Height) < 1e-3);

                if (!isDup)
                {
                    result.Add(b);
                }
            }
            return result;
        }
    }
}
