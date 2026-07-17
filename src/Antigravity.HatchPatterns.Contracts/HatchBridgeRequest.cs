using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Antigravity.HatchPatterns.Contracts
{
    public class HatchBridgeRequest
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        [JsonProperty("requestedAtUtc")]
        public string RequestedAtUtc { get; set; }

        [JsonProperty("sourceDocumentFingerprint")]
        public string SourceDocumentFingerprint { get; set; }

        [JsonProperty("entityHandles")]
        public List<string> EntityHandles { get; set; }

        public HatchBridgeRequest()
        {
            EntityHandles = new List<string>();
        }
    }
}
