using System.Collections.Generic;

namespace Antigravity.ArchModeling.Models
{
    public class CombinedScanResult
    {
        public Dictionary<string, List<WallData>> Walls { get; set; } = new Dictionary<string, List<WallData>>();
        public Dictionary<string, List<BlockInfo>> Doors { get; set; } = new Dictionary<string, List<BlockInfo>>();
    }
}
