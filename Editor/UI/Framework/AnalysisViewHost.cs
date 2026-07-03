// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using SmartAuditor.Editor;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Host shell for <see cref="IAnalysisView"/> — report title chrome, pinned header/footer,
    /// and optional vertical scroll around body content.
    /// </summary>
    internal static class AnalysisViewHost
    {
        /// <summary>
        /// Options for the fixed report title bar rendered above the view.
        /// </summary>
        internal sealed class ReportTitleOptions
        {
            public GUIContent TitleLabel { get; set; }
            public Report Report { get; set; }
            public BuildTarget DisplayPlatform { get; set; }
            public bool HasPendingUnsavedChanges { get; set; }
            public string ViewDescription { get; set; }
        }

        /// <summary>
        /// Renders the report host chrome and invokes <paramref name="drawBody"/> inside the scroll region when required.
        /// </summary>
        public static void Draw(IAnalysisView view, ReportTitleOptions titleOptions, Action drawBody)
        {
            if (view == null)
                return;

            // Pinned header chrome -- rendered outside the scroll view so the report title
            // and any view-supplied pinned UI stay visible regardless of scroll position.
            DrawReportTitle(titleOptions);
            view.DrawPinnedHeader();

            // Capture once per OnGUI invocation. The view can change WorkspaceMode mid-frame
            // (e.g. clicking an issue link inside DrawContent transitions Overview ->
            // IssueInvestigation, and ShowVerticalScrollView is mode-dependent). Reading the
            // property a second time after drawBody would mismatch BeginScrollView /
            // EndScrollView and corrupt the GUILayout state.
            var useScrollView = view.ShowVerticalScrollView;
            if (useScrollView)
            {
                view.VerticalScrollViewPos = EditorGUILayout.BeginScrollView(view.VerticalScrollViewPos,
                    alwaysShowHorizontal: false, alwaysShowVertical: true, GUIStyle.none,
                    GUI.skin.verticalScrollbar, GUI.skin.scrollView);
            }

            drawBody?.Invoke();

            if (useScrollView)
            {
                EditorGUILayout.EndScrollView();

                if (Event.current.type == EventType.Repaint)
                {
                    var rectSize = GUILayoutUtility.GetLastRect().size;
                    view.LastVerticalScrollViewSize = new Vector2(rectSize.x, rectSize.y);
                }
            }

            // Pinned footer rendered after the scroll view so it stays anchored at the bottom
            // of the report area regardless of scroll position.
            view.DrawPinnedFooter();
        }

        static void DrawReportTitle(ReportTitleOptions options)
        {
            if (options == null)
                return;

            GUILayout.Space(2);

            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Space(4);

                    if (options.TitleLabel != null)
                        GUILayout.Label(options.TitleLabel, SharedStyles.MediumTitleLabel);

                    if (options.Report != null)
                    {
                        var group = BuildPipeline.GetBuildTargetGroup(options.DisplayPlatform);
                        var platformName = group == BuildTargetGroup.Unknown ? "Unknown" : group.ToString();
                        var projectName = options.Report.SessionInfo?.ProductName;
                        if (string.IsNullOrWhiteSpace(projectName))
                            projectName = Application.productName;
                        if (string.IsNullOrWhiteSpace(projectName))
                            projectName = "Unknown Project";

                        GUILayout.Label(" | ", SharedStyles.MediumTitleLabel);
                        GUILayout.Label(platformName, SharedStyles.MediumTitleLabel);
                        GUILayout.Label(" | ", SharedStyles.MediumTitleLabel);
                        GUILayout.Label(projectName, SharedStyles.MediumTitleLabel);

                        if (options.HasPendingUnsavedChanges)
                            GUILayout.Label("*", SharedStyles.MediumTitleLabel);
                    }

                    GUILayout.FlexibleSpace();
                }

                GUILayout.Space(8);

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Space(4);
                    if (!string.IsNullOrEmpty(options.ViewDescription))
                        GUILayout.Label(options.ViewDescription, GUILayout.MinWidth(360), GUILayout.ExpandWidth(true));
                    GUILayout.FlexibleSpace();
                }
            }
        }
    }
}
