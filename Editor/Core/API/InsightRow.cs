// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// One row of an <see cref="InsightTable"/>. Anchored by an asset <see cref="Path"/>;
    /// <see cref="Values"/> is keyed by the column name (<see cref="InsightColumn.Name"/>).
    /// Values are stored as their natural CLR type (string, long, int, bool, double, string[], ...)
    /// so Newtonsoft can round-trip them to JSON without a custom converter.
    /// </summary>
    public sealed class InsightRow
    {
        [JsonProperty("path")]
        public string Path { get; }

        [JsonProperty("values")]
        public IReadOnlyDictionary<string, object> Values { get; }

        internal InsightRow(string path, IReadOnlyDictionary<string, object> values)
        {
            Path = path ?? string.Empty;
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }
    }
}
