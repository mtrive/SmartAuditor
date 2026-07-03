// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using Newtonsoft.Json;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Per-column hints that drive both summary computation at export time and the report-view footer.
    /// </summary>
    [Flags]
    public enum ColumnHints
    {
        None = 0,
        /// <summary>Emit a count-by-value distribution in <see cref="InsightSummary.Distributions"/>.</summary>
        Categorical = 1 << 0,
        /// <summary>Emit a sum in <see cref="InsightSummary.Totals"/>. Numeric columns only.</summary>
        Aggregatable = 1 << 1,
        /// <summary>Emit a top-N ranking in <see cref="InsightSummary.TopByMetric"/>. Numeric columns only.</summary>
        TopNMetric = 1 << 2,
    }

    /// <summary>
    /// Declares one column of an <see cref="InsightSchema"/>. The <see cref="Name"/> is the stable slug
    /// used as the JSON key on every row and as the C# key in <see cref="InsightRow.Values"/>; the
    /// <see cref="DisplayName"/> is the user-facing label rendered in tables and the report footer.
    /// </summary>
    public sealed class InsightColumn
    {
        /// <summary>Stable column slug. Used as the JSON key and the row dictionary key.</summary>
        [JsonProperty("name")]
        public string Name { get; }

        /// <summary>User-facing column label. Defaults to <see cref="Name"/> when not supplied.</summary>
        [JsonProperty("displayName")]
        public string DisplayName { get; }

        /// <summary>Rendering format. Drives both UI cell formatting and summary semantics.</summary>
        [JsonProperty("format")]
        public PropertyFormat Format { get; }

        /// <summary>Summary-computation hints. See <see cref="ColumnHints"/>.</summary>
        [JsonProperty("hints", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ColumnHints Hints { get; }

        /// <summary>Default grouping column in the table view.</summary>
        [JsonIgnore]
        public bool IsDefaultGroup { get; }

        /// <summary>Optional auto-width upper bound, in pixels. 0 means no preferred width.</summary>
        [JsonIgnore]
        public int MaxAutoWidth { get; }

        /// <summary>When true, this column word-wraps and contributes to variable row heights.</summary>
        [JsonIgnore]
        public bool Wrap { get; }

        /// <summary>
        /// Long-form column header used as a tooltip when hovering the column header in the table
        /// view. Defaults to <see cref="DisplayName"/> when not explicitly set. UI-only; not exported.
        /// </summary>
        [JsonIgnore]
        public string LongName { get; }

        /// <summary>
        /// When true, the column is hidden in the table view. Typically used to keep a column
        /// available for grouping (<see cref="IsDefaultGroup"/>) without taking up screen space.
        /// UI-only; not exported.
        /// </summary>
        [JsonIgnore]
        public bool IsHidden { get; }

        public InsightColumn(
            string name,
            string displayName = null,
            PropertyFormat format = PropertyFormat.Text,
            ColumnHints hints = ColumnHints.None,
            bool isDefaultGroup = false,
            int maxAutoWidth = 0,
            bool wrap = false,
            string longName = null,
            bool isHidden = false)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Insight column name cannot be null or empty", nameof(name));

            Name = name;
            DisplayName = string.IsNullOrEmpty(displayName) ? name : displayName;
            Format = format;
            Hints = hints;
            IsDefaultGroup = isDefaultGroup;
            MaxAutoWidth = maxAutoWidth;
            Wrap = wrap;
            LongName = string.IsNullOrEmpty(longName) ? DisplayName : longName;
            IsHidden = isHidden;
        }
    }
}
