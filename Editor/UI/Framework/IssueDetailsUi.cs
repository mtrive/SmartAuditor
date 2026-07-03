// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Shared helpers for resolving issue details text and rendering callouts.
    /// </summary>
    internal static class IssueDetailsUi
    {
        public static string GetTitle(ReportItem item, string fallback = "Issue")
        {
            var descriptor = item?.Descriptor;
            if (descriptor != null && descriptor.IsValid() && !string.IsNullOrEmpty(descriptor.Title))
                return descriptor.Title;

            if (!string.IsNullOrEmpty(item?.Description))
                return item.Description;

            return fallback;
        }

        public static string GetDescription(ReportItem item, bool allowItemDescriptionFallback = true, string fallback = null)
        {
            var descriptor = item?.Descriptor;
            if (descriptor != null && descriptor.IsValid() && !string.IsNullOrEmpty(descriptor.Description))
                return Formatting.DecodeHtmlEntities(descriptor.Description);

            if (allowItemDescriptionFallback && !string.IsNullOrEmpty(item?.Description))
                return item.Description;

            return fallback;
        }

        public static string GetRecommendation(ReportItem item)
        {
            var descriptor = item?.Descriptor;
            if (descriptor != null && descriptor.IsValid() && !string.IsNullOrEmpty(descriptor.Recommendation))
                return Formatting.DecodeHtmlEntities(descriptor.Recommendation);

            return null;
        }

        const float k_RecommendationAccentBarWidth = 4f;

        public static void DrawRecommendationCallout(string recommendation, Color accentColor, GUIStyle recommendationStyle, float maxHeight = 0f)
        {
            // Lay out the row first, *without* requesting ExpandHeight on the accent bar.
            // Reserving a 4-wide rect with ExpandHeight(true) used to make the bar match the
            // text height — but the flag propagated up through the layout chain and pulled
            // the whole row (and its enclosing box) taller than its actual content. Now we
            // reserve only horizontal space for the bar, let the VerticalScope's natural
            // height drive the row, then paint the bar manually from the row's measured rect.
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(k_RecommendationAccentBarWidth + UiLayout.SpaceMedium);

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("How to fix", SharedStyles.BoldLabel);
                    if (maxHeight > 0f)
                        EditorGUILayout.LabelField(recommendation, recommendationStyle, GUILayout.MaxHeight(maxHeight));
                    else
                        EditorGUILayout.LabelField(recommendation, recommendationStyle);
                }
            }

            var rowRect = GUILayoutUtility.GetLastRect();
            var barRect = new Rect(rowRect.x, rowRect.y, k_RecommendationAccentBarWidth, rowRect.height);
            SolidDraw.Rect(barRect, accentColor);
        }
    }
}
