using System.Collections.Generic;
using Newtonsoft.Json;

namespace Antigravity.HatchPatterns.Contracts
{
    public class RawPatternLine
    {
        [JsonProperty("angleRadians")]
        public double AngleRadians { get; set; }

        [JsonProperty("baseX")]
        public double BaseX { get; set; }

        [JsonProperty("baseY")]
        public double BaseY { get; set; }

        [JsonProperty("offsetX")]
        public double OffsetX { get; set; }

        [JsonProperty("offsetY")]
        public double OffsetY { get; set; }

        [JsonProperty("dashLengths")]
        public List<double> DashLengths { get; set; }

        public RawPatternLine()
        {
            DashLengths = new List<double>();
        }
    }
}
