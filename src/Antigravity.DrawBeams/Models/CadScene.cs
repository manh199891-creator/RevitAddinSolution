using System.Collections.Generic;

namespace Antigravity.DrawBeams.Models
{
    public class CadScene
    {
        public List<CadSegment> Segments { get; set; } = new List<CadSegment>();
        public List<CadText> Texts { get; set; } = new List<CadText>();
    }
}
