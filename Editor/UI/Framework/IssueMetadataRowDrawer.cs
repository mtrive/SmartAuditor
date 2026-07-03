// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Options for drawing a descriptor metadata row (severity, optional middle content, ID copy).
    /// </summary>
    internal sealed class IssueMetadataRowOptions
    {
        public GUIContent SeverityLabel { get; set; }
        public GUIStyle SeverityLabelStyle { get; set; }
        public float SeverityLabelWidth { get; set; } = -1f;
        public string DescriptorId { get; set; }
        public Action DrawAfterSeverity { get; set; }
        public bool FlexibleSpaceBeforeDescriptorId { get; set; } = true;
        public bool HideDescriptorIdWhenEmpty { get; set; } = true;
    }

    /// <summary>
    /// Shared severity / descriptor-ID metadata row with copy-to-clipboard action.
    /// </summary>
    internal static class IssueMetadataRowDrawer
    {
        const float k_CopyToClipboardButtonSize = 24f;

        public static void Draw(IssueMetadataRowOptions options)
        {
            if (options == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (options.SeverityLabel != null)
                {
                    if (options.SeverityLabelWidth >= 0f)
                    {
                        EditorGUILayout.LabelField(
                            options.SeverityLabel,
                            options.SeverityLabelStyle,
                            GUILayout.Width(options.SeverityLabelWidth));
                    }
                    else
                    {
                        EditorGUILayout.LabelField(
                            options.SeverityLabel,
                            options.SeverityLabelStyle ?? EditorStyles.label,
                            GUILayout.ExpandWidth(false));
                    }
                }

                options.DrawAfterSeverity?.Invoke();

                if (options.FlexibleSpaceBeforeDescriptorId)
                    GUILayout.FlexibleSpace();

                var descriptorId = options.DescriptorId ?? string.Empty;
                if (!options.HideDescriptorIdWhenEmpty || !string.IsNullOrEmpty(descriptorId))
                {
                    EditorGUILayout.LabelField(descriptorId, SharedStyles.LabelDarkWithDynamicSize, GUILayout.Width(60f));
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(descriptorId)))
                    {
                        if (GUILayout.Button(Utility.GetIcon(Utility.IconType.CopyToClipboard), SharedStyles.TabHoverButton,
                            GUILayout.Width(k_CopyToClipboardButtonSize), GUILayout.Height(k_CopyToClipboardButtonSize)))
                        {
                            EditorInterop.CopyToClipboard(descriptorId);
                        }
                    }
                }
            }
        }
    }
}
