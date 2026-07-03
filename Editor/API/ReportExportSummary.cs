using System.Collections.Generic;
using Newtonsoft.Json;

namespace SmartAuditor.Editor
{
    [JsonObject]
    sealed class ReportExportSummary
    {
        [JsonProperty("issueCount")]
        public int IssueCount { get; set; }

        [JsonProperty("insightCount")]
        public int InsightCount { get; set; }

        [JsonProperty("messageCount")]
        public int MessageCount { get; set; }

        [JsonProperty("issuesBySeverity")]
        public Dictionary<string, int> IssuesBySeverity { get; set; }

        [JsonProperty("issuesByCategory")]
        public Dictionary<string, int> IssuesByCategory { get; set; }

        [JsonProperty("messagesBySeverity")]
        public Dictionary<string, int> MessagesBySeverity { get; set; }

        [JsonProperty("messagesBySource")]
        public Dictionary<string, int> MessagesBySource { get; set; }
    }
}
