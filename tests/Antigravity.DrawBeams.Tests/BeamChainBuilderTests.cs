using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class BeamChainBuilderTests
    {
        private readonly BeamChainBuilder _builder;
        private readonly BeamContinuityOptions _options;

        public BeamChainBuilderTests()
        {
            _options = new BeamContinuityOptions
            {
                AngularToleranceDegrees = 2.5,
                EndpointGapToleranceMm = 100,
                LateralOffsetToleranceMm = 30,
                WidthToleranceRatio = 0.20,
                MinimumOverlapMm = 200
            };
            _builder = new BeamChainBuilder(_options);
        }

        // ==========================================
        // PHASE 2 REQUIRED TESTS (12 SCENARIOS)
        // ==========================================

        [Fact]
        public void Phase2_Req1_ThreeConsecutiveStraightSegments_FormsOneLongBeamData()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, IsPaired = true };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600, IsPaired = true };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, IsPaired = true };

            var chains = _builder.BuildChains(new[] { seg1, seg2, seg3 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(3000, chains[0].EndX);
            Assert.Equal(400, chains[0].Width);
            Assert.Equal(600, chains[0].Height);
        }

        [Fact]
        public void Phase2_Req2_ValidSmallGap_StillConnects()
        {
            // Gap = 50mm <= EndpointGapToleranceMm (100mm)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1050, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(2000, chains[0].EndX);
        }

        [Fact]
        public void Phase2_Req3_LargeGap_Splits()
        {
            // Gap = 150mm > EndpointGapToleranceMm (100mm)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1150, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Equal(2, chains.Count);
        }

        [Fact]
        public void Phase2_Req4_SameSize_400x600_To_400x600_FormsOneBeam()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(400, chains[0].Width);
            Assert.Equal(600, chains[0].Height);
        }

        [Fact]
        public void Phase2_Req5_SizeChange_400x600_To_400x700_SplitsIntoTwoBeams()
        {
            // Height changes from 600 -> 700
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 700 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Equal(2, chains.Count);
            Assert.Equal(600, chains.First(c => c.StartX == 0).Height);
            Assert.Equal(700, chains.First(c => c.StartX == 1000).Height);
        }

        [Fact]
        public void Phase2_Req6_RepeatedTextSameSize_DoesNotSplit()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, TextContent = "D1 400x600" };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600, TextContent = "D1 400x600" };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(2000, chains[0].EndX);
        }

        [Fact]
        public void Phase2_Req7_TJunction_DoesNotMergePerpendicularBranch()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };
            var segBranch = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 1000, EndY = 1000, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2, segBranch });

            Assert.Equal(2, chains.Count);
            var mainChain = chains.First(c => c.Segments.Count == 2);
            var branchChain = chains.First(c => c.Segments.Count == 1);

            Assert.Equal(0, mainChain.StartX);
            Assert.Equal(2000, mainChain.EndX);
            Assert.Equal(1000, branchChain.StartX);
            Assert.Equal(1000, branchChain.EndY);
        }

        [Fact]
        public void Phase2_Req8_TwoParallelAxesCloseToEachOther_DoesNotMerge()
        {
            // Lateral offset = 50mm > LateralOffsetToleranceMm (30mm)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 0, StartY = 50, EndX = 1000, EndY = 50, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Equal(2, chains.Count);
        }

        [Fact]
        public void Phase2_Req9_ShuffledInputSegmentOrder_StableOutput()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, Mark = "B1", Confidence = 100 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600, Mark = "B1", Confidence = 200 };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Mark = "B1", Confidence = 150 };

            var run1 = _builder.BuildChains(new[] { seg1, seg2, seg3 });
            var run2 = _builder.BuildChains(new[] { seg3, seg1, seg2 });
            var run3 = _builder.BuildChains(new[] { seg2, seg3, seg1 });

            Assert.Single(run1);
            Assert.Single(run2);
            Assert.Single(run3);

            Assert.Equal(run1[0].StartX, run2[0].StartX);
            Assert.Equal(run1[0].EndX, run2[0].EndX);
            Assert.Equal(run1[0].Width, run2[0].Width);
            Assert.Equal(run1[0].Height, run2[0].Height);
            Assert.Equal(run1[0].Mark, run2[0].Mark);

            Assert.Equal(run1[0].StartX, run3[0].StartX);
            Assert.Equal(run1[0].EndX, run3[0].EndX);
        }

        [Fact]
        public void Phase2_Req10_DoesNotCreateDuplicateCadBeamData()
        {
            // Exact duplicate segments merge into a single chain (1 beam)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg1Duplicate = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg1Duplicate });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(1000, chains[0].EndX);
            Assert.Equal(2, chains[0].Segments.Count);
        }

        [Fact]
        public void Phase2_Req11_Regression_SingleLineBeam()
        {
            // Single-line fallback beam
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 2500, EndY = 0, Width = 300, Height = 500, IsPaired = false, Confidence = 100 };

            var chains = _builder.BuildChains(new[] { seg1 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(2500, chains[0].EndX);
            Assert.Equal(300, chains[0].Width);
            Assert.Equal(500, chains[0].Height);
            Assert.False(chains[0].Segments[0].IsPaired);
        }

        [Fact]
        public void Phase2_Req12_Regression_PairedLineBeam()
        {
            // Standard paired line beam candidate
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 4000, EndY = 0, Width = 400, Height = 700, MeasuredWidth = 400, IsPaired = true, Confidence = 900 };

            var chains = _builder.BuildChains(new[] { seg1 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(4000, chains[0].EndX);
            Assert.Equal(400, chains[0].Width);
            Assert.Equal(700, chains[0].Height);
            Assert.True(chains[0].Segments[0].IsPaired);
        }

        // ==========================================
        // MAJOR 1: CONTAINMENT VS DUPLICATE TESTS
        // ==========================================

        [Fact]
        public void Major1_ContainedShortSegment_OverlapLessThanMinimumOverlap_ReturnsTwoChains()
        {
            var segA = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 5000, EndY = 0, Width = 400, Height = 600 };
            var segB = new CadBeamSegment { StartX = 200, StartY = 0, EndX = 300, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { segA, segB });

            Assert.Equal(2, chains.Count);
        }

        [Fact]
        public void Major1_Regression_ExactDuplicateSegments_ReturnsOneChain()
        {
            var segA = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var segB = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { segA, segB });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(1000, chains[0].EndX);
            Assert.Equal(2, chains[0].Segments.Count);
        }

        // ==========================================
        // MINOR 1: CANONICAL CHAIN AXIS & REVERSED SEGMENTS
        // ==========================================

        [Fact]
        public void Minor1_AllReversedRightToLeftSegments_FormsCanonicalChain()
        {
            var seg1 = new CadBeamSegment { StartX = 3000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg3 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 0, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2, seg3 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(3000, chains[0].EndX);

            Assert.Equal(3, chains[0].Segments.Count);
            Assert.Equal(0, Math.Min(chains[0].Segments[0].StartX, chains[0].Segments[0].EndX));
            Assert.Equal(1000, Math.Min(chains[0].Segments[1].StartX, chains[0].Segments[1].EndX));
            Assert.Equal(2000, Math.Min(chains[0].Segments[2].StartX, chains[0].Segments[2].EndX));
        }
    }
}
