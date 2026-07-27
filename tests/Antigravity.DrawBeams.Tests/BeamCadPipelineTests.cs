using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class BeamCadPipelineTests
    {
        private readonly BeamCadPipeline _pipeline;

        public BeamCadPipelineTests()
        {
            var options = new BeamContinuityOptions
            {
                AngularToleranceDegrees = 2.5,
                EndpointGapToleranceMm = 100,
                LateralOffsetToleranceMm = 30,
                WidthToleranceRatio = 0.20,
                MinimumOverlapMm = 200,
                JunctionToleranceMm = 50.0
            };
            _pipeline = new BeamCadPipeline(options);
        }

        [Fact]
        public void Pipeline_ThreeStraightSegments_ProducesOneLongCadBeamData()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, IsPaired = true, MeasuredWidth = 400, Confidence = 500, TextContent = "D1 400x600" };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600, IsPaired = true, MeasuredWidth = 400, Confidence = 500, TextContent = "D1 400x600" };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, IsPaired = true, MeasuredWidth = 400, Confidence = 500, TextContent = "D1 400x600" };

            var beams = _pipeline.ProcessPipeline(new[] { seg1, seg2, seg3 });

            Assert.Single(beams);
            Assert.Equal(0, beams[0].StartX);
            Assert.Equal(3000, beams[0].EndX);
            Assert.Equal(400, beams[0].Width);
            Assert.Equal(600, beams[0].Height);
            Assert.Equal("D1", beams[0].Mark);
            Assert.True(beams[0].IsPaired);
        }

        [Fact]
        public void Pipeline_400x600_To_400x700_ProducesTwoCadBeamData()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, Confidence = 500, TextContent = "D1 400x600" };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 700, Confidence = 500, TextContent = "D2 400x700" };

            var beams = _pipeline.ProcessPipeline(new[] { seg1, seg2 });

            Assert.Equal(2, beams.Count);
            var beam1 = beams.First(b => Math.Abs(b.StartX - 0) < 1e-3);
            var beam2 = beams.First(b => Math.Abs(b.StartX - 1000) < 1e-3);

            Assert.Equal(600, beam1.Height);
            Assert.Equal("D1", beam1.Mark);

            Assert.Equal(700, beam2.Height);
            Assert.Equal("D2", beam2.Mark);
        }

        [Fact]
        public void Pipeline_UnknownMiddleSegment_SplitsCorrectlyByText()
        {
            // seg1 has 0x0, seg2 has 0x0, seg3 has 0x0
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 0, Height = 0, MeasuredWidth = 400, IsPaired = true, Confidence = 100 };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 0, Height = 0, MeasuredWidth = 400, IsPaired = true, Confidence = 100 };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 3000, EndY = 0, Width = 0, Height = 0, MeasuredWidth = 400, IsPaired = true, Confidence = 100 };

            // Text 1 at x=500 is 400x600 ("D1")
            var text1 = new CadDimensionText { X = 500, Y = 0, Width = 400, Height = 600, Content = "D1 400x600" };
            // Text 2 at x=2500 is 400x700 ("D2")
            var text2 = new CadDimensionText { X = 2500, Y = 0, Width = 400, Height = 700, Content = "D2 400x700" };

            var beams = _pipeline.ProcessPipeline(new[] { seg1, seg2, seg3 }, new[] { text1, text2 });

            Assert.Equal(2, beams.Count);
            var beam1 = beams.First(b => b.Height == 600);
            var beam2 = beams.First(b => b.Height == 700);

            Assert.Equal(400, beam1.Width);
            Assert.Equal(600, beam1.Height);

            Assert.Equal(400, beam2.Width);
            Assert.Equal(700, beam2.Height);
        }

        [Fact]
        public void Pipeline_TJunction_ProducesThreeCadBeamData()
        {
            var segA = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, MeasuredWidth = 400, IsPaired = true, TextContent = "D1 400x600" };
            var segB = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600, MeasuredWidth = 400, IsPaired = true, TextContent = "D1 400x600" };
            var segC = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 1000, EndY = 1000, Width = 400, Height = 600, MeasuredWidth = 400, IsPaired = true, TextContent = "D2 400x600" };

            var beams = _pipeline.ProcessPipeline(new[] { segA, segB, segC });

            Assert.Equal(3, beams.Count);
            Assert.False(beams.Any(b => Math.Abs(b.StartX - 0) < 1e-3 && Math.Abs(b.EndX - 2000) < 1e-3));
        }

        [Fact]
        public void Pipeline_DuplicateInput_DoesNotDuplicateCadBeamData()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, IsPaired = true };
            var seg1Dup = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, IsPaired = true };

            var beams = _pipeline.ProcessPipeline(new[] { seg1, seg1Dup });

            Assert.Single(beams);
            Assert.Equal(0, beams[0].StartX);
            Assert.Equal(1000, beams[0].EndX);
        }

        [Fact]
        public void Pipeline_MetadataAssignment_ConsistentWidthHeightMarkAndMeasuredWidth()
        {
            var seg1 = new CadBeamSegment
            {
                StartX = 0, StartY = 0, EndX = 1000, EndY = 0,
                Width = 350, Height = 550, MeasuredWidth = 350,
                Mark = "SB1", TextContent = "SB1 350x550",
                Confidence = 900, IsPaired = true
            };

            var beams = _pipeline.ProcessPipeline(new[] { seg1 });

            Assert.Single(beams);
            Assert.Equal(350, beams[0].Width);
            Assert.Equal(550, beams[0].Height);
            Assert.Equal(350, beams[0].MeasuredWidth);
            Assert.Equal("SB1", beams[0].Mark);
            Assert.Equal("SB1 350x550", beams[0].TextContent);
        }

        [Fact]
        public void Pipeline_IsPaired_PreservedCorrectly()
        {
            var pairedSeg = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, IsPaired = true, MeasuredWidth = 400 };
            var singleSeg = new CadBeamSegment { StartX = 0, StartY = 100, EndX = 1000, EndY = 100, Width = 400, Height = 600, IsPaired = false };

            var beams = _pipeline.ProcessPipeline(new[] { pairedSeg, singleSeg });

            Assert.Equal(2, beams.Count);
            var pairedBeam = beams.First(b => b.StartY == 0);
            var singleBeam = beams.First(b => b.StartY == 100);

            Assert.True(pairedBeam.IsPaired);
            Assert.False(singleBeam.IsPaired);
        }

        [Fact]
        public void Pipeline_ShuffledInputOrder_ProducesStableOutput()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, IsPaired = true, TextContent = "D1" };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600, IsPaired = true, TextContent = "D1" };
            var seg3 = new CadBeamSegment { StartX = 2000, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, IsPaired = true, TextContent = "D1" };

            var run1 = _pipeline.ProcessPipeline(new[] { seg1, seg2, seg3 });
            var run2 = _pipeline.ProcessPipeline(new[] { seg3, seg1, seg2 });

            Assert.Single(run1);
            Assert.Single(run2);

            Assert.Equal(run1[0].StartX, run2[0].StartX);
            Assert.Equal(run1[0].EndX, run2[0].EndX);
            Assert.Equal(run1[0].Width, run2[0].Width);
            Assert.Equal(run1[0].Height, run2[0].Height);
            Assert.Equal(run1[0].Mark, run2[0].Mark);
        }
    }
}
