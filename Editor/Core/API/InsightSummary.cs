// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Compact aggregate over the rows of an <see cref="InsightTable"/>, computed at export time.
    /// Intended for MCP / LLM consumers that want counts, totals, distributions, and top-N rankings
    /// without paging through every row. See <see cref="ColumnHints"/> for how each table opts in.
    /// </summary>
    public sealed class InsightSummary
    {
        /// <summary>Row count.</summary>
        [JsonProperty("count")]
        public int Count { get; internal set; }

        /// <summary>
        /// Sum per <see cref="ColumnHints.Aggregatable"/> column. Value type preserves the column's
        /// native CLR type (long for byte counts, double for floats) so JSON serialises naturally.
        /// </summary>
        [JsonProperty("totals", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> Totals { get; internal set; }

        /// <summary>
        /// For each <see cref="ColumnHints.Categorical"/> column, a count of how many rows carry
        /// each distinct value. Keyed by column name -> value string -> count.
        /// </summary>
        [JsonProperty("distributions", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, Dictionary<string, int>> Distributions { get; internal set; }

        /// <summary>
        /// For each <see cref="ColumnHints.TopNMetric"/> column, the rows with the highest values.
        /// </summary>
        [JsonProperty("topByMetric", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, List<InsightTopEntry>> TopByMetric { get; internal set; }

        internal InsightSummary()
        {
            Totals = new Dictionary<string, object>();
            Distributions = new Dictionary<string, Dictionary<string, int>>();
            TopByMetric = new Dictionary<string, List<InsightTopEntry>>();
        }
    }

    /// <summary>One entry in an <see cref="InsightSummary.TopByMetric"/> list.</summary>
    public sealed class InsightTopEntry
    {
        [JsonProperty("path")]
        public string Path { get; internal set; }

        [JsonProperty("value")]
        public object Value { get; internal set; }
    }
}
