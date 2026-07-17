using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Antigravity.WallMepClash.Models
{
    public class ClashCheckSettings
    {
        public RevitLinkInstance SelectedLink { get; set; }
        public List<BuiltInCategory> CategoriesToCheck { get; set; } = new List<BuiltInCategory>();
        public double ParallelThresholdDeg { get; set; } = 10.0;
        public bool ActiveViewOnly { get; set; } = false;
    }
}
