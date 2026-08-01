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

            scene.Segments.Add(new CadSegment
            {
                Id = "LINE1",
                StartX = 0,
                StartY = 0,
                EndX = 4000,
                EndY = 0,
                Layer = "BEAM_LAYER",
                Color = 1
            });

            scene.Segments.Add(new CadSegment
            {
                Id = "LINE2",
                StartX = 0,
                StartY = 200,
                EndX = 4000,
                EndY = 200,
                Layer = "BEAM_LAYER",
                Color = 1
            });

            scene.Texts.Add(new CadText
            {
                Id = "TXT1",
                TextString = "200x500",
                X = 2000,
                Y = 100,
                Rotation = 0.0,
                TextHeight = 250,
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
        public void ProcessScene_ReversedPartnerDirection_RecognizesBeamWithCorrectCenterline()
        {
            var scene = new CadScene();

            // Segment A: (0,0) -> (4000,0)
            scene.Segments.Add(new CadSegment
            {
                Id = "SEG_A",
                StartX = 0,
                StartY = 0,
                EndX = 4000,
                EndY = 0,
                Layer = "BEAM_LAYER",
                Color = 1
            });

            // Segment B reversed: (4000,200) -> (0,200)
            scene.Segments.Add(new CadSegment
            {
                Id = "SEG_B",
                StartX = 4000,
                StartY = 200,
                EndX = 0,
                EndY = 200,
                Layer = "BEAM_LAYER",
                Color = 1
            });

            scene.Texts.Add(new CadText
            {
                Id = "TXT_REV",
                TextString = "200x500",
                X = 2000,
                Y = 100,
                Rotation = 0.0,
                TextHeight = 250,
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
        public void ProcessScene_ClosedPolylineGroupId_PairsSegmentsCorrectly()
        {
            var scene = new CadScene();

            // Closed rectangle polyline segments
            scene.Segments.Add(new CadSegment
            {
                Id = "PL_0",
                StartX = 0,
                StartY = 0,
                EndX = 5000,
                EndY = 0,
                Layer = "BEAM_LAYER",
                GroupId = "RECT_1_pairA"
            });

            scene.Segments.Add(new CadSegment
            {
                Id = "PL_1",
                StartX = 5000,
                StartY = 0,
                EndX = 5000,
                EndY = 300,
                Layer = "BEAM_LAYER",
                GroupId = "RECT_1_pairB"
            });

            scene.Segments.Add(new CadSegment
            {
                Id = "PL_2",
                StartX = 5000,
                StartY = 300,
                EndX = 0,
                EndY = 300,
                Layer = "BEAM_LAYER",
                GroupId = "RECT_1_pairA"
            });

            scene.Segments.Add(new CadSegment
            {
                Id = "PL_3",
                StartX = 0,
                StartY = 300,
                EndX = 0,
                EndY = 0,
                Layer = "BEAM_LAYER",
                GroupId = "RECT_1_pairB"
            });

            scene.Texts.Add(new CadText
            {
                Id = "TXT_RECT",
                TextString = "300x600 DB1",
                X = 2500,
                Y = 150,
                Rotation = 0.0,
                TextHeight = 250,
                Layer = "TEXT_LAYER",
                ObjectName = "AcDbText"
            });

            List<CadBeamData> beams = _service.ProcessScene(scene, "BEAM_LAYER", "TEXT_LAYER");

            Assert.Single(beams);
            var beam = beams.First();
            Assert.Equal(300, beam.Width);
            Assert.Equal(600, beam.Height);
            Assert.Equal("DB1", beam.Mark);
        }

        [Fact]
        public void ProcessScene_TextLayerFiltering_FiltersTextsByLayerWhenProvided()
        {
            var scene = new CadScene();

            scene.Segments.Add(new CadSegment
            {
                Id = "LINE1",
                StartX = 0,
                StartY = 0,
                EndX = 4000,
                EndY = 0,
                Layer = "BEAM_LAYER"
            });

            scene.Segments.Add(new CadSegment
            {
                Id = "LINE2",
                StartX = 0,
                StartY = 200,
                EndX = 4000,
                EndY = 200,
                Layer = "BEAM_LAYER"
            });

            // T1 on correct text layer
            scene.Texts.Add(new CadText
            {
                Id = "TXT_MATCH",
                TextString = "200x500",
                X = 2000,
                Y = 100,
                Layer = "BEAM_TEXT"
            });

            // T2 on other text layer
            scene.Texts.Add(new CadText
            {
                Id = "TXT_OTHER",
                TextString = "400x800",
                X = 2000,
                Y = 100,
                Layer = "OTHER_TEXT"
            });

            // With filter BEAM_TEXT
            List<CadBeamData> filteredBeams = _service.ProcessScene(scene, "BEAM_LAYER", "BEAM_TEXT");
            Assert.Single(filteredBeams);
            Assert.Equal(200, filteredBeams[0].Width);

            // Without filter (null)
            List<CadBeamData> unfilteredBeams = _service.ProcessScene(scene, "BEAM_LAYER", null);
            Assert.NotEmpty(unfilteredBeams);
        }

        [Fact]
        public void ProcessScene_SceneWithoutText_ExecutesSafelyWithoutCrashing()
        {
            var scene = new CadScene();

            scene.Segments.Add(new CadSegment
            {
                Id = "S1",
                StartX = 0,
                StartY = 0,
                EndX = 4000,
                EndY = 0,
                Layer = "BEAM_LAYER"
            });

            scene.Segments.Add(new CadSegment
            {
                Id = "S2",
                StartX = 0,
                StartY = 200,
                EndX = 4000,
                EndY = 200,
                Layer = "BEAM_LAYER"
            });

            // No texts added
            List<CadBeamData> beams = _service.ProcessScene(scene, "BEAM_LAYER", "TEXT_LAYER");
            Assert.NotNull(beams);
        }

        [Fact]
        public void ProcessScene_PolylineWidthFastPath_RecognizesBeam()
        {
            var scene = new CadScene();

            scene.Segments.Add(new CadSegment
            {
                Id = "PLINE1",
                StartX = 0,
                StartY = 0,
                EndX = 5000,
                EndY = 0,
                Layer = "BEAM_LAYER",
                PolylineWidth = 300,
                Color = 3
            });

            scene.Texts.Add(new CadText
            {
                Id = "TXT_PL",
                TextString = "300x600 B1",
                X = 2500,
                Y = 50,
                Rotation = 0.0,
                TextHeight = 250,
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
