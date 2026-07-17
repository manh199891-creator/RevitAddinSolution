using Autodesk.Revit.DB;
using System;

namespace Antigravity.ZoneSplit.Models
{
    public sealed class ZoneVolume
    {
        public ElementId SourceId { get; }
        public string ZoneId { get; }
        public string ZoneName { get; }
        public Solid Solid { get; }

        public ZoneVolume(ElementId id, string zoneId, string zoneName, Solid solid)
        {
            SourceId = id;
            ZoneId = zoneId ?? throw new ArgumentNullException(nameof(zoneId));
            ZoneName = zoneName ?? string.Empty;
            Solid = solid ?? throw new ArgumentNullException(nameof(solid));
        }
    }
}
