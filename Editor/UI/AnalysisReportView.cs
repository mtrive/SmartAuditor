// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.UI;
using SmartAuditor.Editor.UI.Framework;
using SmartAuditor.Editor.Utils;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI
{
    /// <summary>
    /// Scrolling analysis report view that replaces the tab-based Summary view.
    /// Groups diagnostic issues by category with severity charts and collapsible top-N lists,
    /// and shows insight cards with high-level stats per category.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="IAnalysisView"/> directly with a custom IMGUI layout that
    /// aggregates data across all categories.
    /// </remarks>
    sealed class AnalysisReportView : IAnalysisView
    {
        const int k_DefaultTopN = 10;
        const int k_MessagePreviewMaxChars = 120;
        const float k_CopyToClipboardButtonSize = 24f;

        // Affected-item rows are constant-height (the per-row snippet and Evidence panel
        // moved to the pinned footer), which lets us virtualize the list: only the rows
        // currently inside the inner scroll viewport are rendered, regardless of how many
        // occurrences the issue has.
        static float AffectedItemRowHeight => EditorGUIUtility.singleLineHeight + 2f;

        // Cap for the inner Affected Items scroll view. Short lists size to their content
        // (no empty padding inside the box); longer lists are bounded here so the list can't
        // push the pinned footer off screen.
        const float k_AffectedItemsMaxScrollHeight = 600f;
        const float k_AffectedItemsMinScrollHeight = 120f;

        /// <summary>Preferred width for the centered report body, completeness bar, and severity bar charts.</summary>
        const float k_ReportContentWidth = 800f;
        const float k_DetailContentWidth = 860f;

        static readonly AnalysisCategory[] k_DiagnosticCategories =
        {
            AnalysisCategory.Code,
            AnalysisCategory.AssetIssue,
            AnalysisCategory.ProjectSetting,
            AnalysisCategory.GameObjectIssue,
            AnalysisCategory.SceneSettings
        };

        /// <summary>Order of insight cards in the diagnostic report.</summary>
        static readonly AnalysisCategory[] k_InsightCategoryOrder =
        {
            AnalysisCategory.Texture,
            AnalysisCategory.Sprite,
            AnalysisCategory.SpriteAtlas,
            AnalysisCategory.Mesh,
            AnalysisCategory.AudioClip,
            AnalysisCategory.Shader,
            AnalysisCategory.ShaderVariant,
            AnalysisCategory.ComputeShaderVariant,
            AnalysisCategory.Material,
            AnalysisCategory.AnimatorController,
            AnalysisCategory.AnimationClip,
            AnalysisCategory.Avatar,
            AnalysisCategory.AvatarMask,
            AnalysisCategory.Assembly,
            AnalysisCategory.PrecompiledAssembly,
            AnalysisCategory.Package,
            AnalysisCategory.Prefab,
            AnalysisCategory.Scene,
            AnalysisCategory.BuildFile,
        };

        struct SeverityStats
        {
            public int Critical;
            public int Major;
            public int Moderate;
            public int Minor;
            public int Ignored;
            public int Total => Critical + Major + Moderate + Minor + Ignored;
        }

        /// <summary>
        /// A pre-digested issue or message group for the row drawers. Replaces the previous
        /// <see cref="IGrouping{TKey, TElement}"/>-based caches: <see cref="Count"/>,
        /// <see cref="First"/> and <see cref="HighestSeverity"/> are materialised at refresh
        /// time so the per-row hot path doesn't enumerate the underlying items.
        /// </summary>
        readonly struct RowGroup
        {
            public readonly string Key;
            public readonly ReportItem First;
            public readonly int Count;
            public readonly Severity HighestSeverity;
            public readonly List<ReportItem> Items;

            public RowGroup(string key, ReportItem first, int count, Severity highestSeverity, List<ReportItem> items)
            {
                Key = key;
                First = first;
                Count = count;
                HighestSeverity = highestSeverity;
                Items = items;
            }
        }

        /// <summary>
        /// Native-<see cref="Message"/> equivalent of <see cref="RowGroup"/> used by the
        /// Overview's compiler/import message sections. Severity is reduced from
        /// <see cref="LogLevel"/> at refresh time and stored alongside, mirroring the
        /// pattern <see cref="RowGroup"/> uses for diagnostics.
        /// </summary>
        readonly struct MessageRowGroup
        {
            public readonly string Key;
            public readonly Message First;
            public readonly int Count;
            public readonly Severity HighestSeverity;
            public readonly List<Message> Items;

            public MessageRowGroup(string key, Message first, int count, Severity highestSeverity, List<Message> items)
            {
                Key = key;
                First = first;
                Count = count;
                HighestSeverity = highestSeverity;
                Items = items;
            }
        }

        struct CompilerMessageStats
        {
            public int Errors;
            public int Warnings;
            public int Infos;
            public int Total => Errors + Warnings + Infos;
        }

        static readonly Color k_CompletenessAnalyzedColor = new Color(0.35f, 0.75f, 0.45f);
        static readonly Color k_CompletenessFailedColor = new Color(0.96f, 0.52f, 0.26f);

        static Color CompletenessPendingColor => SharedStyles.IsDarkMode
            ? new Color(0.35f, 0.35f, 0.35f, 1f)
            : new Color(0.65f, 0.65f, 0.65f, 1f);

        readonly struct CompletenessArea
        {
            public readonly string Name;
            public readonly AnalysisCategory Category;
            public readonly AnalysisCategory[] AnalysisCategories;
            // Toolchain message category surfaced by a failed-area link, so the user can jump
            // directly to the errors that explain the failure. Null when the area has no
            // associated compiler / importer log (e.g. Settings, Build, Scenes & Prefabs).
            public readonly AnalysisCategory? MessagesCategory;

            public CompletenessArea(string name, AnalysisCategory category, AnalysisCategory[] analysisCategories,
                AnalysisCategory? messagesCategory = null)
            {
                Name = name;
                Category = category;
                AnalysisCategories = analysisCategories;
                MessagesCategory = messagesCategory;
            }
        }

        static readonly CompletenessArea[] k_CompletenessAreas =
        {
            new CompletenessArea("Settings", AnalysisCategory.ProjectSetting,
                new[] { AnalysisCategory.ProjectSetting, AnalysisCategory.Package }),
            new CompletenessArea("Code", AnalysisCategory.Code,
                new[] { AnalysisCategory.Code, AnalysisCategory.Assembly, AnalysisCategory.PrecompiledAssembly, AnalysisCategory.CodeCompilerMessage },
                messagesCategory: AnalysisCategory.CodeCompilerMessage),
            new CompletenessArea("Assets", AnalysisCategory.AssetIssue,
                new[] { AnalysisCategory.AssetIssue, AnalysisCategory.Texture, AnalysisCategory.Sprite, AnalysisCategory.SpriteAtlas, AnalysisCategory.Mesh, AnalysisCategory.AudioClip, AnalysisCategory.AnimatorController, AnalysisCategory.AnimationClip, AnalysisCategory.Avatar, AnalysisCategory.AvatarMask, AnalysisCategory.AssetImportMessage },
                messagesCategory: AnalysisCategory.AssetImportMessage),
            // Label matches AnalysisScope.Prefabs (Scenes & Prefabs scope flag).
            new CompletenessArea("Scenes & Prefabs", AnalysisCategory.GameObjectIssue,
                new[] { AnalysisCategory.Prefab, AnalysisCategory.GameObjectIssue, AnalysisCategory.Scene, AnalysisCategory.SceneSettings }),
            new CompletenessArea("Build", AnalysisCategory.BuildFile,
                new[] { AnalysisCategory.BuildFile }),
            // Materials are analyzed with shaders, not under Assets — label matches AnalysisScope.Shaders.
            new CompletenessArea("Shaders & Materials", AnalysisCategory.Shader,
                new[] { AnalysisCategory.Shader, AnalysisCategory.ShaderVariant, AnalysisCategory.ComputeShaderVariant, AnalysisCategory.ShaderCompilerMessage, AnalysisCategory.Material },
                messagesCategory: AnalysisCategory.ShaderCompilerMessage),
        };

        enum WorkspaceMode { Overview, FocusArea, IssueInvestigation, MessageInvestigation, Timeline }

        readonly struct MessageSectionInfo
        {
            public readonly AnalysisCategory Category;
            public readonly string Title;
            public readonly AnalysisCategory AfterDiagnostic;

            public MessageSectionInfo(AnalysisCategory category, string title, AnalysisCategory afterDiagnostic)
            {
                Category = category;
                Title = title;
                AfterDiagnostic = afterDiagnostic;
            }
        }

        static readonly MessageSectionInfo[] k_MessageSections =
        {
            new MessageSectionInfo(AnalysisCategory.CodeCompilerMessage, "Compiler Messages", AnalysisCategory.Code),
            new MessageSectionInfo(AnalysisCategory.ShaderCompilerMessage, "Shader Compiler Messages", AnalysisCategory.Shader),
            new MessageSectionInfo(AnalysisCategory.AssetImportMessage, "Asset Import Messages", AnalysisCategory.AssetIssue),
        };

        // Profiler markers for the report-view hot paths (visible in Unity Profiler under their string names).
        static readonly ProfilerMarker s_DrawContentMarker = new ProfilerMarker("AnalysisReportView.DrawContent");
        static readonly ProfilerMarker s_DrawOverviewMarker = new ProfilerMarker("AnalysisReportView.DrawOverview");
        static readonly ProfilerMarker s_DrawFocusOverviewMarker = new ProfilerMarker("AnalysisReportView.DrawFocusOverview");
        static readonly ProfilerMarker s_DrawIssueInvestigationMarker = new ProfilerMarker("AnalysisReportView.DrawIssueInvestigation");
        static readonly ProfilerMarker s_DrawMessageInvestigationMarker = new ProfilerMarker("AnalysisReportView.DrawMessageInvestigation");
        static readonly ProfilerMarker s_DrawInsightCardsMarker = new ProfilerMarker("AnalysisReportView.DrawInsightCards");
        static readonly ProfilerMarker s_DrawDiagnosticSectionMarker = new ProfilerMarker("AnalysisReportView.DrawDiagnosticSection");
        static readonly ProfilerMarker s_DrawMessageSectionMarker = new ProfilerMarker("AnalysisReportView.DrawMessageSection");
        static readonly ProfilerMarker s_DrawAffectedItemsMarker = new ProfilerMarker("AnalysisReportView.DrawAffectedItems");
        static readonly ProfilerMarker s_RefreshStatsMarker = new ProfilerMarker("AnalysisReportView.RefreshStats");
        static readonly ProfilerMarker s_RefreshFocusAreaStatsMarker = new ProfilerMarker("AnalysisReportView.RefreshFocusAreaStats");

        #region Fields

        SmartAuditorWindow m_Window;
        DiagnosticRules m_Rules;
        bool m_Dirty = true;

        WorkspaceMode m_WorkspaceMode;
        Impact m_FocusImpact;
        string m_InvestigatedIssueId;
        // Diagnostic groups are now keyed by (Id, Severity) — see BuildDiagnosticRowGroups —
        // so a single descriptor can produce multiple rows when hot-path promotion has split
        // its instances across severities. The investigation pin needs to track both halves
        // of the key to filter correctly.
        Severity m_InvestigatedIssueSeverity;
        AnalysisCategory m_InvestigatedMessageCategory;
        string m_InvestigatedMessageGroupKey;
        Report m_TimelineComparisonReport;

        bool m_StatsNeedRefresh = true;
        bool m_HasActionableIssuesCache;
        bool m_ShowInsightCards = true;
        bool m_ShowSessionInfo = true;
        bool m_ShowFirstTriage = true;

        readonly Dictionary<AnalysisCategory, bool> m_SectionExpanded = new Dictionary<AnalysisCategory, bool>();
        readonly Dictionary<AnalysisCategory, bool> m_ShowAll = new Dictionary<AnalysisCategory, bool>();
        readonly Dictionary<AnalysisCategory, SeverityStats> m_StatsCache = new Dictionary<AnalysisCategory, SeverityStats>();
        readonly Dictionary<AnalysisCategory, List<RowGroup>> m_TopIssuesCache = new Dictionary<AnalysisCategory, List<RowGroup>>();
        readonly Dictionary<string, bool> m_IssueDetailExpanded = new Dictionary<string, bool>();
        readonly Dictionary<AnalysisCategory, int> m_InsightCardCountCache = new Dictionary<AnalysisCategory, int>();

        readonly Dictionary<AnalysisCategory, CompilerMessageStats> m_MessageStatsMap = new Dictionary<AnalysisCategory, CompilerMessageStats>();
        readonly Dictionary<AnalysisCategory, List<MessageRowGroup>> m_MessageTopGroupsMap = new Dictionary<AnalysisCategory, List<MessageRowGroup>>();
        readonly Dictionary<AnalysisCategory, bool> m_MessageSectionExpanded = new Dictionary<AnalysisCategory, bool>();
        readonly Dictionary<AnalysisCategory, bool> m_MessageShowAll = new Dictionary<AnalysisCategory, bool>();

        readonly bool[] m_PendingAreaSelected = new bool[k_CompletenessAreas.Length];
        bool m_PendingAreaSelectionInitialized;

        List<RowGroup> m_FocusHighImpact = new List<RowGroup>();
        List<RowGroup> m_FocusNeedsAttention = new List<RowGroup>();
        List<RowGroup> m_FocusInformational = new List<RowGroup>();
        bool m_FocusStatsNeedRefresh;
        int m_FocusCriticalCount;
        int m_FocusMajorCount;
        int m_FocusModerateCount;
        int m_FocusMinorCount;

        List<ReportItem> m_IssueInvestigationItemsCache;
        List<IReportEntry> m_MessageInvestigationItemsCache;
        string m_ReanalyzeBanner;
        double m_ReanalyzeBannerUntil;
        bool m_ReanalyzeBannerIsResolved;

        // Single-expansion semantics for the affected-items list: at most one row expands its
        // code snippet at a time, and clicking another row collapses the previous one. Empty /
        // null when no row is currently expanded.
        string m_ExpandedAffectedItemKey;

        // Captured during DrawAffectedItemsSection on each frame, by scanning allItems for the
        // entry matching m_ExpandedAffectedItemKey. We resolve it that way (rather than from
        // the per-row render path) so the footer keeps showing the snippet + Evidence even
        // when the expanded row scrolls outside the virtualized viewport. Reset at the start
        // of DrawContent so the footer disappears as soon as the expanded row is no longer
        // present (e.g. switched investigation, no longer in the filtered list).
        IReportEntry m_FrameExpandedAffectedItem;

        Vector2 m_AffectedItemsScrollPos;

        // Inner scroll-view height measured at the end of the previous Repaint pass; used to
        // compute the visible row window for virtualization. Not serialized -- it converges
        // within one frame after a window resize from the initial guess.
        float m_AffectedItemsScrollHeight = 400f;
        float m_AffectedItemsListTopY;
        float m_PinnedFooterHeight = 220f;

        #endregion

        #region IAnalysisView

        Report Report => m_Window?.Report;

        /// <inheritdoc />
        public string Description => "A diagnostic report of all issues found in the project.";

        /// <inheritdoc />
        // Investigation modes own their scrolling internally (the affected-items list has its
        // own virtualized scroll view). Disabling the outer scroll there avoids the
        // double-scrollbar / nested-scroll experience.
        public bool ShowVerticalScrollView =>
            m_WorkspaceMode != WorkspaceMode.IssueInvestigation &&
            m_WorkspaceMode != WorkspaceMode.MessageInvestigation;

        /// <summary>
        /// Gets whether the report view is currently investigating a message group.
        /// </summary>
        internal bool IsMessageInvestigationMode => m_WorkspaceMode == WorkspaceMode.MessageInvestigation;

        /// <inheritdoc />
        public Vector2 VerticalScrollViewPos { get; set; }

        /// <inheritdoc />
        public Vector2 LastVerticalScrollViewSize { get; set; }

        internal void SetWindow(SmartAuditorWindow window)
        {
            m_Window = window;
        }

        internal void SetRules(DiagnosticRules rules)
        {
            m_Rules = rules;
        }

        /// <inheritdoc />
        public void OnEnable()
        {
            LoadSettings();
        }

        /// <inheritdoc />
        public bool IsValid() => Report != null && Report.IsValid();

        /// <inheritdoc />
        public void LoadSettings()
        {
            if (m_Window == null)
                return;

            var state = m_Window.ReportViewWorkspaceState;
            if (state.workspaceMode == (int)WorkspaceMode.Overview)
                return;

            m_WorkspaceMode = (WorkspaceMode)state.workspaceMode;
            m_InvestigatedIssueId = state.investigatedIssueId;
            m_InvestigatedIssueSeverity = state.investigatedIssueSeverity;
            m_FocusImpact = state.focusImpact;
            m_InvestigatedMessageCategory = state.investigatedMessageCategory;
            m_InvestigatedMessageGroupKey = state.investigatedMessageGroupKey;
            m_ExpandedAffectedItemKey = state.expandedAffectedItemKey;
            m_AffectedItemsScrollPos = state.affectedItemsScrollPos;
        }

        /// <inheritdoc />
        public void SaveSettings()
        {
            if (m_Window == null)
                return;

            var state = m_Window.ReportViewWorkspaceState;
            state.workspaceMode = (int)m_WorkspaceMode;
            state.investigatedIssueId = m_InvestigatedIssueId;
            state.investigatedIssueSeverity = m_InvestigatedIssueSeverity;
            state.focusImpact = m_FocusImpact;
            state.investigatedMessageCategory = m_InvestigatedMessageCategory;
            state.investigatedMessageGroupKey = m_InvestigatedMessageGroupKey;
            state.expandedAffectedItemKey = m_ExpandedAffectedItemKey;
            state.affectedItemsScrollPos = m_AffectedItemsScrollPos;
        }

        /// <inheritdoc />
        public void MarkDirty()
        {
            m_Dirty = true;
            m_IssueInvestigationItemsCache = null;
            m_MessageInvestigationItemsCache = null;
        }

        /// <inheritdoc />
        public void MarkColumnWidthsDirty()
        {
        }

        /// <summary>Switches to Focus Overview mode for issues whose descriptor matches the given <see cref="Impact"/> flag.</summary>
        public void ShowFocusOverview(Impact focusImpact)
        {
            m_WorkspaceMode = WorkspaceMode.FocusArea;
            m_FocusImpact = focusImpact;
            m_FocusStatsNeedRefresh = true;
            m_InvestigatedIssueId = null;
            ClearMessageInvestigation();
            Repaint();
        }

        /// <summary>
        /// Switches to Issue Investigation mode for the given (descriptor ID, severity) pair.
        /// Severity is part of the key because hot-path promotion can split a single
        /// descriptor's instances across multiple severities — clicking the Major row should
        /// only surface the promoted instances, not the unpromoted Minor ones.
        /// </summary>
        public void ShowIssueInvestigation(string issueId, Severity severity)
        {
            m_WorkspaceMode = WorkspaceMode.IssueInvestigation;
            m_InvestigatedIssueId = issueId;
            m_InvestigatedIssueSeverity = severity;
            m_IssueInvestigationItemsCache = null;
            ClearMessageInvestigation();
            Repaint();
        }

        /// <summary>Switches to message investigation mode for the given compiler message group.</summary>
        public void ShowMessageInvestigation(AnalysisCategory category, string groupKey)
        {
            m_WorkspaceMode = WorkspaceMode.MessageInvestigation;
            m_InvestigatedIssueId = null;
            m_InvestigatedMessageCategory = category;
            m_InvestigatedMessageGroupKey = groupKey ?? string.Empty;
            m_MessageInvestigationItemsCache = null;
            Repaint();
        }

        /// <summary>Switches to Timeline mode, optionally comparing with a previous report.</summary>
        public void ShowTimeline(Report comparisonReport)
        {
            m_WorkspaceMode = WorkspaceMode.Timeline;
            m_TimelineComparisonReport = comparisonReport;
            Repaint();
        }

        /// <summary>Returns to the default overview mode.</summary>
        public void ShowOverview()
        {
            m_WorkspaceMode = WorkspaceMode.Overview;
            m_InvestigatedIssueId = null;
            m_FocusImpact = Impact.None;
            ClearMessageInvestigation();
            m_Window?.ClearReportViewWorkspaceState();
            Repaint();
        }

        /// <inheritdoc />
        public void AddIssues(IEnumerable<IReportEntry> allIssues)
        {
            // Stats are computed on demand from Report; we just need to invalidate.
            m_StatsNeedRefresh = true;
            MarkDirty();
        }

        /// <inheritdoc />
        public void Clear()
        {
            m_StatsCache.Clear();
            m_TopIssuesCache.Clear();
            m_MessageStatsMap.Clear();
            m_MessageTopGroupsMap.Clear();
            m_InsightCardCountCache.Clear();
            m_HasActionableIssuesCache = false;
            m_StatsNeedRefresh = true;
            MarkDirty();
        }

        #endregion

        #region Drawing — entry points

        /// <inheritdoc />
        public void DrawContent(bool showDetails = false)
        {
            using var _ = s_DrawContentMarker.Auto();

            // Reset the per-frame footer state -- DrawAffectedItem will set this if the
            // expanded row is rendered this frame. If it is not (filter / cap / mode change),
            // the footer naturally collapses.
            m_FrameExpandedAffectedItem = null;

            if (m_Dirty)
            {
                m_StatsNeedRefresh = true;
                m_FocusStatsNeedRefresh = true;
                m_Dirty = false;
            }

            if (Report == null)
                return;

            using (CenteredContentScope.Begin(
                m_WorkspaceMode == WorkspaceMode.IssueInvestigation ||
                m_WorkspaceMode == WorkspaceMode.MessageInvestigation
                    ? k_DetailContentWidth
                    : k_ReportContentWidth,
                expandHeight: true))
            {
                switch (m_WorkspaceMode)
                {
                    case WorkspaceMode.FocusArea:
                        DrawFocusOverview();
                        break;
                    case WorkspaceMode.IssueInvestigation:
                        DrawIssueInvestigation();
                        break;
                    case WorkspaceMode.MessageInvestigation:
                        DrawMessageInvestigation();
                        break;
                    case WorkspaceMode.Timeline:
                        DrawTimeline();
                        break;
                    default:
                        DrawOverview();
                        break;
                }
            }
        }

        /// <inheritdoc />
        public void DrawPinnedHeader()
        {
            if (m_WorkspaceMode != WorkspaceMode.IssueInvestigation &&
                m_WorkspaceMode != WorkspaceMode.MessageInvestigation)
                return;
            if (Report == null)
                return;

            using (CenteredContentScope.Begin(k_DetailContentWidth))
            {
                if (m_WorkspaceMode == WorkspaceMode.IssueInvestigation)
                    DrawIssueInvestigationPinnedHeader();
                else
                    DrawMessageInvestigationPinnedHeader();
            }
        }

        /// <inheritdoc />
        public void DrawPinnedFooter()
        {
            // The footer is only meaningful when an investigation is active. The selection
            // always resolves to a row (first item by default), so the footer is always
            // visible in investigation modes.
            if (m_FrameExpandedAffectedItem == null)
                return;

            var item = m_FrameExpandedAffectedItem;
            using (CenteredContentScope.Begin(k_DetailContentWidth))
            {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    var evidenceFields = BuildEvidenceFields(item);
                    var hasSnippet = CodeSnippetDrawer.CanDrawSnippet(item);
                    var evidenceCount = evidenceFields.Count + (hasSnippet ? 1 : 0);
                    UiSection.DrawHeaderWithCount("Evidence", evidenceCount, () =>
                    {
                        using (new EditorGUI.DisabledScope(evidenceFields.Count == 0))
                        {
                            if (GUILayout.Button(Utility.GetIcon(Utility.IconType.CopyToClipboard, "Copy evidence to clipboard"),
                                SharedStyles.TabHoverButton,
                                GUILayout.Width(k_CopyToClipboardButtonSize), GUILayout.Height(k_CopyToClipboardButtonSize)))
                            {
                                EditorInterop.CopyToClipboard(FormatEvidenceForClipboard(evidenceFields));
                            }
                        }
                    });
                    EditorGUILayout.Space(UiLayout.SpaceSmall);

                    // Source snippet for the selected row. Rendered in the footer so that
                    // changing the selection never resizes rows above or below the old one.
                    if (hasSnippet)
                    {
                        CodeSnippetDrawer.Draw(
                            item,
                            m_IssueDetailExpanded,
                            Repaint,
                            CodeSnippetDrawerSettings.Default,
                            showCollapseButton: false);
                        EditorGUILayout.Space(UiLayout.SpaceSmall);
                    }

                    DrawDiagnosticEvidence(evidenceFields);
                }
            }

            if (Event.current.type == EventType.Repaint)
            {
                var footerRect = GUILayoutUtility.GetLastRect();
                if (footerRect.height > 0f)
                    m_PinnedFooterHeight = footerRect.height;
            }

            EditorGUILayout.Space(UiLayout.SpaceMedium);
        }

        #endregion

        #region Overview — data

        void RefreshStats()
        {
            using var _ = s_RefreshStatsMarker.Auto();

            m_StatsCache.Clear();
            m_TopIssuesCache.Clear();
            m_MessageStatsMap.Clear();
            m_MessageTopGroupsMap.Clear();
            m_InsightCardCountCache.Clear();

            // One pass over the report items: bucket diagnostics by category and compute the
            // "any actionable issue?" flag on the way through.
            m_HasActionableIssuesCache = false;
            var diagBuckets = new Dictionary<AnalysisCategory, List<ReportItem>>(k_DiagnosticCategories.Length);
            for (var i = 0; i < k_DiagnosticCategories.Length; i++)
            {
                diagBuckets[k_DiagnosticCategories[i]] = new List<ReportItem>();
            }

            foreach (var item in Report.GetIssues())
            {
                if (!m_HasActionableIssuesCache && item.Severity != Severity.Suppressed)
                {
                    m_HasActionableIssuesCache = true;
                }
                if (diagBuckets.TryGetValue(item.Category, out var bucket))
                {
                    bucket.Add(item);
                }
            }

            foreach (var category in k_DiagnosticCategories)
            {
                var categoryItems = diagBuckets[category];

                var stats = new SeverityStats();
                foreach (var item in categoryItems)
                {
                    if (item.Severity == Severity.Suppressed || IsIgnored(item))
                        stats.Ignored++;
                    else if (item.Severity == Severity.Critical)
                        stats.Critical++;
                    else if (item.Severity == Severity.Major)
                        stats.Major++;
                    else if (item.Severity == Severity.Moderate)
                        stats.Moderate++;
                    else if (item.Severity == Severity.Minor)
                        stats.Minor++;
                }
                m_StatsCache[category] = stats;

                m_TopIssuesCache[category] = BuildDiagnosticRowGroups(categoryItems);
            }

            // Message sections — toolchain output, no descriptor, severity reduced from LogLevel.
            foreach (var ms in k_MessageSections)
            {
                var messages = Report.FindMessagesByCategory(ms.Category);

                var msgStats = default(CompilerMessageStats);
                for (var i = 0; i < messages.Count; i++)
                {
                    switch (messages[i].LogLevel)
                    {
                        case LogLevel.Error: msgStats.Errors++; break;
                        case LogLevel.Warning: msgStats.Warnings++; break;
                        default: msgStats.Infos++; break;
                    }
                }
                m_MessageStatsMap[ms.Category] = msgStats;

                m_MessageTopGroupsMap[ms.Category] = BuildActionableMessageRowGroups(messages);
            }

            // Cache insight card counts so DrawInsightCardsSection doesn't iterate every category per repaint.
            foreach (var category in k_InsightCategoryOrder)
            {
                if (!Report.HasCategory(category))
                {
                    continue;
                }
                m_InsightCardCountCache[category] = CountEnumerable(Report.FindByCategory(category));
            }
        }

        /// <summary>
        /// Group eligible items by (descriptor id, severity), materialise <see cref="RowGroup"/>s
        /// with <see cref="RowGroup.Count"/> / <see cref="RowGroup.HighestSeverity"/> precomputed,
        /// and sort: highest severity first (numerically lowest <see cref="Severity"/>), then
        /// largest count, then descriptor title.
        /// <para>
        /// Severity is part of the key because hot-path promotion in <c>CodeModule</c> can lift a
        /// subset of an Id's instances to a higher severity. Grouping by Id alone would render a
        /// single row at the highest severity but with a count covering all instances, which
        /// misleads the user about how many rows live at each severity. Splitting by severity
        /// makes the row color, the count, and the affected-items drilldown all consistent.
        /// </para>
        /// </summary>
        List<RowGroup> BuildDiagnosticRowGroups(List<ReportItem> categoryItems)
        {
            var byKey = new Dictionary<(string id, Severity sev), List<ReportItem>>();
            foreach (var item in categoryItems)
            {
                if (item.Severity == Severity.Suppressed)
                    continue;
                if (IsIgnored(item))
                    continue;
                if (!IsListMatch(item))
                    continue;
                var key = (item.Id, item.Severity);
                if (!byKey.TryGetValue(key, out var list))
                {
                    list = new List<ReportItem>();
                    byKey[key] = list;
                }
                list.Add(item);
            }

            var result = new List<RowGroup>(byKey.Count);
            foreach (var kvp in byKey)
            {
                var items = kvp.Value;
                result.Add(new RowGroup(
                    key: kvp.Key.id,
                    first: items[0],
                    count: items.Count,
                    highestSeverity: kvp.Key.sev,
                    items: items));
            }

            result.Sort((a, b) =>
            {
                // Severity is an enum where lower = more severe (Critical = 0); keep that order ascending.
                var sevCmp = a.HighestSeverity.CompareTo(b.HighestSeverity);
                if (sevCmp != 0)
                    return sevCmp;
                var countCmp = b.Count.CompareTo(a.Count);
                if (countCmp != 0)
                    return countCmp;
                return string.Compare(a.First.Descriptor.Title, b.First.Descriptor.Title, StringComparison.Ordinal);
            });
            return result;
        }

        /// <summary>
        /// Group native <see cref="Message"/>s by description. Filters to actionable
        /// (error or warning only) — info-level messages are excluded so the per-repaint
        /// <c>DrawMessageSection</c> code doesn't have to filter them out every frame.
        /// </summary>
        List<MessageRowGroup> BuildActionableMessageRowGroups(IReadOnlyList<Message> messages)
        {
            var byKey = new Dictionary<string, List<Message>>();
            foreach (var message in messages)
            {
                if (!IsMessageListMatch(message))
                    continue;
                var key = message.Description ?? string.Empty;
                if (!byKey.TryGetValue(key, out var list))
                {
                    list = new List<Message>();
                    byKey[key] = list;
                }
                list.Add(message);
            }

            var result = new List<MessageRowGroup>();
            foreach (var kvp in byKey)
            {
                var groupMessages = kvp.Value;
                var level = GetHighestMessageLevel(groupMessages);
                if (level == Severity.Info)
                    continue;
                result.Add(new MessageRowGroup(
                    key: kvp.Key,
                    first: groupMessages[0],
                    count: groupMessages.Count,
                    highestSeverity: level,
                    items: groupMessages));
            }

            result.Sort((a, b) =>
            {
                // Error before Warning. Severity enum: Error < Warning numerically.
                var levelCmp = a.HighestSeverity.CompareTo(b.HighestSeverity);
                if (levelCmp != 0)
                    return levelCmp;
                return b.Count.CompareTo(a.Count);
            });
            return result;
        }

        /// <summary>
        /// Reduce a group of <see cref="Message"/>s to the highest severity present.
        /// Reads <see cref="LogLevel"/> directly — messages don't carry a Descriptor,
        /// so there's no <see cref="Severity"/> field to consult.
        /// </summary>
        static Severity GetHighestMessageLevel(List<Message> messages)
        {
            var hasWarning = false;
            for (var i = 0; i < messages.Count; i++)
            {
                var level = messages[i].LogLevel;
                if (level == LogLevel.Error)
                    return Severity.Error;
                if (level == LogLevel.Warning)
                    hasWarning = true;
            }
            return hasWarning ? Severity.Warning : Severity.Info;
        }

        static int CountEnumerable(IEnumerable<ReportItem> source)
        {
            if (source == null)
                return 0;
            if (source is ICollection<ReportItem> col)
                return col.Count;
            var n = 0;
            foreach (var _ in source)
                n++;
            return n;
        }

        bool IsIgnored(ReportItem issue)
        {
            if (!issue.Descriptor.IsValid())
                return true;

            var rule = m_Rules.GetRule(issue.Id, issue.GetContext());
            if (rule == null)
                rule = m_Rules.GetRule(issue.Id);
            return rule != null && rule.Severity == Severity.Suppressed;
        }

        bool IsListMatch(ReportItem issue)
        {
            return m_Window == null || m_Window.IsMatch(issue);
        }

        /// <summary>
        /// Message-shaped counterpart to <see cref="IsListMatch(ReportItem)"/>. Routes the
        /// match through <see cref="IReportItemFilter.IsMatch(Message)"/> so the navigator
        /// text search and assembly selection see the message natively (Description,
        /// Location, Properties dictionary) instead of an adapted ReportItem.
        /// </summary>
        bool IsMessageListMatch(Message message)
        {
            return m_Window == null || m_Window.IsMatch(message);
        }

        string GetCategoryDisplayName(AnalysisCategory category)
        {
            switch (category)
            {
                case AnalysisCategory.Code: return "Code Issues";
                case AnalysisCategory.CodeCompilerMessage: return "Compiler Messages";
                case AnalysisCategory.ShaderCompilerMessage: return "Shader Compiler Messages";
                case AnalysisCategory.AssetImportMessage: return "Asset Import Messages";
                case AnalysisCategory.AssetIssue: return "Asset Issues";
                case AnalysisCategory.ProjectSetting: return "Project Settings Issues";
                case AnalysisCategory.Package: return "Installed Packages";
                case AnalysisCategory.GameObjectIssue: return "GameObject Issues";
                case AnalysisCategory.SceneSettings: return "Scene Settings Issues";
                case AnalysisCategory.Texture: return "Textures";
                case AnalysisCategory.Sprite: return "Sprites";
                case AnalysisCategory.SpriteAtlas: return "Sprite Atlases";
                case AnalysisCategory.Mesh: return "Meshes";
                case AnalysisCategory.AudioClip: return "Audio Clips";
                case AnalysisCategory.Shader: return "Shaders";
                case AnalysisCategory.ShaderVariant: return "Shader Variants";
                case AnalysisCategory.ComputeShaderVariant: return "Compute Shader Variants";
                case AnalysisCategory.Material: return "Materials";
                case AnalysisCategory.AnimatorController: return "Animator Controllers";
                case AnalysisCategory.AnimationClip: return "Animation Clips";
                case AnalysisCategory.Avatar: return "Avatars";
                case AnalysisCategory.AvatarMask: return "Avatar Masks";
                case AnalysisCategory.Assembly: return "Compiled Assemblies";
                case AnalysisCategory.PrecompiledAssembly: return "Precompiled Assemblies";
                case AnalysisCategory.Prefab: return "Prefabs";
                case AnalysisCategory.Scene: return "Scenes";
                case AnalysisCategory.BuildFile: return "Build Size";
                default: return category.ToString();
            }
        }

        #endregion

        #region Overview — drawing

        void DrawOverview()
        {
            using var _ = s_DrawOverviewMarker.Auto();

            if (m_StatsNeedRefresh)
            {
                RefreshStats();
                m_StatsNeedRefresh = false;
            }

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label(
                    new GUIContent(Contents.CompletenessTitle.text.ToUpperInvariant(), Contents.CompletenessTitle.tooltip),
                    SharedStyles.LargeLabel);
                EditorGUILayout.Space(4);
                DrawCompletenessBar();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(4);
            DrawFirstTriageSection();
            EditorGUILayout.Space(4);

            Span<bool> messageDrawn = stackalloc bool[k_MessageSections.Length];

            foreach (var category in k_DiagnosticCategories)
            {
                if (!Report.HasCategory(category))
                    continue;

                var categoryResult = Report.GetCategoryResult(category);
                if (categoryResult == AnalysisResult.Failure)
                {
                    DrawFailedDiagnosticSection(category);
                    EditorGUILayout.Space(4);
                }
                else
                {
                    DrawDiagnosticSection(category);
                    EditorGUILayout.Space(4);
                }

                for (var i = 0; i < k_MessageSections.Length; i++)
                {
                    var ms = k_MessageSections[i];
                    if (ms.AfterDiagnostic == category &&
                        Report.HasCategory(ms.Category))
                    {
                        DrawMessageSection(ms.Category, ms.Title);
                        EditorGUILayout.Space(4);
                        messageDrawn[i] = true;
                    }
                }
            }

            // AfterDiagnostic is a placement hint, not a precondition: a message
            // section can have items even when its anchor diagnostic is empty or
            // isn't part of the Overview's diagnostic flow (e.g. ShaderCompilerMessage
            // anchors to Shader, which surfaces as an insight card, not a diagnostic).
            for (var i = 0; i < k_MessageSections.Length; i++)
            {
                if (messageDrawn[i])
                    continue;

                var ms = k_MessageSections[i];
                if (Report.HasCategory(ms.Category))
                {
                    DrawMessageSection(ms.Category, ms.Title);
                    EditorGUILayout.Space(4);
                }
            }

            EditorGUILayout.Space(8);

            // Temporarily disabled insights since we don't have a table to show them
            // DrawInsightCardsSection();
            //
            // EditorGUILayout.Space(8);

            DrawSessionInfoSection();
        }

        void DrawFirstTriageSection()
        {
            var hasActionableIssues = m_HasActionableIssuesCache;

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                m_ShowFirstTriage = DrawSectionFoldout(m_ShowFirstTriage, Contents.FirstTriageTitle);
                if (!m_ShowFirstTriage)
                    return;

                EditorGUILayout.Space(2);

                if (!hasActionableIssues)
                {
                    EditorGUILayout.LabelField(Contents.FirstTriageNoIssuesHint, SharedStyles.TextArea);
                    return;
                }

                DrawFirstTriageStep(1, Contents.FirstTriageStepOne);
                EditorGUILayout.Space(4);
                DrawFirstTriageStep(2, Contents.FirstTriageStepTwo);
                EditorGUILayout.Space(4);
                DrawFirstTriageStep(3, Contents.FirstTriageStepThree);
                EditorGUILayout.Space(4);
                DrawFirstTriageStep(4, Contents.FirstTriageStepFour);
            }
        }

        static void DrawFirstTriageStep(int index, GUIContent stepContent)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{index}.", SharedStyles.BoldLabel, GUILayout.Width(18));
                EditorGUILayout.LabelField(stepContent, SharedStyles.TextArea);
            }
        }

        #endregion

        #region Completeness Bar

        void DrawCompletenessBar()
        {
            const float k_BarHeight = 18f;

            var report = Report;

            var analyzed = new bool[k_CompletenessAreas.Length];
            var failed = new bool[k_CompletenessAreas.Length];
            var analyzedCount = 0;
            var failedCount = 0;
            for (var i = 0; i < k_CompletenessAreas.Length; i++)
            {
                analyzed[i] = report != null && report.HasCategory(k_CompletenessAreas[i].Category);
                if (analyzed[i])
                {
                    var result = report.GetCategoryResult(k_CompletenessAreas[i].Category);
                    if (result == AnalysisResult.Failure)
                    {
                        analyzed[i] = false;
                        failed[i] = true;
                        failedCount++;
                    }
                    else
                    {
                        analyzedCount++;
                    }
                }
            }
            var pendingCount = k_CompletenessAreas.Length - analyzedCount - failedCount;

            var completeness = k_CompletenessAreas.Length > 0
                ? (float)analyzedCount / k_CompletenessAreas.Length
                : 0f;
            var completenessPercent = Mathf.RoundToInt(completeness * 100f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    // Bar segments follow fixed area order from k_CompletenessAreas.
                    var barRect = EditorGUILayout.GetControlRect(hasLabel: false, k_BarHeight, GUILayout.ExpandWidth(true));
                    var segmentWidth = barRect.width / k_CompletenessAreas.Length;
                    var segmentColors = new Color[k_CompletenessAreas.Length];

                    for (var i = 0; i < k_CompletenessAreas.Length; i++)
                    {
                        Color color;
                        if (analyzed[i])
                            color = k_CompletenessAnalyzedColor;
                        else if (failed[i])
                            color = k_CompletenessFailedColor;
                        else
                            color = CompletenessPendingColor;

                        segmentColors[i] = color;
                    }

                    SolidDraw.EqualSegments(barRect, segmentColors);

                    for (var i = 0; i < k_CompletenessAreas.Length; i++)
                    {
                        var state = analyzed[i] ? "Analyzed" : failed[i] ? "Failed" : "Pending";
                        var segRect = new Rect(barRect.x + i * segmentWidth, barRect.y, segmentWidth, barRect.height);
                        var areaName = k_CompletenessAreas[i].Name;
                        GUI.Label(segRect, new GUIContent(string.Empty, $"{areaName}: {state}"),
                            SolidDraw.TooltipHitAreaStyle);
                    }

                    // Per-segment area labels under the bar so users can identify segments at
                    // a glance without hovering for the tooltip. Aligned with bar segments by
                    // sharing barRect.x and segmentWidth.
                    var labelRect = EditorGUILayout.GetControlRect(hasLabel: false, EditorGUIUtility.singleLineHeight,
                        GUILayout.ExpandWidth(true));
                    for (var i = 0; i < k_CompletenessAreas.Length; i++)
                    {
                        var segLabelRect = new Rect(barRect.x + i * segmentWidth, labelRect.y,
                            segmentWidth, labelRect.height);
                        GUI.Label(segLabelRect, k_CompletenessAreas[i].Name,
                            EditorStyles.centeredGreyMiniLabel);
                    }

                    EditorGUILayout.Space(4);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawCompletenessLegendItem(k_CompletenessAnalyzedColor, $"Analyzed: {analyzedCount}");
                        GUILayout.Space(12);
                        DrawCompletenessLegendItem(CompletenessPendingColor, $"Pending: {pendingCount}");
                        GUILayout.Space(12);
                        DrawCompletenessLegendItem(k_CompletenessFailedColor, $"Failed: {failedCount}");
                        GUILayout.FlexibleSpace();
                    }

                    var hasUnanalyzed = analyzedCount < k_CompletenessAreas.Length;
                    if (hasUnanalyzed && m_Window != null)
                    {
                        // Lazily initialize: all pending areas selected by default
                        if (!m_PendingAreaSelectionInitialized)
                        {
                            for (var i = 0; i < k_CompletenessAreas.Length; i++)
                                m_PendingAreaSelected[i] = !analyzed[i];
                            m_PendingAreaSelectionInitialized = true;
                        }

                        // Ensure areas that became analyzed are deselected
                        for (var i = 0; i < k_CompletenessAreas.Length; i++)
                        {
                            if (analyzed[i])
                                m_PendingAreaSelected[i] = false;
                        }

                        var hasFailedAreas = failedCount > 0;

                        if (hasFailedAreas)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField(Utility.GetIcon(Utility.IconType.Warning), GUILayout.Width(19), GUILayout.Height(19));
                                EditorGUILayout.LabelField("Some areas failed to analyze. Fix the errors and re-analyze them.", SharedStyles.Label);
                            }
                            DrawFailedAreaLinks(failed);
                            EditorGUILayout.Space(2);
                        }

                        EditorGUILayout.LabelField("Select areas to include in the next analysis:", SharedStyles.Label);
                        EditorGUILayout.Space(2);

                        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                        {
                            for (var i = 0; i < k_CompletenessAreas.Length; i++)
                            {
                                if (analyzed[i])
                                    continue;

                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    // Color swatch mirrors the bar legend so pending vs. failed
                                    // is identifiable at a glance, not just from the trailing
                                    // " (failed)" suffix.
                                    var swatchColor = failed[i] ? k_CompletenessFailedColor : CompletenessPendingColor;
                                    SolidDraw.Swatch(swatchColor);
                                    GUILayout.Space(4);

                                    var areaName = k_CompletenessAreas[i].Name;
                                    if (failed[i])
                                        areaName += " (failed)";
                                    m_PendingAreaSelected[i] = EditorGUILayout.ToggleLeft(areaName, m_PendingAreaSelected[i]);
                                }
                            }
                        }

                        EditorGUILayout.Space(4);

                        // Collect selected categories and count pending / failed separately so
                        // the summary line can report each population honestly. (Previously a
                        // single "X selected of Y pending" line conflated the two and could
                        // produce numbers like "3 of 1" when failed areas were selected.)
                        var selectedCategories = new List<AnalysisCategory>();
                        var selectedPendingCount = 0;
                        var selectedFailedCount = 0;
                        for (var i = 0; i < k_CompletenessAreas.Length; i++)
                        {
                            if (analyzed[i] || !m_PendingAreaSelected[i])
                                continue;
                            selectedCategories.AddRange(k_CompletenessAreas[i].AnalysisCategories);
                            if (failed[i])
                                selectedFailedCount++;
                            else
                                selectedPendingCount++;
                        }
                        var totalSelected = selectedPendingCount + selectedFailedCount;

                        string summary;
                        if (failedCount == 0)
                            summary = $"{totalSelected} of {pendingCount} pending selected";
                        else if (pendingCount == 0)
                            summary = $"{totalSelected} of {failedCount} failed selected";
                        else
                            summary = $"{totalSelected} selected " +
                                $"({selectedPendingCount}/{pendingCount} pending, {selectedFailedCount}/{failedCount} failed)";

                        EditorGUILayout.LabelField(summary, SharedStyles.LabelDarkWithDynamicSize);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            using (new EditorGUI.DisabledScope(totalSelected == 0 || m_Window.IsReportReadOnly))
                            {
                                if (GUILayout.Button("Analyze Selected Areas", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                                {
                                    m_PendingAreaSelectionInitialized = false;
                                    m_Window.AnalyzeCategories(selectedCategories.Distinct().ToArray());
                                    GUIUtility.ExitGUI();
                                }
                            }
                            GUILayout.FlexibleSpace();
                        }
                    }
                    else if (!hasUnanalyzed)
                    {
                        EditorGUILayout.LabelField("All areas analyzed.", SharedStyles.Label);
                    }
                }

                GUILayout.Space(8);

                // Percentage label aligned with the bar
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(1);
                    EditorGUILayout.LabelField($"{completenessPercent}%", SharedStyles.BoldLabel, GUILayout.Width(40));
                }
            }
        }

        static void DrawCompletenessLegendItem(Color color, string text)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(false)))
            {
                SolidDraw.Swatch(color);
                GUILayout.Space(4);
                EditorGUILayout.LabelField(text, SharedStyles.Label, GUILayout.ExpandWidth(false));
            }
        }

        /// <summary>Renders the names of failed completeness areas on a single line.</summary>
        void DrawFailedAreaLinks(bool[] failed)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Failed areas:", SharedStyles.LabelDarkWithDynamicSize,
                    GUILayout.Width(90));

                var first = true;
                for (var i = 0; i < k_CompletenessAreas.Length; i++)
                {
                    if (!failed[i])
                        continue;

                    if (!first)
                    {
                        EditorGUILayout.LabelField(",", SharedStyles.LabelDarkWithDynamicSize,
                            GUILayout.Width(8));
                    }
                    first = false;

                    EditorGUILayout.LabelField(k_CompletenessAreas[i].Name, SharedStyles.LabelDarkWithDynamicSize,
                        GUILayout.ExpandWidth(false));
                }

                GUILayout.FlexibleSpace();
            }
        }

        #endregion

        #region Diagnostic Sections

        void DrawDiagnosticSection(AnalysisCategory category)
        {
            using var _ = s_DrawDiagnosticSectionMarker.Auto();

            if (!m_StatsCache.TryGetValue(category, out var stats))
                return;

            if (!m_SectionExpanded.ContainsKey(category))
                m_SectionExpanded[category] = true;

            if (!m_ShowAll.ContainsKey(category))
                m_ShowAll[category] = false;

            var displayName = GetCategoryDisplayName(category);
            var totalNonIgnored = stats.Total - stats.Ignored;

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                var titleContent = new GUIContent($"{displayName.ToUpperInvariant()} ({totalNonIgnored})");
                m_SectionExpanded[category] = CollapsibleSection.DrawHeader(
                    isExpanded: m_SectionExpanded[category],
                    titleContent: titleContent,
                    trailingLinkContent: null,
                    onTrailingLinkClicked: null,
                    onToggled: Repaint);

                if (!m_SectionExpanded[category])
                    return;

                // Issue list
                if (!m_TopIssuesCache.TryGetValue(category, out var issueGroups) || issueGroups.Count == 0)
                {
                    EditorGUILayout.LabelField("  No issues found.", SharedStyles.Label);
                    return;
                }

                EditorGUILayout.Space(4);

                var showAll = m_ShowAll[category];
                var displayCount = showAll ? issueGroups.Count : Math.Min(k_DefaultTopN, issueGroups.Count);

                for (var i = 0; i < displayCount; i++)
                {
                    DrawIssueRow(issueGroups[i], i);
                }

                if (issueGroups.Count > k_DefaultTopN)
                {
                    EditorGUILayout.Space(2);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(20);
                        var label = showAll
                            ? "Show top 10 only"
                            : $"Show all ({issueGroups.Count} issue types)";
                        if (UiSection.DrawLinkButton(label))
                        {
                            m_ShowAll[category] = !showAll;
                        }
                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        void DrawFailedDiagnosticSection(AnalysisCategory category)
        {
            var displayName = GetCategoryDisplayName(category);

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Label(Utility.GetIcon(Utility.IconType.Warning), GUIStyle.none, GUILayout.Width(19), GUILayout.Height(19));
                    GUILayout.Label(
                        new GUIContent($"{displayName.ToUpperInvariant()} — ANALYSIS FAILED"),
                        SharedStyles.LargeLabel);
                }

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField(
                        "Analysis could not be completed due to compilation errors. Fix the errors and re-analyze using the completeness bar above.",
                        SharedStyles.TextArea);
                    GUILayout.Space(20);
                }
            }
        }

        void DrawMessageSection(AnalysisCategory category, string title)
        {
            using var _ = s_DrawMessageSectionMarker.Auto();

            if (!m_MessageStatsMap.TryGetValue(category, out var stats))
                return;

            if (!m_MessageSectionExpanded.ContainsKey(category))
                m_MessageSectionExpanded[category] = true;

            if (!m_MessageShowAll.ContainsKey(category))
                m_MessageShowAll[category] = false;

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                var titleContent = new GUIContent($"{title.ToUpperInvariant()} ({stats.Total})");
                m_MessageSectionExpanded[category] = CollapsibleSection.DrawHeader(
                    isExpanded: m_MessageSectionExpanded[category],
                    titleContent: titleContent,
                    trailingLinkContent: null,
                    onTrailingLinkClicked: null,
                    onToggled: Repaint);

                if (!m_MessageSectionExpanded[category])
                    return;

                // m_MessageTopGroupsMap is already filtered to actionable rows (errors + warnings)
                // by RefreshStats — info-level groups are excluded at refresh time so the
                // per-repaint hot path doesn't have to scan them.
                if (!m_MessageTopGroupsMap.TryGetValue(category, out var actionable) || actionable.Count == 0)
                {
                    EditorGUILayout.LabelField("  No errors or warnings.", SharedStyles.Label);
                    return;
                }

                var showAll = m_MessageShowAll[category];
                var displayCount = showAll ? actionable.Count : Math.Min(k_DefaultTopN, actionable.Count);

                for (var i = 0; i < displayCount; i++)
                {
                    DrawMessageRow(actionable[i], i, category);
                }

                if (actionable.Count > k_DefaultTopN)
                {
                    EditorGUILayout.Space(2);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(20);
                        var label = showAll
                            ? "Show top 10 only"
                            : $"Show all ({actionable.Count} message types)";
                        if (UiSection.DrawLinkButton(label))
                            m_MessageShowAll[category] = !showAll;
                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        void DrawMessageRow(MessageRowGroup group, int rowIndex, AnalysisCategory category)
        {
            var first = group.First;
            var severity = group.HighestSeverity;
            var count = group.Count;
            var rowStyle = rowIndex % 2 == 0 ? SharedStyles.Row : SharedStyles.RowAlternate;
            var supportsDrilldown = IsCompilerMessageCategory(category);
            var filterText = string.IsNullOrEmpty(group.Key) ? (first.Description ?? string.Empty) : group.Key;

            // Show the group key as a code badge only when it differs from the description —
            // currently never, since BuildActionableMessageRowGroups keys every group by
            // description. The badge slot is reserved for a future per-category key
            // (e.g. error code for CodeCompilerMessage) that we haven't wired up yet.
            var key = group.Key;
            var showKeyBadge = !string.IsNullOrEmpty(key) && key != (first.Description ?? string.Empty);

            using (new EditorGUILayout.HorizontalScope(rowStyle))
            {
                GUILayout.Space(8);
                GUILayout.Label(Utility.GetSeverityLogLevelIcon(severity), GUIStyle.none,
                    GUILayout.Width(24), GUILayout.Height(19));

                if (showKeyBadge)
                    GUILayout.Label(key, SharedStyles.BoldLabel, GUILayout.Width(60));

                // Dashboard summary shows a single-line preview (first non-empty line, capped
                // to avoid very long strings pushing adjacent controls off-screen). The full
                // message is always available in the tooltip.
                var fullMessage = first.Description ?? string.Empty;
                var preview = Formatting.TruncatePreview(Formatting.GetFirstLine(fullMessage), k_MessagePreviewMaxChars);
                var tooltip = supportsDrilldown
                    ? (string.IsNullOrEmpty(fullMessage) ? "Open message details." : $"{fullMessage}\n\nOpen message details.")
                    : fullMessage;
                var messageContent = new GUIContent(preview, tooltip);
                var messageStyle = supportsDrilldown ? SharedStyles.LinkLabel : SharedStyles.Label;
                GUILayout.Label(messageContent, messageStyle, GUILayout.ExpandWidth(true));

                if (count > 1)
                    EditorGUILayout.LabelField($"({count})", SharedStyles.LabelDarkWithDynamicSize, GUILayout.Width(50));

                GUILayout.Space(4);
            }

            if (!supportsDrilldown)
                return;

            var rowRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.Repaint)
                EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rowRect.Contains(Event.current.mousePosition))
            {
                ShowMessageInvestigation(category, filterText);
                Event.current.Use();
                Repaint();
            }
        }

        static bool IsCompilerMessageCategory(AnalysisCategory category)
        {
            return category == AnalysisCategory.CodeCompilerMessage ||
                category == AnalysisCategory.ShaderCompilerMessage;
        }

        void DrawIssueRow(RowGroup issueGroup, int rowIndex)
        {
            var first = issueGroup.First;
            var descriptor = first.Descriptor;
            if (!descriptor.IsValid())
                return;

            var severity = issueGroup.HighestSeverity;
            var count = issueGroup.Count;
            var rowStyle = rowIndex % 2 == 0 ? SharedStyles.Row : SharedStyles.RowAlternate;

            using (new EditorGUILayout.HorizontalScope(rowStyle))
            {
                GUILayout.Space(8);
                SeverityPresentation.DrawRowSeverityBar(severity);
                GUILayout.Space(10);
                GUILayout.Label(GUIContentCache.ForDescriptorRich(descriptor), SharedStyles.RichLabel,
                    GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                if (count > 1)
                    EditorGUILayout.LabelField($"({count})", SharedStyles.LabelDarkWithDynamicSize, GUILayout.Width(50));

                GUILayout.Space(4);
            }

            var rowRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.Repaint)
                EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rowRect.Contains(Event.current.mousePosition))
            {
                ShowIssueInvestigation(first.Id, issueGroup.HighestSeverity);
                Event.current.Use();
                Repaint();
            }
        }

        #endregion

        #region Insight Cards

        void DrawInsightCardsSection()
        {
            using var _ = s_DrawInsightCardsMarker.Auto();

            m_ShowInsightCards = DrawSectionFoldout(m_ShowInsightCards, Contents.InsightCardsTitle);
            if (!m_ShowInsightCards)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12);

                using (new EditorGUILayout.VerticalScope())
                {
                    var rowIndex = 0;
                    foreach (var category in k_InsightCategoryOrder)
                    {
                        // Cache key presence == "category was analyzed" (mirrors the prior HasCategory check).
                        if (!m_InsightCardCountCache.TryGetValue(category, out var count))
                        {
                            continue;
                        }

                        // Show a row whenever this category was analyzed (including 0 items).
                        DrawInsightCard(category, count, rowIndex++);
                    }

                    if (rowIndex == 0)
                    {
                        EditorGUILayout.LabelField("No insight data available.", SharedStyles.Label);
                    }
                }

                GUILayout.Space(20);
            }
        }

        void DrawInsightCard(AnalysisCategory category, int count, int rowIndex)
        {
            var rowStyle = rowIndex % 2 == 0 ? SharedStyles.Row : SharedStyles.RowAlternate;

            using (new EditorGUILayout.HorizontalScope(rowStyle))
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField(
                    GetCategoryDisplayName(category),
                    SharedStyles.BoldLabel, GUILayout.Width(180));
                var countText = count == 0 ? "No entries" : $"{count} items";
                var countGuiStyle = count == 0 ? SharedStyles.LabelDarkWithDynamicSize : SharedStyles.Label;
                EditorGUILayout.LabelField(countText, countGuiStyle, GUILayout.Width(100));

                GUILayout.FlexibleSpace();
                GUILayout.Space(4);
            }
        }

        #endregion

        #region Session Info

        void DrawSessionInfoSection()
        {
            m_ShowSessionInfo = DrawSectionFoldout(m_ShowSessionInfo, Contents.SessionInfoTitle);
            if (!m_ShowSessionInfo)
                return;

            var sessionInfo = Report?.SessionInfo;
            if (sessionInfo == null)
                return;

            var analyzerPaths = sessionInfo.RoslynAnalyzerDllPaths;
            var analyzerCount = analyzerPaths?.Length ?? 0;
            var analyzerValue = analyzerCount == 0 ? "None" : analyzerCount.ToString();
            var analyzerTooltip = analyzerCount == 0
                ? "No Roslyn analyzer / source-generator DLLs are tagged with the 'RoslynAnalyzer' asset label."
                : string.Join("\n", analyzerPaths);

            var keyValues = new[]
            {
                new KeyValuePair<string, GUIContent>("Host Platform", new GUIContent(sessionInfo.HostPlatform)),
                new KeyValuePair<string, GUIContent>("Company Name", new GUIContent(sessionInfo.CompanyName)),
                new KeyValuePair<string, GUIContent>("Project Name", new GUIContent(sessionInfo.ProductName)),
                new KeyValuePair<string, GUIContent>("Project Version", new GUIContent(sessionInfo.ProjectVersion)),
                new KeyValuePair<string, GUIContent>("VCS Revision", new GUIContent(
                    !string.IsNullOrEmpty(sessionInfo.VersionControlRevision)
                        ? $"{VersionControlUtil.GetShortRevision(sessionInfo.VersionControlRevision)} ({sessionInfo.VersionControlRevision})"
                        : "Unknown")),
                new KeyValuePair<string, GUIContent>("Unity Version", new GUIContent(sessionInfo.UnityVersion)),
                new KeyValuePair<string, GUIContent>($"{SmartAuditor.DisplayName} Version", new GUIContent(sessionInfo.SmartAuditorVersion)),
                new KeyValuePair<string, GUIContent>("Roslyn Analyzers", new GUIContent(analyzerValue, analyzerTooltip))
            };

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12);

                using (new EditorGUILayout.VerticalScope())
                {
                    for (var i = 0; i < keyValues.Length; i++)
                    {
                        var pair = keyValues[i];
                        using (new EditorGUILayout.HorizontalScope(i % 2 == 0 ? SharedStyles.Row : SharedStyles.RowAlternate))
                        {
                            EditorGUILayout.LabelField($"{pair.Key}:", SharedStyles.Label, GUILayout.Width(160));
                            EditorGUILayout.LabelField(pair.Value, SharedStyles.Label, GUILayout.ExpandWidth(true));
                        }
                    }
                }

                GUILayout.Space(20);
            }
        }

        #endregion

        #region Focus Overview (Mode A)

        void DrawFocusOverview()
        {
            using var _ = s_DrawFocusOverviewMarker.Auto();

            UiSection.DrawBackLinkRow("\u2190 Overview", () => ShowOverview());

            EditorGUILayout.Space(UiLayout.SpaceSmall);

            if (m_FocusStatsNeedRefresh)
            {
                RefreshFocusAreaStats();
                m_FocusStatsNeedRefresh = false;
            }

            FocusOverviewPresenter.Draw(
                m_FocusCriticalCount,
                m_FocusMajorCount,
                m_FocusModerateCount,
                m_FocusMinorCount,
                m_FocusHighImpact,
                m_FocusNeedsAttention,
                m_FocusInformational,
                DrawFocusIssueCard);
        }

        void RefreshFocusAreaStats()
        {
            using var _ = s_RefreshFocusAreaStatsMarker.Auto();

            m_FocusHighImpact.Clear();
            m_FocusNeedsAttention.Clear();
            m_FocusInformational.Clear();
            m_FocusCriticalCount = 0;
            m_FocusMajorCount = 0;
            m_FocusModerateCount = 0;
            m_FocusMinorCount = 0;

            if (m_FocusImpact == Impact.None)
            {
                return;
            }

            // Single pass: severity counts use the broader filter (matches DrawFocusOverview's prior behavior),
            // tier groups use the narrower filter (excludes ignored / non-list-matched).
            var passingNarrow = new List<ReportItem>();
            foreach (var item in Report.GetIssues())
            {
                if (!item.Descriptor.IsValid())
                {
                    continue;
                }
                if (!item.Descriptor.MatchesAnyImpact(m_FocusImpact))
                {
                    continue;
                }

                switch (item.Severity)
                {
                    case Severity.Critical:
                    case Severity.Error:
                        m_FocusCriticalCount++;
                        break;
                    case Severity.Major:
                        m_FocusMajorCount++;
                        break;
                    case Severity.Moderate:
                        m_FocusModerateCount++;
                        break;
                    case Severity.Minor:
                        m_FocusMinorCount++;
                        break;
                }

                if (IsIgnored(item))
                {
                    continue;
                }
                if (!IsListMatch(item))
                {
                    continue;
                }
                passingNarrow.Add(item);
            }

            var grouped = BuildDiagnosticRowGroups(passingNarrow);
            FocusOverviewPresenter.BucketByTier(
                grouped,
                group => group.HighestSeverity,
                m_FocusHighImpact,
                m_FocusNeedsAttention,
                m_FocusInformational);
        }

        void DrawFocusIssueCard(RowGroup group, int rowIndex)
        {
            var first = group.First;
            var descriptor = first.Descriptor;
            if (!descriptor.IsValid())
                return;

            var severity = group.HighestSeverity;
            var count = group.Count;
            var rowStyle = rowIndex % 2 == 0 ? SharedStyles.Row : SharedStyles.RowAlternate;

            using (new EditorGUILayout.HorizontalScope(rowStyle))
            {
                GUILayout.Space(8);
                SeverityPresentation.DrawRowSeverityBar(severity);
                GUILayout.Space(10);
                GUILayout.Label(GUIContentCache.ForDescriptorRich(descriptor), SharedStyles.RichLabel,
                    GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                if (count > 1)
                    EditorGUILayout.LabelField($"({count})", SharedStyles.LabelDarkWithDynamicSize, GUILayout.Width(50));

                GUILayout.Space(4);
            }

            var rowRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.Repaint)
                EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rowRect.Contains(Event.current.mousePosition))
            {
                ShowIssueInvestigation(first.Id, group.HighestSeverity);
                Event.current.Use();
                Repaint();
            }
        }

        #endregion

        #region Issue Investigation (Mode B)

        void DrawIssueInvestigation()
        {
            using var _ = s_DrawIssueInvestigationMarker.Auto();

            // Back link, finding panel and action buttons are rendered by
            // DrawIssueInvestigationPinnedHeader so they stay visible while the user
            // scrolls through the affected items list. If the pinned header decided to
            // exit the investigation (no items, or back-link click), the workspace mode
            // is already changed and we have nothing to draw here.
            if (m_WorkspaceMode != WorkspaceMode.IssueInvestigation)
                return;

            var allItems = GetIssueInvestigationItems();
            if (allItems.Count == 0)
                return;

            EditorGUILayout.Space(UiLayout.SpaceSmall);

            DrawReanalyzeBanner();

            // Affected items: each row in this list can be selected to surface the code
            // snippet plus its Evidence block (Category, Severity, structured properties)
            // in the pinned footer. At most one row is selected at a time. The list owns
            // its own virtualized scroll view and fills the remaining body height -- no
            // trailing FlexibleSpace, since that would compete with ExpandHeight.
            DrawAffectedItemsSection(allItems);
        }

        void DrawIssueInvestigationPinnedHeader()
        {
            var allItems = GetIssueInvestigationItems();
            if (allItems.Count == 0)
            {
                ShowOverview();
                return;
            }

            var first = allItems[0];
            var descriptor = first.Descriptor;

            if (DrawInvestigationBackLink())
                return;

            EditorGUILayout.Space(UiLayout.SpaceSmall);

            // Finding panel
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                var effectiveSeverity = GetHighestSeverity(allItems);
                var options = new IssueDetailsBodyOptions
                {
                    Item = first,
                    PresentationSeverity = effectiveSeverity,
                    ShowLocationPath = false,
                    AllowItemDescriptionFallback = false,
                    DescriptionMaxHeight = 0f,
                    RecommendationMaxHeight = 0f,
                    ShowNoRecommendationPlaceholder = false,
                    MetadataBottomSpacing = 4f,
                    DescriptionTopSpacing = 6f,
                    RecommendationTopSpacing = 8f,
                    NoRecommendationTopSpacing = 0f
                };

                IssueDetailsBody.Draw(
                    options,
                    () => DrawIssueInvestigationMetadataRow(descriptor, effectiveSeverity),
                    () => GUILayout.Label(IssueDetailsUi.GetTitle(first), SharedStyles.LargeLabel));

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (!string.IsNullOrEmpty(descriptor.DocumentationUrl))
                    {
                        if (GUILayout.Button("Documentation", EditorStyles.miniButton, GUILayout.Width(100)))
                            Application.OpenURL(descriptor.DocumentationUrl);
                    }

                    // Ask Assistant placeholder — wired in Phase 5
                    /*
                    using (new EditorGUI.DisabledScope(disabled: true))
                    {
                        GUILayout.Button("Ask Assistant", EditorStyles.miniButton, GUILayout.Width(100));
                    }
                    */

                    GUILayout.FlexibleSpace();
                }
            }
        }

        void DrawMessageInvestigation()
        {
            using var _ = s_DrawMessageInvestigationMarker.Auto();

            // Header (back link, log-level summary, group label) is rendered by
            // DrawMessageInvestigationPinnedHeader so it stays visible while the user
            // scrolls through the affected items list.
            if (m_WorkspaceMode != WorkspaceMode.MessageInvestigation)
                return;
            if (!IsCompilerMessageCategory(m_InvestigatedMessageCategory))
                return;

            var allItems = GetMessageInvestigationItems();
            if (allItems.Count == 0)
                return;

            EditorGUILayout.Space(UiLayout.SpaceSmall);

            DrawAffectedItemsSection(allItems);
        }

        void DrawMessageInvestigationPinnedHeader()
        {
            if (!IsCompilerMessageCategory(m_InvestigatedMessageCategory))
            {
                ShowOverview();
                return;
            }

            var allItems = GetMessageInvestigationItems();
            if (allItems.Count == 0)
            {
                ShowOverview();
                return;
            }

            var first = allItems[0];
            var groupLogLevel = GetHighestMessageLogLevel(allItems);
            var displayName = GetCategoryDisplayName(m_InvestigatedMessageCategory);
            var groupLabel = string.IsNullOrEmpty(m_InvestigatedMessageGroupKey)
                ? (first.Description ?? "Message")
                : m_InvestigatedMessageGroupKey;

            if (DrawInvestigationBackLink())
                return;

            EditorGUILayout.Space(UiLayout.SpaceSmall);

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(Utility.GetLogLevelIcon(groupLogLevel),
                        GUILayout.Width(24), GUILayout.Height(19), GUILayout.ExpandWidth(false));
                    EditorGUILayout.LabelField(groupLogLevel.ToString(),
                        SharedStyles.BoldLabel, GUILayout.ExpandWidth(false));
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField(displayName, SharedStyles.LabelDarkWithDynamicSize, GUILayout.ExpandWidth(false));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"({allItems.Count})", SharedStyles.LabelDarkWithDynamicSize, GUILayout.Width(60));
                }

                EditorGUILayout.Space(4);
                GUILayout.Label(groupLabel, SharedStyles.WrappedLargeLabel);

                EditorGUILayout.Space(4);
            }
        }

        bool DrawInvestigationBackLink()
        {
            if (m_FocusImpact != Impact.None)
            {
                var handled = false;
                UiSection.DrawBackLinkRow("\u2190 " + m_FocusImpact, () =>
                {
                    m_WorkspaceMode = WorkspaceMode.FocusArea;
                    Repaint();
                    handled = true;
                });
                return handled;
            }

            var wentToOverview = false;
            UiSection.DrawBackLinkRow("\u2190 Overview", () =>
            {
                ShowOverview();
                wentToOverview = true;
            });
            return wentToOverview;
        }

        void DrawAffectedItemsSection(IReadOnlyList<IReportEntry> allItems)
        {
            using var _ = s_DrawAffectedItemsMarker.Auto();

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                UiSection.DrawHeaderWithCount("Affected Items", allItems.Count);
                EditorGUILayout.Space(UiLayout.SpaceSmall);

                if (allItems.Count == 0)
                    return;

                // Handle arrow-key navigation before resolving the selected row so the new
                // key is in place when ResolveFrameExpandedItem runs and we know which row
                // to keep in view.
                HandleAffectedItemsKeyboard(allItems);

                // Resolve the expanded row's item up front, before deciding which rows to
                // render. This way the pinned footer keeps the snippet + Evidence visible
                // even when the expanded row scrolls outside the virtualized viewport.
                ResolveFrameExpandedItem(allItems);

                var rowHeight = AffectedItemRowHeight;
                var totalHeight = allItems.Count * rowHeight;
                var footerReserve = m_FrameExpandedAffectedItem != null
                    ? m_PinnedFooterHeight + UiLayout.SpaceSmall
                    : 0f;

                var maxScrollViewHeight = k_AffectedItemsMaxScrollHeight;
                if (m_Window != null && m_AffectedItemsListTopY > 0f)
                {
                    var availableHeight = m_Window.position.height - m_AffectedItemsListTopY - footerReserve - UiLayout.SpaceSmall;
                    maxScrollViewHeight = Mathf.Clamp(
                        availableHeight,
                        k_AffectedItemsMinScrollHeight,
                        k_AffectedItemsMaxScrollHeight);
                }

                // Size to content for short lists, cap at k_AffectedItemsMaxScrollHeight for
                // longer ones (so the box doesn't expand to fill all remaining vertical space
                // when there are only a handful of items, and doesn't push the pinned footer
                // off screen when there are many).
                var listTopRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true));
                var scrollViewHeight = Mathf.Min(totalHeight, maxScrollViewHeight);
                m_AffectedItemsScrollPos = EditorGUILayout.BeginScrollView(
                    m_AffectedItemsScrollPos,
                    GUILayout.Height(scrollViewHeight),
                    GUILayout.ExpandWidth(true));

                var contentRect = GUILayoutUtility.GetRect(0, totalHeight, GUILayout.ExpandWidth(true));

                // Compute the visible row window (one-row slack on either side prevents
                // the topmost / bottommost row from popping in mid-scroll). The visible
                // height is captured at the end of the previous Repaint, so it converges
                // within one frame on resize.
                var scrollY = m_AffectedItemsScrollPos.y;
                var visibleHeight = m_AffectedItemsScrollHeight;
                var firstVisible = Mathf.Max(0, Mathf.FloorToInt(scrollY / rowHeight) - 1);
                var lastVisible = Mathf.Min(allItems.Count - 1,
                    Mathf.CeilToInt((scrollY + visibleHeight) / rowHeight) + 1);

                for (var i = firstVisible; i <= lastVisible; i++)
                {
                    var rowRect = new Rect(
                        contentRect.x,
                        contentRect.y + i * rowHeight,
                        contentRect.width,
                        rowHeight);
                    DrawAffectedItemAtRect(rowRect, allItems[i], i);
                }

                EditorGUILayout.EndScrollView();

                if (Event.current.type == EventType.Repaint)
                {
                    if (listTopRect.y > 0f)
                        m_AffectedItemsListTopY = listTopRect.y;

                    var scrollRect = GUILayoutUtility.GetLastRect();
                    if (scrollRect.height > 0)
                        m_AffectedItemsScrollHeight = scrollRect.height;
                }
            }
        }

        void HandleAffectedItemsKeyboard(IReadOnlyList<IReportEntry> allItems)
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown)
                return;
            if (EditorGUIUtility.editingTextField)
                return;
            if (evt.keyCode != KeyCode.UpArrow && evt.keyCode != KeyCode.DownArrow)
                return;

            var selectedIndex = -1;
            for (var i = 0; i < allItems.Count; i++)
            {
                if (IsAffectedItemSelected(allItems[i], i))
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0)
                selectedIndex = 0;
            else if (evt.keyCode == KeyCode.UpArrow)
                selectedIndex = Mathf.Max(0, selectedIndex - 1);
            else
                selectedIndex = Mathf.Min(allItems.Count - 1, selectedIndex + 1);

            m_ExpandedAffectedItemKey = GetAffectedItemSelectionKey(allItems[selectedIndex]);
            SaveSettings();

            // Scroll so the newly selected row is visible inside the virtualized viewport.
            var rowHeight = AffectedItemRowHeight;
            var rowTop = selectedIndex * rowHeight;
            var rowBottom = rowTop + rowHeight;
            if (rowTop < m_AffectedItemsScrollPos.y)
                m_AffectedItemsScrollPos.y = rowTop;
            else if (rowBottom > m_AffectedItemsScrollPos.y + m_AffectedItemsScrollHeight)
                m_AffectedItemsScrollPos.y = rowBottom - m_AffectedItemsScrollHeight;

            evt.Use();
            Repaint();
        }

        void ResolveFrameExpandedItem(IReadOnlyList<IReportEntry> allItems)
        {
            if (!string.IsNullOrEmpty(m_ExpandedAffectedItemKey))
            {
                for (var i = 0; i < allItems.Count; i++)
                {
                    if (IsAffectedItemSelected(allItems[i], i))
                    {
                        m_FrameExpandedAffectedItem = allItems[i];
                        var normalizedKey = GetAffectedItemSelectionKey(allItems[i]);
                        if (m_ExpandedAffectedItemKey != normalizedKey)
                        {
                            m_ExpandedAffectedItemKey = normalizedKey;
                            SaveSettings();
                        }
                        return;
                    }
                }
            }

            // No stored key, or the stored key no longer matches any item (e.g. on first
            // entry into an investigation, or after a filter change). Default to the first
            // row so the footer always has something to show.
            m_ExpandedAffectedItemKey = GetAffectedItemSelectionKey(allItems[0]);
            m_FrameExpandedAffectedItem = allItems[0];
            SaveSettings();
        }

        void DrawIssueInvestigationMetadataRow(Descriptor descriptor, Severity effectiveSeverity)
        {
            IssueMetadataRowDrawer.Draw(new IssueMetadataRowOptions
            {
                SeverityLabel = new GUIContent(effectiveSeverity.ToString()),
                SeverityLabelStyle = SharedStyles.BoldLabel,
                DescriptorId = descriptor.Id,
                HideDescriptorIdWhenEmpty = false,
                DrawAfterSeverity = () =>
                {
                    GUILayout.Space(8);
                    DrawImpactTags(descriptor.Impact);
                }
            });
        }

        static Severity GetHighestSeverity(List<ReportItem> items)
        {
            var highest = Severity.Minor;
            foreach (var item in items)
            {
                if (item.Severity == Severity.Critical || item.Severity == Severity.Error)
                    return Severity.Critical;
                if (item.Severity < highest)
                    highest = item.Severity;
            }
            return highest;
        }

        static LogLevel GetHighestMessageLogLevel(IReadOnlyList<IReportEntry> items)
        {
            var hasWarning = false;
            foreach (var item in items)
            {
                if (item.LogLevel == LogLevel.Error)
                    return LogLevel.Error;
                if (item.LogLevel == LogLevel.Warning)
                    hasWarning = true;
            }

            return hasWarning ? LogLevel.Warning : LogLevel.Info;
        }

        static void DrawImpactTags(Impact impact)
        {
            if (impact == Impact.None)
                return;

            var first = true;
            foreach (var flag in ImpactCatalog.IndividualFlags)
            {
                if ((impact & flag) == 0)
                    continue;

                if (!first)
                    GUILayout.Label("\u00B7", SharedStyles.LabelDarkWithDynamicSize, GUILayout.ExpandWidth(false));
                GUILayout.Label(flag.ToString(), SharedStyles.Label, GUILayout.ExpandWidth(false));
                first = false;
            }
        }

        string GetAffectedItemIdentityKey(IReportEntry entry)
        {
            if (entry == null)
                return string.Empty;

            var fingerprint = entry.Fingerprint;
            if (!string.IsNullOrEmpty(fingerprint))
                return fingerprint;

            var location = entry.Location;
            var pathKey = location?.Path ?? "<none>";
            var lineKey = location?.Line?.ToString() ?? "<none>";
            var endLineKey = location?.EndLine?.ToString() ?? "<none>";
            var descriptionKey = entry.Description ?? string.Empty;
            return $"{pathKey}:{lineKey}:{endLineKey}:{descriptionKey}";
        }

        string GetAffectedItemSelectionKey(IReportEntry entry)
        {
            var issueKey = m_InvestigatedIssueId ?? "<none>";
            var messageCategoryKey = m_InvestigatedMessageCategory.ToString();
            var messageGroupKey = m_InvestigatedMessageGroupKey ?? "<none>";
            return $"affected-item:{issueKey}:{messageCategoryKey}:{messageGroupKey}:{GetAffectedItemIdentityKey(entry)}";
        }

        bool IsAffectedItemSelected(IReportEntry entry, int rowIndex)
        {
            if (string.IsNullOrEmpty(m_ExpandedAffectedItemKey))
                return false;

            if (m_ExpandedAffectedItemKey == GetAffectedItemSelectionKey(entry))
                return true;

            // Legacy keys written before stable identity keys (row-index suffix or
            // fingerprint populated only after autosave restore).
            return LegacyExpandedKeyMatchesEntry(m_ExpandedAffectedItemKey, entry, rowIndex);
        }

        static bool LegacyExpandedKeyMatchesEntry(string storedKey, IReportEntry entry, int rowIndex)
        {
            if (entry == null || string.IsNullOrEmpty(storedKey))
                return false;

            var fingerprint = entry.Fingerprint;
            if (!string.IsNullOrEmpty(fingerprint) &&
                storedKey.EndsWith(":" + fingerprint, StringComparison.Ordinal))
            {
                return true;
            }

            var location = entry.Location;
            if (location == null || !location.IsValid)
                return false;

            var pathKey = location.Path ?? "<none>";
            var lineKey = location.Line?.ToString() ?? "<none>";
            var endLineKey = location.EndLine?.ToString() ?? "<none>";
            var locationPrefix = $"{pathKey}:{lineKey}:{endLineKey}:";
            if (storedKey.Contains(locationPrefix, StringComparison.Ordinal))
                return true;

            var legacyWithRowIndex = $"{pathKey}:{lineKey}:{endLineKey}:{rowIndex}";
            return storedKey.EndsWith(legacyWithRowIndex, StringComparison.Ordinal);
        }

        void DrawAffectedItemAtRect(Rect rect, IReportEntry entry, int rowIndex)
        {
            var location = entry.Location;
            var hasLocation = location != null && location.IsValid;

            var relativePath = location?.Path ?? string.Empty;
            var description = !string.IsNullOrEmpty(entry.Description) ? entry.Description : relativePath;

            // Inset the row content so it does not run flush against the box edges.
            var contentRect = new Rect(rect.x + 8f, rect.y, Mathf.Max(0f, rect.width - 16f), rect.height);

            // Carve out the action button slots before computing the link/toggle layout so
            // the existing rects are not affected when the buttons are not shown. The
            // re-analyze button sits at the far right; copy-for-agent to its left; the
            // rule-actions (suppress/change-severity) button to the left of that.
            const float k_RowActionButtonWidth = 20f;
            const float k_RowActionButtonGap = 4f;
            var canReanalyze = CanReanalyzeItem(entry);
            var reanalyzeButtonRect = Rect.zero;
            if (canReanalyze)
            {
                reanalyzeButtonRect = new Rect(
                    contentRect.xMax - k_RowActionButtonWidth,
                    contentRect.y,
                    k_RowActionButtonWidth,
                    contentRect.height);
                contentRect = new Rect(
                    contentRect.x, contentRect.y,
                    contentRect.width - k_RowActionButtonWidth - k_RowActionButtonGap,
                    contentRect.height);
            }

            var canCopyForAgent = entry is ReportItem || entry is Message;
            var copyForAgentButtonRect = Rect.zero;
            if (canCopyForAgent)
            {
                copyForAgentButtonRect = new Rect(
                    contentRect.xMax - k_RowActionButtonWidth,
                    contentRect.y,
                    k_RowActionButtonWidth,
                    contentRect.height);
                contentRect = new Rect(
                    contentRect.x, contentRect.y,
                    contentRect.width - k_RowActionButtonWidth - k_RowActionButtonGap,
                    contentRect.height);
            }

            var ruleActionItem = entry as ReportItem;
            var canRuleActions = ruleActionItem != null
                && ruleActionItem.Descriptor.IsValid()
                && m_Window != null && !m_Window.IsReportReadOnly;
            var ruleActionsButtonRect = Rect.zero;
            if (canRuleActions)
            {
                ruleActionsButtonRect = new Rect(
                    contentRect.xMax - k_RowActionButtonWidth,
                    contentRect.y,
                    k_RowActionButtonWidth,
                    contentRect.height);
                contentRect = new Rect(
                    contentRect.x, contentRect.y,
                    contentRect.width - k_RowActionButtonWidth - k_RowActionButtonGap,
                    contentRect.height);
            }

            // Reserve the right-hand link column first, then give the remainder (minus a gap)
            // to the message. The link is sized to its content, capped at 45% of the row so
            // it cannot crowd the message on narrow windows. A larger gap than before (12px vs
            // the previous 6px) keeps the message from butting up against the filename.
            GUIContent linkContent = null;
            var hasLocationLink = false;
            if (hasLocation)
            {
                var linkText = Formatting.FormatPath(location.Filename, location.Line, location.EndLine);
                if (string.IsNullOrEmpty(linkText))
                    linkText = relativePath;
                linkContent = new GUIContent(linkText, relativePath);
                hasLocationLink = true;
            }

            const float k_MessageLinkGap = 12f;
            var linkRect = Rect.zero;
            var toggleRect = contentRect;
            if (hasLocationLink)
            {
                var linkWidth = Mathf.Min(
                    contentRect.width * 0.45f,
                    SharedStyles.LinkLabelMuted.CalcSize(linkContent).x + 8f);
                linkWidth = Mathf.Max(80f, linkWidth);
                linkRect = new Rect(contentRect.xMax - linkWidth, contentRect.y, linkWidth, contentRect.height);
                toggleRect = new Rect(contentRect.x, contentRect.y,
                    Mathf.Max(0f, linkRect.x - contentRect.x - k_MessageLinkGap), contentRect.height);
            }

            var currentEvent = Event.current;
            var isSelected = IsAffectedItemSelected(entry, rowIndex);
            var isRowHovered = rect.Contains(currentEvent.mousePosition);

            if (isSelected)
                SolidDraw.ThemeSelectionOverlay(rect);
            else if (isRowHovered)
                SolidDraw.ThemeHoverOverlay(rect);

            var descriptionContent = GUIContentCache.TrimToWidth(description, string.Empty, SharedStyles.Label, toggleRect.width);
            EditorGUI.LabelField(toggleRect, descriptionContent, SharedStyles.Label);
            EditorGUIUtility.AddCursorRect(toggleRect, MouseCursor.Link);
            if (GUI.Button(toggleRect, GUIContent.none, GUIStyle.none))
            {
                m_ExpandedAffectedItemKey = GetAffectedItemSelectionKey(entry);
                SaveSettings();
                Repaint();
            }

            if (hasLocationLink && GUI.Button(linkRect, linkContent, SharedStyles.LinkLabelMuted))
                IssueActionRegistry.OpenIssue(entry.Location, entry.Category);

            if (canCopyForAgent)
            {
                var isMessage = entry is Message;
                var copyTooltip = isMessage
                    ? "Copy message as AI agent prompt"
                    : "Copy issue as AI agent prompt";
                var copyContent = Utility.GetIcon(Utility.IconType.CopyToClipboard, copyTooltip);
                if (GUI.Button(copyForAgentButtonRect, copyContent, SharedStyles.TabHoverButton))
                {
                    var prompt = isMessage
                        ? AgentPromptFormatter.FormatMessage((Message)entry)
                        : AgentPromptFormatter.FormatIssue((ReportItem)entry);
                    EditorInterop.CopyToClipboard(prompt);
                    m_Window?.ShowNotification(new GUIContent(isMessage
                        ? "Copied message for AI agent"
                        : "Copied issue for AI agent"));
                }
            }

            if (canReanalyze)
            {
                var reanalyzeContent = Utility.GetIcon(Utility.IconType.Refresh, "Re-analyze this item (run analysis again on this asset)");
                if (GUI.Button(reanalyzeButtonRect, reanalyzeContent, SharedStyles.TabHoverButton))
                    ReanalyzeAffectedItem((ReportItem)entry);
            }

            if (canRuleActions)
            {
                var ruleContent = Utility.GetIcon(Utility.IconType.Settings, "Suppress or adjust severity of this diagnostic");
                if (GUI.Button(ruleActionsButtonRect, ruleContent, SharedStyles.TabHoverButton))
                    ShowRuleActionsMenu(ruleActionItem, ruleActionsButtonRect);
            }
        }

        void ShowRuleActionsMenu(ReportItem issue, Rect anchorRect)
        {
            var menu = new GenericMenu();

            // Suppress this occurrence (keyed by descriptor Id + item context).
            menu.AddItem(new GUIContent("Suppress this occurrence/Project Settings"), on: false, () =>
            {
                IssueRuleActions.Apply(m_Rules, issue, RuleAction.SetOccurrence, Severity.Suppressed, RuleStore.ProjectSettings);
                OnRuleApplied(issue);
            });
            menu.AddItem(new GUIContent("Suppress this occurrence/User Preferences"), on: false, () =>
            {
                IssueRuleActions.Apply(m_Rules, issue, RuleAction.SetOccurrence, Severity.Suppressed, RuleStore.UserPreferences);
                OnRuleApplied(issue);
            });

            // Suppress all occurrences of this diagnostic (descriptor-wide, empty pattern).
            menu.AddItem(new GUIContent($"Suppress all '{issue.Id}'/Project Settings"), on: false, () =>
            {
                IssueRuleActions.Apply(m_Rules, issue, RuleAction.SetDescriptor, Severity.Suppressed, RuleStore.ProjectSettings);
                OnRuleApplied(issue);
            });
            menu.AddItem(new GUIContent($"Suppress all '{issue.Id}'/User Preferences"), on: false, () =>
            {
                IssueRuleActions.Apply(m_Rules, issue, RuleAction.SetDescriptor, Severity.Suppressed, RuleStore.UserPreferences);
                OnRuleApplied(issue);
            });

            // Suppress in folder — only available when the item has a real asset path.
            if (IssueRuleActions.HasAssetPath(issue))
            {
                foreach (var folderScope in IssueRuleActions.GetFolderScopeOptions(issue))
                {
                    var capturedScope = folderScope;
                    menu.AddItem(new GUIContent($"Suppress in folder/{capturedScope.Label}/Project Settings"), on: false, () =>
                    {
                        IssueRuleActions.Apply(m_Rules, issue, RuleAction.SetFolder, Severity.Suppressed, RuleStore.ProjectSettings, capturedScope.Pattern);
                        OnRuleApplied(issue);
                    });
                    menu.AddItem(new GUIContent($"Suppress in folder/{capturedScope.Label}/User Preferences"), on: false, () =>
                    {
                        IssueRuleActions.Apply(m_Rules, issue, RuleAction.SetFolder, Severity.Suppressed, RuleStore.UserPreferences, capturedScope.Pattern);
                        OnRuleApplied(issue);
                    });
                }
            }

            menu.AddSeparator(string.Empty);

            // Change severity for this occurrence.
            foreach (var sev in s_ChangeSeverityValues)
            {
                var capturedSev = sev;
                var sevName = sev.ToString();
                menu.AddItem(new GUIContent($"Change severity/{sevName}/Project Settings"), on: false, () =>
                {
                    IssueRuleActions.Apply(m_Rules, issue, RuleAction.SetOccurrence, capturedSev, RuleStore.ProjectSettings);
                    OnRuleApplied(issue);
                });
                menu.AddItem(new GUIContent($"Change severity/{sevName}/User Preferences"), on: false, () =>
                {
                    IssueRuleActions.Apply(m_Rules, issue, RuleAction.SetOccurrence, capturedSev, RuleStore.UserPreferences);
                    OnRuleApplied(issue);
                });
            }

            menu.DropDown(anchorRect);
        }

        // Severity values offered in the "Change severity" submenu, ordered from most to least severe.
        // Aliases (Warning == Moderate, Info == Minor) and non-configurable values are excluded.
        static readonly Severity[] s_ChangeSeverityValues =
        {
            Severity.Critical,
            Severity.Error,
            Severity.Major,
            Severity.Moderate,
            Severity.Minor,
        };

        void OnRuleApplied(ReportItem issue)
        {
            MarkDirty();
            Repaint();
        }

        // Returns true when the entry is an asset-backed issue that can be re-analyzed
        // by running analysis on its single asset file. Limited to AssetIssue category
        // because that is the only diagnostic category whose items map 1:1 to an
        // importable asset file. Code, ProjectSetting, GameObjectIssue and SceneSettings
        // require different scoping and are excluded here.
        static bool CanReanalyzeItem(IReportEntry entry)
        {
            if (entry is not ReportItem item)
                return false;
            if (item.Category != AnalysisCategory.AssetIssue)
                return false;
            var path = item.Location?.Path;
            if (string.IsNullOrEmpty(path))
                return false;
            return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
        }

        void ReanalyzeAffectedItem(ReportItem item)
        {
            var assetPath = item.Location.Path;
            var targetFingerprint = item.Fingerprint;
            var report = Report;
            var platform = report.SessionInfo?.Platform ?? BuildTarget.NoTarget;

            var fresh = SmartAuditor.Analyze(new AnalysisOptions
            {
                Categories = new[] { item.Category },
                AssetPathFilter = path => path == assetPath,
                Platform = platform
            });

            IReadOnlyList<ReportItem> freshIssues = fresh?.Issues ?? Array.Empty<ReportItem>();
            report.ReplaceAssetIssues(assetPath, item.Category, freshIssues);

            var resolved = true;
            foreach (var i in freshIssues)
            {
                if (i.Fingerprint == targetFingerprint)
                {
                    resolved = false;
                    break;
                }
            }

            m_IssueInvestigationItemsCache = null;
            m_StatsNeedRefresh = true;

            var title = item.Descriptor?.Title ?? item.Description ?? "Issue";
            m_ReanalyzeBannerIsResolved = resolved;
            m_ReanalyzeBanner = resolved
                ? $"Resolved: '{title}' is no longer reported."
                : $"Still reported: '{title}' was found again after re-analysis.";
            m_ReanalyzeBannerUntil = EditorApplication.timeSinceStartup + 4.0;

            Repaint();
        }

        void DrawReanalyzeBanner()
        {
            if (string.IsNullOrEmpty(m_ReanalyzeBanner))
                return;
            if (EditorApplication.timeSinceStartup > m_ReanalyzeBannerUntil)
            {
                m_ReanalyzeBanner = null;
                return;
            }

            var messageType = m_ReanalyzeBannerIsResolved ? MessageType.Info : MessageType.Warning;
            EditorGUILayout.HelpBox(m_ReanalyzeBanner, messageType);
            EditorGUILayout.Space(UiLayout.SpaceXSmall);

            // Keep requesting repaints so the banner disappears promptly when it expires.
            Repaint();
        }

        static bool HasEntryEvidence(IReportEntry entry)
        {
            if (entry is ReportItem item)
                return item.PropertyCount > 0 || (item.Evidence != null && item.Evidence.Count > 0);
            if (entry is Message message)
                return message.Properties != null && message.Properties.Count > 0;
            return false;
        }

        // Single source of truth for the evidence rows so the rendered panel and the
        // clipboard copy can never drift apart. Returns empty when there is nothing beyond
        // the descriptor itself to show, which the caller renders as "No additional properties."
        List<(string Label, string Value, string Tooltip)> BuildEvidenceFields(IReportEntry entry)
        {
            if (entry is ReportItem item)
                return BuildDiagnosticEvidenceFields(item);
            if (entry is Message message)
                return BuildMessageEvidenceFields(message);
            return new List<(string Label, string Value, string Tooltip)>();
        }

        List<(string Label, string Value, string Tooltip)> BuildDiagnosticEvidenceFields(ReportItem item)
        {
            var fields = new List<(string Label, string Value, string Tooltip)>();
            var hasProperties = item.PropertyCount > 0;
            var hasEvidence = item.Evidence != null && item.Evidence.Count > 0;
            if (!hasProperties && !hasEvidence)
            {
                return fields;
            }

            if (hasProperties)
            {
                // Iterate in Ordinal key order so the evidence panel and clipboard copy stay
                // stable across runs (dictionary iteration order is not guaranteed).
                var schema = ResolveSchemaForCategory(item.Category);
                foreach (var kv in item.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (string.IsNullOrEmpty(kv.Value))
                        continue;
                    if (IsHiddenProperty(schema, kv.Key))
                        continue;
                    var value = PropertyFormatting.FormatPropertyText(schema, kv.Key, kv.Value);
                    fields.Add((GetPropertyLabel(schema, kv.Key), value, GetPropertyTooltip(schema, kv.Key)));
                }
            }

            if (hasEvidence)
            {
                // Sorted by key so the layout is stable across runs (the dictionary's iteration
                // order is not guaranteed after a JSON round-trip).
                foreach (var key in item.Evidence.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    fields.Add((key, EvidenceFormatter.Format(item.Evidence[key], boolAsYesNo: true), string.Empty));
                }
            }

            return fields;
        }

        List<(string Label, string Value, string Tooltip)> BuildMessageEvidenceFields(Message message)
        {
            var fields = new List<(string Label, string Value, string Tooltip)>();
            if (message.Properties == null || message.Properties.Count == 0)
                return fields;

            var schema = ResolveSchemaForCategory(message.Category);
            foreach (var kv in message.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(kv.Value))
                    continue;
                if (IsHiddenProperty(schema, kv.Key))
                    continue;
                var value = PropertyFormatting.FormatPropertyText(schema, kv.Key, kv.Value);
                fields.Add((GetPropertyLabel(schema, kv.Key), value, GetPropertyTooltip(schema, kv.Key)));
            }

            return fields;
        }

        void DrawDiagnosticEvidence(List<(string Label, string Value, string Tooltip)> fields)
        {
            if (fields.Count == 0)
            {
                EditorGUILayout.LabelField("No additional properties.", SharedStyles.LabelDarkWithDynamicSize);
                return;
            }

            const float k_EvidenceLabelWidth = 160f;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                using (new EditorGUILayout.VerticalScope())
                {
                    foreach (var field in fields)
                    {
                        DrawEvidenceField(field.Label, field.Value, field.Tooltip, k_EvidenceLabelWidth);
                    }
                }
            }
        }

        static string FormatEvidenceForClipboard(List<(string Label, string Value, string Tooltip)> fields)
        {
            var lines = new string[fields.Count];
            for (var i = 0; i < fields.Count; i++)
            {
                lines[i] = $"{fields[i].Label}: {fields[i].Value}";
            }
            return string.Join("\n", lines);
        }

        static void DrawEvidenceField(string label, string value, string tooltip, float labelWidth)
        {
            // Use GUILayout.Label (not EditorGUILayout.LabelField) for the value so the row
            // grows to the content height. Array evidence is newline-joined, and a single-line
            // LabelField rect would let the extra lines overflow onto the next field.
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent($"{label}:", tooltip), SharedStyles.BoldLabel, GUILayout.Width(labelWidth));
                GUILayout.Label(value, SharedStyles.LabelWrapped, GUILayout.ExpandWidth(true));
            }
        }

        // Display label for an evidence row: the schema column label for the dict key.
        // Falls back to the raw key when no match is found.
        static string GetPropertyLabel(InsightSchema schema, string canonicalKey)
        {
            var column = schema?.GetColumn(canonicalKey);
            if (column != null && !string.IsNullOrEmpty(column.DisplayName))
                return column.DisplayName;

            return canonicalKey;
        }

        static string GetPropertyTooltip(InsightSchema schema, string canonicalKey)
        {
            var column = schema?.GetColumn(canonicalKey);
            return column?.LongName ?? string.Empty;
        }

        static bool IsHiddenProperty(InsightSchema schema, string canonicalKey)
        {
            var column = schema?.GetColumn(canonicalKey);
            return column != null && column.IsHidden;
        }

        InsightSchema ResolveSchemaForCategory(AnalysisCategory category)
        {
            var schema = CategorySchemaRegistry.GetSchema(category);
            if (schema != null)
                return schema;

            if (Report?.InsightTables != null && Report.InsightTables.TryGetValue(category, out var table))
                return table.Schema;

            return null;
        }

        #endregion

        #region Timeline (Mode C)

        void DrawTimeline()
        {
            UiSection.DrawBackLinkRow("\u2190 Overview", () => ShowOverview());

            EditorGUILayout.Space(UiLayout.SpaceSmall);

            // Current health score stays global and does not change with navigator text search.
            var currentIssues = Report.GetIssues();
            var healthScore = HealthScore.FromIssues(currentIssues);

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("CURRENT HEALTH SCORE", SharedStyles.LargeLabel);
                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"{healthScore.Score} / 100 — {healthScore.GetHealthStateLabel()}",
                        SharedStyles.BoldLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"Critical: {healthScore.CriticalCount}  " +
                        $"Major: {healthScore.MajorCount}  " +
                        $"Moderate: {healthScore.ModerateCount}  " +
                        $"Minor: {healthScore.MinorCount}",
                        SharedStyles.Label);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(healthScore.ExplainCalculation(), SharedStyles.LabelDarkWithDynamicSize);
            }

            EditorGUILayout.Space(4);

            if (m_TimelineComparisonReport != null)
            {
                var prevIssues = m_TimelineComparisonReport.GetIssues();
                var prevHealth = HealthScore.FromIssues(prevIssues);
                var diff = Report.CompareWith(m_TimelineComparisonReport);
                var addedForUi = diff.AddedIssues.Where(IsListMatch).ToList();
                var removedForUi = diff.RemovedIssues.Where(IsListMatch).ToList();

                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("COMPARISON WITH PREVIOUS SCAN", SharedStyles.LargeLabel);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(
                        $"Previous analysis: {FormatAnalysisDateTimeForDisplay(m_TimelineComparisonReport.SessionInfo)}",
                        SharedStyles.LabelDarkWithDynamicSize);

                    EditorGUILayout.Space(4);

                    var scoreDelta = healthScore.Score - prevHealth.Score;
                    var deltaSign = scoreDelta >= 0 ? "+" : string.Empty;
                    EditorGUILayout.LabelField(
                        $"Score change: {deltaSign}{scoreDelta}  (previous: {prevHealth.Score} → current: {healthScore.Score})",
                        SharedStyles.BoldLabel);

                    EditorGUILayout.Space(4);

                    if (diff.HasDifferences)
                    {
                        if (addedForUi.Count == 0 && removedForUi.Count == 0)
                        {
                            EditorGUILayout.LabelField(
                                "No differences match the current filters.",
                                SharedStyles.Label);
                        }
                        else
                        {
                            if (addedForUi.Count > 0)
                            {
                                EditorGUILayout.LabelField($"New issues: +{addedForUi.Count}", SharedStyles.Label);
                                foreach (var g in addedForUi.GroupBy(i => i.Category))
                                {
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        GUILayout.Space(20);
                                        EditorGUILayout.LabelField($"• {g.Count()} in {g.Key}", SharedStyles.LabelDarkWithDynamicSize);
                                    }
                                }
                            }

                            EditorGUILayout.Space(2);

                            if (removedForUi.Count > 0)
                            {
                                EditorGUILayout.LabelField($"Resolved issues: -{removedForUi.Count}", SharedStyles.Label);
                                foreach (var g in removedForUi.GroupBy(i => i.Category))
                                {
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        GUILayout.Space(20);
                                        EditorGUILayout.LabelField($"• {g.Count()} in {g.Key}", SharedStyles.LabelDarkWithDynamicSize);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No differences found compared to the previous scan.", SharedStyles.Label);
                    }
                }
            }
            else
            {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    EditorGUILayout.LabelField("No previous scan available for comparison.", SharedStyles.Label);
                    EditorGUILayout.LabelField(
                        "Run another analysis to see how your project health changes over time.",
                        SharedStyles.LabelDarkWithDynamicSize);
                }
            }
        }

        #endregion

        #region Helpers

        static string FormatAnalysisDateTimeForDisplay(SessionInfo sessionInfo)
        {
            var raw = sessionInfo?.DateTime;
            if (string.IsNullOrWhiteSpace(raw))
                return "Unknown";

            try
            {
                return Json.DeserializeDateTime(raw).ToString("g");
            }
            catch
            {
                return raw;
            }
        }

        bool DrawSectionFoldout(bool expanded, GUIContent content)
        {
            Rect headerRect;
            Rect titleRect;
            using (new GUILayout.HorizontalScope())
            {
                var icon = expanded ? Utility.FoldoutExpandedIcon : Utility.FoldoutFoldedIcon;
                GUILayout.Label(icon, GUIStyle.none, GUILayout.Width(19), GUILayout.Height(19));
                var labelContent = new GUIContent((content.text ?? string.Empty).ToUpperInvariant(), content.tooltip);
                titleRect = EditorGUILayout.GetControlRect(
                    hasLabel: false,
                    height: SharedStyles.LargeLabel.fixedHeight,
                    GUILayout.ExpandWidth(true),
                    GUILayout.MinWidth(40f));
                GUI.Label(titleRect, labelContent, SharedStyles.LargeLabel);
            }
            headerRect = GUILayoutUtility.GetLastRect();

            if (Event.current.type == EventType.MouseDown)
            {
                if (headerRect.Contains(Event.current.mousePosition) || titleRect.Contains(Event.current.mousePosition))
                {
                    expanded = !expanded;
                    Event.current.Use();
                    Repaint();
                }
            }

            return expanded;
        }

        void Repaint()
        {
            m_Window?.Repaint();
        }

        void ClearMessageInvestigation()
        {
            m_InvestigatedMessageCategory = default;
            m_InvestigatedMessageGroupKey = null;
            m_MessageInvestigationItemsCache = null;
        }

        List<ReportItem> GetIssueInvestigationItems()
        {
            if (m_IssueInvestigationItemsCache != null)
            {
                return m_IssueInvestigationItemsCache;
            }

            var list = new List<ReportItem>();
            foreach (var item in Report.GetIssues())
            {
                if (item.Id != m_InvestigatedIssueId)
                {
                    continue;
                }
                // Match the (Id, Severity) compound key used by BuildDiagnosticRowGroups —
                // hot-path promotion can produce same-Id items at different severities and
                // the user's click identified one specific severity bucket.
                if (item.Severity != m_InvestigatedIssueSeverity)
                {
                    continue;
                }
                if (!IsListMatch(item))
                {
                    continue;
                }
                list.Add(item);
            }
            list.Sort(CompareByLocation);
            m_IssueInvestigationItemsCache = list;
            return list;
        }

        List<IReportEntry> GetMessageInvestigationItems()
        {
            if (m_MessageInvestigationItemsCache != null)
            {
                return m_MessageInvestigationItemsCache;
            }

            var list = new List<IReportEntry>();
            foreach (var message in Report.MessagesList)
            {
                if (message.Category != m_InvestigatedMessageCategory)
                    continue;
                if (!IsMessageListMatch(message))
                    continue;
                if (!string.Equals(message.Description ?? string.Empty, m_InvestigatedMessageGroupKey ?? string.Empty, StringComparison.Ordinal))
                    continue;
                list.Add(message);
            }
            list.Sort(CompareByLocation);
            m_MessageInvestigationItemsCache = list;
            return list;
        }

        static int CompareByLocation(IReportEntry a, IReportEntry b)
        {
            // Sort affected items so all occurrences in the same file appear together (in
            // line / column order). Items without a path drop to the end so they don't
            // disrupt the file groupings.
            var pathA = a?.Location?.Path ?? string.Empty;
            var pathB = b?.Location?.Path ?? string.Empty;
            var hasPathA = !string.IsNullOrEmpty(pathA);
            var hasPathB = !string.IsNullOrEmpty(pathB);
            if (hasPathA != hasPathB)
                return hasPathA ? -1 : 1;

            var pathCmp = string.Compare(pathA, pathB, StringComparison.OrdinalIgnoreCase);
            if (pathCmp != 0)
                return pathCmp;

            var lineA = a?.Location?.Line ?? 0;
            var lineB = b?.Location?.Line ?? 0;
            if (lineA != lineB)
                return lineA.CompareTo(lineB);

            var colA = a?.Location?.Column ?? 0;
            var colB = b?.Location?.Column ?? 0;
            return colA.CompareTo(colB);
        }

        #endregion

        static class Contents
        {
            public static readonly GUIContent CompletenessTitle = new GUIContent(
                "Report Completeness",
                "Which areas of the project were analyzed successfully, and which are incomplete or skipped.");
            public static readonly GUIContent InsightCardsTitle = new GUIContent(
                "Insights",
                "Per-area shortcuts to view the full Smart Auditor listing for that category (diagnostics with severities, compiler or import messages, asset insight tables, or other views depending on the category).");
            public static readonly GUIContent SessionInfoTitle = new GUIContent(
                "Session Information",
                "When and how this report was produced.");
            public static readonly GUIContent FirstTriageTitle = new GUIContent(
                "Recommended First Pass",
                "A quick sequence for reviewing analysis results the first time.");
            public static readonly GUIContent FirstTriageNoIssuesHint = new GUIContent(
                "No actionable issues were found in the selected scope. You can broaden the scope, switch platform, or save this report as a baseline."
            );
            public static readonly GUIContent FirstTriageStepOne = new GUIContent(
                "Prioritize Critical and Major findings to reduce the highest risk first."
            );
            public static readonly GUIContent FirstTriageStepTwo = new GUIContent(
                "Use Focus Areas and filters to narrow the report to the subsystem you are currently improving."
            );
            public static readonly GUIContent FirstTriageStepThree = new GUIContent(
                "Open issue details to review why each issue was reported and what recommendation is suggested."
            );
            public static readonly GUIContent FirstTriageStepFour = new GUIContent(
                "Apply quick fixes where available, then ignore only confirmed false positives."
            );
        }
    }
}
