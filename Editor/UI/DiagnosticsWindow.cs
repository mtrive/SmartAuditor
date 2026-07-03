// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using SmartAuditor.Editor.UI.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#if DIAGNOSTICS_INSPECTOR

namespace SmartAuditor.Editor.UI
{
    class DiagnosticsWindow : EditorWindow
    {
        enum DiagnosticsTab
        {
            Selection,
            Scene,
            Project
        }

        static readonly string[] k_TabNames =
        {
            "Selection",
            "Scene",
            "Project"
        };

        readonly List<ReportItem> m_SelectionDiagnostics = new List<ReportItem>();

        DiagnosticsTab m_ActiveTab = DiagnosticsTab.Selection;
        Report m_SceneReport;
        Report m_ProjectReport;
        Vector2 m_SelectionScroll;
        Vector2 m_SceneScroll;
        Vector2 m_ProjectScroll;
        string m_SelectionSummary = "Nothing selected";
        string m_LastAnalyzedScenePath;
        DateTime? m_LastProjectAnalysisTime;
        bool m_IsSceneAnalysisRunning;
        bool m_IsProjectAnalysisRunning;

        [MenuItem("Window/Analysis/Diagnostics")]
        static void ShowWindow()
        {
            var window = GetWindow<DiagnosticsWindow>();
            window.titleContent = new GUIContent("Diagnostics");
            window.minSize = new Vector2(420, 260);
            window.RefreshSelectionDiagnostics();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Diagnostics");
            minSize = new Vector2(420, 260);

            Selection.selectionChanged += OnSelectionChanged;
            RefreshSelectionDiagnostics();
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        void OnSelectionChanged()
        {
            RefreshSelectionDiagnostics();
            Repaint();
        }

        void RefreshSelectionDiagnostics()
        {
            m_SelectionDiagnostics.Clear();

            var targets = Selection.objects;
            if (targets == null || targets.Length == 0)
            {
                m_SelectionSummary = "Nothing selected";
                return;
            }

            var assetPaths = targets
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                .Distinct()
                .ToArray();

            if (assetPaths.Length > 0)
            {
                var assetDiagnostics = DiagnosticsCache.GetIssues(assetPaths);
                m_SelectionDiagnostics.AddRange(assetDiagnostics);
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                var gameObjects = targets
                    .OfType<GameObject>()
                    .Where(go => go != null && go.scene == scene)
                    .ToArray();

                if (gameObjects.Length > 0)
                {
                    var hierarchyPaths = gameObjects.Select(GetHierarchyPath).ToArray();
                    var gameObjectIssueDiagnostics = DiagnosticsCache.GetGameObjectIssues(scene.name, hierarchyPaths);
                    var globalObjectIds = gameObjects
                        .Select(go => GlobalObjectId.GetGlobalObjectIdSlow(go).ToString())
                        .ToArray();

                    var filtered = gameObjectIssueDiagnostics
                        .Where(item => item.Properties.ContainsKey(GameObjectIssueColumns.GlobalObjectId))
                        .Where(item =>
                        {
                            var itemGlobalObjectId = item.GetProperty(GameObjectIssueColumns.GlobalObjectId);
                            if (string.IsNullOrEmpty(itemGlobalObjectId))
                                return false;
                            return globalObjectIds.Any(globalId => globalId == itemGlobalObjectId);
                        });

                    m_SelectionDiagnostics.AddRange(filtered);
                }
            }

            m_SelectionSummary = BuildSelectionSummary(targets);
        }

        static string BuildSelectionSummary(UnityEngine.Object[] targets)
        {
            var names = targets
                .Where(t => t != null)
                .Select(t => t.name)
                .Distinct()
                .ToArray();

            if (names.Length == 0)
                return "Nothing selected";

            if (names.Length == 1)
                return names[0];

            var head = names.Take(3);
            var suffix = names.Length > 3 ? $" (+{names.Length - 3})" : string.Empty;
            return string.Join(", ", head) + suffix;
        }

        void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space();

            switch (m_ActiveTab)
            {
                case DiagnosticsTab.Selection:
                    DrawSelectionTab();
                    break;
                case DiagnosticsTab.Scene:
                    DrawSceneTab();
                    break;
                case DiagnosticsTab.Project:
                    DrawProjectTab();
                    break;
            }
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var newTabIndex = GUILayout.Toolbar((int)m_ActiveTab, k_TabNames, EditorStyles.toolbarButton);
                if (newTabIndex != (int)m_ActiveTab)
                    m_ActiveTab = (DiagnosticsTab)newTabIndex;

                GUILayout.FlexibleSpace();
            }
        }

