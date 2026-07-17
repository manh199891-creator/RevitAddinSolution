using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Antigravity.HoanThien.Models;

namespace Antigravity.HoanThien.Services
{
    public class FinishFloorBuilder
    {
        public static Floor BuildFinishFloor(Document doc, Room room, FinishLayerConfig finishConfig, ElementId levelId, FloorType floorType)
        {
            try
            {
                var options = new SpatialElementBoundaryOptions
                {
                    SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
                };

                var boundaries = room.GetBoundarySegments(options);
                if (boundaries == null || boundaries.Count == 0) return null;

                var loops = new List<CurveLoop>();
                foreach (var segmentList in boundaries)
                {
                    var loop = new CurveLoop();
                    foreach (var seg in segmentList)
                    {
                        var curve = seg.GetCurve();
                        if (curve.Length >= 0.05) // R1 mitigation adapted for floor
                        {
                            loop.Append(curve);
                        }
                    }
                    if (loop.IsOpen() == false && loop.NumberOfCurves() > 2)
                    {
                        loops.Add(loop);
                    }
                }

                if (loops.Count > 0)
                {
                    var floor = Floor.Create(doc, loops, floorType.Id, levelId);
                    if (floor != null)
                    {
                        var heightParam = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                        var thicknessParam = floorType.get_Parameter(BuiltInParameter.FLOOR_ATTR_DEFAULT_THICKNESS_PARAM);
                        double thicknessInFeet = thicknessParam != null ? thicknessParam.AsDouble() : 0.05;

                        if (heightParam != null) heightParam.Set(-thicknessInFeet);

                        var finishTypeParam = floor.LookupParameter("AG_FinishType");
                        if (finishTypeParam != null) finishTypeParam.Set(finishConfig.LayerName);
                        
                        var roomNumberParam = floor.LookupParameter("AG_RoomNumber");
                        if (roomNumberParam != null) roomNumberParam.Set(room.Number);
                        
                        var roomNameParam = floor.LookupParameter("AG_RoomName");
                        if (roomNameParam != null) roomNameParam.Set(room.Name);
                    }
                    return floor;
                }
            }
            catch (Exception)
            {
                // Ignore creation errors
            }
            return null;
        }
    }
}
