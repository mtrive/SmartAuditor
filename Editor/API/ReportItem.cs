using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Describes an individual report item
    /// </summary>
    [Serializable]
    public class ReportItem : IReportEntry
    {
        [SerializeField]
        [JsonIgnore]
        Descriptor m_Descriptor;

        [SerializeField]
        [JsonIgnore]
        string m_DescriptorId;

        [SerializeField]
        AnalysisCategory m_Category;

        [SerializeField]
        string m_Description;

        [SerializeField]
        Severity m_Severity;

        [SerializeField]
        DependencyNode m_Dependencies;

        [SerializeField]
        Location m_Location;

        // Keyed by the property's canonical name, which by convention matches the enum value
        // name on the analyzer side. Newtonsoft
        // serializes this directly; Unity's [SerializeField] doesn't support generic
        // dictionaries, but ReportItems are persisted as JSON, so the in-memory-only Unity
        // path is acceptable — Message already works the same way.
        Dictionary<string, string> m_Properties;

        static readonly IReadOnlyDictionary<string, string> s_EmptyProperties = new Dictionary<string, string>(0);

        [NonSerialized]
        Dictionary<string, object> m_Evidence;

        [SerializeField]
        string m_Fingerprint;

        [SerializeField]
        string m_FingerprintStability;

        [SerializeField]
        string[] m_FingerprintParts;

        [NonSerialized]
        string[] m_MessageArgs;

        [JsonIgnore]
        public Descriptor Descriptor
        {
            get => m_Descriptor;
            internal set => m_Descriptor = value;
        }

        /// <summary>
        /// A unique identifier for the issue descriptor (read-only).
        /// </summary>
        /// <remarks>
        /// Reports can contain two different types of ReportItem:
        /// - Issues, which indicate a potential problem which should be investigated and possibly fixed: for example, a texture with its Read/Write Enabled checkbox ticked.
        /// - Insights, for informational purposes: for example, general information about a texture in the project.
        ///
        /// Issues can be identified by having a valid <seealso cref="Id"/>.
        /// </remarks>
        [JsonIgnore]
        public string Id => m_DescriptorId;

        // Private JSON property used solely for serialization/deserialization.
        // The setter rehydrates the descriptor from the library after load.
        [JsonProperty("id")]
        string JsonId
        {
            get => m_DescriptorId;
            set => SetDescriptorId(value);
        }

        void SetDescriptorId(string id)
        {
            m_DescriptorId = id ?? string.Empty;
            if (string.IsNullOrEmpty(m_DescriptorId))
            {
                m_Descriptor = new Descriptor();
                RefreshFingerprint();
                return;
            }

            if (DescriptorLibrary.TryGetDescriptor(m_DescriptorId, out var descriptor))
                m_Descriptor = descriptor;
            else
                m_Descriptor = new Descriptor();

            RefreshFingerprint();
        }

        /// <summary>
        /// This issue's category (read-only).
        /// </summary>
        [JsonProperty("category")]
        public AnalysisCategory Category
        {
            get => m_Category;
            internal set
            {
                m_Category = value;
                RefreshFingerprint();
            }
        }

        /// <summary>
        /// Custom properties, keyed by the property's canonical name (matches the property-enum value name).
        /// Returns an empty dictionary rather than null.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<string, string> Properties => m_Properties ?? s_EmptyProperties;

        // For deserialization and bulk replacement from adapters/builders.
        internal void SetAllProperties(Dictionary<string, string> properties)
        {
            m_Properties = properties;
            RefreshFingerprint();
        }

        // Serialization shim: omit null/empty so NullValueHandling.Ignore drops the field entirely.
        [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)]
        Dictionary<string, string> JsonProperties
        {
            get => m_Properties != null && m_Properties.Count > 0 ? m_Properties : null;
            set
            {
                m_Properties = value;
                RefreshFingerprint();
            }
        }

        /// <summary>
        /// Per-descriptor evidence as key/value pairs. Carried on issues only; insights leave this null.
        /// See `evidence` in an exported Report JSON file for the JSON shape.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<string, object> Evidence
        {
            get => m_Evidence;
            internal set
            {
                if (value == null || value.Count == 0)
                {
                    m_Evidence = null;
                }
                else
                {
                    var copy = new Dictionary<string, object>(value.Count, StringComparer.Ordinal);
                    foreach (var kv in value)
                        copy[kv.Key] = kv.Value;
                    m_Evidence = copy;
                }
                RefreshFingerprint();
            }
        }

        // Private JSON property: serialises evidence when non-empty.
        // On load, the setter rehydrates the dictionary from the JSON object. Fingerprint refresh is
        // intentionally skipped here -- the fingerprint field is serialised separately and rehydrated
        // by OnDeserialized; refreshing here would recompute over JToken values (different canonical
        // form from the in-memory CLR values), invalidating the persisted fingerprint.
        [JsonProperty("evidence", NullValueHandling = NullValueHandling.Ignore)]
        Dictionary<string, object> JsonEvidence
        {
            get => m_Evidence != null && m_Evidence.Count > 0 ? m_Evidence : null;
            set
            {
                m_Evidence = value == null || value.Count == 0
                    ? null
                    : new Dictionary<string, object>(value, StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// Project issue Description (read-only).
        /// </summary>
        [JsonProperty("description")]
        public string Description
        {
            get => m_Description;
            internal set
            {
                m_Description = value;
                RefreshFingerprint();
            }
        }

        /// <summary>
        /// Stable issue identity for diffing reports across analysis runs.
        /// </summary>
        /// <remarks>
        /// Fingerprints are emitted for all diagnostic issues.
        /// </remarks>
        [JsonProperty("fingerprint", Order = 100, NullValueHandling = NullValueHandling.Ignore)]
        public string Fingerprint
        {
            get => m_Fingerprint;
            private set => m_Fingerprint = value;
        }

        /// <summary>
        /// Qualitative stability of the fingerprint identity inputs.
        /// </summary>
        [JsonProperty("fingerprintStability", Order = 101, NullValueHandling = NullValueHandling.Ignore)]
        public string FingerprintStability
        {
            get => m_FingerprintStability;
            private set => m_FingerprintStability = value;
        }

        /// <summary>
        /// Human-readable identity inputs used to compute <see cref="Fingerprint"/>.
        /// </summary>
        [JsonProperty("fingerprintParts", Order = 102, NullValueHandling = NullValueHandling.Ignore)]
        public string[] FingerprintParts
        {
            get => m_FingerprintParts;
            private set => m_FingerprintParts = value;
        }

        /// <summary>
        /// Dependencies of this project issue.
        /// </summary>
        internal DependencyNode Dependencies
        {
            get => m_Dependencies;
            set => m_Dependencies = value;
        }

        /// <summary>
        /// Name of the file that contains this issue.
        /// </summary>
        [JsonIgnore]
        public string Filename => m_Location == null ? string.Empty : m_Location.Filename;

        /// <summary>
        /// Relative path of the file that contains this issue.
        /// </summary>
        [JsonIgnore]
        public string RelativePath => m_Location == null ? string.Empty : m_Location.Path;

        /// <summary>
        /// Start line in the file that contains this issue.
        /// </summary>
        [JsonIgnore]
        public int? Line => m_Location == null ? 0 : m_Location.Line;

        /// <summary>
        /// End line in the file for multi-line issue locations, or null for single-line locations.
        /// </summary>
        [JsonIgnore]
        public int? EndLine => m_Location?.EndLine;

        /// <summary>
        /// Start column in the file that contains this issue.
        /// </summary>
        [JsonIgnore]
        public int? Column => m_Location?.Column;

        /// <summary>
        /// End column in the file for multi-column or multi-line issue locations, or null when not available.
        /// </summary>
        [JsonIgnore]
        public int? EndColumn => m_Location?.EndColumn;

        /// <summary>
        /// Location of the Insight or Issue (read-only).
        /// </summary>
        [JsonProperty("location")]
        public Location Location
        {
            get => m_Location;
            internal set
            {
                m_Location = value;
                RefreshFingerprint();
            }
        }

        /// <summary>
        /// Log level.
        /// </summary>
        [JsonIgnore]
        public LogLevel LogLevel
        {
            get => LogLevelSeverityMapping.SeverityToLogLevel(m_Severity);
            internal set => m_Severity = LogLevelSeverityMapping.LogLevelToSeverity(value);
        }

        /// <summary>
        /// Issue-specific Severity (read-only).
        /// </summary>
        [JsonIgnore]
        public Severity Severity
        {
            get => m_Severity == Severity.Default && m_Descriptor.IsValid() ? m_Descriptor.DefaultSeverity : m_Severity;
            internal set => m_Severity = value;
        }

        /// <summary>
        /// Checks whether this issue is major or critical.
        /// </summary>
        /// <value>True if the issue's Severity is Major or Critical. Otherwise, returns false.</value>
        [JsonIgnore]
        public bool IsMajorOrCritical => Severity == Severity.Critical || Severity == Severity.Major;

        [JsonProperty("severity")]
        internal string SeverityAsString
        {
            get => m_Severity.ToString();
            set => m_Severity = (Severity)Enum.Parse(typeof(Severity), value);
        }

        /// <summary>
        /// Gets the number of custom properties this issue has.
        /// </summary>
        /// <value>The number of custom property strings</value>
        [JsonIgnore]
        public int PropertyCount => m_Properties?.Count ?? 0;

        [JsonConstructor]
        internal ReportItem()
        {
            // only for json serialization purposes
            m_DescriptorId = string.Empty;
            m_Descriptor = new Descriptor();
        }

        /// <summary>
        /// Constructs and returns an instance of ReportItem.
        /// </summary>
        /// <param name="category">Issue category</param>
        /// <param name="id">Descriptor ID</param>
        /// <param name="args">Arguments to be used in the message formatting</param>
        internal ReportItem(AnalysisCategory category, string id, params object[] args)
        {
            Debug.Assert(!category.IsMessageCategory(),
                $"A ReportItem with a descriptor id cannot have a message category ({category}). Use MessageBuilder for toolchain messages.");
            m_DescriptorId = id ?? string.Empty;
            m_MessageArgs = NormalizeObjects(args);

            if (!DescriptorLibrary.TryGetDescriptor(m_DescriptorId, out var descriptor))
                descriptor = new Descriptor();

            m_Category = category;

            try
            {
                m_Description = string.IsNullOrEmpty(descriptor.MessageFormat) ? descriptor.Title : string.Format(descriptor.MessageFormat, args);
            }
            catch (Exception e)
            {
                Debug.LogError("Error formatting message: " + descriptor.MessageFormat + " with args: " + string.Join(", ", args) + " - " + e.Message);
                m_Description = descriptor.Title;
            }

            m_Descriptor = descriptor;
            m_Severity = descriptor.DefaultSeverity;
            RefreshFingerprint();
        }

        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {
            if (string.IsNullOrEmpty(m_Fingerprint))
                RefreshFingerprint();
        }

        /// <summary>
        /// Checks whether this issue is valid.
        /// </summary>
        /// <returns>True if the issue has a valid description string. Otherwise, returns false.</returns>
        public bool IsValid()
        {
            return Description != null;
        }

        /// <summary>
        /// Get a custom property value by name. Returns empty string when absent.
        /// </summary>
        public string GetProperty(string name)
        {
            if (m_Properties == null || string.IsNullOrEmpty(name))
                return string.Empty;
            return m_Properties.TryGetValue(name, out var value) ? value : string.Empty;
        }


        /// <summary>
        /// Set a custom property value by name. Creates the backing dictionary on first write.
        /// </summary>
        public void SetProperty(string name, object value)
        {
            if (string.IsNullOrEmpty(name))
                return;
            m_Properties ??= new Dictionary<string, string>();
            m_Properties[name] = value == null ? string.Empty : value.ToString();
            RefreshFingerprint();
        }

        // Schema-keyed accessors take an InsightSchema column slug, which by convention equals
        // the canonical property name and so doubles as the dict key directly.

        /// <summary>Read a property by its <see cref="InsightSchema"/> column slug.</summary>
        public string GetProperty(InsightSchema schema, string columnSlug)
        {
            return GetProperty(columnSlug);
        }

        /// <summary>Read a boolean property by schema column slug.</summary>
        public bool GetPropertyBool(InsightSchema schema, string columnSlug)
        {
            return bool.TryParse(GetProperty(columnSlug), out var v) && v;
        }

        /// <summary>Read an int property by schema column slug. Returns 0 if absent or unparseable.</summary>
        public int GetPropertyInt32(InsightSchema schema, string columnSlug)
        {
            return int.TryParse(GetProperty(columnSlug), out var v) ? v : 0;
        }

        /// <summary>Read a long property by schema column slug. Returns 0 if absent or unparseable.</summary>
        public long GetPropertyInt64(InsightSchema schema, string columnSlug)
        {
            return long.TryParse(GetProperty(columnSlug), out var v) ? v : 0;
        }

        /// <summary>Write a property by schema column slug.</summary>
        public void SetProperty(InsightSchema schema, string columnSlug, object value)
        {
            SetProperty(columnSlug, value);
        }

        /// <summary>
        /// Set a single evidence value by key. Creates the backing dictionary on first write;
        /// replaces the existing value if the key already exists.
        /// </summary>
        internal void SetEvidence(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                return;
            m_Evidence ??= new Dictionary<string, object>(StringComparer.Ordinal);
            m_Evidence[key] = value;
            RefreshFingerprint();
        }

        /// <summary>
        /// Read an evidence value as a string. Returns empty string if the key is absent.
        /// </summary>
        /// <remarks>
        /// Evidence values land as raw CLR types when the report was just produced and as
        /// <see cref="Newtonsoft.Json.Linq.JToken"/> after a JSON round-trip; this accessor
        /// hides the difference. Returns <see cref="string.Empty"/> for missing keys or null values.
        /// </remarks>
        public string GetEvidenceString(string key)
        {
            return TryReadEvidence(key, out var value)
                ? ValueCoercion.CoerceString(value)
                : string.Empty;
        }

        /// <summary>
        /// Read an evidence value as int. Returns 0 if the key is absent or the value isn't parseable.
        /// </summary>
        public int GetEvidenceInt32(string key)
        {
            return TryReadEvidence(key, out var value) && ValueCoercion.TryCoerceInt64(value, out var longValue)
                ? unchecked((int)longValue)
                : 0;
        }

        /// <summary>
        /// Read an evidence value as long. Returns 0 if the key is absent or the value isn't parseable.
        /// </summary>
        public long GetEvidenceInt64(string key)
        {
            return TryReadEvidence(key, out var value) && ValueCoercion.TryCoerceInt64(value, out var longValue)
                ? longValue
                : 0L;
        }

        /// <summary>
        /// Read an evidence value as bool. Returns false if the key is absent or the value isn't parseable.
        /// </summary>
        public bool GetEvidenceBool(string key)
        {
            if (!TryReadEvidence(key, out var value))
                return false;
            if (value is bool b)
                return b;
            if (value is JValue jv && jv.Value is bool jb)
                return jb;
            return bool.TryParse(ValueCoercion.CoerceString(value), out var parsed) && parsed;
        }

        /// <summary>
        /// Read an evidence value as float. Returns 0f if the key is absent or the value isn't parseable.
        /// </summary>
        public float GetEvidenceFloat(string key)
        {
            return TryReadEvidence(key, out var value) && ValueCoercion.TryToDouble(value, out var d)
                ? (float)d
                : 0f;
        }

        /// <summary>
        /// Read an evidence value as double. Returns 0.0 if the key is absent or the value isn't parseable.
        /// </summary>
        public double GetEvidenceDouble(string key)
        {
            return TryReadEvidence(key, out var value) && ValueCoercion.TryToDouble(value, out var d)
                ? d
                : 0.0;
        }

        bool TryReadEvidence(string key, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(key) || m_Evidence == null)
                return false;
            if (!m_Evidence.TryGetValue(key, out value) || value == null)
                return false;
            // A JValue holding JTokenType.Null is also nothing-of-interest.
            return !(value is JValue jv && jv.Type == JTokenType.Null);
        }

        /// <summary>
        /// Sets the location of this report item and returns the modified item.
        /// </summary>
        /// <param name="path">The file path of the issue location</param>
        /// <param name="line">Optional start line number in the file, or null for asset/file-level locations</param>
        /// <param name="endLine">Optional end line number for multi-line locations</param>
        /// <param name="column">Optional start column number</param>
        /// <param name="endColumn">Optional end column number</param>
        /// <returns>The modified ReportItem for method chaining</returns>
        public ReportItem WithLocation(string path, int? line = null, int? endLine = null, int? column = null, int? endColumn = null)
        {
            m_Location = new Location(path, line, endLine, column, endColumn);
            RefreshFingerprint();
            return this;
        }

        void RefreshFingerprint()
        {
            var parts = BuildFingerprintParts(out var stability);
            m_FingerprintParts = parts.ToArray();
            m_FingerprintStability = stability;
            m_Fingerprint = FingerprintUtils.ComputeSha256Fingerprint(m_FingerprintParts);
        }

        void ClearFingerprint()
        {
            m_Fingerprint = null;
            m_FingerprintStability = null;
            m_FingerprintParts = null;
        }

        IEnumerable<string> BuildFingerprintParts(out string stability)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(Id))
                parts.Add($"id:{FingerprintUtils.NormalizeFingerprintPart(Id)}");

            parts.Add($"category:{Category}");

            var hasGuid = false;
            var hasPath = false;
            var hasSpecificContext = false;

            var normalizedPath = NormalizePath(RelativePath);
            if (!string.IsNullOrEmpty(normalizedPath))
            {
                var guid = GetAssetGuid(normalizedPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    parts.Add($"assetGuid:{guid}");
                    hasGuid = true;
                }
                else
                {
                    parts.Add($"path:{normalizedPath}");
                }

                hasPath = true;
            }

            AddContextParts(parts, "arg", m_MessageArgs, ref hasSpecificContext);
            AddKeyedContextParts(parts, "property", m_Properties, ref hasSpecificContext);
            AddEvidenceParts(parts, m_Evidence, ref hasSpecificContext);

            var line = m_Location?.Line;
            if (line.HasValue)
            {
                parts.Add($"line:{line.Value.ToString(CultureInfo.InvariantCulture)}");
                hasSpecificContext = true;
            }

            if (Category == AnalysisCategory.Code)
            {
                var column = m_Location?.Column;
                if (column.HasValue)
                {
                    parts.Add($"column:{column.Value.ToString(CultureInfo.InvariantCulture)}");
                    hasSpecificContext = true;
                }
            }

            if (!hasPath && !hasSpecificContext && !string.IsNullOrEmpty(Description))
            {
                parts.Add($"description:{FingerprintUtils.NormalizeFingerprintPart(Description)}");
                hasSpecificContext = true;
            }

            stability = GetFingerprintStability(hasGuid, hasPath, hasSpecificContext);
            return parts;
        }

        static void AddContextParts(ICollection<string> parts, string name, IEnumerable<string> values, ref bool hasSpecificContext)
        {
            if (values == null)
                return;

            var index = 0;
            foreach (var value in values)
            {
                var normalizedValue = FingerprintUtils.NormalizeFingerprintPart(value);
                if (string.IsNullOrEmpty(normalizedValue))
                {
                    index++;
                    continue;
                }

                parts.Add($"{name}[{index}]:{normalizedValue}");
                hasSpecificContext = true;
                index++;
            }
        }

        // Keyed sibling of AddContextParts: iterates an entry-keyed dictionary in Ordinal key
        // order so the resulting fingerprint is stable across runs (dictionary enumeration
        // order is not guaranteed after a JSON round-trip).
        static void AddKeyedContextParts(ICollection<string> parts, string name, IReadOnlyDictionary<string, string> values, ref bool hasSpecificContext)
        {
            if (values == null || values.Count == 0)
                return;

            foreach (var key in values.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var normalizedValue = FingerprintUtils.NormalizeFingerprintPart(values[key]);
                if (string.IsNullOrEmpty(normalizedValue))
                    continue;
                parts.Add($"{name}[{key}]:{normalizedValue}");
                hasSpecificContext = true;
            }
        }

        static void AddEvidenceParts(ICollection<string> parts, IReadOnlyDictionary<string, object> evidence, ref bool hasSpecificContext)
        {
            if (evidence == null || evidence.Count == 0)
                return;

            // Sorted by key so the fingerprint is deterministic regardless of dictionary insertion order
            // or post-deserialise ordering.
            var keys = new List<string>(evidence.Keys);
            keys.Sort(StringComparer.Ordinal);

            foreach (var key in keys)
            {
                var canonical = CanonicalEvidenceValue(evidence[key]);
                if (string.IsNullOrEmpty(canonical))
                    continue;

                parts.Add($"evidence[{key}]:{canonical}");
                hasSpecificContext = true;
            }
        }

        static string CanonicalEvidenceValue(object value)
        {
            if (value == null)
                return string.Empty;
            // Newtonsoft handles primitives, strings, arrays, nested objects, and JToken values returned
            // by the JSON round-trip. Formatting.None and InvariantCulture (the library default) keep the
            // canonical form stable across machines.
            return JsonConvert.SerializeObject(value, Formatting.None);
        }

        static string GetFingerprintStability(bool hasGuid, bool hasPath, bool hasSpecificContext)
        {
            if (hasGuid && hasSpecificContext)
                return "Strong";
            if (hasGuid)
                return "Good";
            if (hasPath && hasSpecificContext)
                return "Good";
            if (hasPath)
                return "Medium";
            return "Weak";
        }

        static string GetAssetGuid(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            try
            {
                var metaPath = Path.Combine(SmartAuditor.ProjectPath, path.Replace('/', Path.DirectorySeparatorChar)) + ".meta";
                if (!File.Exists(metaPath))
                    return string.Empty;

                foreach (var line in File.ReadLines(metaPath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("guid:", StringComparison.Ordinal))
                        return FingerprintUtils.NormalizeFingerprintPart(trimmed.Substring("guid:".Length));
                }
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        static string NormalizePath(string path)
        {
            return FingerprintUtils.NormalizeFingerprintPart(path).Replace('\\', '/');
        }

        static string[] NormalizeObjects(IEnumerable<object> values)
        {
            if (values == null)
                return null;

            return values
                .Select(ToInvariantString)
                .Select(FingerprintUtils.NormalizeFingerprintPart)
                .Where(value => !string.IsNullOrEmpty(value))
                .ToArray();
        }

        static string ToInvariantString(object value)
        {
            if (value == null)
                return string.Empty;

            return value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString();
        }

    }
}
