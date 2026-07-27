using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
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
        // FINDING 1: OVERLAP & GAP & TOUCHING TESTS
        // ==========================================

        [Fact]
        public void Finding1_Overlap50mm_MinimumOverlap200mm_ReturnsTwoChains()
        {
            // Overlap = 50mm < MinimumOverlapMm (200mm)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 950, StartY = 0, EndX = 1950, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Equal(2, chains.Count);
        }

        [Fact]
        public void Finding1_Overlap250mm_MinimumOverlap200mm_ReturnsOneChain()
        {
            // Overlap = 250mm >= MinimumOverlapMm (200mm)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 750, StartY = 0, EndX = 1750, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(1750, chains[0].EndX);
        }

        [Fact]
        public void Finding1_TouchingEndpoints_ReturnsOneChain()
        {
            // Gap = 0, Overlap = 0 -> Touching endpoints
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(2000, chains[0].EndX);
        }

        [Fact]
        public void Finding1_GapSmallerThanTolerance_ReturnsOneChain()
        {
            // Gap = 50mm <= EndpointGapToleranceMm (100mm)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1050, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(2000, chains[0].EndX);
        }

        // ==========================================
        // FINDING 2: CUMULATIVE DRIFT PREVENTION
        // ==========================================

        [Fact]
        public void Finding2_CumulativeAngularDrift_0_2_4_Deg_DoesNotFormSingleChain()
        {
            // Seg1 at 0°, Seg2 at 2°, Seg3 at 4° (Total drift 4° > 2.5° tolerance)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            // 2° angle: dx=1000, dy=34.92
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 34.92, Width = 400, Height = 600 };
            // 4° angle: dx=1000, dy=69.92
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 34.92, EndX = 3000, EndY = 104.84, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2, seg3 });

            // Must split because 0° and 4° differ by 4° > 2.5°
            Assert.True(chains.Count > 1);
        }

        [Fact]
        public void Finding2_CumulativeLateralDrift_y0_y25_y50_DoesNotFormSingleChain()
        {
            // Seg1 at y=0, Seg2 at y=25, Seg3 at y=50 (Total lateral spread 50mm > 30mm tolerance)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 25, EndX = 2000, EndY = 25, Width = 400, Height = 600 };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 50, EndX = 3000, EndY = 50, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2, seg3 });

            // Must split because y=0 and y=50 differ by 50mm > 30mm
            Assert.True(chains.Count > 1);
        }

        [Fact]
        public void Finding2_Diagonal45Deg_ThreeStraightSegments_FormsSingleChain()
        {
            // 3 truly straight segments along 45° line: (0,0)->(1000,1000)->(2000,2000)->(3000,3000)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 1000, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 1000, EndX = 2000, EndY = 2000, Width = 400, Height = 600 };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 2000, EndX = 3000, EndY = 3000, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2, seg3 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(0, chains[0].StartY);
            Assert.Equal(3000, chains[0].EndX);
            Assert.Equal(3000, chains[0].EndY);
        }

        // ==========================================
        // FINDING 3: INPUT ORDER INDEPENDENCE & METADATA
        // ==========================================

        [Fact]
        public void Finding3_ShuffledInputOrder_YieldsIdenticalResultsAndMetadata()
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
            Assert.Equal(run1[0].Mark, run3[0].Mark);

            // Ordered segments in chain must be strictly sorted by projection
            Assert.Equal(seg1.StartX, run1[0].Segments[0].StartX);
            Assert.Equal(seg2.StartX, run1[0].Segments[1].StartX);
            Assert.Equal(seg3.StartX, run1[0].Segments[2].StartX);

            Assert.Equal(seg1.StartX, run2[0].Segments[0].StartX);
            Assert.Equal(seg2.StartX, run2[0].Segments[1].StartX);
            Assert.Equal(seg3.StartX, run2[0].Segments[2].StartX);
        }

        // ==========================================
        // ADDITIONAL TESTS: DUPLICATES, BRANCHING, DIRECTION, NULL/ZERO
        // ==========================================

        [Fact]
        public void Additional_ExactDuplicateSegments_HandledStably()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg1Duplicate = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg1Duplicate });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(1000, chains[0].EndX);
            Assert.Equal(2, chains[0].Segments.Count);
        }

        [Fact]
        public void Additional_BranchingCandidate_DoesNotMergePerpendicularBranch()
        {
            // Main beam S1 + S2, perpendicular branch S3 at T-junction (x=1000)
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
        public void Additional_Diagonal30DegChain_FormsOneChain()
        {
            // 30° angle: cos(30°)=0.866025, sin(30°)=0.500000
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 866.025, EndY = 500, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 866.025, StartY = 500, EndX = 1732.05, EndY = 1000, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(0, chains[0].StartY);
            Assert.Equal(1732.05, chains[0].EndX);
            Assert.Equal(1000, chains[0].EndY);
        }

        [Fact]
        public void Additional_ReversedEndpoints_FormsOneChainCorrectly()
        {
            // Seg2 has reversed Start/End points (StartX=2000, EndX=1000)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(2000, chains[0].EndX);
        }

        [Fact]
        public void Additional_NullAndZeroLengthSegments_FilteredOutGracefully()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            CadBeamSegment nullSeg = null;
            var zeroSeg = new CadBeamSegment { StartX = 500, StartY = 0, EndX = 500, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, nullSeg, zeroSeg });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(1000, chains[0].EndX);
            Assert.Single(chains[0].Segments);
        }
    }
}
