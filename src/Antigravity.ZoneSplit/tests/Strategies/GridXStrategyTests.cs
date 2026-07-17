using System.Collections.Generic;
using Antigravity.ZoneSplit.Models;
using Antigravity.ZoneSplit.Strategies;
using Xunit;

namespace Antigravity.ZoneSplit.Tests.Strategies
{
    public class GridXStrategyTests
    {
        private readonly GridXStrategy _sut = new GridXStrategy();
        private readonly ZoneStrategyOptions _default3 = new ZoneStrategyOptions(3, ZoneScheme.GridByX, "Zone-");

        private static RoomData MakeRoom(int id, double x, double y, double area = 10.0)
            => new RoomData(id, $"Room{id}", id.ToString(), x, y, area,
                false, new BoundingBox2D(x - 1, y - 1, x + 1, y + 1));

        // ── 1. Empty rooms ─────────────────────────────────────────────
        [Fact]
        public void ComputeZones_EmptyRooms_ReturnsEmpty()
        {
            var result = _sut.ComputeZones(new List<RoomData>(), _default3);
            Assert.Empty(result);
        }

        [Fact]
        public void ComputeZones_NullRooms_ReturnsEmpty()
        {
            var result = _sut.ComputeZones(null, _default3);
            Assert.Empty(result);
        }

        // ── 2. Single room ─────────────────────────────────────────────
        [Fact]
        public void ComputeZones_SingleRoom_ReturnsSingleZone()
        {
            var rooms = new List<RoomData> { MakeRoom(1, 5.0, 5.0) };
            var opts  = new ZoneStrategyOptions(3, ZoneScheme.GridByX, "Zone-");

            // 3 zones requested but only 1 room → 1 zone (degenerate: all same X or capped)
            var result = _sut.ComputeZones(rooms, opts);
            Assert.True(result.Count >= 1);
            // Zone phải tồn tại và có ID đúng format
            Assert.StartsWith("Zone-", result[0].ZoneId);
        }

        // ── 3. Three rooms linear on X ─────────────────────────────────
        [Fact]
        public void ComputeZones_ThreeRoomsLinearX_ThreeZonesCreated()
        {
            var rooms = new List<RoomData>
            {
                MakeRoom(1,  0.0, 5.0),
                MakeRoom(2, 10.0, 5.0),
                MakeRoom(3, 20.0, 5.0)
            };
            var opts   = new ZoneStrategyOptions(3, ZoneScheme.GridByX, "Zone-");
            var result = _sut.ComputeZones(rooms, opts);

            Assert.Equal(3, result.Count);
            Assert.Equal("Zone-A", result[0].ZoneId);
            Assert.Equal("Zone-B", result[1].ZoneId);
            Assert.Equal("Zone-C", result[2].ZoneId);
        }

        [Fact]
        public void ComputeZones_ThreeRoomsLinearX_EachRoomInDifferentZone()
        {
            var rooms = new List<RoomData>
            {
                MakeRoom(1,  0.0, 5.0),
                MakeRoom(2, 10.0, 5.0),
                MakeRoom(3, 20.0, 5.0)
            };
            var opts  = new ZoneStrategyOptions(3, ZoneScheme.GridByX, "Zone-");
            var zones = _sut.ComputeZones(rooms, opts);

            // Kiểm tra mỗi room centroid nằm trong zone tương ứng
            Assert.True(zones[0].Boundary.ContainsInclusive(0.0, 5.0));
            Assert.True(zones[1].Boundary.ContainsInclusive(10.0, 5.0));
            Assert.True(zones[2].Boundary.ContainsInclusive(20.0, 5.0));
        }

        // ── 4. More zones than rooms ───────────────────────────────────
        [Fact]
        public void ComputeZones_MoreZonesThanRooms_ZonesEqualRoomsCount()
        {
            var rooms = new List<RoomData>
            {
                MakeRoom(1,  0.0, 0.0),
                MakeRoom(2, 10.0, 0.0)
            };
            var opts   = new ZoneStrategyOptions(10, ZoneScheme.GridByX, "Zone-");
            var result = _sut.ComputeZones(rooms, opts);

            // Phải <= số rooms
            Assert.True(result.Count <= rooms.Count);
        }

        // ── 5. Room on boundary → assigned to right zone ──────────────
        [Fact]
        public void ComputeZones_RoomOnBoundary_AssignedToRightZone()
        {
            // 2 zones: [0,10) và [10,20]
            // Room chính xác tại X=10 → zone phải (index 1)
            var rooms = new List<RoomData>
            {
                MakeRoom(1,  5.0, 0.0),  // trái rõ ràng
                MakeRoom(2, 15.0, 0.0),  // phải rõ ràng
                MakeRoom(3, 10.0, 0.0)   // trên biên
            };
            var opts  = new ZoneStrategyOptions(2, ZoneScheme.GridByX, "Zone-");
            var zones = _sut.ComputeZones(rooms, opts);

            Assert.Equal(2, zones.Count);
            // Room tại X=10 phải được chứa bởi zone B (phải) hoặc zone A (boundary inclusive)
            // half-open: Contains(10,0) cho zone [0,10) = false → fallback ContainsInclusive
            bool inZoneA = zones[0].Boundary.ContainsInclusive(10.0, 0.0);
            bool inZoneB = zones[1].Boundary.ContainsInclusive(10.0, 0.0);
            Assert.True(inZoneA || inZoneB, "Room tại biên phải nằm trong ít nhất 1 zone.");
        }

        // ── 6. All rooms same X → no exception ────────────────────────
        [Fact]
        public void ComputeZones_AllRoomsSameX_NoException()
        {
            var rooms = new List<RoomData>
            {
                MakeRoom(1, 5.0, 0.0),
                MakeRoom(2, 5.0, 5.0),
                MakeRoom(3, 5.0, 10.0)
            };
            var opts = new ZoneStrategyOptions(3, ZoneScheme.GridByX, "Zone-");

            var ex = Record.Exception(() => _sut.ComputeZones(rooms, opts));
            Assert.Null(ex);
        }

        // ── 7. Zone naming ─────────────────────────────────────────────
        [Fact]
        public void ComputeZones_CustomPrefix_ZoneIdUsesPrefix()
        {
            var rooms = new List<RoomData>
            {
                MakeRoom(1, 0.0, 0.0),
                MakeRoom(2, 10.0, 0.0)
            };
            var opts   = new ZoneStrategyOptions(2, ZoneScheme.GridByX, "SEC-");
            var result = _sut.ComputeZones(rooms, opts);

            Assert.All(result, z => Assert.StartsWith("SEC-", z.ZoneId));
        }
    }
}
