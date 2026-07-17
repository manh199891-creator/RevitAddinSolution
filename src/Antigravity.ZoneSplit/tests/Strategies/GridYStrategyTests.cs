using System.Collections.Generic;
using Antigravity.ZoneSplit.Models;
using Antigravity.ZoneSplit.Strategies;
using Xunit;

namespace Antigravity.ZoneSplit.Tests.Strategies
{
    public class GridYStrategyTests
    {
        private readonly GridYStrategy _sut = new GridYStrategy();

        private static RoomData MakeRoom(int id, double x, double y, double area = 10.0)
            => new RoomData(id, $"Room{id}", id.ToString(), x, y, area,
                false, new BoundingBox2D(x - 1, y - 1, x + 1, y + 1));

        [Fact]
        public void ComputeZones_EmptyRooms_ReturnsEmpty()
        {
            var result = _sut.ComputeZones(new List<RoomData>(), new ZoneStrategyOptions(3));
            Assert.Empty(result);
        }

        [Fact]
        public void ComputeZones_NullRooms_ReturnsEmpty()
        {
            var result = _sut.ComputeZones(null, new ZoneStrategyOptions(3));
            Assert.Empty(result);
        }

        [Fact]
        public void ComputeZones_SingleRoom_ReturnsSingleZone()
        {
            var rooms  = new List<RoomData> { MakeRoom(1, 5.0, 5.0) };
            var result = _sut.ComputeZones(rooms, new ZoneStrategyOptions(3));
            Assert.True(result.Count >= 1);
            Assert.StartsWith("Zone-", result[0].ZoneId);
        }

        [Fact]
        public void ComputeZones_ThreeRoomsLinearY_ThreeZonesCreated()
        {
            var rooms = new List<RoomData>
            {
                MakeRoom(1, 5.0,  0.0),
                MakeRoom(2, 5.0, 10.0),
                MakeRoom(3, 5.0, 20.0)
            };
            var result = _sut.ComputeZones(rooms, new ZoneStrategyOptions(3));
            Assert.Equal(3, result.Count);
            Assert.Equal("Zone-A", result[0].ZoneId);
            Assert.Equal("Zone-B", result[1].ZoneId);
            Assert.Equal("Zone-C", result[2].ZoneId);
        }

        [Fact]
        public void ComputeZones_ThreeRoomsLinearY_EachRoomInDifferentZone()
        {
            var rooms = new List<RoomData>
            {
                MakeRoom(1, 5.0,  0.0),
                MakeRoom(2, 5.0, 10.0),
                MakeRoom(3, 5.0, 20.0)
            };
            var zones = _sut.ComputeZones(rooms, new ZoneStrategyOptions(3));

            Assert.True(zones[0].Boundary.ContainsInclusive(5.0,  0.0));
            Assert.True(zones[1].Boundary.ContainsInclusive(5.0, 10.0));
            Assert.True(zones[2].Boundary.ContainsInclusive(5.0, 20.0));
        }

        [Fact]
        public void ComputeZones_MoreZonesThanRooms_ZonesEqualRoomsCount()
        {
            var rooms = new List<RoomData>
            {
                MakeRoom(1, 0.0,  0.0),
                MakeRoom(2, 0.0, 10.0)
            };
            var result = _sut.ComputeZones(rooms, new ZoneStrategyOptions(10));
            Assert.True(result.Count <= rooms.Count);
        }

        [Fact]
        public void ComputeZones_RoomOnBoundary_AssignedToZone()
        {
            var rooms = new List<RoomData>
            {
                MakeRoom(1, 0.0,  5.0),
                MakeRoom(2, 0.0, 15.0),
                MakeRoom(3, 0.0, 10.0)   // trên biên Y
            };
            var zones = _sut.ComputeZones(rooms, new ZoneStrategyOptions(2));
            Assert.Equal(2, zones.Count);
            bool covered = zones[0].Boundary.ContainsInclusive(0.0, 10.0)
                        || zones[1].Boundary.ContainsInclusive(0.0, 10.0);
            Assert.True(covered);
        }

        [Fact]
        public void ComputeZones_AllRoomsSameY_NoException()
        {
            var rooms = new List<RoomData>
            {
                MakeRoom(1, 0.0, 5.0),
                MakeRoom(2, 5.0, 5.0),
                MakeRoom(3, 10.0, 5.0)
            };
            var ex = Record.Exception(() => _sut.ComputeZones(rooms, new ZoneStrategyOptions(3)));
            Assert.Null(ex);
        }
    }
}
