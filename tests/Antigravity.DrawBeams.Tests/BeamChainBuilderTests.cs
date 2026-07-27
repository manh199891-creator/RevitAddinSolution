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

        public BeamChainBuilderTests()
        {
            var options = new BeamContinuityOptions
            {
                AngularToleranceDegrees = 2.5,
                EndpointGapToleranceMm = 100,
                LateralOffsetToleranceMm = 30,
                WidthToleranceRatio = 0.20,
                MinimumOverlapMm = 200
            };
            _builder = new BeamChainBuilder(options);
        }

        [Fact]
        public void Test1_TwoCollinearSegments_ConnectedEndpoints_SameWidth_OneChain()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(2000, chains[0].EndX);
            Assert.Equal(2, chains[0].Segments.Count);
        }

        [Fact]
        public void Test2_ThreeConsecutiveCollinearSegments_OneChain()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2, seg3 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(3000, chains[0].EndX);
            Assert.Equal(3, chains[0].Segments.Count);
        }

        [Fact]
        public void Test3_TwoSegments_GapSmallerThanTolerance_OneChain()
        {
            // Gap = 50mm <= 100mm
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1050, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(2000, chains[0].EndX);
        }

        [Fact]
        public void Test4_TwoSegments_GapLargerThanTolerance_TwoChains()
        {
            // Gap = 150mm > 100mm
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1150, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Equal(2, chains.Count);
        }

        [Fact]
        public void Test5_AngularDeviationExceedingTolerance_TwoChains()
        {
            // Angle diff ~ 5.7 degrees > 2.5 degrees
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 100, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Equal(2, chains.Count);
        }

        [Fact]
        public void Test6_WidthChangeExceedingTolerance_TwoChains()
        {
            // Width changes 400 -> 700 (ratio (700-400)/700 = 0.42 > 0.20)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 700, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Equal(2, chains.Count);
        }

        [Fact]
        public void Test7_ParallelSegmentsOffsetToAnotherAxis_NoJoin()
        {
            // Lateral offset = 50mm > 30mm tolerance
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 0, StartY = 50, EndX = 1000, EndY = 50, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Equal(2, chains.Count);
        }

        [Fact]
        public void Test8_PartiallyOverlappingSegments_HandledStably_NoDuplicates()
        {
            // Overlapping from x=500 to x=1000
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 500, StartY = 0, EndX = 1500, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(1500, chains[0].EndX);
            Assert.Equal(2, chains[0].Segments.Count);
        }

        [Fact]
        public void Test9_ShuffledInputOrder_ConsistentChainOutput()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600 };

            // Reversed input order: seg3, seg1, seg2
            var chains = _builder.BuildChains(new[] { seg3, seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(3000, chains[0].EndX);
        }

        [Fact]
        public void Test10_ReversedEndpointDirections_StillJoinsCorrectly()
        {
            // seg2 direction is backwards (StartX=2000, EndX=1000)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(2000, chains[0].EndX);
        }
    }
}
