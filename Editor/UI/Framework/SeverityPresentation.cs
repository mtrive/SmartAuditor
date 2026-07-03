// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Shared severity colors and row severity bar drawing for report UIs.
    /// </summary>
    internal static class SeverityPresentation
    {
        /// <summary>
        /// Chart / tier accent palette: Critical, Major, Moderate, Minor, Ignored/default.
        /// Matches the former <c>AnalysisReportView.m_SeverityColors</c> ordering.
        /// </summary>
        internal static readonly Color[] Palette =
        {
            new Color(0.96f, 0.3f, 0.26f),
            new Color(1f, 0.52f, 0.26f),
            new Color(0.95f, 0.74f, 0f),
            new Color(0.43f, 0.71f, 1f),
            new Color(0.768f, 0.768f, 0.768f, 1f)
        };

        internal static Color GetColorForSeverity(Severity severity)
        {
            switch (severity)
            {
                case Severity.Critical:
                case Severity.Error:
                    return Palette[0];
                case Severity.Major:
                    return Palette[1];
                case Severity.Moderate:
                    return Palette[2];
                case Severity.Minor:
                    return Palette[3];
                default:
                    return Palette[4];
            }
        }

        internal static void DrawRowSeverityBar(Severity severity)
        {
            var barRect = GUILayoutUtility.GetRect(4f, 15f, GUILayout.Width(4f), GUILayout.ExpandHeight(false));
            SolidDraw.VerticalBar(barRect, GetColorForSeverity(severity));
        }
    }
}
