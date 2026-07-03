// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Full-window overlay UI for <see cref="ScrollingAnalysisProgress"/> (shared by main and scoped analysis windows).
    /// </summary>
    internal static class ScrollingAnalysisProgressUI
    {
        static GUIStyle s_AnalysisActivityLineStyle;

        /// <summary>
        /// Draws a modal-style overlay with progress bar, activity log strip, and cancel.
        /// </summary>
        internal static void DrawOverlay(EditorWindow window, ScrollingAnalysisProgress progress, List<string> analysisLogBuffer)
        {
            if (progress == null || window == null)
                return;

            progress.CopyLogLines(analysisLogBuffer);

            var full = new Rect(0f, 0f, window.position.width, window.position.height);

            SolidDraw.Rect(full, new Color(0.06f, 0.06f, 0.07f, 0.82f));

            const float margin = 20f;
            var panelW = Mathf.Min(560f, window.position.width - margin * 2f);

            GUILayout.BeginArea(full);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(panelW));

            var headline = string.IsNullOrEmpty(progress.Title) ? "Starting…" : progress.Title;
            GUILayout.Label(headline, SharedStyles.MediumTitleLabel);
            GUILayout.Space(4);

            var barRect = EditorGUILayout.GetControlRect(GUILayout.Height(18f));
            EditorGUI.ProgressBar(barRect, progress.ProgressFraction,
                progress.ProgressFraction >= 0.999f ? "Finishing…" : $"{Mathf.RoundToInt(progress.ProgressFraction * 100f)}%");

            GUILayout.Space(6);
            DrawAnalysisActivityLinesFaded(analysisLogBuffer);

            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(100f)))
                    progress.RequestCancel();
                GUILayout.FlexibleSpace();
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        /// <summary>Shows the last few log lines in a short strip; upper lines use lower opacity.</summary>
        static void DrawAnalysisActivityLinesFaded(List<string> lines)
        {
            const int kVisibleLines = 6;
            var lineH = EditorGUIUtility.singleLineHeight;
            if (s_AnalysisActivityLineStyle == null)
            {
                s_AnalysisActivityLineStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    wordWrap = false,
                    clipping = TextClipping.Clip
                };
            }

            var style = s_AnalysisActivityLineStyle;

            var start = Mathf.Max(0, lines.Count - kVisibleLines);
            var count = lines.Count - start;
            if (count == 0)
            {
                using (new EditorGUI.DisabledScope(disabled: true))
                    GUILayout.Label("…", style, GUILayout.Height(lineH));
                return;
            }

            var savedColor = GUI.color;
            for (var i = 0; i < count; i++)
            {
                var alpha = count <= 1
                    ? 1f
                    : Mathf.Lerp(0.18f, 1f, (i + 1f) / count);
                var c = savedColor;
                GUI.color = new Color(c.r, c.g, c.b, c.a * alpha);

                var text = lines[start + i];
                if (text != null && text.Length > 120)
                    text = text.Substring(0, 117) + "…";

                GUILayout.Label(text ?? string.Empty, style, GUILayout.Height(lineH));
            }

            GUI.color = savedColor;
        }
    }
}
