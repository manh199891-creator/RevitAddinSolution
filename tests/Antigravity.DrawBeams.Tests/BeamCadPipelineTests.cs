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

        // ==========================================
        // MAJOR 1: SEGMENT CUTTING AT DIMENSION BOUNDARY
        // ==========================================

        [Fact]
        public void Pipeline_SplitRegions_DimensionTextOverridesOldMetadata()
        {
            var rawSeg = new CadBeamSegment
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, TextContent = "D1 400x600", Mark = "D1",
                MeasuredWidth = 400, IsPaired = true, Confidence = 500
            };

            var text1 = new CadDimensionText { X = 500, Y = 0, Content = "D1 400x600", Width = 400, Height = 600 };
            var text2 = new CadDimensionText { X = 2500, Y = 0, Content = "D2 400x700", Width = 400, Height = 700 };

            var beams = _pipeline.ProcessPipeline(new[] { rawSeg }, new[] { text1, text2 });

            Assert.Equal(2, beams.Count);

            var beam1 = beams.First(b => Math.Abs(b.StartX - 0) < 1e-3);
            var beam2 = beams.First(b => Math.Abs(b.EndX - 3000) < 1e-3);

            Assert.Equal(0, beam1.StartX, 2);
            Assert.Equal(1500, beam1.EndX, 2);
            Assert.Equal(400, beam1.Width);
            Assert.Equal(600, beam1.Height);
            Assert.Equal("D1", beam1.Mark);
            Assert.Equal("D1 400x600", beam1.TextContent);

            Assert.Equal(1500, beam2.StartX, 2);
            Assert.Equal(3000, beam2.EndX, 2);
            Assert.Equal(400, beam2.Width);
            Assert.Equal(700, beam2.Height);
            Assert.Equal("D2", beam2.Mark);
            Assert.Equal("D2 400x700", beam2.TextContent);
        }

        [Fact]
        public void Pipeline_SingleLongSegment_CrossesDimensionBoundary_CutsSegmentExactlyAtSplitPoint()
        {
            // Single long horizontal segment 0 -> 3000
            var longSeg = new CadBeamSegment
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 0, Height = 0, MeasuredWidth = 400,
                IsPaired = true, Confidence = 100
            };

            var text1 = new CadDimensionText { X = 500, Y = 0, Width = 400, Height = 600, Content = "D1 400x600" };
            var text2 = new CadDimensionText { X = 2500, Y = 0, Width = 400, Height = 700, Content = "D2 400x700" };

            var beams = _pipeline.ProcessPipeline(new[] { longSeg }, new[] { text1, text2 });

            Assert.Equal(2, beams.Count);

            var beam1 = beams.First(b => b.Height == 600);
            var beam2 = beams.First(b => b.Height == 700);

            Assert.Equal(0, beam1.StartX, 2);
            Assert.Equal(1500, beam1.EndX, 2);
            Assert.Equal(400, beam1.Width);
            Assert.Equal(600, beam1.Height);

            Assert.Equal(1500, beam2.StartX, 2);
            Assert.Equal(3000, beam2.EndX, 2);
            Assert.Equal(400, beam2.Width);
            Assert.Equal(700, beam2.Height);
        }

        [Fact]
        public void Pipeline_Diagonal45DegSegment_CrossesDimensionBoundary_CutsSegmentAtExactLineCoordinates()
        {
            double rad = Math.PI / 4.0; // 45 deg
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            // 45 deg segment 0 -> 3000 along 45 deg ray
            var diagSeg = new CadBeamSegment
            {
                StartX = 0, StartY = 0,
                EndX = 3000 * cos, EndY = 3000 * sin,
                Width = 0, Height = 0, MeasuredWidth = 400,
                IsPaired = true, Confidence = 100
            };

            var text1 = new CadDimensionText { X = 500 * cos, Y = 500 * sin, Width = 400, Height = 600, Content = "D1 400x600" };
            var text2 = new CadDimensionText { X = 2500 * cos, Y = 2500 * sin, Width = 400, Height = 700, Content = "D2 400x700" };

            var beams = _pipeline.ProcessPipeline(new[] { diagSeg }, new[] { text1, text2 });

            Assert.Equal(2, beams.Count);

            var beam1 = beams.First(b => b.Height == 600);
            var beam2 = beams.First(b => b.Height == 700);

            Assert.Equal(0, beam1.StartX, 2);
            Assert.Equal(0, beam1.StartY, 2);
            Assert.Equal(1500 * cos, beam1.EndX, 2);
            Assert.Equal(1500 * sin, beam1.EndY, 2);

            Assert.Equal(1500 * cos, beam2.StartX, 2);
            Assert.Equal(1500 * sin, beam2.StartY, 2);
            Assert.Equal(3000 * cos, beam2.EndX, 2);
            Assert.Equal(3000 * sin, beam2.EndY, 2);
        }

        // ==========================================
        // MAJOR 2: CONSISTENT PRIMARY SEGMENT METADATA
        // ==========================================

        [Fact]
        public void Pipeline_ConsistentPrimarySegmentMetadata_TakesMetadataFromHighestConfidenceSegment()
        {
            var segLowConf = new CadBeamSegment
            {
                StartX = 0, StartY = 0, EndX = 1000, EndY = 0,
                Width = 400, Height = 600, Mark = "D1", TextContent = "D1 400x600",
                Confidence = 100, IsPaired = true, MeasuredWidth = 400
            };

            var segHighConf = new CadBeamSegment
            {
                StartX = 1000, StartY = 0, EndX = 2000, EndY = 0,
                Width = 400, Height = 600, Mark = "D2", TextContent = "D2 400x600",
                Confidence = 900, IsPaired = true, MeasuredWidth = 400
            };

            var beams = _pipeline.ProcessPipeline(new[] { segLowConf, segHighConf });

            Assert.Single(beams);
            Assert.Equal("D2", beams[0].Mark);
            Assert.Equal("D2 400x600", beams[0].TextContent);
        }

        // ==========================================
        // MAJOR 3: OPTIONS OVERRIDE PROPAGATION
        // ==========================================

        [Fact]
        public void Pipeline_OptionsOverride_PropagatesEndpointGapToleranceToChainBuilder()
        {
            // Default options in _pipeline has EndpointGapToleranceMm = 100.
            // Pass overrideOptions with EndpointGapToleranceMm = 10.
            var overrideOptions = new BeamContinuityOptions
            {
                AngularToleranceDegrees = 2.5,
                EndpointGapToleranceMm = 10, // Override gap tolerance to 10mm
                LateralOffsetToleranceMm = 30,
                WidthToleranceRatio = 0.20,
                MinimumOverlapMm = 200,
                JunctionToleranceMm = 50.0
            };

            // Two segments with a gap of 50mm
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600 };
            var seg2 = new CadBeamSegment { StartX = 1050, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600 };

            // With default options (gap 100), they would merge into 1 beam.
            // With overrideOptions (gap 10), 50mm gap > 10mm -> MUST split into 2 beams!
            var beams = _pipeline.ProcessPipeline(new[] { seg1, seg2 }, null, overrideOptions);

            Assert.Equal(2, beams.Count);
        }

        // ==========================================
        // MINOR 1: DIMENSION GROUPING TOLERANCE
        // ==========================================

        [Fact]
        public void Pipeline_DimensionGroupingTolerance_TreatsSlightImprecisionAsSameDimension()
        {
            var seg1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, MeasuredWidth = 400, IsPaired = true };
            var seg2 = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, MeasuredWidth = 400, IsPaired = true };

            // Text 1: 400.0 x 600.0, Text 2: 399.999 x 600.001
            var text1 = new CadDimensionText { X = 500, Y = 0, Width = 400.0, Height = 600.0, Content = "D1 400x600" };
            var text2 = new CadDimensionText { X = 1500, Y = 0, Width = 399.999, Height = 600.001, Content = "D1 400x600" };

            var beams = _pipeline.ProcessPipeline(new[] { seg1, seg2 }, new[] { text1, text2 });

            // Treated as same dimension -> NO split -> 1 beam!
            Assert.Single(beams);
            Assert.Equal(0, beams[0].StartX);
            Assert.Equal(2000, beams[0].EndX);
        }

        // ==========================================
        // MINOR 2: DIRECTION-INDEPENDENT DEDUPLICATION
        // ==========================================

        [Fact]
        public void Pipeline_DirectionIndependentDeduplication_TreatsReversedGeometryAsDuplicate()
        {
            var segForward = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, IsPaired = true };
            var segReversed = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 0, EndY = 0, Width = 400, Height = 600, IsPaired = true };

            var beams = _pipeline.ProcessPipeline(new[] { segForward, segReversed });

            // Reversed endpoints -> deduplicated into 1 beam!
            Assert.Single(beams);
        }

        // ==========================================
        // STANDARD PIPELINE REGRESSION TESTS
        // ==========================================

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
        public void Pipeline_TJunction_ProducesThreeCadBeamData()
        {
            var segA = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 1000, EndY = 0, Width = 400, Height = 600, MeasuredWidth = 400, IsPaired = true, TextContent = "D1 400x600" };
            var segB = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 2000, EndY = 0, Width = 400, Height = 600, MeasuredWidth = 400, IsPaired = true, TextContent = "D1 400x600" };
            var segC = new CadBeamSegment { StartX = 1000, StartY = 0, EndX = 1000, EndY = 1000, Width = 400, Height = 600, MeasuredWidth = 400, IsPaired = true, TextContent = "D2 400x600" };

            var beams = _pipeline.ProcessPipeline(new[] { segA, segB, segC });

            Assert.Equal(3, beams.Count);
            Assert.DoesNotContain(beams, b => Math.Abs(b.StartX - 0) < 1e-3 && Math.Abs(b.EndX - 2000) < 1e-3);
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
