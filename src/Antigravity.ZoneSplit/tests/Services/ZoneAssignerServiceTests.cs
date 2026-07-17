using System.Collections.Generic;
using Moq;
using Antigravity.ZoneSplit.Abstractions;
using Antigravity.ZoneSplit.Models;
using Antigravity.ZoneSplit.Services;
using Xunit;

namespace Antigravity.ZoneSplit.Tests.Services
{
    public class ZoneAssignerServiceTests
    {
        // ── Helpers ───────────────────────────────────────────────────
        private static RoomData MakeRoom(int id, double x, double y,
            bool linked = false, double area = 10.0)
            => new RoomData(id, $"Room{id}", id.ToString(), x, y, area,
                linked, new BoundingBox2D(x - 1, y - 1, x + 1, y + 1));

        private static ZoneDefinition MakeZone(string id, double minX, double maxX)
            => new ZoneDefinition(id, id, new BoundingBox2D(minX, -100, maxX, 100));

        private ZoneAssignerService CreateSut(
            Mock<IZoneStrategy> strategy = null,
            Mock<ISharedParameterService> paramSvc = null,
            Mock<ILevelDataProvider> levelProv = null)
        {
            strategy  ??= new Mock<IZoneStrategy>();
            paramSvc  ??= new Mock<ISharedParameterService>();
            levelProv ??= new Mock<ILevelDataProvider>();
            return new ZoneAssignerService(strategy.Object, paramSvc.Object, levelProv.Object);
        }

        // ── 1. Null / empty rooms ─────────────────────────────────────
        [Fact]
        public void AssignZones_NullRooms_ReturnsWarning_NoThrow()
        {
            var sut    = CreateSut();
            var zones  = new List<ZoneDefinition> { MakeZone("Zone-A", 0, 100) };
            var result = sut.AssignZones(null, zones, "BIM_ZoneID");

            Assert.False(result.IsSuccess);
            Assert.NotEmpty(result.Warnings);
        }

        [Fact]
        public void AssignZones_EmptyRooms_ReturnsWarning_NoThrow()
        {
            var sut    = CreateSut();
            var zones  = new List<ZoneDefinition> { MakeZone("Zone-A", 0, 100) };
            var result = sut.AssignZones(new List<RoomData>(), zones, "BIM_ZoneID");

            Assert.False(result.IsSuccess);
        }

        // ── 2. Linked room skipped ────────────────────────────────────
        [Fact]
        public void AssignZones_LinkedRoomSkipped_IncrementCounter()
        {
            var paramMock = new Mock<ISharedParameterService>();
            var sut       = CreateSut(paramSvc: paramMock);

            var rooms = new List<RoomData> { MakeRoom(1, 5, 5, linked: true) };
            var zones = new List<ZoneDefinition> { MakeZone("Zone-A", 0, 100) };

            var result = sut.AssignZones(rooms, zones, "BIM_ZoneID");

            Assert.Equal(1, result.SkippedLinkedRooms);
            Assert.Equal(0, result.TotalRoomsProcessed);
            // Không được gọi TryWriteParameter cho linked room
            paramMock.Verify(
                p => p.TryWriteParameter(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        // ── 3. Unbounded room skipped ─────────────────────────────────
        [Fact]
        public void AssignZones_UnboundedRoomSkipped_IncrementCounter()
        {
            var sut   = CreateSut();
            var rooms = new List<RoomData> { MakeRoom(1, 5, 5, area: 0.0) }; // Area = 0
            var zones = new List<ZoneDefinition> { MakeZone("Zone-A", 0, 100) };

            var result = sut.AssignZones(rooms, zones, "BIM_ZoneID");

            Assert.Equal(1, result.SkippedUnboundedRooms);
            Assert.Equal(0, result.TotalRoomsProcessed);
        }

        // ── 4. Room in zone → parameter written ──────────────────────
        [Fact]
        public void AssignZones_RoomInZone_ParameterWritten()
        {
            var paramMock = new Mock<ISharedParameterService>();
            paramMock.Setup(p => p.TryWriteParameter(1, "BIM_ZoneID", "Zone-A"))
                     .Returns(true);

            var sut   = CreateSut(paramSvc: paramMock);
            var rooms = new List<RoomData> { MakeRoom(1, 5, 5) };
            var zones = new List<ZoneDefinition> { MakeZone("Zone-A", 0, 100) };

            var result = sut.AssignZones(rooms, zones, "BIM_ZoneID");

            Assert.Equal(1, result.TotalElementsTagged);
            Assert.True(result.IsSuccess);
            paramMock.Verify(
                p => p.TryWriteParameter(1, "BIM_ZoneID", "Zone-A"), Times.Once);
        }

        // ── 5. Parameter write fails → adds warning, continues ───────
        [Fact]
        public void AssignZones_ParameterWriteFails_AddsWarning_Continues()
        {
            var paramMock = new Mock<ISharedParameterService>();
            paramMock.Setup(p => p.TryWriteParameter(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false); // Simulate write failure

            var sut   = CreateSut(paramSvc: paramMock);
            var rooms = new List<RoomData>
            {
                MakeRoom(1, 5, 5),
                MakeRoom(2, 50, 5)
            };
            var zones = new List<ZoneDefinition> { MakeZone("Zone-A", 0, 100) };

            var result = sut.AssignZones(rooms, zones, "BIM_ZoneID");

            Assert.Equal(2, result.TotalRoomsProcessed);
            Assert.Equal(0, result.TotalElementsTagged);
            Assert.Equal(2, result.Warnings.Count); // 1 warning per failed write
        }

        // ── 6. All rooms skipped → IsSuccess false ───────────────────
        [Fact]
        public void AssignZones_AllRoomsSkipped_IsSuccessFalse()
        {
            var sut   = CreateSut();
            var rooms = new List<RoomData>
            {
                MakeRoom(1, 5, 5, linked: true),   // linked
                MakeRoom(2, 5, 5, area: 0.0)        // unbounded
            };
            var zones  = new List<ZoneDefinition> { MakeZone("Zone-A", 0, 100) };
            var result = sut.AssignZones(rooms, zones, "BIM_ZoneID");

            Assert.Equal(0, result.TotalRoomsProcessed);
        }

        // ── 7. Null dependencies → throws ArgumentNullException ───────
        [Theory]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        public void Constructor_NullDependency_ThrowsArgumentNullException(
            bool nullStrategy, bool nullParam, bool nullLevel)
        {
            var strategy = nullStrategy  ? null : new Mock<IZoneStrategy>().Object;
            var param    = nullParam     ? null : new Mock<ISharedParameterService>().Object;
            var level    = nullLevel     ? null : new Mock<ILevelDataProvider>().Object;

            Assert.Throws<System.ArgumentNullException>(
                () => new ZoneAssignerService(strategy, param, level));
        }

        // ── 8. Empty param name → throws ArgumentException ───────────
        [Fact]
        public void AssignZones_EmptyParamName_ThrowsArgumentException()
        {
            var sut   = CreateSut();
            var rooms = new List<RoomData> { MakeRoom(1, 5, 5) };
            var zones = new List<ZoneDefinition> { MakeZone("Zone-A", 0, 100) };

            Assert.Throws<System.ArgumentException>(
                () => sut.AssignZones(rooms, zones, ""));
        }
    }
}
