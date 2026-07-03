// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using SmartAuditor.Editor.Core.Services;
using SmartAuditor.Editor.UI.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SmartAuditor.Editor.UI
{
    internal sealed class SmartConsoleWindow : EditorWindow
    {
        const float k_MinTopPanelHeight = 180f;
        const float k_MinBottomPanelHeight = 150f;
        const float k_SplitterHeight = 6f;
        const float k_RowHeight = 42f;

        [SerializeField]
        string m_SearchText = string.Empty;
        [SerializeField]
        LogLevelMask m_LogLevelMask = LogLevelMask.All;
        [SerializeField]
        bool m_CollapseDuplicates = true;
        [SerializeField]
        bool m_ShowTimestamps = true;
        [SerializeField]
        bool m_PauseOnError;
        [SerializeField]
        float m_ListPanelRatio = 0.56f;
        [SerializeField]
        Vector2 m_ListScroll;
        [SerializeField]
        Vector2 m_DetailScroll;
        [SerializeField]
        int m_SelectedMessageId = -1;
        bool m_IsDraggingSplitter;

        readonly List<LiveConsoleMessage> m_FilteredItems = new List<LiveConsoleMessage>();
        readonly Dictionary<LogLevel, int> m_LevelCounts = new Dictionary<LogLevel, int>
        {
            { LogLevel.Error, 0 },
            { LogLevel.Warning, 0 },
            { LogLevel.Info, 0 }
        };

        // Temporarily hidden while the Smart Console feature is refined before exposure.
        // [MenuItem("Window/Smart Auditor/Smart Console")]
        // static void ShowWindow()
        // {
        //     Open();
        // }

        internal static SmartConsoleWindow Open()
        {
            var window = GetWindow<SmartConsoleWindow>();
            window.titleContent = new GUIContent("Smart Console");
            window.minSize = new Vector2(700f, 420f);
            return window;
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Smart Console");
            minSize = new Vector2(700f, 420f);
            LiveConsoleLogService.Instance.PauseOnError = m_PauseOnError;
            RebuildFilteredItems();
        }

        void OnGUI()
        {
            SyncWithLogService();
            HandleArrowKeyNavigation();
            DrawToolbar();
            EditorGUILayout.Space(UiLayout.SpaceSmall);

            var availableHeight = Mathf.Max(1f, position.height - 70f);
            var minTopByBottom = Mathf.Max(k_MinTopPanelHeight, availableHeight - k_MinBottomPanelHeight - k_SplitterHeight);
            var minTop = Mathf.Min(k_MinTopPanelHeight, minTopByBottom);
            var maxTop = Mathf.Max(minTop, availableHeight - k_MinBottomPanelHeight - k_SplitterHeight);
            var desiredTop = availableHeight * Mathf.Clamp01(m_ListPanelRatio);
            var topPanelHeight = Mathf.Clamp(desiredTop, minTop, maxTop);

            using (new EditorGUILayout.VerticalScope())
            {
                DrawMessageList(topPanelHeight);
                DrawSplitter(topPanelHeight, availableHeight, minTop, maxTop);
                DrawMessageDetails();
            }
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawLogLevelToggle(LogLevel.Error, "Errors");
                DrawLogLevelToggle(LogLevel.Warning, "Warnings");
                DrawLogLevelToggle(LogLevel.Info, "Info");
                GUILayout.Space(UiLayout.SpaceSmall);

                var collapse = GUILayout.Toggle(m_CollapseDuplicates, "Collapse", EditorStyles.toolbarButton);
                if (collapse != m_CollapseDuplicates)
                {
                    m_CollapseDuplicates = collapse;
                    RebuildFilteredItems();
                }

                var pauseOnError = GUILayout.Toggle(m_PauseOnError, "Pause On Error", EditorStyles.toolbarButton);
                if (pauseOnError != m_PauseOnError)
                {
                    m_PauseOnError = pauseOnError;
                    LiveConsoleLogService.Instance.PauseOnError = m_PauseOnError;
                }

                var showTimestamps = GUILayout.Toggle(m_ShowTimestamps, "Timestamps", EditorStyles.toolbarButton);
                if (showTimestamps != m_ShowTimestamps)
                    m_ShowTimestamps = showTimestamps;

                GUILayout.Space(UiLayout.SpaceMedium);
                GUILayout.FlexibleSpace();
                if (ToolbarSearchField.DrawInline(ref m_SearchText, useDelayed: false, maxFieldWidth: 260f))
                    RebuildFilteredItems();

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                {
                    LiveConsoleLogService.Instance.Clear();
                    RebuildFilteredItems();
                }
            }
        }

        void DrawLogLevelToggle(LogLevel level, string label)
        {
            var isSelected = LogLevelFilter.HasLevel(m_LogLevelMask, level);
            var count = CountLevel(level);
            var icon = Utility.GetLogLevelIcon(level);
            var content = new GUIContent($"{label} ({count})", icon.image, $"Toggle {label.ToLowerInvariant()}.");
            var nextSelected = GUILayout.Toggle(isSelected, content, EditorStyles.toolbarButton);
            if (nextSelected == isSelected)
                return;

            var nextMask = LogLevelFilter.SetLevel(m_LogLevelMask, level, nextSelected);
            if (nextMask == LogLevelMask.None)
                return;

            m_LogLevelMask = nextMask;
            RebuildFilteredItems();
        }

        int CountLevel(LogLevel level)
        {
            return m_LevelCounts.TryGetValue(level, out var count) ? count : 0;
        }

        void DrawMessageList(float panelHeight)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Height(panelHeight), GUILayout.ExpandWidth(true)))
            {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    UiSection.DrawHeaderWithCount("Messages", m_FilteredItems.Count);
                }

                using (new EditorGUILayout.VerticalScope(SharedStyles.RowBackground, GUILayout.ExpandHeight(true)))
                {
                    m_ListScroll = EditorGUILayout.BeginScrollView(m_ListScroll);
                    if (m_FilteredItems.Count == 0)
                    {
                        EmptyStateDrawer.DrawInfoHelpBox("No messages match the current filters.");
                        EditorGUILayout.EndScrollView();
                        return;
                    }

                    for (var index = 0; index < m_FilteredItems.Count; index++)
                        DrawListRow(index, m_FilteredItems[index]);

                    EditorGUILayout.EndScrollView();
                }
            }
        }

        void DrawListRow(int index, LiveConsoleMessage item)
        {
            var rowStyle = item.Id == m_SelectedMessageId
                ? SharedStyles.SelectedRowStyle
                : (index % 2 == 0 ? SharedStyles.RowBackground : SharedStyles.RowBackgroundAlternate);

            var rowRect = EditorGUILayout.BeginVertical(rowStyle, GUILayout.Height(k_RowHeight), GUILayout.ExpandWidth(true));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4f);
                EditorGUILayout.LabelField(Utility.GetLogLevelIcon(item.LogLevel), GUILayout.Width(20f), GUILayout.Height(18f));

                using (new EditorGUILayout.VerticalScope())
                {
                    var text = string.IsNullOrEmpty(item.Message) ? "(No message)" : item.Message;
                    EditorGUILayout.LabelField(text, SharedStyles.Label, GUILayout.ExpandWidth(true));

                    var secondary = BuildSecondaryLine(item);
                    if (!string.IsNullOrEmpty(secondary))
                        EditorGUILayout.LabelField(secondary, SharedStyles.LabelDarkWithDynamicSize);
                }

                if (item.Count > 1)
                    EditorGUILayout.LabelField($"x{item.Count}", SharedStyles.BoldLabel, GUILayout.Width(46f));
            }

            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                m_SelectedMessageId = item.Id;
                Event.current.Use();
                Repaint();
            }

            if (Event.current.type == EventType.MouseDown &&
                Event.current.clickCount == 2 &&
                rowRect.Contains(Event.current.mousePosition))
            {
                OpenFirstStackFrame(item);
                Event.current.Use();
            }
        }

        string BuildSecondaryLine(LiveConsoleMessage item)
        {
            if (string.IsNullOrEmpty(item.Message) && string.IsNullOrEmpty(item.StackTrace))
                return string.Empty;

            var parts = new List<string>(2);
            parts.Add(item.LogType.ToString());
            if (m_ShowTimestamps)
                parts.Add(item.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff"));
            return string.Join(" - ", parts);
        }

        void DrawMessageDetails()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
            {
                if (!TryGetSelectedItem(out var selected))
                {
                    using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.ExpandHeight(true)))
                        EditorGUILayout.HelpBox("Select a message to inspect its stack trace.", MessageType.Info);
                    return;
                }

                m_DetailScroll = EditorGUILayout.BeginScrollView(m_DetailScroll);
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(Utility.GetLogLevelIcon(selected.LogLevel), GUILayout.Width(20f), GUILayout.Height(18f));
                        EditorGUILayout.LabelField(selected.LogLevel.ToString(), SharedStyles.BoldLabel, GUILayout.ExpandWidth(false));
                        GUILayout.Space(6f);
                        EditorGUILayout.LabelField(selected.LogType.ToString(), SharedStyles.LabelDarkWithDynamicSize);
                        if (m_ShowTimestamps)
                        {
                            GUILayout.Space(6f);
                            EditorGUILayout.LabelField(
                                selected.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                SharedStyles.LabelDarkWithDynamicSize);
                        }
                        GUILayout.FlexibleSpace();
                        if (selected.Count > 1)
                            EditorGUILayout.LabelField($"x{selected.Count}", SharedStyles.BoldLabel, GUILayout.Width(60f));
                    }

                    EditorGUILayout.Space(UiLayout.SpaceSmall);
                    EditorGUILayout.SelectableLabel(selected.Message ?? "(No message)", SharedStyles.TextArea, GUILayout.MinHeight(54f));
                }

                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Stack Trace", SharedStyles.LargeLabel);
                    EditorGUILayout.Space(2f);
                    DrawStackTrace(selected);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        void DrawStackTrace(LiveConsoleMessage selected)
        {
            var frames = StackTraceFrameParser.Parse(selected.StackTrace);
            if (frames.Count == 0)
            {
                EditorGUILayout.LabelField("No stack trace available.", SharedStyles.LabelDarkWithDynamicSize);
                return;
            }

            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                if (frame.IsOpenable)
                {
                    if (GUILayout.Button(frame.RawLine, SharedStyles.LinkLabel, GUILayout.ExpandWidth(true)))
                        OpenStackFrame(frame);
                }
                else
                {
                    EditorGUILayout.SelectableLabel(frame.RawLine, SharedStyles.TextArea, GUILayout.MinHeight(18f));
                }
            }
        }

        bool TryGetSelectedItem(out LiveConsoleMessage item)
        {
            item = default;
            if (m_FilteredItems.Count == 0)
                return false;

            var selected = m_FilteredItems.FirstOrDefault(x => x.Id == m_SelectedMessageId);
            if (selected.Id <= 0)
                selected = m_FilteredItems[m_FilteredItems.Count - 1];

            m_SelectedMessageId = selected.Id;
            item = selected;
            return true;
        }

        void HandleArrowKeyNavigation()
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown)
                return;
            if (EditorGUIUtility.editingTextField)
                return;
            if (evt.keyCode != KeyCode.UpArrow && evt.keyCode != KeyCode.DownArrow)
                return;
            if (m_FilteredItems.Count == 0)
                return;

            var selectedIndex = GetSelectedIndex();
            if (selectedIndex < 0)
                selectedIndex = m_FilteredItems.Count - 1;

            if (evt.keyCode == KeyCode.UpArrow)
                selectedIndex = Mathf.Max(0, selectedIndex - 1);
            else
                selectedIndex = Mathf.Min(m_FilteredItems.Count - 1, selectedIndex + 1);

            m_SelectedMessageId = m_FilteredItems[selectedIndex].Id;
            evt.Use();
            Repaint();
        }

        int GetSelectedIndex()
        {
            for (var i = 0; i < m_FilteredItems.Count; i++)
            {
                if (m_FilteredItems[i].Id == m_SelectedMessageId)
                    return i;
            }

            return -1;
        }

        void RebuildFilteredItems()
        {
            UpdateLevelCounts();
            m_FilteredItems.Clear();
            var snapshot = LiveConsoleLogService.Instance.GetSnapshot();

            var sourceItems = m_CollapseDuplicates
                ? Collapse(snapshot)
                : snapshot;

            var items = sourceItems
                .Where(item => LogLevelFilter.IsMatch(m_LogLevelMask, item.LogLevel))
                .Where(item => MatchesSearch(item, m_SearchText))
                .OrderBy(item => GetSortRank(item.LogLevel))
                .ThenBy(item => item.TimestampUtc)
                .ToList();

            m_FilteredItems.AddRange(items);
            if (m_FilteredItems.Count == 0)
            {
                m_SelectedMessageId = -1;
                return;
            }

            if (!m_FilteredItems.Any(x => x.Id == m_SelectedMessageId))
                m_SelectedMessageId = m_FilteredItems[m_FilteredItems.Count - 1].Id;
        }

        internal static bool MatchesSearch(LiveConsoleMessage item, string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
                return true;

            return item.Message.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.StackTrace.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static List<LiveConsoleMessage> Collapse(List<LiveConsoleMessage> items)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var latest = new Dictionary<string, LiveConsoleMessage>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var key = item.CollapseKey;
                if (!counts.TryGetValue(key, out var count))
                    count = 0;
                counts[key] = count + 1;
                latest[key] = item;
            }

            var collapsed = new List<LiveConsoleMessage>(latest.Count);
            foreach (var pair in latest)
                collapsed.Add(pair.Value.WithCount(counts[pair.Key]));
            return collapsed;
        }

        void UpdateLevelCounts()
        {
            m_LevelCounts[LogLevel.Error] = 0;
            m_LevelCounts[LogLevel.Warning] = 0;
            m_LevelCounts[LogLevel.Info] = 0;

            var snapshot = LiveConsoleLogService.Instance.GetSnapshot();
            for (var i = 0; i < snapshot.Count; i++)
            {
                var level = snapshot[i].LogLevel;
                m_LevelCounts[level] = m_LevelCounts[level] + 1;
            }
        }

        void SyncWithLogService()
        {
            var service = LiveConsoleLogService.Instance;
            service.PauseOnError = m_PauseOnError;
            var drained = service.DrainPendingEntries();
            if (drained > 0 || service.HasPendingChanges)
            {
                RebuildFilteredItems();
                service.ConsumePendingChanges();
            }
        }

        static void OpenFirstStackFrame(LiveConsoleMessage message)
        {
            var frames = StackTraceFrameParser.Parse(message.StackTrace);
            for (var i = 0; i < frames.Count; i++)
            {
                if (frames[i].IsOpenable)
                {
                    OpenStackFrame(frames[i]);
                    return;
                }
            }
        }

        static void OpenStackFrame(StackTraceFrameParser.StackTraceFrame frame)
        {
            if (!frame.IsOpenable)
                return;

            var location = new Location(frame.Path, frame.Line);
            EditorInterop.OpenTextFile<TextAsset>(location);

            var hasAsset = AssetDatabase.LoadMainAssetAtPath(frame.Path) != null;
            if (hasAsset)
                return;

            var fullPath = System.IO.Path.Combine(SmartAuditor.ProjectPath, frame.Path);
            InternalEditorUtility.OpenFileAtLineExternal(fullPath, frame.Line);
        }

        void DrawSplitter(float topPanelHeight, float availableHeight, float minTop, float maxTop)
        {
            var splitterRect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.Height(k_SplitterHeight));
            var evt = Event.current;

            SolidDraw.ThemeHorizontalSeparator(splitterRect);

            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            if (evt.type == EventType.MouseDown && evt.button == 0 && splitterRect.Contains(evt.mousePosition))
            {
                m_IsDraggingSplitter = true;
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseUp && evt.button == 0 && m_IsDraggingSplitter)
            {
                m_IsDraggingSplitter = false;
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDrag && m_IsDraggingSplitter)
            {
                var nextTop = Mathf.Clamp(topPanelHeight + evt.delta.y, minTop, maxTop);
                m_ListPanelRatio = Mathf.Clamp01(nextTop / Mathf.Max(1f, availableHeight));
                evt.Use();
                Repaint();
            }
        }

        static int GetSortRank(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    return 0;
                case LogLevel.Warning:
                    return 1;
                default:
                    return 2;
            }
        }
    }
}
