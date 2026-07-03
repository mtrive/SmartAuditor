// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartAuditor.Editor.Utils
{
    /// <summary>
    /// Formats <see cref="ReportItem.Evidence"/> values for display as a single text label.
    /// Handles both raw CLR values (as authored by analyzers in the same process) and
    /// <see cref="JToken"/> values (the shape after a JSON round-trip), so consumers don't have
    /// to branch on whether the report was just produced or loaded from disk.
    /// Array/list values are joined with newlines so the IMGUI label renders them multi-line.
    /// Booleans are rendered canonically as <c>true</c>/<c>false</c> (matching the serialized
    /// JSON); the human-facing report UI opts into <c>Yes</c>/<c>No</c> via <c>boolAsYesNo</c>.
    /// Suitable for the in-editor report footer; exporters that need a different separator
    /// or escaping should not reuse this directly.
    /// </summary>
    internal static class EvidenceFormatter
    {
        /// <summary>
        /// Formats an evidence value for display.
        /// </summary>
        /// <param name="value">The raw CLR value or <see cref="JToken"/> from a JSON round-trip.</param>
        /// <param name="boolAsYesNo">
        /// When true, booleans render as <c>Yes</c>/<c>No</c> for human-facing UI; otherwise they
        /// render canonically as <c>true</c>/<c>false</c>. This only affects display: serialization
        /// always stores the underlying CLR <see cref="bool"/>.
        /// </param>
        public static string Format(object value, bool boolAsYesNo = false)
        {
            if (value == null)
                return string.Empty;

            if (value is JToken token)
                return FormatToken(token, boolAsYesNo);

            if (value is bool b)
                return FormatBool(b, boolAsYesNo);

            if (value is string s)
                return s;

            if (value is IEnumerable<string> strings)
                return string.Join("\n", strings);

            if (value is IEnumerable enumerable)
            {
                var items = new List<string>();
                foreach (var element in enumerable)
                    items.Add(element == null ? string.Empty : Convert.ToString(element, CultureInfo.InvariantCulture));
                return string.Join("\n", items);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        // Canonical bool text matches the JSON form (lowercase true/false); the UI passes
        // boolAsYesNo to surface the friendlier Yes/No without changing what gets serialized.
        static string FormatBool(bool value, bool boolAsYesNo)
        {
            if (boolAsYesNo)
                return value ? "Yes" : "No";
            return value ? "true" : "false";
        }

        static string FormatToken(JToken token, bool boolAsYesNo)
        {
            switch (token.Type)
            {
                case JTokenType.Null:
                    return string.Empty;
                case JTokenType.String:
                    return token.Value<string>() ?? string.Empty;
                case JTokenType.Boolean:
                    return FormatBool(token.Value<bool>(), boolAsYesNo);
                case JTokenType.Integer:
                case JTokenType.Float:
                    return token.ToString(Newtonsoft.Json.Formatting.None);
                case JTokenType.Array:
                    return FormatArray((JArray)token, boolAsYesNo);
                default:
                    return token.ToString(Newtonsoft.Json.Formatting.None);
            }
        }

        static string FormatArray(JArray array, bool boolAsYesNo)
        {
            // Flatten arrays of scalars to newline-joined values so the footer reads as a list.
            // Mixed / nested arrays fall back to compact JSON so the data is visible without a recursion budget.
            foreach (var element in array)
            {
                switch (element.Type)
                {
                    case JTokenType.String:
                    case JTokenType.Integer:
                    case JTokenType.Float:
                    case JTokenType.Boolean:
                    case JTokenType.Null:
                        continue;
                    default:
                        return array.ToString(Newtonsoft.Json.Formatting.None);
                }
            }

            var items = new List<string>(array.Count);
            foreach (var element in array)
            {
                if (element.Type == JTokenType.String)
                    items.Add(element.Value<string>() ?? string.Empty);
                else if (element.Type == JTokenType.Boolean)
                    items.Add(FormatBool(element.Value<bool>(), boolAsYesNo));
                else
                    items.Add(element.ToString(Newtonsoft.Json.Formatting.None));
            }
            return string.Join("\n", items);
        }
    }
}
