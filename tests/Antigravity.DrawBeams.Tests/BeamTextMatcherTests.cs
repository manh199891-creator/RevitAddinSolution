using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System.Collections.Generic;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class BeamTextMatcherTests
    {
        private readonly BeamTextMatcher _textMatcher = new BeamTextMatcher();

        [Theory]
        [InlineData("200x500", 200, 500)]
        [InlineData("200X500", 200, 500)]
        [InlineData("200*500", 200, 500)]
        [InlineData("200-500", 200, 500)]
        [InlineData("200/500", 200, 500)]
        [InlineData("20x50", 200, 500)] // scale x10
        [InlineData(@"{\fArial;200x500 B1}", 200, 500)] // MText format
        public void FindMatches_ParsesVariousDimensionFormats(string inputContent, double expectedWidth, double expectedHeight)
        {
            var anchor = new CadSegment
            {
                StartX = 0,
                StartY = 0,
                EndX = 4000,
                EndY = 0,
                Id = "ANCHOR"
            };

            var text = new CadText
            {
                Id = "TXT1",
                TextString = inputContent,
                X = 2000,
                Y = 50,
                Rotation = 0,
                ObjectName = inputContent.Contains("{") ? "AcDbMText" : "AcDbText"
            };

            List<BeamTextMatch> matches = _textMatcher.FindMatches(anchor, new[] { text });

            Assert.Single(matches);
            Assert.Equal(expectedWidth, matches[0].ParsedWidth);
            Assert.Equal(expectedHeight, matches[0].ParsedHeight);
        }
    }
}
