using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Represents a diagnostic issue that can be reported by an analyzer.
    /// Similar to Roslyn's Diagnostic class, this provides a way to create and report issues.
    /// </summary>
    public sealed class Diagnostic
    {
        /// <summary>
        /// The issue category.
        /// </summary>
        public AnalysisCategory Category { get; }

        /// <summary>
        /// The descriptor ID for issues.
        /// </summary>
        public string DescriptorId { get; }

        /// <summary>
        /// Arguments to be used in message formatting for issues.
        /// </summary>
        public object[] MessageArgs { get; }

        /// <summary>
        /// The location of the diagnostic.
        /// </summary>
        public Location Location { get; }

        /// <summary>
        /// Custom properties for the diagnostic, keyed by canonical property name.
        /// </summary>
        public IReadOnlyDictionary<string, string> Properties { get; }

        /// <summary>
        /// Per-descriptor evidence as key/value pairs. Surfaced as the JSON `evidence`
        /// object and rendered in the report view's Diagnostic Evidence footer.
        /// </summary>
        public IReadOnlyDictionary<string, object> Evidence { get; }

        /// <summary>
        /// Dependencies for the diagnostic.
        /// </summary>
        public DependencyNode Dependencies { get; }

        /// <summary>
        /// Optional severity override. If null, uses the descriptor's default severity.
        /// </summary>
        public Severity? Severity { get; }

        Diagnostic(
            AnalysisCategory category,
            string descriptorId,
            object[] messageArgs,
            Location location,
            IReadOnlyDictionary<string, string> properties,
            IReadOnlyDictionary<string, object> evidence,
            DependencyNode dependencies,
            Severity? severity)
        {
            Category = category;
            DescriptorId = descriptorId ?? string.Empty;
            MessageArgs = messageArgs;
            Location = location;
            Properties = properties;
            Evidence = evidence;
            Dependencies = dependencies;
            Severity = severity;
        }

        /// <summary>
        /// Creates a diagnostic for an issue (with a descriptor ID).
        /// </summary>
        /// <param name="category">Issue category</param>
        /// <param name="descriptorId">Descriptor ID</param>
        /// <param name="messageArgs">Arguments to be used in message formatting</param>
        /// <returns>A new Diagnostic instance</returns>
        public static Diagnostic Create(AnalysisCategory category, string descriptorId, params object[] messageArgs)
        {
            if (string.IsNullOrEmpty(descriptorId))
                throw new ArgumentException("Descriptor ID cannot be null or empty for issue diagnostics", nameof(descriptorId));

            ValidateMessageFormat(descriptorId, messageArgs);

            return new Diagnostic(
                category,
                descriptorId,
                messageArgs,
                null,
                null,
                null,
                null,
                null);
        }

        // Matches "{N}" or "{N:format}" — the indexed placeholders string.Format uses. Doesn't
        // model the literal "{{" / "}}" escape; the descriptor MessageFormats in this codebase
        // don't use literal braces, and validation would false-positive on them if we tried to.
        static readonly Regex s_PlaceholderRegex = new Regex(@"\{(\d+)(?::[^}]*)?\}", RegexOptions.Compiled);

        /// <summary>
        /// Cross-checks the descriptor's <see cref="Descriptor.MessageFormat"/> against the
        /// number of <paramref name="messageArgs"/> supplied. Mismatches are silent-data bugs:
        /// extra args get dropped during rendering, missing args render as literal "{N}". Throw
        /// at the call site so the analyzer author sees it immediately, instead of leaking a
        /// malformed diagnostic into the report.
        /// </summary>
        static void ValidateMessageFormat(string descriptorId, object[] messageArgs)
        {
            // DescriptorLibrary populates from analyzer types during PrepareAnalysis. Tests and
            // pre-init paths may create diagnostics before the library is populated; skip silently
            // in that case rather than throwing on a missing-descriptor false positive.
            if (!DescriptorLibrary.TryGetDescriptor(descriptorId, out var descriptor))
                return;

            var argCount = messageArgs?.Length ?? 0;
            var format = descriptor.MessageFormat;

            if (string.IsNullOrEmpty(format))
            {
                if (argCount > 0)
                    throw new InvalidOperationException(
                        $"Descriptor '{descriptorId}' has no MessageFormat but Diagnostic.Create was called with {argCount} message arg(s). " +
                        "Add MessageFormat = \"...{0}...\" to the descriptor or drop the args from the call.");
                return;
            }

            var expected = CountPlaceholders(format);
            if (expected != argCount)
                throw new InvalidOperationException(
                    $"Descriptor '{descriptorId}' MessageFormat references {expected} placeholder(s) but Diagnostic.Create received {argCount} arg(s). MessageFormat: \"{format}\".");
        }

        static int CountPlaceholders(string format)
        {
            var maxIndex = -1;
            foreach (Match match in s_PlaceholderRegex.Matches(format))
            {
                if (int.TryParse(match.Groups[1].Value, out var index) && index > maxIndex)
                    maxIndex = index;
            }
            return maxIndex + 1;
        }

        /// <summary>
        /// Creates a new diagnostic with the specified location.
        /// </summary>
        public Diagnostic WithLocation(Location location)
        {
            return new Diagnostic(
                Category,
                DescriptorId,
                MessageArgs,
                location,
                Properties,
                Evidence,
                Dependencies,
                Severity);
        }

        /// <summary>
        /// Creates a new diagnostic carrying one additional custom property. Existing entries
        /// are preserved; if <paramref name="name"/> already exists, its value is replaced.
        /// Use the property's canonical name (whitespace-stripped, matching the analyzer-side
        /// enum value).
        /// </summary>
        public Diagnostic WithProperty(string name, object value)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Property name cannot be null or empty", nameof(name));

            var next = Properties == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(Properties.Count + 1, StringComparer.Ordinal);

            if (Properties != null)
            {
                foreach (var kv in Properties)
                    next[kv.Key] = kv.Value;
            }

            next[name] = value == null ? string.Empty : value.ToString();

            return new Diagnostic(
                Category,
                DescriptorId,
                MessageArgs,
                Location,
                next,
                Evidence,
                Dependencies,
                Severity);
        }

        /// <summary>
        /// Creates a new diagnostic carrying one additional evidence pair. Existing entries are preserved;
        /// if <paramref name="key"/> already exists, its value is replaced.
        /// </summary>
        public Diagnostic WithEvidence(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Evidence key cannot be null or empty", nameof(key));

            var next = Evidence == null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(Evidence.Count + 1, StringComparer.Ordinal);

            if (Evidence != null)
            {
                foreach (var kv in Evidence)
                    next[kv.Key] = kv.Value;
            }

            next[key] = value;

            return new Diagnostic(
                Category,
                DescriptorId,
                MessageArgs,
                Location,
                Properties,
                next,
                Dependencies,
                Severity);
        }

        /// <summary>
        /// Creates a new diagnostic that replaces the evidence dictionary with <paramref name="evidence"/>.
        /// </summary>
        public Diagnostic WithEvidence(IDictionary<string, object> evidence)
        {
            IReadOnlyDictionary<string, object> next = null;
            if (evidence != null && evidence.Count > 0)
                next = new Dictionary<string, object>(evidence, StringComparer.Ordinal);

            return new Diagnostic(
                Category,
                DescriptorId,
                MessageArgs,
                Location,
                Properties,
                next,
                Dependencies,
                Severity);
        }

        /// <summary>
        /// Creates a new diagnostic with the specified dependencies.
        /// </summary>
        public Diagnostic WithDependencies(DependencyNode dependencies)
        {
            return new Diagnostic(
                Category,
                DescriptorId,
                MessageArgs,
                Location,
                Properties,
                Evidence,
                dependencies,
                Severity);
        }

        /// <summary>
        /// Creates a new diagnostic with the specified severity.
        /// </summary>
        public Diagnostic WithSeverity(Severity severity)
        {
            return new Diagnostic(
                Category,
                DescriptorId,
                MessageArgs,
                Location,
                Properties,
                Evidence,
                Dependencies,
                severity);
        }

        /// <summary>
        /// Converts this diagnostic to a ReportItem.
        /// </summary>
        internal ReportItem ToReportItem()
        {
            // Issue with descriptor
            var reportItem = new ReportItem(Category, DescriptorId, MessageArgs ?? Array.Empty<object>());

            if (Location != null)
                reportItem.Location = Location;

            if (Properties != null && Properties.Count > 0)
            {
                var copy = new Dictionary<string, string>(Properties.Count, StringComparer.Ordinal);
                foreach (var kv in Properties)
                    copy[kv.Key] = kv.Value;
                reportItem.SetAllProperties(copy);
            }

            if (Evidence != null && Evidence.Count > 0)
                reportItem.Evidence = Evidence.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

            if (Dependencies != null)
                reportItem.Dependencies = Dependencies;

            if (Severity.HasValue)
                reportItem.Severity = Severity.Value;

            return reportItem;
        }
    }
}
