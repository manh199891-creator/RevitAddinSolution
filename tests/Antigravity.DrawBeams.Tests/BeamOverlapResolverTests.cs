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

        // --- 15 PROMPT TEST CASES ---

        [Fact]
        public void Test1_RuleA_PairedVsFallback_EdgeEnvelope_SuppressFallback()
        {
            var paired = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 8000, EndY = 0,
                Width = 400, Height = 600, HasDimensionText = true,
                DetectionMethod = BeamDetectionMethod.PairedLines, DiagnosticId = "PAIRED_01",
                SourceLineIds = new HashSet<string> { "LINE_1", "LINE_2" }
            };

            var fallback = new CadBeamData
            {
                StartX = 0, StartY = 200, EndX = 8000, EndY = 200, // On edge of 400 wide paired beam
                Width = 400, Height = 600, HasDimensionText = false,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback, DiagnosticId = "FALLBACK_01"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { paired, fallback });

            Assert.Single(resolved);
            Assert.Equal("PAIRED_01", resolved[0].DiagnosticId);
        }

        [Fact]
        public void Test2_RuleB_TwoPairedLines_Parallel300mm_IndependentSourceLines_KeepBoth()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "LINE_A", "LINE_B" }, DiagnosticId = "BEAM_1"
            };

            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 300, EndX = 3000, EndY = 300,
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "LINE_C", "LINE_D" }, DiagnosticId = "BEAM_2"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
            Assert.Contains(resolved, b => b.DiagnosticId == "BEAM_1");
            Assert.Contains(resolved, b => b.DiagnosticId == "BEAM_2");
        }

        [Fact]
        public void Test3_RuleB_TwoPairedLines_DifferentWidths_350mm_KeepBoth()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "LINE_A", "LINE_B" }, DiagnosticId = "BEAM_400"
            };

            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 350, EndX = 3000, EndY = 350,
                Width = 400, Height = 700, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "LINE_C", "LINE_D" }, DiagnosticId = "BEAM_700"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test4_RuleC_TwoCompleteDimensionedBeams_SameEnvelope_KeepBoth()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, HasDimensionText = true, TextContent = "B1 (40X60)",
                DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "LINE_A1", "LINE_A2" }, DiagnosticId = "B1"
            };

            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 150, EndX = 3000, EndY = 150,
                Width = 400, Height = 600, HasDimensionText = true, TextContent = "B2 (40X60)",
                DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "LINE_B1", "LINE_B2" }, DiagnosticId = "B2"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test5_ExactDuplicate_PairedLines_KeepOne()
        {
            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500, DiagnosticId = "B1" };
            var b2 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500, DiagnosticId = "B2" };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Single(resolved);
        }

        [Fact]
        public void Test6_ReversedExactDuplicate_PairedLines_KeepOne()
        {
            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500, DiagnosticId = "B1" };
            var b2 = new CadBeamData { StartX = 3000, StartY = 0, EndX = 0, EndY = 0, Width = 400, Height = 600, Confidence = 500, DiagnosticId = "B2" };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Single(resolved);
        }

        [Fact]
        public void Test7_SharedSourceLineIds_KeepHigherPriority()
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
            Assert.Equal(BeamDetectionMethod.PairedLines, resolved[0].DetectionMethod);
        }

        [Fact]
        public void Test8_IncompleteFallback_200x0_InsideEnvelope_400x700_SuppressIncomplete()
        {
            var complete = new CadBeamData
            {
                StartX = 100120, StartY = 0, EndX = 100120, EndY = 8000,
                Width = 400, Height = 700, HasDimensionText = true, Confidence = 500,
                DetectionMethod = BeamDetectionMethod.PairedLines, DiagnosticId = "COMPLETE_400x700"
            };

            var incomplete = new CadBeamData
            {
                StartX = 100230, StartY = 0, EndX = 100230, EndY = 8000,
                Width = 200, Height = 0, HasDimensionText = false, Confidence = 10000,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback, DiagnosticId = "FALLBACK_200x0"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { complete, incomplete });

            Assert.Single(resolved);
            Assert.Equal("COMPLETE_400x700", resolved[0].DiagnosticId);
        }

        [Fact]
        public void Test9_LongIncomplete_CoveringTwoDimensionedRegions_SameLineage_SuppressLong()
        {
            var longFallback = new CadBeamData
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

            var resolved = _resolver.ResolveOverlaps(new[] { longFallback, region1, region2 });

            Assert.Equal(2, resolved.Count);
            Assert.DoesNotContain(resolved, b => b.DiagnosticId == "LONG_FALLBACK");
            Assert.Contains(resolved, b => b.DiagnosticId == "REGION_1");
            Assert.Contains(resolved, b => b.DiagnosticId == "REGION_2");
        }

        [Fact]
        public void Test10_LongIncomplete_NearTwoIndependentBeams_KeepAll()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L1", "L2" }, DiagnosticId = "B1"
            };

            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 500, EndX = 3000, EndY = 500,
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L3", "L4" }, DiagnosticId = "B2"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test11_PartialOverlap_LessThan80Percent_KeepBoth()
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
        public void Test12_ShuffledInput_DeterministicOutput()
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

        [Fact]
        public void Test13_SuppressionDiagnosticMetrics_NonZeroAndTraceable()
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

            var entries = session.Entries.ToList();
            var suppressedEntry = entries.FirstOrDefault(e => e.Stage == BeamDiagnosticStage.OverlapDecision && e.Action == BeamDiagnosticAction.Suppressed);

            Assert.NotNull(suppressedEntry);
            Assert.Equal("PAIRED_01", suppressedEntry.WinnerDiagnosticId);
            Assert.Equal("FALLBACK_01", suppressedEntry.LoserDiagnosticId);
            Assert.True(suppressedEntry.CenterlineDistanceMm > 0, "CenterlineDistanceMm must be non-zero");
            Assert.True(suppressedEntry.OverlapRatio > 0, "OverlapRatio must be non-zero");
        }

        [Fact]
        public void Test14_Regression_BorderBeams_NoDuplicates()
        {
            var borderPaired = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 12000, EndY = 0,
                Width = 400, Height = 700, HasDimensionText = true,
                DetectionMethod = BeamDetectionMethod.PairedLines, DiagnosticId = "BORDER_PAIRED"
            };

            var borderFallback = new CadBeamData
            {
                StartX = 0, StartY = 180, EndX = 12000, EndY = 180,
                Width = 400, Height = 700, HasDimensionText = false,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback, DiagnosticId = "BORDER_FALLBACK"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { borderPaired, borderFallback });

            Assert.Single(resolved);
            Assert.Equal("BORDER_PAIRED", resolved[0].DiagnosticId);
        }

        [Fact]
        public void Test15_Regression_InteriorBeams_Preserved()
        {
            var borderBeam = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 12000, EndY = 0,
                Width = 400, Height = 700, HasDimensionText = true,
                SourceLineIds = new HashSet<string> { "BORDER_1", "BORDER_2" }, DiagnosticId = "BORDER"
            };

            var interiorBeam1 = new CadBeamData
            {
                StartX = 0, StartY = 3000, EndX = 12000, EndY = 3000,
                Width = 220, Height = 0, HasDimensionText = false, MeasuredWidth = 220,
                DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "INT1_1", "INT1_2" }, DiagnosticId = "INT_1"
            };

            var interiorBeam2 = new CadBeamData
            {
                StartX = 0, StartY = 6000, EndX = 12000, EndY = 6000,
                Width = 300, Height = 0, HasDimensionText = false, MeasuredWidth = 300,
                DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "INT2_1", "INT2_2" }, DiagnosticId = "INT_2"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { borderBeam, interiorBeam1, interiorBeam2 });

            Assert.Equal(3, resolved.Count);
            Assert.Contains(resolved, b => b.DiagnosticId == "BORDER");
            Assert.Contains(resolved, b => b.DiagnosticId == "INT_1");
            Assert.Contains(resolved, b => b.DiagnosticId == "INT_2");
        }
    }
}
