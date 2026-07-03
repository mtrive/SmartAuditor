// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Utils;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Single source of truth for converting a <see cref="PropertyFormat"/> plus a stored
    /// property value into displayable text. Consumed by the evidence panel, clipboard export,
    /// and any future table renderers.
    /// </summary>
    internal static class PropertyFormatting
    {
        /// <summary>
        /// Formats <paramref name="raw"/> according to the <see cref="InsightSchema"/> column whose
        /// <see cref="InsightColumn.Name"/> matches <paramref name="columnSlug"/>.
        /// </summary>
        public static string FormatPropertyText(InsightSchema schema, string columnSlug, string raw)
        {
            if (string.IsNullOrEmpty(raw) || schema == null || string.IsNullOrEmpty(columnSlug))
                return raw;

            var column = schema.GetColumn(columnSlug);
            return column == null ? raw : FormatPropertyText(column.Format, raw);
        }

        /// <summary>
        /// Formats a stored property string for text display according to <paramref name="format"/>.
        /// </summary>
        /// <param name="format">The property's declared format.</param>
        /// <param name="raw">The stored value as a string.</param>
        public static string FormatPropertyText(PropertyFormat format, string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            switch (format)
            {
                case PropertyFormat.Boolean:
                    return bool.TryParse(raw, out var b) ? b ? "Yes" : "No" : raw;
                case PropertyFormat.DataSize:
                    return ulong.TryParse(raw, out var sz) ? Formatting.FormatSize(sz) : raw;
                case PropertyFormat.DurationFixed:
                    return float.TryParse(raw, out var df) ? Formatting.FormatLengthInSeconds(df) : raw;
                case PropertyFormat.DurationAdaptive:
                    return float.TryParse(raw, out var da) ? Formatting.FormatTime(da) : raw;
                case PropertyFormat.Frequency:
                    return int.TryParse(raw, out var fr) ? Formatting.FormatFrequency(fr) : raw;
                case PropertyFormat.Percentage:
                    return float.TryParse(raw, out var pct) ? Formatting.FormatPercentage(pct, 1) : raw;
                case PropertyFormat.NumberAbbreviated:
                    return ulong.TryParse(raw, out var na) ? Formatting.FormatNumber(na) : raw;
                default:
                    return raw;
            }
        }
    }
}
