// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using SmartAuditor.Editor;
using UnityEditor;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Orchestrates severity summary, tiered issue sections, and empty state for focus/scoped views.
    /// </summary>
    internal static class FocusOverviewPresenter
    {
        public static void Draw<T>(
            int criticalCount,
            int majorCount,
            int moderateCount,
            int minorCount,
            IReadOnlyList<T> highPriority,
            IReadOnlyList<T> needsAttention,
            IReadOnlyList<T> informational,
            Action<T, int> drawRow)
        {
            TieredIssueSections.DrawSeveritySummary(criticalCount, majorCount, moderateCount, minorCount);
            EditorGUILayout.Space(UiLayout.SpaceSmall);

            var hasHigh = highPriority != null && highPriority.Count > 0;
            var hasNeeds = needsAttention != null && needsAttention.Count > 0;
            var hasInfo = informational != null && informational.Count > 0;

            if (hasHigh)
            {
                TieredIssueSections.DrawTierSection("HIGH PRIORITY", highPriority, SeverityPresentation.Palette[0], drawRow);
                EditorGUILayout.Space(UiLayout.SpaceSmall);
            }

            if (hasNeeds)
            {
                TieredIssueSections.DrawTierSection("NEEDS ATTENTION", needsAttention, SeverityPresentation.Palette[2], drawRow);
                EditorGUILayout.Space(UiLayout.SpaceSmall);
            }

            if (hasInfo)
                TieredIssueSections.DrawTierSection("INFORMATIONAL", informational, SeverityPresentation.Palette[3], drawRow);

            if (!hasHigh && !hasNeeds && !hasInfo)
                EmptyStateDrawer.DrawLabel("No issues found in this area.");
        }

        public static void BucketByTier<T>(
            IEnumerable<T> items,
            Func<T, Severity> getSeverity,
            List<T> highPriority,
            List<T> needsAttention,
            List<T> informational)
        {
            highPriority?.Clear();
            needsAttention?.Clear();
            informational?.Clear();

            if (items == null)
                return;

            foreach (var item in items)
            {
                switch (FocusOverviewTiering.GetTier(getSeverity(item)))
                {
                    case FocusOverviewTier.HighPriority:
                        highPriority?.Add(item);
                        break;
                    case FocusOverviewTier.NeedsAttention:
                        needsAttention?.Add(item);
                        break;
                    default:
                        informational?.Add(item);
                        break;
                }
            }
        }
    }
}
