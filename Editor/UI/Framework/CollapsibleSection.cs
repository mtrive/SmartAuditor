// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Reusable foldout-style section header used by dashboard views (diagnostic sections,
    /// message sections). Renders a foldout icon, a trimmed title (auto-shortened to fit),
    /// an optional right-aligned trailing link button, and supports click-anywhere-on-header
    /// toggle. The component is stateless — callers own the expanded flag and pass it in.
    /// </summary>
    internal static class CollapsibleSection
    {
        const float k_FoldoutIconSize = 19f;
        const float k_DefaultTrailingColumnWidth = 100f;

        /// <summary>
        /// Renders a section header. Returns the (possibly toggled) <c>isExpanded</c> state.
        /// </summary>
        /// <param name="isExpanded">Current expanded state — caller stores this.</param>
        /// <param name="titleContent">Title content; tooltip is preserved when the title is trimmed.</param>
        /// <param name="trailingLinkContent">Trailing link content (e.g. "View all ↗"); pass <c>null</c> to omit.</param>
        /// <param name="onTrailingLinkClicked">Callback when the trailing link is clicked. Ignored when <paramref name="trailingLinkContent"/> is null.</param>
        /// <param name="onToggled">Callback fired when the user clicks the header to toggle expansion (typically used to request a window repaint).</param>
        /// <param name="trailingColumnWidth">Width reserved for the trailing-link column (also the click-exclusion zone). Defaults to 100px to match other dashboard rows.</param>
        public static bool DrawHeader(
            bool isExpanded,
            GUIContent titleContent,
            GUIContent trailingLinkContent,
            Action onTrailingLinkClicked,
            Action onToggled,
            float trailingColumnWidth = k_DefaultTrailingColumnWidth)
        {
            var titleHeaderRect = default(Rect);
            var trailingLinkRect = default(Rect);

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                var foldoutIcon = isExpanded ? Utility.FoldoutExpandedIcon : Utility.FoldoutFoldedIcon;
                GUILayout.Label(foldoutIcon, GUIStyle.none, GUILayout.Width(k_FoldoutIconSize), GUILayout.Height(k_FoldoutIconSize));

                titleHeaderRect = EditorGUILayout.GetControlRect(
                    hasLabel: false,
                    height: SharedStyles.LargeLabel.fixedHeight,
                    GUILayout.ExpandWidth(true),
                    GUILayout.MinWidth(40f));

                using (new EditorGUILayout.HorizontalScope(GUILayout.Width(trailingColumnWidth)))
                {
                    GUILayout.FlexibleSpace();
                    if (trailingLinkContent != null)
                    {
                        if (GUILayout.Button(trailingLinkContent, SharedStyles.LinkLabel,
                                GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true)))
                        {
                            onTrailingLinkClicked?.Invoke();
                        }
                        trailingLinkRect = GUILayoutUtility.GetLastRect();
                    }
                    else
                    {
                        // No link this section: still reserve the column so the title alignment
                        // matches sections that do have a link, and capture the rect to exclude
                        // it from the click-toggle zone.
                        GUILayout.Space(trailingColumnWidth);
                        trailingLinkRect = GUILayoutUtility.GetLastRect();
                    }
                }
            }

            if (Event.current.type == EventType.Repaint && titleContent != null)
            {
                var trimmed = GUIContentCache.TrimToWidth(
                    titleContent.text, titleContent.tooltip, SharedStyles.LargeLabel, titleHeaderRect.width);
                GUI.Label(titleHeaderRect, trimmed, SharedStyles.LargeLabel);
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                var headerRowRect = GUILayoutUtility.GetLastRect();
                if (headerRowRect.Contains(Event.current.mousePosition) &&
                    !trailingLinkRect.Contains(Event.current.mousePosition))
                {
                    isExpanded = !isExpanded;
                    Event.current.Use();
                    onToggled?.Invoke();
                }
            }

            return isExpanded;
        }
    }
}
