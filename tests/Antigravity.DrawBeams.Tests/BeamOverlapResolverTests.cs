using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    [Collection("Sequential")]
    public class BeamOverlapResolverTests
    {
        private readonly BeamOverlapResolver _resolver;
        private readonly BeamOverlapOptions _options;

        public BeamOverlapResolverTests()
        {
            _options = new BeamOverlapOptions
            {
                AngularToleranceDegrees = 1.0,
                CenterlineDistanceToleranceMm = 25.0,
                EndpointToleranceMm = 50.0,
                MinimumOverlapRatio = 0.80,
                ContainmentToleranceMm = 50.0
            };
            _resolver = new BeamOverlapResolver(_options);
        }

        [Fact]
        public void Test1_ExactDuplicate_ReturnsOneBeam()
        {
            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };
            var b2 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Single(resolved);
        }

        [Fact]
        public void Test2_ReversedDuplicate_ReturnsOneBeam()
        {
            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };
            var b2 = new CadBeamData { StartX = 3000, StartY = 0, EndX = 0, EndY = 0, Width = 400, Height = 600, Confidence = 500 };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Single(resolved);
        }

        [Fact]
        public void Test3_CenterlineOffset10mm_Overlap100Percent_ReturnsOneBeam()
        {
            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };
            var b2 = new CadBeamData { StartX = 0, StartY = 10, EndX = 3000, EndY = 10, Width = 400, Height = 600, Confidence = 500 };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Single(resolved);
        }

        [Fact]
        public void Test4_CenterlineOffset40mm_ReturnsTwoBeams()
        {
            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };
            var b2 = new CadBeamData { StartX = 0, StartY = 40, EndX = 3000, EndY = 40, Width = 400, Height = 600, Confidence = 500 };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test5_LongBeamContainsShortBeam_SameSize_ReturnsOneBeam()
        {
            var longBeam = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };
            var shortBeam = new CadBeamData { StartX = 500, StartY = 0, EndX = 2500, EndY = 0, Width = 400, Height = 600, Confidence = 500 };

            var resolved = _resolver.ResolveOverlaps(new[] { longBeam, shortBeam });

            Assert.Single(resolved);
            Assert.Equal(3000, Math.Abs(resolved[0].EndX - resolved[0].StartX));
        }

        [Fact]
        public void Test6_LongBeamSpansTwoRegions_LowConfidenceLongBeam_DiscardsLongBeam()
        {
            var longBeam = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, Confidence = 100,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback
            };

            var region1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 1500, EndY = 0,
                Width = 400, Height = 550, Confidence = 500,
                DetectionMethod = BeamDetectionMethod.PairedLines,
                HasDimensionText = true
            };

            var region2 = new CadBeamData
            {
                StartX = 1500, StartY = 0, EndX = 3000, EndY = 0,
                Width = 600, Height = 550, Confidence = 500,
                DetectionMethod = BeamDetectionMethod.PairedLines,
                HasDimensionText = true
            };

            var resolved = _resolver.ResolveOverlaps(new[] { longBeam, region1, region2 });

            Assert.Equal(2, resolved.Count);
            Assert.DoesNotContain(longBeam, resolved);
            Assert.Contains(region1, resolved);
            Assert.Contains(region2, resolved);
        }

        [Fact]
        public void Test7_TwoContiguousRegions_DifferentSizes_KeepsBoth()
        {
            var r1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 1500, EndY = 0, Width = 400, Height = 550, Confidence = 500 };
            var r2 = new CadBeamData { StartX = 1500, StartY = 0, EndX = 3000, EndY = 0, Width = 600, Height = 550, Confidence = 500 };

            var resolved = _resolver.ResolveOverlaps(new[] { r1, r2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test8_TwoParallelBeams300mmApart_KeepsBoth()
        {
            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };
            var b2 = new CadBeamData { StartX = 0, StartY = 300, EndX = 3000, EndY = 300, Width = 400, Height = 600, Confidence = 500 };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test9_PartialOverlap20Percent_KeepsBoth()
        {
            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };
            var b2 = new CadBeamData { StartX = 800, StartY = 0, EndX = 1800, EndY = 0, Width = 400, Height = 600, Confidence = 500 };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test10_ShuffledInput_ReturnsStableOutput()
        {
            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 1500, EndY = 0, Width = 400, Height = 600, Confidence = 500 };
            var b2 = new CadBeamData { StartX = 1500, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };
            var dup = new CadBeamData { StartX = 0, StartY = 0, EndX = 1500, EndY = 0, Width = 400, Height = 600, Confidence = 300 };

            var order1 = _resolver.ResolveOverlaps(new[] { b1, b2, dup });
            var order2 = _resolver.ResolveOverlaps(new[] { dup, b2, b1 });
            var order3 = _resolver.ResolveOverlaps(new[] { b2, dup, b1 });

            Assert.Equal(order1.Count, order2.Count);
            Assert.Equal(order1.Count, order3.Count);
            Assert.Equal(order1[0].StartX, order2[0].StartX);
            Assert.Equal(order1[0].StartX, order3[0].StartX);
        }

        [Fact]
        public void Test11_SingleLineFallbackVsPairedLines_KeepsPairedLines()
        {
            var fallback = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, Confidence = 100,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback
            };

            var paired = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, Confidence = 500,
                DetectionMethod = BeamDetectionMethod.PairedLines,
                HasDimensionText = true
            };

            var resolved = _resolver.ResolveOverlaps(new[] { fallback, paired });

            Assert.Single(resolved);
            Assert.Equal(BeamDetectionMethod.PairedLines, resolved[0].DetectionMethod);
        }

        [Fact]
        public void Test12_IntersectingSourceLineIds_PrioritizesBetterCandidate()
        {
            var cand1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, Confidence = 100,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback,
                SourceLineIds = new HashSet<string> { "LINE_1" }
            };

            var cand2 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, Confidence = 800,
                DetectionMethod = BeamDetectionMethod.PairedLines,
                HasDimensionText = true,
                SourceLineIds = new HashSet<string> { "LINE_1", "LINE_2" }
            };

            var resolved = _resolver.ResolveOverlaps(new[] { cand1, cand2 });

            Assert.Single(resolved);
            Assert.Equal(800, resolved[0].Confidence);
        }

        // --- NEW REQUIRED TESTS (Rules A, B, C, D & Sanity) ---

        [Fact]
        public void Test13_PairedBeamVsFallbackAtHalfWidth_SuppressFallback()
        {
            var paired = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 8000, EndY = 0,
                Width = 400, Height = 600, HasDimensionText = true,
                DetectionMethod = BeamDetectionMethod.PairedLines, DiagnosticId = "PAIRED_01"
            };

            var fallback = new CadBeamData
            {
                StartX = 0, StartY = 200, EndX = 8000, EndY = 200,
                Width = 400, Height = 600, HasDimensionText = false,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback, DiagnosticId = "FALLBACK_01"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { paired, fallback });

            Assert.Single(resolved);
            Assert.Equal("PAIRED_01", resolved[0].DiagnosticId);
        }

        [Fact]
        public void Test14_PairedBeamVsFallbackAtHalfWidth_ReversedInputOrder_SuppressFallback()
        {
            var paired = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 8000, EndY = 0,
                Width = 400, Height = 600, HasDimensionText = true,
                DetectionMethod = BeamDetectionMethod.PairedLines, DiagnosticId = "PAIRED_01"
            };

            var fallback = new CadBeamData
            {
                StartX = 0, StartY = 200, EndX = 8000, EndY = 200,
                Width = 400, Height = 600, HasDimensionText = false,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback, DiagnosticId = "FALLBACK_01"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { fallback, paired });

            Assert.Single(resolved);
            Assert.Equal("PAIRED_01", resolved[0].DiagnosticId);
        }

        [Fact]
        public void Test15_DimensionedCompleteVsIncompleteHighRawConfidence_KeepsDimensioned()
        {
            var candA = new CadBeamData
            {
                StartX = 100120, StartY = 0, EndX = 100120, EndY = 8000,
                Width = 400, Height = 700, HasDimensionText = true, Confidence = 500,
                DetectionMethod = BeamDetectionMethod.PairedLines, DiagnosticId = "CAND_A"
            };

            var candB = new CadBeamData
            {
                StartX = 100230, StartY = 0, EndX = 100230, EndY = 8000,
                Width = 200, Height = 0, HasDimensionText = false, Confidence = 10000, // Very high raw confidence
                DetectionMethod = BeamDetectionMethod.SingleLineFallback, DiagnosticId = "CAND_B"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { candA, candB });

            Assert.Single(resolved);
            Assert.Equal("CAND_A", resolved[0].DiagnosticId);
        }

        [Fact]
        public void Test16_MeasuredWidthMismatch_GetsEvidencePenalty()
        {
            var cand1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 600, Height = 500, MeasuredWidth = 780,
                DetectionMethod = BeamDetectionMethod.PairedLines, HasDimensionText = true
            };

            var cand2 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 600, Height = 500, MeasuredWidth = 600,
                DetectionMethod = BeamDetectionMethod.PairedLines, HasDimensionText = true
            };

            double score1 = BeamOverlapResolver.GetPriorityScore(cand1);
            double score2 = BeamOverlapResolver.GetPriorityScore(cand2);

            Assert.True(score2 > score1, "Candidate with perfect measured width agreement must score higher than candidate with weak agreement mismatch.");
        }

        [Fact]
        public void Test17_TwoPhysicalBeamsFarEnough_KeepsBoth()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines
            };

            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 600, EndX = 3000, EndY = 600, // 600 mm centerline distance
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test18_TwoBeamsWithIndependentText_NotSuppressed()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, HasDimensionText = true, TextContent = "B1 400x600",
                SourceLineIds = new HashSet<string> { "LINE_A", "LINE_B" },
                DetectionMethod = BeamDetectionMethod.PairedLines
            };

            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 300, EndX = 3000, EndY = 300, // 300 mm centerline dist
                Width = 400, Height = 600, HasDimensionText = true, TextContent = "B2 400x600",
                SourceLineIds = new HashSet<string> { "LINE_C", "LINE_D" },
                DetectionMethod = BeamDetectionMethod.PairedLines
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test19_LongIncompleteCandidateCoveringTwoDimensionRegions_SuppressesLongCandidate()
        {
            var longIncomplete = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 6000, EndY = 0,
                Width = 400, Height = 0, HasDimensionText = false,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback, DiagnosticId = "LONG_FALLBACK"
            };

            var region1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, HasDimensionText = true,
                DetectionMethod = BeamDetectionMethod.PairedLines, DiagnosticId = "REGION_1"
            };

            var region2 = new CadBeamData
            {
                StartX = 3000, StartY = 0, EndX = 6000, EndY = 0,
                Width = 600, Height = 600, HasDimensionText = true,
                DetectionMethod = BeamDetectionMethod.PairedLines, DiagnosticId = "REGION_2"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { longIncomplete, region1, region2 });

            Assert.Equal(2, resolved.Count);
            Assert.DoesNotContain(resolved, b => b.DiagnosticId == "LONG_FALLBACK");
            Assert.Contains(resolved, b => b.DiagnosticId == "REGION_1");
            Assert.Contains(resolved, b => b.DiagnosticId == "REGION_2");
        }

        [Fact]
        public void Test20_TwoDimensionRegionsDifferentSizes_KeepsBoth()
        {
            var r1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 1500, EndY = 0, Width = 400, Height = 550, HasDimensionText = true
            };
            var r2 = new CadBeamData
            {
                StartX = 1500, StartY = 0, EndX = 3000, EndY = 0, Width = 600, Height = 550, HasDimensionText = true
            };

            var resolved = _resolver.ResolveOverlaps(new[] { r1, r2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test21_PartialOverlapLessThan80Percent_KeepsBoth()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600
            };
            var b2 = new CadBeamData
            {
                StartX = 800, StartY = 0, EndX = 1800, EndY = 0, Width = 400, Height = 600 // 20% overlap
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test22_EnvelopeOverlapButAngleOutsideTolerance_KeepsBoth()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600
            };

            // 5 degrees rotation
            double angleRad = 5.0 * Math.PI / 180.0;
            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000 * Math.Cos(angleRad), EndY = 3000 * Math.Sin(angleRad), Width = 400, Height = 600
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test23_EveryFinalCandidateHasRootRawCandidateIds()
        {
            BeamDiagnosticCollector.Instance.StartSession();

            var raw1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, DiagnosticId = "RAW_1", RootRawCandidateIds = new List<string> { "RAW_1" } };
            var raw2 = new CadBeamSegment { StartX = 3000, StartY = 0, EndX = 6000, EndY = 0, Width = 400, Height = 600, DiagnosticId = "RAW_2", RootRawCandidateIds = new List<string> { "RAW_2" } };

            var pipeline = new BeamCadPipeline();
            var finals = pipeline.ProcessPipeline(new[] { raw1, raw2 });

            BeamDiagnosticCollector.Instance.CompleteSession();

            Assert.NotEmpty(finals);
            foreach (var f in finals)
            {
                Assert.NotNull(f.RootRawCandidateIds);
                Assert.NotEmpty(f.RootRawCandidateIds);
            }
        }

        [Fact]
        public void Test24_SuppressionDiagnosticMetricsAreNonZero()
        {
            var collector = BeamDiagnosticCollector.Instance;
            var session = collector.StartSession();

            var paired = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 8000, EndY = 0,
                Width = 400, Height = 600, HasDimensionText = true,
                DetectionMethod = BeamDetectionMethod.PairedLines, DiagnosticId = "PAIRED_01"
            };

            var fallback = new CadBeamData
            {
                StartX = 0, StartY = 200, EndX = 8000, EndY = 200,
                Width = 400, Height = 600, HasDimensionText = false,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback, DiagnosticId = "FALLBACK_01"
            };

            _resolver.ResolveOverlaps(new[] { paired, fallback });
            collector.CompleteSession();

            var suppressedEntry = session.Entries.FirstOrDefault(e => e.Stage == BeamDiagnosticStage.OverlapDecision && e.Action == BeamDiagnosticAction.Suppressed);

            Assert.NotNull(suppressedEntry);
            Assert.Equal("PAIRED_01", suppressedEntry.WinnerDiagnosticId);
            Assert.Equal("FALLBACK_01", suppressedEntry.LoserDiagnosticId);
            Assert.True(suppressedEntry.CenterlineDistanceMm > 0, "CenterlineDistanceMm must be non-zero");
            Assert.True(suppressedEntry.OverlapRatio > 0, "OverlapRatio must be non-zero");
            Assert.True(suppressedEntry.PriorityScore > 0, "PriorityScore must be non-zero");
            Assert.True(suppressedEntry.CompetingPriorityScore > 0, "CompetingPriorityScore must be non-zero");
        }

        [Fact]
        public void Test25_OutputDeterministicWhenShuffled()
        {
            var c1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, DiagnosticId = "C1" };
            var c2 = new CadBeamData { StartX = 3000, StartY = 0, EndX = 6000, EndY = 0, Width = 400, Height = 600, DiagnosticId = "C2" };
            var c3 = new CadBeamData { StartX = 0, StartY = 200, EndX = 3000, EndY = 200, Width = 400, Height = 0, DetectionMethod = BeamDetectionMethod.SingleLineFallback, DiagnosticId = "C3" };

            var list = new List<CadBeamData> { c1, c2, c3 };
            var baseResult = _resolver.ResolveOverlaps(list).Select(b => b.DiagnosticId).ToList();

            var rng = new Random(42);
            for (int i = 0; i < 10; i++)
            {
                var shuffled = list.OrderBy(_ => rng.Next()).ToList();
                var result = _resolver.ResolveOverlaps(shuffled).Select(b => b.DiagnosticId).ToList();
                Assert.Equal(baseResult, result);
            }
        }
    }
}