        void DrawSelectionTab()
        {
            EditorGUILayout.LabelField("Selection", SharedStyles.BoldLabel);
            EditorGUILayout.LabelField(m_SelectionSummary, EditorStyles.miniLabel);

            DrawSeveritySummary(m_SelectionDiagnostics);
            DrawDiagnosticsList(m_SelectionDiagnostics, ref m_SelectionScroll, "Select an asset or GameObject to see diagnostics.");
        }

        void DrawSceneTab()
        {
            var scene = SceneManager.GetActiveScene();
            EditorGUILayout.LabelField("Active Scene", SharedStyles.BoldLabel);

            var sceneLabel = !scene.IsValid() ? "No active scene" :
                string.IsNullOrEmpty(scene.path) ? scene.name : $"{scene.name} ({scene.path})";
            EditorGUILayout.LabelField(sceneLabel, EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = scene.IsValid() && scene.isLoaded && !m_IsSceneAnalysisRunning;
                if (GUILayout.Button("Analyze Scene", GUILayout.Width(140)))
                    AnalyzeActiveScene(scene);
                GUI.enabled = true;

                if (!string.IsNullOrEmpty(m_LastAnalyzedScenePath))
                    EditorGUILayout.LabelField($"Last analyzed: {m_LastAnalyzedScenePath}", EditorStyles.miniLabel);
            }

            if (m_IsSceneAnalysisRunning)
            {
                EditorGUILayout.HelpBox("Analyzing scene...", MessageType.Info);
                return;
            }

            var issues = m_SceneReport?.Issues ?? Array.Empty<ReportItem>();
            DrawSeveritySummary(issues);
            DrawDiagnosticsList(issues, ref m_SceneScroll, "Click Analyze Scene to populate diagnostics.");
        }

