// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using UnityEditor;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Shared empty-state rendering helpers.
    /// </summary>
    internal static class EmptyStateDrawer
    {
        public static void DrawInfoHelpBox(string text)
        {
            EditorGUILayout.HelpBox(text, MessageType.Info);
        }

        public static void DrawLabel(string text)
        {
            EditorGUILayout.LabelField(text, SharedStyles.Label);
        }
    }
}
