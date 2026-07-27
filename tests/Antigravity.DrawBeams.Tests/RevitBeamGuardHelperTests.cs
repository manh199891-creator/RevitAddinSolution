using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class RevitBeamGuardHelperTests
    {
        private readonly BeamOverlapOptions _options = new BeamOverlapOptions();

        [Fact]
        public void IsDuplicateRevitBeam_ExactMatch_ReturnsTrue()
        {
            bool isDup = RevitBeamGuardHelper.IsDuplicateRevitBeam(
                0, 0, 3000, 0, 400, 600,
                0, 0, 3000, 0, 400, 600,
                _options);

            Assert.True(isDup);
        }

        [Fact]
        public void IsDuplicateRevitBeam_ReversedDirection_ReturnsTrue()
        {
            bool isDup = RevitBeamGuardHelper.IsDuplicateRevitBeam(
                0, 0, 3000, 0, 400, 600,
                3000, 0, 0, 0, 400, 600,
                _options);

            Assert.True(isDup);
        }

        [Fact]
        public void IsDuplicateRevitBeam_SlightCenterlineOffset_ReturnsTrue()
        {
            bool isDup = RevitBeamGuardHelper.IsDuplicateRevitBeam(
                0, 0, 3000, 0, 400, 600,
                0, 10, 3000, 10, 400, 600,
                _options);

            Assert.True(isDup);
        }

        [Fact]
        public void IsDuplicateRevitBeam_LargeOffset_ReturnsFalse()
        {
            bool isDup = RevitBeamGuardHelper.IsDuplicateRevitBeam(
                0, 0, 3000, 0, 400, 600,
                0, 300, 3000, 300, 400, 600,
                _options);

            Assert.False(isDup);
        }

        [Fact]
        public void IsDuplicateRevitBeam_DifferentDimensions_ReturnsFalse()
        {
            bool isDup = RevitBeamGuardHelper.IsDuplicateRevitBeam(
                0, 0, 3000, 0, 400, 600,
                0, 0, 3000, 0, 500, 700,
                _options);

            Assert.False(isDup);
        }
    }
}
