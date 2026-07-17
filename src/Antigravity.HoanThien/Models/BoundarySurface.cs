using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Antigravity.HoanThien.Models
{
    public class BoundarySurface
    {
        public Curve Curve { get; set; }
        public ElementId HostElementId { get; set; }
        public SubstrateClass Substrate { get; set; } = SubstrateClass.Unknown;
        public Element HostElement { get; set; }
        
        // Raycast resolved ceiling
        public double ResolvedCeilingHeight { get; set; }
        public ElementId CeilingElementId { get; set; }
        
        public List<FinishLayerRule> AppliedRules { get; } = new List<FinishLayerRule>();
    }
}
