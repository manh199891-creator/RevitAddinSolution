using System.Collections.Generic;

namespace Antigravity.HoanThien.Models
{
    public class FinishLayerRule
    {
        public SubstrateClass TargetSubstrate { get; set; }
        public FinishLayerKind LayerKind { get; set; }
        public string ElementTypeName { get; set; }
        public double Thickness { get; set; }
        public TopConstraintMode TopConstraint { get; set; }
        public double TopOffset { get; set; }
    }

    public class FinishPlan
    {
        public List<RoomContext> Rooms { get; } = new List<RoomContext>();
        public ProcessingMode Mode { get; set; } = ProcessingMode.CreateMissing;
    }
}
