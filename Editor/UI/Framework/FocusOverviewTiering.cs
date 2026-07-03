// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Severity buckets used by Focus overview and scoped analysis UIs, aligned with focus-area refresh logic in the main report view.
    /// </summary>
    internal enum FocusOverviewTier
    {
        HighPriority,
        NeedsAttention,
        Informational
    }

    /// <summary>
    /// Maps <see cref="Severity"/> to focus tiers for grouping issues in the UI.
    /// </summary>
    internal static class FocusOverviewTiering
    {
        /// <summary>
        /// Returns the tier used for section headers and grouping (HIGH PRIORITY, NEEDS ATTENTION, INFORMATIONAL).
        /// </summary>
        internal static FocusOverviewTier GetTier(Severity severity)
        {
            if (severity == Severity.Critical || severity == Severity.Error || severity == Severity.Major)
                return FocusOverviewTier.HighPriority;
            if (severity == Severity.Moderate)
                return FocusOverviewTier.NeedsAttention;
            return FocusOverviewTier.Informational;
        }
    }
}
