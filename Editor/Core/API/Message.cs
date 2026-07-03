// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A toolchain diagnostic — a compiler warning, a shader compilation error, an asset-importer
    /// message. Distinct from <see cref="Diagnostic"/> (descriptor-driven, severity-bearing issues)
    /// and from <see cref="InsightTable"/> rows (per-asset tabular data). Messages carry their own
    /// <see cref="LogLevel"/> (set by the producing tool) and a small string-keyed
    /// <see cref="Properties"/> dictionary (e.g. <c>{"Code":"CS0219","Assembly":"FM.Editor"}</c>).
    /// </summary>
    public sealed class Message : IReportEntry
    {
        [JsonProperty("category")]
        public AnalysisCategory Category { get; }

        [JsonProperty("description")]
        public string Description { get; }

        [JsonProperty("logLevel")]
        public LogLevel LogLevel { get; }

        [JsonProperty("location", NullValueHandling = NullValueHandling.Ignore)]
        public Location Location { get; }

        [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyDictionary<string, string> Properties { get; }

        [JsonProperty("fingerprint", NullValueHandling = NullValueHandling.Ignore)]
        public string Fingerprint { get; }

        [JsonProperty("fingerprintStability", NullValueHandling = NullValueHandling.Ignore)]
        public string FingerprintStability { get; }

        // [JsonConstructor] tells Newtonsoft this is the constructor to invoke for
        // deserialization. Parameter names are matched to JSON property names
        // (case-insensitive), so "category", "description", "logLevel", "location",
        // and "properties" from the JSON map onto the corresponding parameters here.
        // Fingerprint / FingerprintStability are recomputed from the inputs — the
        // serialized "fingerprint" / "fingerprintStability" JSON entries are ignored
        // on load and would be identical anyway given the same input content.
        [JsonConstructor]
        internal Message(
            AnalysisCategory category,
            string description,
            LogLevel logLevel,
            Location location,
            IReadOnlyDictionary<string, string> properties)
        {
            Category = category;
            Description = description ?? string.Empty;
            LogLevel = logLevel;
            Location = location;
            Properties = properties;
            (Fingerprint, FingerprintStability) = ComputeFingerprint();
        }

        (string fingerprint, string stability) ComputeFingerprint()
        {
            var parts = new List<string>();
            parts.Add($"category:{Category}");

            var hasPath = false;
            var hasSpecific = false;

            var normalizedPath = FingerprintUtils.NormalizeFingerprintPart(Location?.Path).Replace('\\', '/');
            if (!string.IsNullOrEmpty(normalizedPath))
            {
                parts.Add($"path:{normalizedPath}");
                hasPath = true;
            }

            if (Properties != null)
            {
                var keys = new List<string>(Properties.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (var key in keys)
                {
                    var value = FingerprintUtils.NormalizeFingerprintPart(Properties[key]);
                    if (string.IsNullOrEmpty(value))
                        continue;
                    parts.Add($"prop[{key}]:{value}");
                    hasSpecific = true;
                }
            }

            if (!string.IsNullOrEmpty(Description))
            {
                parts.Add($"message:{FingerprintUtils.NormalizeFingerprintPart(Description)}");
                hasSpecific = true;
            }

            var line = Location?.Line;
            if (line.HasValue)
            {
                parts.Add($"line:{line.Value.ToString(CultureInfo.InvariantCulture)}");
                hasSpecific = true;
            }

            var stability = hasPath && hasSpecific ? "Good" : hasPath ? "Medium" : hasSpecific ? "Weak" : "Weak";
            return (FingerprintUtils.ComputeSha256Fingerprint(parts), stability);
        }
    }
}
