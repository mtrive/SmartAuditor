// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Unity editor toolbar search field layout (matches built-in Project/Console search strips).
    /// </summary>
    public static class ToolbarSearchField
    {
        /// <summary>Default maximum width for the search text field (excluding the clear button).</summary>
        public const float DefaultSearchFieldMaxWidth = 250f;

        /// <summary>Built-in or fallback style for the search text field.</summary>
        public static GUIStyle SearchTextFieldStyle =>
            GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField;

        /// <summary>Built-in clear button style, or null when the active GUI skin does not define it.</summary>
        public static GUIStyle SearchCancelButtonStyle =>
            GUI.skin.FindStyle("ToolbarSearchCancelButton");

        /// <summary>
        /// Draws a toolbar row with search text field and optional clear control.
        /// </summary>
        /// <param name="searchText">Current search text; updated when the user commits an edit or clears.</param>
        /// <param name="useDelayed">When true, uses <see cref="EditorGUILayout.DelayedTextField"/> (commit on Enter / defocus).</param>
        /// <param name="fieldLabel">Optional label (e.g. empty text with tooltip only).</param>
        /// <param name="maxFieldWidth">Maximum width of the text field; use 0 or negative for no limit.</param>
        /// <returns>True if <paramref name="searchText"/> changed this frame.</returns>
        public static bool DrawRow(ref string searchText, bool useDelayed, GUIContent fieldLabel = null, float maxFieldWidth = DefaultSearchFieldMaxWidth)
        {
            var changed = false;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            changed = DrawInline(ref searchText, useDelayed, fieldLabel, maxFieldWidth);

            EditorGUILayout.EndHorizontal();
            return changed;
        }

        /// <summary>
        /// Draws search controls inside an existing toolbar row.
        /// </summary>
        public static bool DrawInline(ref string searchText, bool useDelayed, GUIContent fieldLabel = null, float maxFieldWidth = DefaultSearchFieldMaxWidth)
        {
            var changed = false;
            var style = SearchTextFieldStyle;
            var label = fieldLabel ?? GUIContent.none;

            GUILayoutOption[] fieldLayout = maxFieldWidth > 0f
                ? new[] { GUILayout.ExpandWidth(true), GUILayout.MaxWidth(maxFieldWidth) }
                : new[] { GUILayout.ExpandWidth(true) };

            EditorGUI.BeginChangeCheck();
            if (useDelayed)
                searchText = EditorGUILayout.DelayedTextField(label, searchText, style, fieldLayout);
            else
                searchText = EditorGUILayout.TextField(label, searchText, style, fieldLayout);

            if (EditorGUI.EndChangeCheck())
                changed = true;

            var cancelStyle = SearchCancelButtonStyle;
            if (cancelStyle != null && GUILayout.Button(GUIContent.none, cancelStyle) && !string.IsNullOrEmpty(searchText))
            {
                searchText = string.Empty;
                GUI.FocusControl(null);
                changed = true;
            }

            return changed;
        }
    }
}
