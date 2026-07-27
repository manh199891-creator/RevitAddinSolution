using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
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
    }
}
