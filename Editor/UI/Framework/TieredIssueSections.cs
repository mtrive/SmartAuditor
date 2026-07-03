// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Shared drawing helpers for severity summaries and tiered issue sections.
    /// </summary>
    internal static class TieredIssueSections
    {
        public static void DrawSeveritySummary(int critical, int major, int moderate, int minor)
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("Severity Summary", SharedStyles.LargeLabel);
                EditorGUILayout.Space(UiLayout.SpaceXSmall);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Critical: {critical}", SharedStyles.Label, GUILayout.Width(100));
                    EditorGUILayout.LabelField($"Major: {major}", SharedStyles.Label, GUILayout.Width(90));
                    EditorGUILayout.LabelField($"Moderate: {moderate}", SharedStyles.Label, GUILayout.Width(110));
                    EditorGUILayout.LabelField($"Minor: {minor}", SharedStyles.Label, GUILayout.Width(90));
                }
            }
        }

        public static void DrawTierSection<T>(string title, IReadOnlyList<T> items, Color accentColor, Action<T, int> drawRow)
        {
            if (items == null || items.Count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var colorRect = GUILayoutUtility.GetRect(4f, EditorGUIUtility.singleLineHeight + 2f, GUILayout.Width(4f));
                    SolidDraw.Rect(colorRect, accentColor);
                    GUILayout.Space(UiLayout.SpaceSmall);
                    GUILayout.Label(title, SharedStyles.LargeLabel);
                }

                EditorGUILayout.Space(UiLayout.SpaceXSmall);
                for (var i = 0; i < items.Count; i++)
                    drawRow?.Invoke(items[i], i);
            }
        }
    }
}
