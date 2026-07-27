using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class BeamChainBuilderPhase1Tests
    {
        private readonly BeamChainBuilder _builder;
        private readonly BeamContinuityOptions _options;

        public BeamChainBuilderPhase1Tests()
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

        [Fact]
        public void Phase1_CumulativeAngularDrift_RejectsSmallCumulativeAngleExtendingBeyondTolerance()
        {
            // Each segment pair has 1.5 deg difference (<= 2.5 deg), but total angle diff is 4.5 deg (> 2.5 deg)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0 }; // 0 deg
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 26.18 }; // 1.5 deg
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 26.18, EndX = 3000, EndY = 78.54 }; // 3.0 deg
            var seg4 = new CadBeamSegment { StartX = 3000, StartY = 78.54, EndX = 4000, EndY = 157.08 }; // 4.5 deg

            var chains = _builder.BuildChains(new[] { seg1, seg2, seg3, seg4 });

            // Chain builder rejects seg4 because pair angle diff (seg1 vs seg4) exceeds AngularToleranceDegrees
            Assert.True(chains.Count > 1);
        }

        [Fact]
        public void Phase1_CumulativeLateralDrift_RejectsCumulativeOffsetBeyondTolerance()
        {
            // Lateral offset exceeds LateralOffsetToleranceMm (30mm)
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 20, EndX = 2000, EndY = 20 };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 40, EndX = 3000, EndY = 40 };

            var chains = _builder.BuildChains(new[] { seg1, seg2, seg3 });

            // seg1 to seg3 total lateral offset = 40mm > 30mm -> cannot be in 1 chain
            Assert.True(chains.Count > 1);
        }

        [Fact]
        public void Phase1_OverlapMinimum_OverlapLessThanMinimum_ReturnsTwoChains()
        {
            var segA = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var segB = new CadBeamSegment { StartX = 950, StartY = 0, EndX = 1950, EndY = 0, Width = 400, Height = 600 }; // 50mm overlap < 200mm

            var chains = _builder.BuildChains(new[] { segA, segB });

            Assert.Equal(2, chains.Count);
        }

        [Fact]
        public void Phase1_OverlapMinimum_OverlapGreaterThanMinimum_ReturnsOneChain()
        {
            var segA = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var segB = new CadBeamSegment { StartX = 750, StartY = 0, EndX = 1750, EndY = 0, Width = 400, Height = 600 }; // 250mm overlap > 200mm

            var chains = _builder.BuildChains(new[] { segA, segB });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(1750, chains[0].EndX);
        }

        [Fact]
        public void Phase1_ContainmentVsDuplicate_ContainedNonDuplicate_RequiresMinimumOverlap()
        {
            var segA = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 5000, EndY = 0, Width = 400, Height = 600 };
            var segB = new CadBeamSegment { StartX = 200, StartY = 0, EndX = 300, EndY = 0, Width = 400, Height = 600 }; // 100mm overlap < 200mm

            var chains = _builder.BuildChains(new[] { segA, segB });

            Assert.Equal(2, chains.Count);
        }

        [Fact]
        public void Phase1_ContainmentVsDuplicate_ExactDuplicate_ReturnsOneChain()
        {
            var segA = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var segB = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { segA, segB });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(1000, chains[0].EndX);
        }

        [Fact]
        public void Phase1_ReversedEndpoints_SingleSegmentReversed_FormsCorrectChain()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 }; // Reversed (2000 -> 1000)

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(2000, chains[0].EndX);
        }

        [Fact]
        public void Phase1_AllReversedEndpoints_FormsCanonicalChain()
        {
            var seg1 = new CadBeamSegment { StartX = 3000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg3 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 0, EndY = 0, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2, seg3 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(3000, chains[0].EndX);
        }

        [Fact]
        public void Phase1_DiagonalBeams_30Degrees_FormsCorrectChain()
        {
            double rad = Math.PI / 6.0; // 30 deg
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000 * cos, EndY = 1000 * sin, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000 * cos, StartY = 1000 * sin, EndX = 2000 * cos, EndY = 2000 * sin, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX, 2);
            Assert.Equal(0, chains[0].StartY, 2);
            Assert.Equal(2000 * cos, chains[0].EndX, 2);
            Assert.Equal(2000 * sin, chains[0].EndY, 2);
        }

        [Fact]
        public void Phase1_DiagonalBeams_45Degrees_FormsCorrectChain()
        {
            double rad = Math.PI / 4.0; // 45 deg
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000 * cos, EndY = 1000 * sin, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000 * cos, StartY = 1000 * sin, EndX = 2000 * cos, EndY = 2000 * sin, Width = 400, Height = 600 };

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX, 2);
            Assert.Equal(0, chains[0].StartY, 2);
            Assert.Equal(2000 * cos, chains[0].EndX, 2);
            Assert.Equal(2000 * sin, chains[0].EndY, 2);
        }

        [Fact]
        public void Phase1_ShuffledInputOrder_ProducesStableOutput()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600 };

            var run1 = _builder.BuildChains(new[] { seg1, seg2, seg3 });
            var run2 = _builder.BuildChains(new[] { seg3, seg1, seg2 });

            Assert.Single(run1);
            Assert.Single(run2);
            Assert.Equal(run1[0].StartX, run2[0].StartX);
            Assert.Equal(run1[0].EndX, run2[0].EndX);
        }

        [Fact]
        public void Phase1_NullOrZeroLengthSegments_HandledGracefully()
        {
            var segValid = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var segZero = new CadBeamSegment { StartX = 500, StartY = 0, EndX = 500, EndY = 0 };

            var chains = _builder.BuildChains(new CadBeamSegment[] { null, segValid, segZero });

            Assert.Single(chains);
            Assert.Equal(0, chains[0].StartX);
            Assert.Equal(1000, chains[0].EndX);
        }

        [Fact]
        public void Phase1_ParallelDifferentAxes_SeparatedIntoMultipleChains()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 0, StartY = 100, EndX = 1000, EndY = 100, Width = 400, Height = 600 }; // Lateral offset = 100mm > 30mm

            var chains = _builder.BuildChains(new[] { seg1, seg2 });

            Assert.Equal(2, chains.Count);
        }
    }
}
