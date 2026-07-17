using System.Collections.Generic;

namespace Antigravity.AutoDimWalls.Models
{
    public sealed class AutoDimResult
    {
        public int WallsProcessed { get; set; }
        public int DimensionsCreated { get; set; }
        public int WallsSkipped { get; set; }
        public int DimensionsDeleted { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> LogMessages { get; set; } = new List<string>();
    }
}
