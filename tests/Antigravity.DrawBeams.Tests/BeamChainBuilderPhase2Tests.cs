using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class BeamChainBuilderPhase2Tests
    {
        private readonly BeamChainBuilder _builder;
        private readonly BeamContinuityOptions _options;

        public BeamChainBuilderPhase2Tests()
        {
            _options = new BeamContinuityOptions
            {
                AngularToleranceDegrees = 2.5,
                EndpointGapToleranceMm = 100,
                LateralOffsetToleranceMm = 30,
                WidthToleranceRatio = 0.20,
                MinimumOverlapMm = 200,
                JunctionToleranceMm = 50.0
            };
            _builder = new BeamChainBuilder(_options);
        }

        [Fact]
        public void Phase2_TJunction_SplitsMainChainAtJunctionNode()
        {
            // Horizontal A: 0 -> 1000
            var segA = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            // Horizontal B: 1000 -> 2000
            var segB = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };
            // Vertical C: (1000,0) -> (1000,1000)
            var segC = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 1000, EndY = 1000, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { segA, segB, segC });

            // Expected: 3 chains (horizontal left 0->1000, horizontal right 1000->2000, vertical)
            // NO horizontal chain spanning 0->2000!
            Assert.Equal(3, chains.Count);
            Assert.False(chains.Any(c => c.StartX == 0 && c.EndX == 2000));

            var leftChain = chains.First(c => Math.Abs(c.StartX - 0) < 1e-3 && Math.Abs(c.EndX - 1000) < 1e-3);
            var rightChain = chains.First(c => Math.Abs(c.StartX - 1000) < 1e-3 && Math.Abs(c.EndX - 2000) < 1e-3);
            var vertChain = chains.First(c => Math.Abs(c.StartX - 1000) < 1e-3 && Math.Abs(c.EndY - 1000) < 1e-3);

            Assert.NotNull(leftChain);
            Assert.NotNull(rightChain);
            Assert.NotNull(vertChain);
        }

        [Fact]
        public void Phase2_CrossingAtMiddleOfSegment_SplitsSegmentAtIntersection()
        {
            // Single long horizontal segment (0,0) -> (2000,0)
            var segLong = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };
            // Crossing vertical segment (1000,-1000) -> (1000,1000)
            var segCross = new CadBeamSegment { StartX = 1000, StartY = -1000, EndX = 1000, EndY = 1000, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { segLong, segCross });

            // Long segment splits at intersection x=1000 into 2 sub-chains + 1 vertical chain
            Assert.True(chains.Count >= 3);
            Assert.False(chains.Any(c => c.StartX == 0 && c.EndX == 2000));
        }

        [Fact]
        public void Phase2_JunctionNearEndpointWithinTolerance_SplitsAtJunction()
        {
            // Endpoint near x=1000 within 50mm tolerance
            var segA = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 990, EndY = 0, Width = 400, Height = 600 };
            var segB = new CadBeamSegment { StartX = 1010, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };
            var segC = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 1000, EndY = 1000, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { segA, segB, segC });

            Assert.Equal(3, chains.Count);
            Assert.False(chains.Any(c => c.StartX == 0 && c.EndX == 2000));
        }

        [Fact]
        public void Phase2_ParallelLineTouchingEndpoint_DoesNotSplit()
        {
            // Parallel line touching at endpoint (NOT a perpendicular junction)
            var segA = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var segB = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };
            var segParallel = new CadBeamSegment { StartX = 1000, StartY = 10, EndX = 2000, EndY = 10, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { segA, segB, segParallel });

            // Parallel line does NOT split A and B -> A and B merge into 0 -> 2000
            Assert.True(chains.Any(c => c.StartX == 0 && c.EndX == 2000));
        }

        [Fact]
        public void Phase2_ThreeConsecutiveStraightSegments_FormsOneLongChain()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2, seg3 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(3000, chains[0].EndX);
        }

        [Fact]
        public void Phase2_ValidSmallGap_StillConnects()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1050, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(2000, chains[0].EndX);
        }

        [Fact]
        public void Phase2_LargeGap_Splits()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1150, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Equal(2, chains.Count);
        }
    }
}
