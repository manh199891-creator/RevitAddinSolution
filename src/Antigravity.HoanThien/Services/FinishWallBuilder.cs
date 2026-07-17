using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Antigravity.HoanThien.Models;

namespace Antigravity.HoanThien.Services
{
    public class FinishWallBuilder
    {
        public static Wall BuildFinishWall(Document doc, Curve roomCurve, FinishLayerConfig finishConfig, Room room, double finishHeight, WallType wallType)
        {
            try
            {
                // Ensure curve length is valid
                if (roomCurve.Length < 0.05) return null;

                var wall = Wall.Create(doc, roomCurve, wallType.Id, room.LevelId, finishHeight, 0.0, false, false);
                if (wall != null)
                {
                    // Disallow join at both ends
                    var locCurve = wall.Location as LocationCurve;
                    if (locCurve != null)
                    {
                        WallUtils.DisallowWallJoinAtEnd(wall, 0);
                        WallUtils.DisallowWallJoinAtEnd(wall, 1);
                    }

                    // Set Shared Parameters
                    var finishTypeParam = wall.LookupParameter("AG_FinishType");
                    if (finishTypeParam != null) finishTypeParam.Set(finishConfig.LayerName);

                    var roomNumberParam = wall.LookupParameter("AG_RoomNumber");
                    if (roomNumberParam != null) roomNumberParam.Set(room.Number);

                    var roomNameParam = wall.LookupParameter("AG_RoomName");
                    if (roomNameParam != null) roomNameParam.Set(room.Name);
                }
                return wall;
            }
            catch (Exception)
            {
                // Revit might reject wall creation (e.g., too thin)
                return null;
            }
        }
    }
}
