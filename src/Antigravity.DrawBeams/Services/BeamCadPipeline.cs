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
            BeamContinuityOptions options = null,
            BeamOverlapOptions overlapOptions = null)
        {
            var opt = options ?? _options;
            if (rawSegments == null) return new List<CadBeamData>();

            var textList = dimensionTexts != null ? dimensionTexts.ToList() : new List<CadDimensionText>();

            // Step 1: Build Geometry Chains with Junction Splitting using opt
            var chainBuilder = new BeamChainBuilder(opt);
            var initialChains = chainBuilder.BuildChains(rawSegments);

            var summary = BeamDiagnosticCollector.Instance.CurrentSession?.PipelineSummary;
            if (summary != null)
            {
                summary.ContinuityChainsCount = initialChains.Count;
            }

            // Step 2: Dimension Resolver & Splitting along Chains using opt
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

            if (summary != null)
            {
                summary.BeforeOverlapCount = result.Count;
            }

            // Step 4: Overlap Suppression using BeamOverlapResolver
            var overlapResolver = new BeamOverlapResolver(overlapOptions);
            var resolvedBeams = overlapResolver.ResolveOverlaps(result);

            // Step 5: Deduplicate CadBeamData output by direction-independent geometry
            return DeduplicateBeamData(resolvedBeams);
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

                var assignedTexts = FindTextsForChain(chain, allTexts, opt);

                // Group assigned texts by rounded dimensions (0.1mm tolerance for floating point)
                var dimGroups = assignedTexts
                    .Where(t => t.Width > 0)
                    .GroupBy(t => new
                    {
                        Width = Math.Round(t.Width, 1),
                        Height = Math.Round(t.Height, 1)
                    })
                    .ToList();

                if (dimGroups.Count <= 1)
                {
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
            double dx = chain.EndX - chain.StartX;
            double dy = chain.EndY - chain.StartY;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return new List<BeamChain> { chain };

            double ux = dx / len;
            double uy = dy / len;

            var projectedTexts = texts
                .Where(t => t.Width > 0)
                .Select(t => new { Text = t, ProjectionT = t.X * ux + t.Y * uy })
                .OrderBy(p => p.ProjectionT)
                .ToList();

            if (projectedTexts.Count <= 1) return new List<BeamChain> { chain };

            // Find split projection points where dimensions change
            var splitProjections = new List<double>();
            for (int i = 0; i < projectedTexts.Count - 1; i++)
            {
                var t1 = projectedTexts[i];
                var t2 = projectedTexts[i + 1];

                if (Math.Abs(t1.Text.Width - t2.Text.Width) > 5.0 || Math.Abs(t1.Text.Height - t2.Text.Height) > 5.0)
                {
                    double midT = (t1.ProjectionT + t2.ProjectionT) / 2.0;
                    splitProjections.Add(midT);
                }
            }

            if (splitProjections.Count == 0) return new List<BeamChain> { chain };

            // Cut any segment that crosses a split projection point into two exact sub-segments
            var cutSegments = CutSegmentsAtProjections(chain.Segments, splitProjections, ux, uy);

            // Group cut segments into sub-chains bounded by splitProjections
            var sortedSplits = splitProjections.Distinct().OrderBy(t => t).ToList();
            var subChains = new List<BeamChain>();

            double currentStartT = chain.StartX * ux + chain.StartY * uy;

            foreach (double splitT in sortedSplits)
            {
                var bucketSegs = cutSegments.Where(s =>
                {
                    double segMidT = ((s.StartX + s.EndX) / 2.0) * ux + ((s.StartY + s.EndY) / 2.0) * uy;
                    return segMidT >= currentStartT - 1e-3 && segMidT < splitT;
                }).ToList();

                if (bucketSegs.Count > 0)
                {
                    var subChain = FinalizeSubChain(bucketSegs, texts, ux, uy, opt);
                    subChains.Add(subChain);
                }
                currentStartT = splitT;
            }

            var lastBucketSegs = cutSegments.Where(s =>
            {
                double segMidT = ((s.StartX + s.EndX) / 2.0) * ux + ((s.StartY + s.EndY) / 2.0) * uy;
                return segMidT >= currentStartT - 1e-3;
            }).ToList();

            if (lastBucketSegs.Count > 0)
            {
                var subChain = FinalizeSubChain(lastBucketSegs, texts, ux, uy, opt);
                subChains.Add(subChain);
            }

            return subChains.Count > 0 ? subChains : new List<BeamChain> { chain };
        }

        private List<CadBeamSegment> CutSegmentsAtProjections(List<CadBeamSegment> segments, List<double> splitProjections, double ux, double uy)
        {
            var currentSegments = new List<CadBeamSegment>(segments);

            foreach (double splitT in splitProjections)
            {
                var nextSegments = new List<CadBeamSegment>();

                foreach (var seg in currentSegments)
                {
                    double t1 = seg.StartX * ux + seg.StartY * uy;
                    double t2 = seg.EndX * ux + seg.EndY * uy;
                    double tMin = Math.Min(t1, t2);
                    double tMax = Math.Max(t1, t2);

                    // Check if splitT is strictly inside segment projection interval
                    if (splitT > tMin + 1e-3 && splitT < tMax - 1e-3)
                    {
                        double fraction = (t1 != t2) ? (splitT - t1) / (t2 - t1) : 0.5;
                        fraction = Math.Max(0.0, Math.Min(1.0, fraction));

                        double splitX = seg.StartX + fraction * (seg.EndX - seg.StartX);
                        double splitY = seg.StartY + fraction * (seg.EndY - seg.StartY);

                        var piece1 = new CadBeamSegment
                        {
                            StartX = seg.StartX,
                            StartY = seg.StartY,
                            EndX = splitX,
                            EndY = splitY,
                            Width = seg.Width,
                            Height = seg.Height,
                            MeasuredWidth = seg.MeasuredWidth,
                            Mark = seg.Mark,
                            TextContent = seg.TextContent,
                            IsPaired = seg.IsPaired,
                            Confidence = seg.Confidence,
                            Layer = seg.Layer,
                            DetectionMethod = BeamDetectionMethod.DimensionSplit,
                            SourceLineIds = seg.SourceLineIds != null ? new List<string>(seg.SourceLineIds) : new List<string>()
                        };

                        var piece2 = new CadBeamSegment
                        {
                            StartX = splitX,
                            StartY = splitY,
                            EndX = seg.EndX,
                            EndY = seg.EndY,
                            Width = seg.Width,
                            Height = seg.Height,
                            MeasuredWidth = seg.MeasuredWidth,
                            Mark = seg.Mark,
                            TextContent = seg.TextContent,
                            IsPaired = seg.IsPaired,
                            Confidence = seg.Confidence,
                            Layer = seg.Layer,
                            DetectionMethod = BeamDetectionMethod.DimensionSplit,
                            SourceLineIds = seg.SourceLineIds != null ? new List<string>(seg.SourceLineIds) : new List<string>()
                        };

                        if (piece1.Length >= 1e-3) nextSegments.Add(piece1);
                        if (piece2.Length >= 1e-3) nextSegments.Add(piece2);
                    }
                    else
                    {
                        nextSegments.Add(seg);
                    }
                }

                currentSegments = nextSegments;
            }

            return currentSegments;
        }

        private BeamChain FinalizeSubChain(List<CadBeamSegment> segments, List<CadDimensionText> texts, double ux, double uy, BeamContinuityOptions opt)
        {
            var builder = new BeamChainBuilder(opt);
            var subChains = builder.BuildChains(segments);
            var subChain = subChains.FirstOrDefault() ?? new BeamChain { Segments = segments };

            double midT = ((subChain.StartX + subChain.EndX) / 2.0) * ux + ((subChain.StartY + subChain.EndY) / 2.0) * uy;
            var closestText = texts
                .Where(t => t.Width > 0)
                .OrderBy(t => Math.Abs((t.X * ux + t.Y * uy) - midT))
                .FirstOrDefault();

            if (closestText != null)
            {
                foreach (var s in subChain.Segments)
                {
                    s.Width = closestText.Width;
                    s.Height = closestText.Height;
                    s.TextContent = closestText.Content;
                    s.Mark = null;
                }
            }

            return subChain;
        }

        private CadBeamData ConvertChainToBeamData(BeamChain chain)
        {
            if (chain == null || chain.Segments == null || chain.Segments.Count == 0) return null;

            var primarySeg = chain.Segments
                .OrderByDescending(s => BeamOverlapResolver.GetPriorityScore(new CadBeamData
                {
                    Confidence = s.Confidence,
                    HasDimensionText = s.HasDimensionText,
                    DetectionMethod = s.DetectionMethod
                }))
                .ThenByDescending(s => s.Length)
                .First();

            double width = primarySeg.Width > 0 ? primarySeg.Width : chain.Width;
            double height = primarySeg.Height > 0 ? primarySeg.Height : chain.Height;

            string textContent = !string.IsNullOrEmpty(primarySeg.TextContent)
                ? primarySeg.TextContent
                : chain.Segments.FirstOrDefault(s => !string.IsNullOrEmpty(s.TextContent))?.TextContent;

            string mark = !string.IsNullOrEmpty(primarySeg.Mark) ? primarySeg.Mark : null;

            double measuredW = primarySeg.MeasuredWidth > 0
                ? primarySeg.MeasuredWidth
                : (chain.Segments.FirstOrDefault(s => s.MeasuredWidth > 0)?.MeasuredWidth ?? 0);

            var sourceLineIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in chain.Segments)
            {
                if (s.SourceLineIds != null)
                {
                    foreach (var id in s.SourceLineIds)
                    {
                        if (!string.IsNullOrEmpty(id)) sourceLineIds.Add(id);
                    }
                }
            }

            var beam = new CadBeamData
            {
                StartX = chain.StartX,
                StartY = chain.StartY,
                EndX = chain.EndX,
                EndY = chain.EndY,
                Width = width,
                Height = height,
                TextContent = textContent,
                Mark = mark,
                IsPaired = chain.Segments.Any(s => s.IsPaired),
                MeasuredWidth = measuredW,
                Confidence = primarySeg.Confidence,
                SourceLayer = primarySeg.Layer,
                HasDimensionText = primarySeg.HasDimensionText || !string.IsNullOrEmpty(textContent),
                DetectionMethod = primarySeg.DetectionMethod,
                SourceLineIds = sourceLineIds
            };

            if (string.IsNullOrEmpty(beam.Mark))
            {
                ExtractMark(beam);
            }

            return beam;
        }

        private void ExtractMark(CadBeamData beam)
        {
            if (string.IsNullOrEmpty(beam.TextContent)) return;

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
                {
                    bool sameDims = Math.Abs(existing.Width - b.Width) < 1e-3 &&
                                     Math.Abs(existing.Height - b.Height) < 1e-3;
                    if (!sameDims) return false;

                    bool forwardMatch = Math.Abs(existing.StartX - b.StartX) < 1e-3 &&
                                        Math.Abs(existing.StartY - b.StartY) < 1e-3 &&
                                        Math.Abs(existing.EndX - b.EndX) < 1e-3 &&
                                        Math.Abs(existing.EndY - b.EndY) < 1e-3;

                    bool reverseMatch = Math.Abs(existing.StartX - b.EndX) < 1e-3 &&
                                        Math.Abs(existing.StartY - b.EndY) < 1e-3 &&
                                        Math.Abs(existing.EndX - b.StartX) < 1e-3 &&
                                        Math.Abs(existing.EndY - b.StartY) < 1e-3;

                    return forwardMatch || reverseMatch;
                });

                if (!isDup)
                {
                    result.Add(b);
                }
            }
            return result;
        }
    }
}
