using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Antigravity.CheckFloorElevation.Models
{
    public class CheckSettings
    {
        public double ToleranceMm { get; set; } = 20.0;

        public RevitLinkInstance SelectedLinkInstance { get; set; }

        public bool CheckActiveViewOnly { get; set; }

        public ElementId SelectedHostLevelId { get; set; }

        public IList<ElementId> SelectedHostLevelIds { get; set; }

        public string SelectedHostLevelName { get; set; }
    }
}
