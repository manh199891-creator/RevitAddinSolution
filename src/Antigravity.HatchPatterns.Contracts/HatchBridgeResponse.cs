using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Antigravity.HatchPatterns.Contracts
{
    public class HatchBridgeResponse
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        [JsonProperty("bridgeVersion")]
        public string BridgeVersion { get; set; }

        [JsonProperty("autoCadVersion")]
        public string AutoCadVersion { get; set; }

        [JsonProperty("sourceDocumentFingerprint")]
        public string SourceDocumentFingerprint { get; set; }

        [JsonProperty("items")]
        public List<HatchBridgeResponseItem> Items { get; set; }

        public HatchBridgeResponse()
        {
            Items = new List<HatchBridgeResponseItem>();
        }
    }

    public class HatchBridgeResponseItem
    {
        [JsonProperty("handle")]
        public string Handle { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("patternName")]
        public string PatternName { get; set; }

        [JsonProperty("layer")]
        public string Layer { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("patternType")]
        public string PatternType { get; set; }

        [JsonProperty("patternScale")]
        public double PatternScale { get; set; }

        [JsonProperty("patternAngleRadians")]
        public double PatternAngleRadians { get; set; }

        [JsonProperty("patternDouble")]
        public bool PatternDouble { get; set; }

        [JsonProperty("isSolid")]
        public bool IsSolid { get; set; }

        [JsonProperty("definitionSemantics")]
        public string DefinitionSemantics { get; set; }

        [JsonProperty("definitionLines")]
        public List<RawPatternLine> DefinitionLines { get; set; }

        [JsonProperty("diagnostics")]
        public List<string> Diagnostics { get; set; }

        public HatchBridgeResponseItem()
        {
            DefinitionLines = new List<RawPatternLine>();
            Diagnostics = new List<string>();
        }
    }
}
