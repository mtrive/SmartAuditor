// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Ordered list of <see cref="InsightColumn"/>s describing the shape of every row in an
    /// <see cref="InsightTable"/>. Each <see cref="AnalysisModule"/> declares one schema per
    /// insight category it emits; the schema is embedded in the exported JSON so a consumer
    /// (e.g. an LLM via MCP) can interpret row values without consulting C# code.
    /// </summary>
    public sealed class InsightSchema
    {
        [JsonProperty("columns")]
        public IReadOnlyList<InsightColumn> Columns { get; }

        [JsonIgnore]
        readonly Dictionary<string, int> m_IndexByName;

        public InsightSchema(params InsightColumn[] columns)
        {
            if (columns == null)
                throw new ArgumentNullException(nameof(columns));

            Columns = columns;
            m_IndexByName = new Dictionary<string, int>(columns.Length, StringComparer.Ordinal);
            for (var i = 0; i < columns.Length; i++)
            {
                var name = columns[i].Name;
                if (m_IndexByName.ContainsKey(name))
                    throw new ArgumentException($"Insight schema contains duplicate column name '{name}'.", nameof(columns));
                m_IndexByName[name] = i;
            }
        }

        /// <summary>Returns the column with the given name, or null if no such column exists.</summary>
        public InsightColumn GetColumn(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            return m_IndexByName.TryGetValue(name, out var index) ? Columns[index] : null;
        }

        /// <summary>Returns the column's position, or -1 if no such column exists.</summary>
        public int IndexOf(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;
            return m_IndexByName.TryGetValue(name, out var index) ? index : -1;
        }

        public bool Contains(string name) => IndexOf(name) >= 0;
    }
}
