using Antigravity.ZoneSplit.Models;
using Xunit;

namespace Antigravity.ZoneSplit.Tests.Models
{
    /// <summary>
    /// Unit tests cho BoundingBox2D — đặc biệt kiểm tra Contains() half-open interval.
    /// </summary>
    public class BoundingBox2DTests
    {
        private readonly BoundingBox2D _bbox = new BoundingBox2D(0, 0, 10, 10);

        // ── Contains (half-open) ──────────────────────────────────────
        [Fact]
        public void Contains_PointInsideBox_ReturnsTrue()
            => Assert.True(_bbox.Contains(5, 5));

        [Fact]
        public void Contains_PointAtMinCorner_ReturnsTrue()
            => Assert.True(_bbox.Contains(0, 0));

        [Fact]
        public void Contains_PointAtMaxCorner_ReturnsFalse_HalfOpen()
            => Assert.False(_bbox.Contains(10, 10)); // half-open: MaxX/MaxY excluded

        [Fact]
        public void Contains_PointOnMaxX_ReturnsFalse_HalfOpen()
            => Assert.False(_bbox.Contains(10, 5));

        [Fact]
        public void Contains_PointOutsideBox_ReturnsFalse()
            => Assert.False(_bbox.Contains(15, 5));

        [Fact]
        public void Contains_PointJustInsideMaxX_ReturnsTrue()
            => Assert.True(_bbox.Contains(9.9999, 5));

        // ── ContainsInclusive ─────────────────────────────────────────
        [Fact]
        public void ContainsInclusive_PointAtMaxCorner_ReturnsTrue()
            => Assert.True(_bbox.ContainsInclusive(10, 10));

        [Fact]
        public void ContainsInclusive_PointOutside_ReturnsFalse()
            => Assert.False(_bbox.ContainsInclusive(10.1, 5));

        // ── Width / Height ────────────────────────────────────────────
        [Fact]
        public void Width_ReturnsCorrectValue()
            => Assert.Equal(10.0, _bbox.Width);

        [Fact]
        public void Height_ReturnsCorrectValue()
            => Assert.Equal(10.0, _bbox.Height);

        // ── Union ─────────────────────────────────────────────────────
        [Fact]
        public void Union_TwoBoxes_ReturnsCorrectUnion()
        {
            var a   = new BoundingBox2D(0, 0, 5, 5);
            var b   = new BoundingBox2D(3, 3, 10, 10);
            var u   = BoundingBox2D.Union(a, b);

            Assert.Equal(0,  u.MinX);
            Assert.Equal(0,  u.MinY);
            Assert.Equal(10, u.MaxX);
            Assert.Equal(10, u.MaxY);
        }

        [Fact]
        public void Union_SameBox_ReturnsSameBox()
        {
            var a = new BoundingBox2D(1, 2, 3, 4);
            var u = BoundingBox2D.Union(a, a);
            Assert.Equal(a.MinX, u.MinX);
            Assert.Equal(a.MaxY, u.MaxY);
        }
    }
}
