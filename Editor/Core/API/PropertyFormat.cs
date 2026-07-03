// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Display format for a property value in the findings table. Format-specific
    /// output details (the K/M/B/T abbreviation for <see cref="NumberAbbreviated"/>,
    /// the adaptive unit choice for <see cref="DurationAdaptive"/>, etc.) belong to
    /// the formatter, not this enum.
    /// </summary>
    /// <remarks>
    /// Values are pinned so the enum's integer representation is stable across
    /// inserts and reorders. Reports persisted as JSON use the
    /// <see cref="StringEnumConverter"/> for forward-compatible names on the wire.
    /// </remarks>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PropertyFormat
    {
        /// <summary>Raw text.</summary>
        Text = 0,

        /// <summary>Boolean value; renders as an icon (checkmark / nothing) rather than text.</summary>
        Boolean = 1,

        /// <summary>Raw integer.</summary>
        Number = 2,

        /// <summary>Integer with magnitude suffix for large values (K / M / B / T).</summary>
        NumberAbbreviated = 3,

        /// <summary>File or memory size with binary (1024-based) units.</summary>
        DataSize = 4,

        /// <summary>Duration in seconds at fixed precision.</summary>
        DurationFixed = 5,

        /// <summary>Duration in seconds with an adaptive unit chosen from the value's magnitude.</summary>
        DurationAdaptive = 6,

        /// <summary>Frequency value.</summary>
        Frequency = 7,

        /// <summary>Percentage of a 0-1 value.</summary>
        Percentage = 8,

        /// <summary>Resolution as a "WxH" string. See <c>Formatting.FormatResolution</c> and <c>Formatting.CompareResolution</c>.</summary>
        Resolution = 9
    }
}
