using System;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using SmartAuditor.Editor.Core.Serialization;
using UnityEditor;
using SmartAuditor.Editor.Core;

[assembly: InternalsVisibleTo("SmartAuditor.Editor.Analyzers")]

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Descriptor defines a potential problem and a recommended course of action.
    /// </summary>
    [Serializable]
    public sealed class Descriptor : IEquatable<Descriptor>
    {
        /// <summary>
        /// A unique identifier for the issue. IDs must have exactly 3 upper case characters, followed by 4 digits.
        /// </summary>
        public string Id;

        /// <summary>
        /// Issue title. Short noun phrase identifying the class of problem.
        /// See <c>Documentation~/DiagnosticDrafting.md</c> §2 for Title-Case, domain prefix
        /// (<c>Audio:</c>, <c>URP:</c>, <c>Shader:</c>, ...), and length conventions.
        /// </summary>
        public string Title;

        /// <summary>
        /// Per-instance message rendered into the issue list row. Uses <c>{N}</c> placeholders
        /// substituted with the args passed to <c>Diagnostic.Create</c>.
        /// See <c>Documentation~/DiagnosticDrafting.md</c> §2 for the 120-character length budget
        /// and the MessageFormat-vs-evidence split rule (numeric detail belongs in
        /// <c>WithEvidence(...)</c>, not in the message body).
        /// </summary>
        public string MessageFormat;

        /// <summary>
        /// Default Severity of the issue.
        /// </summary>
        public Severity DefaultSeverity;

        /// <summary>
        /// Returns true if the issue is enabled by default.
        /// </summary>
        public bool IsEnabledByDefault = true;

        /// <summary>
        /// The project area(s) affected by this issue. See <see cref="SmartAuditor.Editor.Impact"/> for available flags.
        /// </summary>
        public Impact Impact;

        /// <summary>
        /// Affected platforms. If null, the issue applies to all platforms.
        /// </summary>
        [JsonConverter(typeof(BuildTargetJsonConverter))]
        public BuildTarget[] Platforms;

        /// <summary>
        /// One to two sentences explaining what is happening and why it's a problem.
        /// Must be self-contained: an LLM agent reading only this field (without the title or
        /// recommendation) should understand the issue.
        /// See <c>Documentation~/DiagnosticDrafting.md</c> §2 for self-containment rules and
        /// §3 for the <c>&lt;b&gt;...&lt;/b&gt;</c> markup conventions on UI identifiers.
        /// </summary>
        public string Description;

        /// <summary>
        /// One to three sentences telling the reader what to do, in imperative voice
        /// (<c>"Set X to Y."</c>, <c>"Use Z instead of W."</c>).
        /// See <c>Documentation~/DiagnosticDrafting.md</c> §2 for voice and hedging rules
        /// (<c>"Consider ..."</c> is reserved for genuinely context-dependent guidance, not
        /// as a softener).
        /// </summary>
        public string Recommendation;

        /// <summary>
        /// URL to documentation.
        /// </summary>
        public string DocumentationUrl;

        /// <summary>
        /// Minimum Unity version this issue applies to. If not specified, the issue applies to all versions.
        /// </summary>
        public string MinimumVersion;

        /// <summary>
        /// Maximum Unity version this issue applies to. If not specified, the issue applies to all versions.
        /// </summary>
        public string MaximumVersion;

        internal static bool IsValidId(string id)
        {
            // ID must be exactly 7 characters, beginning with exactly 3 uppercase letters, followed by exactly 4 digits
            if (id.Length != 7 || id[0] < 'A' || id[0] > 'Z' || id[1] < 'A' || id[1] > 'Z' || id[2] < 'A' || id[2] > 'Z' ||
                id[3] < '0' || id[3] > '9' || id[4] < '0' || id[4] > '9' || id[5] < '0' || id[5] > '9' || id[6] < '0' || id[6] > '9')
            {
                return false;
            }
            return true;
        }

        [JsonConstructor]
        internal Descriptor()
        {
            // only for json serialization purposes.
        }

        /// <summary>
        /// Initializes and returns an instance of Descriptor.
        /// </summary>
        /// <param name="id">The Issue ID string.</param>
        /// <param name="title">A short human-readable 'name' for the issue</param>
        /// <param name="impact">The area(s) affected by this issue.</param>
        /// <param name="description">A description of the issue.</param>
        /// <param name="recommendation">Advice on how to resolve the issue.</param>
        public Descriptor(string id, string title, Impact impact, string description, string recommendation)
        {
            // ID must be exactly 7 characters, beginning with exactly 3 uppercase letters, followed by exactly 4 digits
            if (!IsValidId(id))
            {
                throw new ArgumentException("Invalid descriptor ID: " + id);
            }

            Id = id;
            Title = title;
            this.Impact = impact;
            MessageFormat = string.Empty;
            Description = description;
            Recommendation = recommendation;

            DefaultSeverity = Severity.Moderate;
        }

        /// <summary>Returns true if the Descriptor is equal to a given Descriptor, false otherwise.</summary>
        /// <param name="other">The Descriptor to compare equality with.</param>
        /// <returns>The result of the equality comparison.</returns>
        public bool Equals(Descriptor other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Id == other.Id;
        }

        /// <summary>Returns true if the Descriptor is equal to a given object, false otherwise.</summary>
        /// <param name="obj">The object to compare equality with.</param>
        /// <returns>The result of the equality comparison.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj.GetType() == GetType() && Equals((Descriptor)obj);
        }

        /// <summary>Returns the hash code for the Descriptor's Issue ID.</summary>
        /// <returns>The computed hash code.</returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Id);
        }
    }
}
