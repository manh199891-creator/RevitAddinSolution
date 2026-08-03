using Antigravity.DrawBeams.Models;
using System;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class CadSceneModelTests
    {
        [Fact]
        public void CadSegment_HorizontalSegment_CalculatesProperties()
        {
            var segment = new CadSegment
            {
                StartX = 0,
                StartY = 0,
                EndX = 4000,
                EndY = 0,
                Id = "SEG_H",
                Layer = "BEAM"
            };

            Assert.Equal(4000, segment.DirectionX);
            Assert.Equal(0, segment.DirectionY);
            Assert.Equal(4000, segment.Length);
            Assert.Equal(2000, segment.MidX);
            Assert.Equal(0, segment.MidY);
            Assert.Equal(0, segment.Angle);
            Assert.Equal(0, segment.NormalX);
            Assert.Equal(1, segment.NormalY);
        }

        [Fact]
        public void CadSegment_VerticalSegment_CalculatesProperties()
        {
            var segment = new CadSegment
            {
                StartX = 0,
                StartY = 0,
                EndX = 0,
                EndY = 3000,
                Id = "SEG_V",
                Layer = "BEAM"
            };

            Assert.Equal(0, segment.DirectionX);
            Assert.Equal(3000, segment.DirectionY);
            Assert.Equal(3000, segment.Length);
            Assert.Equal(0, segment.MidX);
            Assert.Equal(1500, segment.MidY);
            Assert.True(Math.Abs(segment.Angle - Math.PI / 2.0) < 1e-6);
            Assert.Equal(-1, segment.NormalX);
            Assert.Equal(0, segment.NormalY);
        }

        [Fact]
        public void CadSegment_DiagonalSegment_CalculatesProperties()
        {
            var segment = new CadSegment
            {
                StartX = 0,
                StartY = 0,
                EndX = 3000,
                EndY = 4000,
                Id = "SEG_D",
                Layer = "BEAM"
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
        public void CadSegment_ReversedSegment_NormalizesAngleAndPositiveLength()
        {
            var segment = new CadSegment
            {
                StartX = 4000,
                StartY = 0,
                EndX = 0,
                EndY = 0,
                Id = "SEG_REV",
                Layer = "BEAM"
            };

            Assert.Equal(-4000, segment.DirectionX);
            Assert.Equal(0, segment.DirectionY);
            Assert.Equal(4000, segment.Length);
            Assert.Equal(2000, segment.MidX);
            Assert.Equal(0, segment.MidY);
            Assert.Equal(0, segment.Angle); // Angle normalized [0, PI)
        }

        [Fact]
        public void CadSegment_ZeroLengthSegment_DoesNotReturnNaN()
        {
            var segment = new CadSegment
            {
                StartX = 100,
                StartY = 100,
                EndX = 100,
                EndY = 100,
                Id = "SEG_ZERO",
                Layer = "BEAM"
            };

            Assert.Equal(0, segment.Length);
            Assert.Equal(0, segment.Angle);
            Assert.False(double.IsNaN(segment.NormalX));
            Assert.False(double.IsNaN(segment.NormalY));
            Assert.Equal(0, segment.NormalX);
            Assert.Equal(0, segment.NormalY);
        }

        [Fact]
        public void CadText_PropertiesAndCleanText_WorkCorrectly()
        {
            var cadText = new CadText
            {
                Id = "TXT123",
                TextString = @"{\fArial|b0|i0;200x500}",
                X = 100,
                Y = 200,
                Rotation = 0.5,
                TextHeight = 250,
                Layer = "TEXT_LAYER",
                ObjectName = "AcDbMText"
            };

            Assert.Equal("TXT123", cadText.Id);
            Assert.Equal(100, cadText.X);
            Assert.Equal(200, cadText.Y);
            Assert.Equal(0.5, cadText.Rotation);
            Assert.Equal(250, cadText.TextHeight);
            Assert.Equal("200x500", cadText.CleanText);
        }
    }
}
