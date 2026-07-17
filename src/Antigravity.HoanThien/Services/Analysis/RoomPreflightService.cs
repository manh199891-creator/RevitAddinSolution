using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Antigravity.HoanThien.Models;

namespace Antigravity.HoanThien.Services.Analysis
{
    public interface IRoomPreflightService
    {
        RoomContext AnalyzeRoom(Room room);
    }

    public class RoomPreflightService : IRoomPreflightService
    {
        private readonly SpatialElementBoundaryOptions _boundaryOptions;

        public RoomPreflightService()
        {
            _boundaryOptions = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
            };
        }

        public RoomContext AnalyzeRoom(Room room)
        {
            var context = new RoomContext(room);

            if (room.Area <= 0 || room.Location == null)
            {
                context.Status = PlanStatus.Error;
                context.StatusMessage = "Phòng không hợp lệ hoặc không có diện tích.";
                return context;
            }

            if (!SpatialElementGeometryCalculator.CanCalculateGeometry(room))
            {
                context.Status = PlanStatus.Error;
                context.StatusMessage = "Không thể tính toán hình học phòng.";
                return context;
            }

            var calculator = new SpatialElementGeometryCalculator(room.Document, _boundaryOptions);

            var results = calculator.CalculateSpatialElementGeometry(room);
            var roomSolid = results.GetGeometry();

            foreach (Face face in roomSolid.Faces)
            {
                var boundaryFaces = results.GetBoundaryFaceInfo(face);
                foreach (var boundaryFace in boundaryFaces)
                {
                    var hostElement = room.Document.GetElement(boundaryFace.SpatialBoundaryElement.HostElementId);
                    if (hostElement == null) continue;

                    var surface = new BoundarySurface
                    {
                        HostElementId = hostElement.Id,
                        HostElement = hostElement
                        // In a real implementation we would extract the boundary curve representing the face intersection
                    };
                    context.Surfaces.Add(surface);
                }
            }

            return context;
        }
    }
}