        void AnalyzeActiveScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            m_IsSceneAnalysisRunning = true;
            try
            {
                var report = SmartAuditor.Analyze(new AnalysisOptions
                {
                    Categories = new[] { AnalysisCategory.GameObjectIssue },
                    AnalysisSource = AnalysisSource.LoadedScene,
                    SceneName = scene.name
                });

                m_SceneReport = report;
                m_LastAnalyzedScenePath = string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
            }
            finally
            {
                m_IsSceneAnalysisRunning = false;
            }
        }

        void DrawProjectTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !m_IsProjectAnalysisRunning;
                if (GUILayout.Button("Analyze Project", GUILayout.Width(140)))
                    AnalyzeProject();
                GUI.enabled = true;

                if (m_LastProjectAnalysisTime.HasValue)
                {
                    var timestamp = m_LastProjectAnalysisTime.Value.ToString("g");
                    EditorGUILayout.LabelField($"Last analyzed: {timestamp}", EditorStyles.miniLabel);
                }
            }

            if (m_IsProjectAnalysisRunning)
            {
                EditorGUILayout.HelpBox("Analyzing project...", MessageType.Info);
                return;
            }

            var issues = m_ProjectReport?.Issues ?? Array.Empty<ReportItem>();
            DrawSeveritySummary(issues);
            DrawDiagnosticsList(issues, ref m_ProjectScroll, "Click Analyze Project to populate diagnostics.");
        }

        void AnalyzeProject()
        {
            m_IsProjectAnalysisRunning = true;
            try
            {
                var report = SmartAuditor.Analyze(new AnalysisOptions());
                m_ProjectReport = report;
                m_LastProjectAnalysisTime = DateTime.Now;
            }
            finally
            {
                m_IsProjectAnalysisRunning = false;
            }
        }

        void DrawSeveritySummary(IReadOnlyCollection<ReportItem> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return;

            var errorCount = diagnostics.Count(item => item.LogLevel == LogLevel.Error);
            var warningCount = diagnostics.Count(item => item.LogLevel == LogLevel.Warning);
            var infoCount = diagnostics.Count(item => item.LogLevel == LogLevel.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSeverityBadge("Errors", errorCount, LogLevel.Error);
                DrawSeverityBadge("Warnings", warningCount, LogLevel.Warning);
                DrawSeverityBadge("Info", infoCount, LogLevel.Info);
                GUILayout.FlexibleSpace();
            }
        }

        void DrawSeverityBadge(string label, int count, LogLevel level)
        {
            if (count == 0)
                return;

            var icon = GetSeverityIcon(level);
            var previousColor = GUI.contentColor;
            GUI.contentColor = GetSeverityColor(level);
            GUILayout.Label(new GUIContent($"{label}: {count}", icon.image), EditorStyles.miniBoldLabel);
            GUI.contentColor = previousColor;
        }

        void DrawDiagnosticsList(IReadOnlyList<ReportItem> diagnostics, ref Vector2 scrollPosition, string emptyMessage)
        {
            using (new EditorGUILayout.VerticalScope(SharedStyles.TextBoxBackground))
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                if (diagnostics == null || diagnostics.Count == 0)
                {
                    EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                for (var index = 0; index < diagnostics.Count; index++)
                {
                    var item = diagnostics[index];
                    var rowStyle = index % 2 == 0 ? SharedStyles.RowBackground : SharedStyles.RowBackgroundAlternate;

                    using (new EditorGUILayout.HorizontalScope(rowStyle))
                    {
                        GUILayout.Label(GetSeverityIcon(item.LogLevel), GUILayout.Width(24), GUILayout.Height(18));

                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField(item.Description, SharedStyles.BoldLabel);
                            var location = GetLocationLabel(item);
                            if (!string.IsNullOrEmpty(location))
                                EditorGUILayout.LabelField(location, EditorStyles.miniLabel);
                        }

                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(64)))
                            PingIssue(item);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        static string GetLocationLabel(ReportItem issue)
        {
            if (issue == null)
                return string.Empty;

            if (issue.Location != null && !string.IsNullOrEmpty(issue.Location.Path))
                return issue.Location.Path;

            if (issue.Category == AnalysisCategory.GameObjectIssue)
                return "Scene issue";

            return string.Empty;
        }

        static GUIContent GetSeverityIcon(LogLevel level)
        {
            var iconName = "console.infoicon";
            switch (level)
            {
                case LogLevel.Error:
                    iconName = "console.erroricon";
                    break;
                case LogLevel.Warning:
                    iconName = "console.warnicon";
                    break;
            }

            return EditorGUIUtility.IconContent(iconName);
        }

        static Color GetSeverityColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    return new Color(0.9f, 0.2f, 0.2f);
                case LogLevel.Warning:
                    return new Color(0.95f, 0.7f, 0.2f);
                default:
                    return SharedStyles.IsDarkMode ? Color.white : Color.black;
            }
        }

        static void PingIssue(ReportItem item)
        {
            if (item == null)
                return;

            if (item.Category == AnalysisCategory.GameObjectIssue)
            {
                var globalObjectId = item.GetProperty(GameObjectIssueColumns.GlobalObjectId);
                if (!string.IsNullOrEmpty(globalObjectId))
                {
                    var go = FindGameObjectByGlobalObjectId(globalObjectId);
                    if (go != null)
                    {
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                        return;
                    }
                }
            }

            var path = item.Location?.Path;
            if (string.IsNullOrEmpty(path))
                return;

            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null)
                return;

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        static GameObject FindGameObjectByGlobalObjectId(string globalObjectIdString)
        {
            if (string.IsNullOrEmpty(globalObjectIdString))
                return null;

            if (!GlobalObjectId.TryParse(globalObjectIdString, out var globalObjectId))
                return null;

            var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
            return obj as GameObject;
        }

        static string GetHierarchyPath(GameObject obj)
        {
            if (obj == null)
                return string.Empty;

            var path = obj.name;
            var parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
#endif
