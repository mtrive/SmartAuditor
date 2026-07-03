using System;
using Newtonsoft.Json;
using UnityEngine;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Represents a rule which modifies the <seealso cref="Severity"/> of an Issue <seealso cref="ReportItem"/>
    /// or all of the ProjectIssues that share a <seealso cref="Descriptor"/>.
    /// </summary>
    [Serializable]
    public class Rule
    {
        // Backing field for the public Pattern property. SerializeField so the
        // default inspector renders it under each Rule entry in Project
        // Settings > Smart Auditor > Rules; JsonIgnore on the field keeps
        // Newtonsoft routed through the public property (which has its own
        // [JsonProperty] with NullValueHandling.Ignore for empty patterns).
        [JsonIgnore]
        [SerializeField]
        private string m_Pattern;

        /// <summary>
        /// The Severity level to apply to the issue(s) represented by this Rule
        /// </summary>
        [JsonProperty("severity")]
        public Severity Severity;

        [JsonProperty("pattern", NullValueHandling = NullValueHandling.Ignore)]
        public string Pattern
        {
            get => m_Pattern;
            set => m_Pattern = string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// The Descriptor ID
        /// </summary>
        public string Id;

        internal Rule() { }

        /// <summary>Get the hashed integer representation of the Rule.</summary>
        /// <returns>The computed hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Id.GetHashCode();
                hash = hash * 23 + Pattern.GetHashCode();
                hash = hash * 23 + Severity.GetHashCode();
                return hash;
            }
        }
    }
}
