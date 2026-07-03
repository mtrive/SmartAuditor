// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Left-side navigation panel for focus areas, work tracking, and filters.
    /// </summary>
    internal sealed class NavigatorPanel
    {
        bool m_ShowFocusAreas = true;
        bool m_ShowFilters = true;
        Vector2 m_ScrollPosition;

        // Cached per-impact issue totals; recomputed only when the report instance or its
        // ItemCount changes. ItemCount is O(1) so the staleness check is cheap. Without this
        // cache DrawFocusAreas calls GetImpactStats once per individual impact flag every
        // repaint, and each call allocates an array of every report item (Report.GetItemCount
        // does m_Issues.ToArray() under a mutex).
        readonly Dictionary<Impact, int> m_ImpactTotalsCache = new Dictionary<Impact, int>();
        Report m_LastImpactReport;
        int m_LastImpactItemCount = -1;

        /// <summary>
        /// Draws the navigator panel.
        /// </summary>
        public void Draw(NavigatorPanelContext context)
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.ExpandHeight(true)))
            {
                //m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

                DrawModeSelector(context);
                EditorGUILayout.Space(UiLayout.SpaceSmall);

                m_ShowFocusAreas = EditorGUILayout.Foldout(m_ShowFocusAreas, "Focus Areas", toggleOnLabelClick: true, SharedStyles.Foldout);
                if (m_ShowFocusAreas)
                {
                    DrawFocusAreas(context);
                    EditorGUILayout.Space(UiLayout.SpaceLarge);
                }

                m_ShowFilters = EditorGUILayout.Foldout(m_ShowFilters, "Filters", toggleOnLabelClick: true, SharedStyles.Foldout);
                if (m_ShowFilters)
                {
                    DrawFilters(context);
                }

                //EditorGUILayout.EndScrollView();
            }
        }

        void DrawModeSelector(NavigatorPanelContext context)
        {
            var allIssuesLabel = context.IsAllImpactScope ? "All Issues" : "All Issues (Reset Impact)";
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(allIssuesLabel, EditorStyles.miniButton))
                    context.OnOverviewRequested?.Invoke();
            }

            EditorGUILayout.LabelField($"Impact Scope: {context.ActiveImpactSummary}", EditorStyles.miniLabel);
        }

        void DrawFocusAreas(NavigatorPanelContext context)
        {
            if (context.Report == null)
            {
                EmptyStateDrawer.DrawLabel("No report loaded.");
                return;
            }

            if (context.IsMessageFocusActive)
            {
                EditorGUILayout.LabelField("Focus areas are unavailable while investigating a message.", EditorStyles.miniLabel);
            }

            EnsureImpactTotals(context.Report);

            var row = 0;
            var visibleRows = 0;
            var flags = ImpactCatalog.IndividualFlags;
            for (var i = 0; i < flags.Count; i++)
            {
                var impact = flags[i];
                if (!m_ImpactTotalsCache.TryGetValue(impact, out var total) || total == 0)
                    continue;

                visibleRows++;
                var rowStyle = row++ % 2 == 0 ? SharedStyles.Row : SharedStyles.RowAlternate;
                var label = impact.ToString();
                var isActive = !context.IsAllImpactScope && string.Equals(context.ActiveImpactSummary, label, StringComparison.Ordinal);

                using (new EditorGUILayout.HorizontalScope(rowStyle))
                {
                    GUILayout.Space(4);
                    if (isActive || context.IsMessageFocusActive)
                    {
                        var style = isActive ? SharedStyles.BoldLabel : SharedStyles.Label;
                        EditorGUILayout.LabelField(label, style, GUILayout.ExpandWidth(true));
                    }
                    else if (UiSection.DrawLinkButton(label, GUILayout.ExpandWidth(true)))
                    {
                        context.OnFocusAreaSelected?.Invoke(impact);
                    }

                    EditorGUILayout.LabelField($"{total}", SharedStyles.BoldLabel, GUILayout.Width(36));
                    GUILayout.Space(4);
                }
            }

            if (visibleRows == 0)
                EmptyStateDrawer.DrawLabel("No focus areas match the current report.");
        }

        /// <summary>
        /// Recomputes <see cref="m_ImpactTotalsCache"/> if the report instance or its item
        /// count has changed since the last call. One pass over the report fills counts for
        /// every individual impact flag, replacing the prior per-flag <c>GetImpactStats</c>
        /// loop that walked the whole report 10× per repaint.
        /// </summary>
        void EnsureImpactTotals(Report report)
        {
            var issueCount = report.IssueCount;
            if (ReferenceEquals(report, m_LastImpactReport) && issueCount == m_LastImpactItemCount)
            {
                return;
            }

            var flags = ImpactCatalog.IndividualFlags;
            m_ImpactTotalsCache.Clear();
            for (var i = 0; i < flags.Count; i++)
            {
                m_ImpactTotalsCache[flags[i]] = 0;
            }

            // Single pass over all issues. Report.GetIssues() allocates an array and locks
            // a mutex; doing it once per refresh (instead of once per impact flag) is the main win.
            foreach (var item in report.GetIssues())
            {
                if (!item.Descriptor.IsValid())
                    continue;

                var itemImpact = item.Descriptor.Impact;
                if (itemImpact == Impact.None)
                    continue;

                for (var i = 0; i < flags.Count; i++)
                {
                    var flag = flags[i];
                    if ((itemImpact & flag) != 0)
                    {
                        m_ImpactTotalsCache[flag]++;
                    }
                }
            }

            m_LastImpactReport = report;
            m_LastImpactItemCount = issueCount;
        }

        static readonly Severity[] s_MinViewSeverityChoices = ReportFilter.ValidThresholds;

        static readonly GUIContent[] s_MinViewSeverityChoiceLabels = BuildMinViewSeverityChoiceLabels();

        static readonly GUIContent k_MinViewSeverityLabel = new GUIContent(
            "Min Severity",
            "Hides issues below this severity from the views. Default = no filter.");

        void DrawFilters(NavigatorPanelContext context)
        {
            if (context.IsMessageFocusActive)
                EditorGUILayout.LabelField("Filters are unavailable while investigating a message. Return to Overview to edit them.", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(context.IsMessageFocusActive))
            {
                EditorGUILayout.LabelField("Search (applies on Enter)", EditorStyles.miniLabel);
                var navigatorSearch = context.NavigatorSearchText;
                if (ToolbarSearchField.DrawRow(ref navigatorSearch, useDelayed: true, GUIContent.none, maxFieldWidth: 220f))
                    context.OnNavigatorSearchTextChanged?.Invoke(navigatorSearch);

                EditorGUILayout.Space(UiLayout.SpaceSmall);
                DrawMinViewSeverityPopup(context);
            }
        }

        static void DrawMinViewSeverityPopup(NavigatorPanelContext context)
        {
            var currentIndex = IndexOfThreshold(s_MinViewSeverityChoices, context.MinViewSeverity);
            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUILayout.Popup(k_MinViewSeverityLabel, currentIndex, s_MinViewSeverityChoiceLabels);
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < s_MinViewSeverityChoices.Length)
                context.OnMinViewSeverityChanged?.Invoke(s_MinViewSeverityChoices[newIndex]);
        }

        static int IndexOfThreshold(Severity[] thresholds, Severity value)
        {
            for (var i = 0; i < thresholds.Length; i++)
                if (thresholds[i] == value)
                    return i;
            return 0;
        }

        static GUIContent[] BuildMinViewSeverityChoiceLabels()
        {
            var labels = new GUIContent[s_MinViewSeverityChoices.Length];
            for (var i = 0; i < s_MinViewSeverityChoices.Length; i++)
            {
                var label = s_MinViewSeverityChoices[i] == Severity.Default
                    ? "Default (no filter)"
                    : s_MinViewSeverityChoices[i].ToString();
                labels[i] = new GUIContent(label);
            }
            return labels;
        }

        /// <summary>
        /// Computes issue counts for a single <see cref="Impact"/> focus area (same filter as the navigator list).
        /// </summary>
        /// <remarks>
        /// Used by tests and external callers. Production NavigatorPanel rendering goes
        /// through the cached path in <see cref="EnsureImpactTotals"/> instead, which fills
        /// every individual impact flag in one pass.
        /// </remarks>
        internal static FocusAreaStats GetImpactStats(Report report, Impact impact)
        {
            var total = 0;
            foreach (var item in report.GetIssues())
            {
                if (!item.Descriptor.IsValid())
                    continue;
                if ((item.Descriptor.Impact & impact) != 0)
                    total++;
            }
            return new FocusAreaStats { Total = total };
        }

        internal struct FocusAreaStats
        {
            public int Total;
        }
    }

    /// <summary>
    /// Data and callbacks used to render navigator content.
    /// </summary>
    internal struct NavigatorPanelContext
    {
        public Report Report;
        public Report PreviousReport;
        public string ActiveImpactSummary;
        public bool IsAllImpactScope;
        public bool IsMessageFocusActive;
        /// <summary>Current minimum severity surfaced by the views. Severity.Default = no filter.</summary>
        public Severity MinViewSeverity;
        /// <summary>Current navigator text filter for the main report window.</summary>
        public string NavigatorSearchText;
        /// <summary>Invoked when the navigator min-severity dropdown changes.</summary>
        public Action<Severity> OnMinViewSeverityChanged;
        /// <summary>Invoked when the navigator search text is committed or cleared.</summary>
        public Action<string> OnNavigatorSearchTextChanged;
        /// <summary>Invoked when the user selects an <see cref="Impact"/> focus row.</summary>
        public Action<Impact> OnFocusAreaSelected;
        /// <summary>Invoked when the user requests the All Issues overview.</summary>
        public Action OnOverviewRequested;
    }
}
