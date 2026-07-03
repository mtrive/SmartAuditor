using System;
using Newtonsoft.Json;
using UnityEngine;

namespace SmartAuditor.Editor
{
    // Project-level filter applied when a Report is saved. Surfaced in
    // Project Settings > Smart Auditor so a project can drop noisy items
    // (e.g. Minor / Moderate) from saved reports without affecting the
    // in-memory view in the Smart Auditor window.
    [Serializable]
    internal sealed class ReportFilter
    {
        [JsonProperty("min_save_severity")]
        [SerializeField]
        Severity m_MinSaveSeverity = Severity.Default;

        public Severity MinSaveSeverity => m_MinSaveSeverity;

        public ReportFilter()
        { }

        // Returns true when the supplied severity is at least as severe as the
        // configured threshold. Severity.Default acts as "no filter" -- everything
        // is included. None / Hidden values are not valid thresholds and short-circuit
        // to "include everything"; the GUI / CLI parsers reject those before they
        // reach this predicate, but the guard here keeps the contract safe.
        // Used at save time (drop sub-threshold items from the saved JSON) and at
        // render time (hide sub-threshold items from the issue table).
        public static bool Includes(Severity itemSeverity, Severity threshold)
        {
            if (threshold == Severity.Default || threshold == Severity.Suppressed || threshold == Severity.Hidden)
                return true;

            var s = (int)itemSeverity;
            return s >= (int)Severity.Error && s <= (int)threshold;
        }

        // Severities valid as a threshold value -- the dropdown in Project Settings
        // is constrained to these. Default means "no filter".
        public static readonly Severity[] ValidThresholds =
        {
            Severity.Default,
            Severity.Error,
            Severity.Critical,
            Severity.Major,
            Severity.Moderate,
            Severity.Minor
        };

        public static bool IsValidThreshold(Severity value)
        {
            for (var i = 0; i < ValidThresholds.Length; i++)
                if (ValidThresholds[i] == value)
                    return true;
            return false;
        }
    }
}
