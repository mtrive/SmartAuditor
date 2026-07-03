// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Per-category tabular data emitted by an analyzer for informational purposes.
    /// Carries a stable <see cref="Schema"/> plus a list of <see cref="Rows"/>. The
    /// <see cref="Summary"/> is computed lazily by <see cref="EnsureSummary"/>;
    /// the report serializer requests it at export time.
    /// </summary>
    public sealed class InsightTable
    {
        // Category is the JSON dictionary key in Report.InsightTables, so don't repeat it on the table.
        [JsonIgnore]
        public AnalysisCategory Category { get; }

        [JsonProperty("schema")]
        public InsightSchema Schema { get; }

        [JsonProperty("rows")]
        public IReadOnlyList<InsightRow> Rows { get; }

        InsightSummary m_Summary;

        [JsonProperty("summary")]
        public InsightSummary Summary
        {
            get
            {
                EnsureSummary();
                return m_Summary;
            }
        }

        internal InsightTable(AnalysisCategory category, InsightSchema schema, IReadOnlyList<InsightRow> rows)
        {
            Category = category;
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        internal void EnsureSummary()
        {
            if (m_Summary != null)
                return;

            var summary = new InsightSummary
            {
                Count = Rows.Count
            };

            const int k_TopN = 10;

            foreach (var column in Schema.Columns)
            {
                var hints = column.Hints;
                if (hints == ColumnHints.None)
                    continue;

                if ((hints & ColumnHints.Aggregatable) != 0)
                    summary.Totals[column.Name] = ComputeTotal(column);

                if ((hints & ColumnHints.Categorical) != 0)
                    summary.Distributions[column.Name] = ComputeDistribution(column);

                if ((hints & ColumnHints.TopNMetric) != 0)
                    summary.TopByMetric[column.Name] = ComputeTopByMetric(column, k_TopN);
            }

            m_Summary = summary;
        }

        object ComputeTotal(InsightColumn column)
        {
            var useDouble = false;
            double totalDouble = 0;
            long totalLong = 0;

            foreach (var row in Rows)
            {
                if (!row.Values.TryGetValue(column.Name, out var value) || value == null)
                    continue;

                if (useDouble)
                {
                    if (ValueCoercion.TryToDouble(value, out var asDouble))
                        totalDouble += asDouble;
                    continue;
                }

                if (ValueCoercion.UsesFloatingPointTotal(value))
                {
                    useDouble = true;
                    totalDouble = totalLong;
                    if (ValueCoercion.TryToDouble(value, out var asDouble))
                        totalDouble += asDouble;
                    continue;
                }

                if (ValueCoercion.TryToLong(value, out var asLong))
                    totalLong = unchecked(totalLong + asLong);
                else if (ValueCoercion.TryToDouble(value, out var wholeDouble))
                    totalLong = unchecked(totalLong + (long)wholeDouble);
            }

            // Preserve integer type when no row produced a fractional value, so byte counts stay as longs.
            return useDouble ? (object)totalDouble : (object)totalLong;
        }

        Dictionary<string, int> ComputeDistribution(InsightColumn column)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in Rows)
            {
                if (!row.Values.TryGetValue(column.Name, out var value) || value == null)
                    continue;

                var key = ValueCoercion.CoerceString(value);
                if (counts.TryGetValue(key, out var existing))
                    counts[key] = existing + 1;
                else
                    counts[key] = 1;
            }
            return counts;
        }

        List<InsightTopEntry> ComputeTopByMetric(InsightColumn column, int n)
        {
            // Build (path, doubleValue, rawValue) triples, sort desc, take n. The raw value is what
            // we emit in JSON so consumers see the column's native type.
            var entries = new List<(string Path, double SortKey, object Raw)>(Rows.Count);
            foreach (var row in Rows)
            {
                if (!row.Values.TryGetValue(column.Name, out var value) || value == null)
                    continue;
                if (!ValueCoercion.TryToDouble(value, out var asDouble))
                    continue;
                entries.Add((row.Path, asDouble, value));
            }

            entries.Sort((a, b) => b.SortKey.CompareTo(a.SortKey));

            var result = new List<InsightTopEntry>(Math.Min(n, entries.Count));
            for (var i = 0; i < entries.Count && i < n; i++)
            {
                result.Add(new InsightTopEntry { Path = entries[i].Path, Value = entries[i].Raw });
            }
            return result;
        }
    }
}
