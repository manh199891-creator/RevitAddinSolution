using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class BeamGeometryServiceTests
    {
        private readonly BeamGeometryService _geometry = new BeamGeometryService();

        [Fact]
        public void ZeroLengthSegment_HandledSafelyWithoutNaNOrException()
        {
            var zeroSeg = new CadSegment
            {
                StartX = 100,
                StartY = 100,
                EndX = 100,
                EndY = 100,
                Id = "SEG_ZERO"
            };

            var validSeg = new CadSegment
            {
                StartX = 0,
                StartY = 0,
                EndX = 1000,
                EndY = 0,
                Id = "SEG_VALID"
            };

            double dist = _geometry.GetPerpendicularDistance(zeroSeg, validSeg);
            Assert.Equal(0, dist);
            Assert.False(double.IsNaN(dist));

            double angleDiff = _geometry.GetAngleDifference(zeroSeg, validSeg);
            Assert.Equal(0, angleDiff);
            Assert.False(double.IsNaN(angleDiff));

            double overlap = _geometry.GetOverlapLength(zeroSeg, validSeg);
            Assert.Equal(0, overlap);
            Assert.False(double.IsNaN(overlap));

            bool parallel = _geometry.AreParallel(zeroSeg, validSeg);
            Assert.False(parallel);
        }
    }
}
