using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Antigravity.HoanThien.Models;

namespace Antigravity.HoanThien.Services
{
    public class RoomBoundaryService
    {
        public static List<Curve> GetBoundarySegments(Room room, FinishLayerConfig config)
        {
            var curves = new List<Curve>();
            var options = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
            };

            var boundarySegments = room.GetBoundarySegments(options);
            if (boundarySegments == null) return curves; // e.g. ComputeVolumes is off or room unbounded

            foreach (var segmentList in boundarySegments)
            {
                foreach (var segment in segmentList)
                {
                    if (segment.ElementId != ElementId.InvalidElementId)
                    {
                        var curve = segment.GetCurve();
                        var elem = room.Document.GetElement(segment.ElementId);
                        if (elem is Wall wall)
                        {
                            if (config.ExcludeCurtainWalls && wall.WallType.Kind == WallKind.Curtain)
                            {
                                continue;
                            }
                            var openings = OpeningHandler.GetOpenings(wall, room.Document);
                            var splitCurves = OpeningHandler.SplitCurveAroundOpening(curve, openings);
                            curves.AddRange(splitCurves);
                        }
                        else
                        {
                            curves.Add(curve);
                        }
                    }
                    else
                    {
                        curves.Add(segment.GetCurve());
                    }
                }
            }

            return curves;
        }
    }
}
