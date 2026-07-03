// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Reusable section and link drawing helpers for editor views.
    /// </summary>
    internal static class UiSection
    {
        public static void DrawBackLinkRow(string label, Action onClick)
        {
            using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
            {
                if (GUILayout.Button(label, SharedStyles.LinkLabel, GUILayout.ExpandWidth(false)))
                {
                    onClick?.Invoke();
                    return;
                }

                GUILayout.FlexibleSpace();
            }
        }

        public static bool DrawLinkButton(string label, params GUILayoutOption[] options)
        {
            return GUILayout.Button(label, SharedStyles.LinkLabel, options);
        }

        public static bool DrawLinkButton(GUIContent label, params GUILayoutOption[] options)
        {
            return GUILayout.Button(label, SharedStyles.LinkLabel, options);
        }

        public static void DrawHeaderWithCount(string title, int count)
        {
            DrawHeaderWithCount(title, count, drawTrailing: null);
        }

        public static void DrawHeaderWithCount(string title, int count, Action drawTrailing)
        {
            if (drawTrailing == null)
            {
                GUILayout.Label($"{title} ({count})", SharedStyles.LargeLabel);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{title} ({count})", SharedStyles.LargeLabel);
                GUILayout.FlexibleSpace();
                drawTrailing();
            }
        }
    }
}
