using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class BeamSceneProcessorTests
    {
        private readonly CadInteropService _service = new CadInteropService();

        [Fact]
        public void ProcessScene_RecognizesBeamFromParallelSegmentsAndText()
        {
            var scene = new CadScene();

            // Segment 1 (Anchor on BEAM_LAYER)
            scene.Segments.Add(new CadSegment
            {
                Id = "LINE1",
                StartPoint = new double[] { 0, 0, 0 },
                EndPoint = new double[] { 4000, 0, 0 },
                Layer = "BEAM_LAYER",
                Color = 1
            });

            // Segment 2 (Parallel partner on BEAM_LAYER, 200mm offset)
            scene.Segments.Add(new CadSegment
            {
                Id = "LINE2",
                StartPoint = new double[] { 0, 200, 0 },
                EndPoint = new double[] { 4000, 200, 0 },
                Layer = "BEAM_LAYER",
                Color = 1
            });

            // Text matching 200x500
            scene.Texts.Add(new CadText
            {
                TextString = "200x500",
                InsertionPoint = new double[] { 2000, 100, 0 },
                Rotation = 0.0,
                Layer = "TEXT_LAYER",
                ObjectName = "AcDbText"
            });

            List<CadBeamData> beams = _service.ProcessScene(scene, "BEAM_LAYER", "TEXT_LAYER");

            Assert.Single(beams);
            var beam = beams.First();
            Assert.Equal(200, beam.Width);
            Assert.Equal(500, beam.Height);
            Assert.True(beam.IsPaired);
            Assert.Equal(2000, (beam.StartX + beam.EndX) / 2.0);
            Assert.Equal(100, (beam.StartY + beam.EndY) / 2.0);
        }

        [Fact]
        public void ProcessScene_PolylineWidthFastPath_RecognizesBeam()
        {
            var scene = new CadScene();

            scene.Segments.Add(new CadSegment
            {
                Id = "PLINE1",
                StartPoint = new double[] { 0, 0, 0 },
                EndPoint = new double[] { 5000, 0, 0 },
                Layer = "BEAM_LAYER",
                PolylineWidth = 300,
                Color = 3
            });

            scene.Texts.Add(new CadText
            {
                TextString = "300x600 B1",
                InsertionPoint = new double[] { 2500, 50, 0 },
                Rotation = 0.0,
                Layer = "TEXT_LAYER",
                ObjectName = "AcDbText"
            });

            List<CadBeamData> beams = _service.ProcessScene(scene, "BEAM_LAYER", "TEXT_LAYER");

            Assert.Single(beams);
            var beam = beams.First();
            Assert.Equal(300, beam.Width);
            Assert.Equal(600, beam.Height);
            Assert.Equal("B1", beam.Mark);
        }
    }
}
