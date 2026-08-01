using Antigravity.DrawBeams.Models;
using System;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class CadSceneModelTests
    {
        [Fact]
        public void CadSegment_CalculatesGeometryPropertiesCorrectly()
        {
            var segment = new CadSegment
            {
                StartPoint = new double[] { 0, 0, 0 },
                EndPoint = new double[] { 3000, 4000, 0 },
                Id = "SEG1",
                Layer = "BEAM_LAYER",
                Color = 1
            };

            Assert.Equal(3000, segment.DirectionX);
            Assert.Equal(4000, segment.DirectionY);
            Assert.Equal(5000, segment.Length);
            Assert.Equal(1500, segment.MidX);
            Assert.Equal(2000, segment.MidY);
            Assert.True(Math.Abs(segment.NormalX - (-0.8)) < 1e-6);
            Assert.True(Math.Abs(segment.NormalY - 0.6) < 1e-6);
        }

        [Fact]
        public void CadText_CalculatesCleanTextCorrectly()
        {
            var cadText = new CadText
            {
                TextString = @"{\fArial|b0|i0;200x500}",
                InsertionPoint = new double[] { 100, 200, 0 },
                Rotation = 0.0,
                Layer = "TEXT_LAYER",
                ObjectName = "AcDbMText"
            };

            Assert.Equal("200x500", cadText.CleanText);
        }

        [Fact]
        public void CadScene_ContainerHoldsSegmentsAndTexts()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "S1" });
            scene.Texts.Add(new CadText { TextString = "B200x500" });

            Assert.Single(scene.Segments);
            Assert.Single(scene.Texts);
        }
    }
}
