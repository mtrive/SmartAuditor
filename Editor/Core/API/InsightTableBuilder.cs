// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Mutable builder for an <see cref="InsightTable"/>. Returned from
    /// <see cref="AnalysisContext.GetInsightTable"/>; analyzers call <see cref="AddRow"/> as they
    /// enumerate assets. The orchestrator finalises the builder into an immutable
    /// <see cref="InsightTable"/> at flush time.
    /// </summary>
    public sealed class InsightTableBuilder
    {
        public AnalysisCategory Category { get; }
        public InsightSchema Schema { get; }

        readonly List<InsightRow> m_Rows = new List<InsightRow>();

        internal InsightTableBuilder(AnalysisCategory category, InsightSchema schema)
        {
            Category = category;
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        /// <summary>
        /// Append a row anchored by <paramref name="path"/>. Every key in <paramref name="values"/>
        /// must be declared in the schema; unknown keys throw to surface analyzer typos at the
        /// emission site rather than silently in the report.
        /// </summary>
        public void AddRow(string path, IReadOnlyDictionary<string, object> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            foreach (var kvp in values)
            {
                if (!Schema.Contains(kvp.Key))
                    throw new ArgumentException(
                        $"Insight column '{kvp.Key}' is not declared in the {Category} schema.",
                        nameof(values));
            }

            var copy = new Dictionary<string, object>(values.Count, StringComparer.Ordinal);
            foreach (var kvp in values)
                copy[kvp.Key] = kvp.Value;

            m_Rows.Add(new InsightRow(path ?? string.Empty, copy));
        }

        public int RowCount => m_Rows.Count;

        internal InsightTable Build()
        {
            return new InsightTable(Category, Schema, m_Rows.ToArray());
        }

        internal void Clear() => m_Rows.Clear();
    }
}
