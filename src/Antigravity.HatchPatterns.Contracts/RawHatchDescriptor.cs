using System.Collections.Generic;

namespace Antigravity.HatchPatterns.Contracts
{
    public class RawHatchDescriptor
    {
        public string MaterialName { get; set; }
        public string PatternName { get; set; }
        public bool IsDrafting { get; set; }
        public List<RawPatternLine> Lines { get; set; }

        public RawHatchDescriptor()
        {
            Lines = new List<RawPatternLine>();
        }
    }
}
