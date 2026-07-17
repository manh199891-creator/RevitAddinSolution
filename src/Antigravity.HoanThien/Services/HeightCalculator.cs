using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Antigravity.HoanThien.Services
{
    public class HeightCalculator
    {
        public static double GetFinishHeight(Room room, double heightOffsetAboveCeilingMm)
        {
            if (room.UpperLimit != null)
            {
                double offsetFt = UnitUtils.ConvertToInternalUnits(heightOffsetAboveCeilingMm, UnitTypeId.Millimeters);
                // Return relative height of room's upper limit + room's limit offset + extra offset
                double roomBaseElevation = room.Level.Elevation;
                double upperElevation = room.UpperLimit.Elevation;
                return (upperElevation - roomBaseElevation) + room.LimitOffset + offsetFt;
            }
            else
            {
                // Fallback to room's unbounded height if no upper limit is set
                double offsetFt = UnitUtils.ConvertToInternalUnits(heightOffsetAboveCeilingMm, UnitTypeId.Millimeters);
                return room.UnboundedHeight + offsetFt;
            }
        }
    }
}
