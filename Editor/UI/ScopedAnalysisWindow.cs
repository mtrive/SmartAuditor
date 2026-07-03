// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using SmartAuditor.Editor;
using SmartAuditor.Editor.Modules;
using SmartAuditor.Editor.UI.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SmartAuditor.Editor.Settings;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.UI
{
    public class ScopedAnalysisWindow : EditorWindow
    {
        Report m_Report;
        Vector2 m_ScrollPosition;
        string m_SearchString = "";
        string[] m_SelectedPaths;
        Func<ReportItem, bool> m_ItemFilter;
        ReportItem m_SelectedIssue;

        bool m_AnalysisInProgress;
        ScrollingAnalysisProgress m_ScrollingAnalysisProgress;
        readonly List<string> m_AnalysisLogBuffer = new List<string>(1024);

        DiagnosticRules m_ScopedRules;

        const string k_ViewAllLinkFallbackText = "View all \u2192";
        const int k_OpenViewLinkMaxChars = 48;
        const int k_ScopedTitleLinkMaxChars = 140;
        const int k_ScopePreviewMaxItems = 3;
        const float k_DetailsPanelTextMaxHeight = 110f;

        /// <summary>
        /// Opens a scoped analysis window and runs analysis with the same in-window scrolling progress UI as the main auditor window.
        /// </summary>
        internal static void BeginScopedAnalysis(AnalysisOptions options, string[] selectedPaths, Func<ReportItem, bool> itemFilter = null)
        {
            var window = CreateInstance<ScopedAnalysisWindow>();
            window.m_SelectedPaths = selectedPaths;
            window.m_ItemFilter = itemFilter;
            window.m_Report = null;
            window.m_AnalysisInProgress = true;
            window.titleContent = new GUIContent(BuildWindowTitleForScope());

            var priorOnCompleted = options.OnCompleted;
            options.OnCompleted = report =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (window == null)
                        return;
                    window.CompleteScopedAnalysis(report, priorOnCompleted);
                };
            };

            window.m_ScrollingAnalysisProgress = new ScrollingAnalysisProgress(window);
            window.Show();

            _ = SmartAuditor.AnalyzeAsync(options, window.m_ScrollingAnalysisProgress, window.m_ScrollingAnalysisProgress.CancellationToken);
        }

        void CompleteScopedAnalysis(Report report, Action<Report> priorOnCompleted)
        {
            m_AnalysisInProgress = false;
            m_Report = report;
            m_ScrollingAnalysisProgress?.Dispose();
            m_ScrollingAnalysisProgress = null;
            priorOnCompleted?.Invoke(report);
            Repaint();
        }

        void OnDestroy()
        {
            if (m_ScrollingAnalysisProgress != null)
            {
                m_ScrollingAnalysisProgress.RequestCancel();
                m_ScrollingAnalysisProgress.Dispose();
                m_ScrollingAnalysisProgress = null;
            }
        }

        static string BuildWindowTitleForScope()
        {
            return "Scoped Analysis";
        }

        static string GetScopePreviewName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "(Unnamed)";

            var name = System.IO.Path.GetFileName(path);
            return string.IsNullOrEmpty(name) ? path : name;
        }

        static string BuildScopePreviewLine(string[] selectedPaths)
        {
            var count = selectedPaths?.Length ?? 0;
            if (count <= 0)
                return string.Empty;

            var previewCount = Mathf.Min(count, k_ScopePreviewMaxItems);
            var previews = selectedPaths
                .Take(previewCount)
                .Select(GetScopePreviewName);

            var previewLine = string.Join(", ", previews);
            var remaining = count - previewCount;
            if (remaining > 0)
                previewLine += $" +{remaining} more";

            return previewLine;
        }

        static string BuildScopeSummaryLine(string[] selectedPaths)
        {
            var count = selectedPaths?.Length ?? 0;
            if (count <= 0)
                return "Scope: current selection";

            var label = count == 1 ? "1 selected item" : $"{count} selected items";
            return $"Scope: {label} - {BuildScopePreviewLine(selectedPaths)}";
        }

        void DrawScopeSummaryLine()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                var selectedCount = m_SelectedPaths?.Length ?? 0;
                UiSection.DrawHeaderWithCount($"{SmartAuditor.DisplayName} Scoped Analysis", selectedCount);
                EditorGUILayout.Space(1);

                var tooltip = m_SelectedPaths == null || m_SelectedPaths.Length == 0
                    ? "Current selection"
                    : string.Join("\n", m_SelectedPaths);
                EditorGUILayout.LabelField(new GUIContent(BuildScopeSummaryLine(m_SelectedPaths), tooltip), EditorStyles.miniLabel);
            }
        }

        public static void CreateWindowWithReport(Report report, string[] selectedPaths, Func<ReportItem, bool> itemFilter = null)
        {
            var window = CreateInstance<ScopedAnalysisWindow>();

            window.titleContent = new GUIContent(BuildWindowTitleForScope());
            window.m_Report = report;
            window.m_SelectedPaths = selectedPaths;
            window.m_ItemFilter = itemFilter;
            window.Show();
        }

        void Update()
        {
            if (m_AnalysisInProgress)
                Repaint();
        }

        void OnGUI()
        {
            if (m_AnalysisInProgress && m_ScrollingAnalysisProgress != null)
            {
                ScrollingAnalysisProgressUI.DrawOverlay(this, m_ScrollingAnalysisProgress, m_AnalysisLogBuffer);
                return;
            }

            if (m_Report == null)
            {
                EditorGUILayout.HelpBox($"No {SmartAuditor.DisplayName} scoped analysis results loaded. Use 'Assets/Analyze Assets...' to run a scoped analysis.", MessageType.Info);
                return;
            }

            var navigationDelta = ConsumeKeyboardNavigationInput();

            DrawScopeSummaryLine();
            EditorGUILayout.Space(UiLayout.SpaceXSmall);
            ToolbarSearchField.DrawRow(ref m_SearchString, useDelayed: false);

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, GUI.skin.box);

            var filteredItems = m_Report.Issues
                .Where(item => (m_ItemFilter == null || m_ItemFilter(item)) &&
                              ReportItemTextSearch.MatchesSubstringFilter(item, m_SearchString))
                .ToArray();

            UpdateSelectedIssue(filteredItems);
            ApplyKeyboardNavigation(filteredItems, navigationDelta);

            if (filteredItems.Length == 0)
            {
                if (string.IsNullOrEmpty(m_SearchString))
                {
                    var count = m_SelectedPaths?.Length ?? 0;
                    var suffix = count > 0 ? $" ({count} selected item{(count == 1 ? string.Empty : "s")})" : string.Empty;
                    EmptyStateDrawer.DrawInfoHelpBox($"No issues found in the selected scope{suffix}.");
                }
                else
                {
                    EmptyStateDrawer.DrawInfoHelpBox($"No scoped issues match '{m_SearchString}'.");
                }
            }
            else
            {
                EditorGUILayout.Space(UiLayout.SpaceXSmall);

                var critCount = filteredItems.Count(i => i.Severity == Severity.Critical || i.Severity == Severity.Error);
                var majCount = filteredItems.Count(i => i.Severity == Severity.Major);
                var modCount = filteredItems.Count(i => i.Severity == Severity.Moderate);
                var minCount = filteredItems.Count(i => i.Severity == Severity.Minor);

                var high = new List<ReportItem>();
                var needsAttention = new List<ReportItem>();
                var informational = new List<ReportItem>();
                FocusOverviewPresenter.BucketByTier(filteredItems, item => item.Severity, high, needsAttention, informational);

                var rules = GetOrCreateScopedRules();
                FocusOverviewPresenter.Draw(
                    critCount,
                    majCount,
                    modCount,
                    minCount,
                    high,
                    needsAttention,
                    informational,
                    (item, index) => DrawScopedIssueRow(item, index, rules));
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(UiLayout.SpaceXSmall);
            DrawSelectedIssueDetailsPanel(filteredItems);
        }

        void UpdateSelectedIssue(ReportItem[] filteredItems)
        {
            if (filteredItems == null || filteredItems.Length == 0)
            {
                m_SelectedIssue = null;
                return;
            }

            if (m_SelectedIssue != null && !filteredItems.Contains(m_SelectedIssue))
                m_SelectedIssue = null;
        }

        int ConsumeKeyboardNavigationInput()
        {
            var current = Event.current;
            if (current.type != EventType.KeyDown)
                return 0;

            if (current.keyCode == KeyCode.UpArrow)
            {
                current.Use();
                return -1;
            }

            if (current.keyCode == KeyCode.DownArrow)
            {
                current.Use();
                return 1;
            }

            return 0;
        }

        void ApplyKeyboardNavigation(ReportItem[] filteredItems, int navigationDelta)
        {
            if (navigationDelta == 0 || filteredItems == null || filteredItems.Length == 0)
                return;

            var selectedIndex = Array.IndexOf(filteredItems, m_SelectedIssue);
            if (selectedIndex < 0)
                selectedIndex = navigationDelta < 0 ? filteredItems.Length : -1;

            if (navigationDelta < 0)
                selectedIndex = Mathf.Max(0, selectedIndex - 1);
            else
                selectedIndex = Mathf.Min(filteredItems.Length - 1, selectedIndex + 1);

            m_SelectedIssue = filteredItems[selectedIndex];
            Repaint();
        }

        DiagnosticRules GetOrCreateScopedRules()
        {
            if (m_ScopedRules == null)
                m_ScopedRules = new DiagnosticRules(SmartAuditorSettings.instance.Rules);
            return m_ScopedRules;
        }

        static GUIContent GetIssueAssetLinkGuiContent(ReportItem item)
        {
            var category = item.Category;
            string linkText;
            var path = item.Location?.Path;
            if (!string.IsNullOrEmpty(path))
            {
                linkText = System.IO.Path.GetFileName(path);
                if (string.IsNullOrEmpty(linkText))
                    linkText = path;
                if (linkText.Length > k_OpenViewLinkMaxChars)
                    linkText = linkText.Substring(0, k_OpenViewLinkMaxChars) + "\u2026";

                return new GUIContent(linkText, $"Click to ping this asset in the Project window.\n\n{path}");
            }

            linkText = k_ViewAllLinkFallbackText;
            return new GUIContent(linkText, $"View all items for {category}.");
        }

        static GUIContent GetScopedTitleLinkContent(ReportItem item)
        {
            var descriptor = item.Descriptor;
            string title;
            if (!string.IsNullOrEmpty(item.Description))
                title = item.Description;
            else if (descriptor != null && descriptor.IsValid() && !string.IsNullOrEmpty(descriptor.Title))
                title = descriptor.Title;
            else
                title = "Issue";

            var display = title.Length <= k_ScopedTitleLinkMaxChars
                ? title
                : title.Substring(0, k_ScopedTitleLinkMaxChars) + "\u2026";

            string tooltip;
            if (descriptor != null && descriptor.IsValid() && !string.IsNullOrEmpty(descriptor.Description))
                tooltip = $"{title}\n\n{descriptor.Description}\n\nClick to select this issue in the Editor.";
            else if (!string.IsNullOrEmpty(descriptor?.Title))
                tooltip = $"{title}\n\nClick to select this issue in the Editor.";
            else
                tooltip = "Click to select this issue in the Editor.";

            return new GUIContent(display, tooltip);
        }

        void DrawScopedIssueRow(ReportItem item, int rowIndex, DiagnosticRules rules)
        {
            var rowStyle = ReferenceEquals(item, m_SelectedIssue)
                ? SharedStyles.SelectedRowStyle
                : (rowIndex % 2 == 0 ? SharedStyles.Row : SharedStyles.RowAlternate);

            var rowRect = EditorGUILayout.BeginHorizontal(rowStyle);
            try
            {
                GUILayout.Space(8);
                SeverityPresentation.DrawRowSeverityBar(item.Severity);
                GUILayout.Space(10);

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    if (UiSection.DrawLinkButton(GetScopedTitleLinkContent(item), GUILayout.ExpandWidth(true)))
                    {
                        m_SelectedIssue = item;
                        SelectIssue(item);
                    }

                    var path = item.Location?.Path;
                    if (!string.IsNullOrEmpty(path))
                        EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (UiSection.DrawLinkButton(GetIssueAssetLinkGuiContent(item),
                            GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true)))
                    {
                        m_SelectedIssue = item;
                        OnIssueAssetLinkClicked(item);
                    }
                }

                GUILayout.Space(4);
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }

            var current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && rowRect.Contains(current.mousePosition))
            {
                m_SelectedIssue = item;
                Repaint();
            }
        }

        void DrawSelectedIssueDetailsPanel(ReportItem[] filteredItems)
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label($"{SmartAuditor.DisplayName} Issue Details", SharedStyles.LargeLabel);
                EditorGUILayout.Space(2);

                if (m_SelectedIssue == null)
                {
                    EditorGUILayout.LabelField("Select an issue to view the problem details and recommendation.", EditorStyles.miniLabel);
                    return;
                }

                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    var options = new IssueDetailsBodyOptions
                    {
                        Item = m_SelectedIssue,
                        PresentationSeverity = m_SelectedIssue.Severity,
                        ShowLocationPath = true,
                        LocationStyle = SharedStyles.LabelDarkWithDynamicSize,
                        AllowItemDescriptionFallback = true,
                        DescriptionFallback = "No additional details available for this issue.",
                        DescriptionMaxHeight = k_DetailsPanelTextMaxHeight,
                        RecommendationMaxHeight = k_DetailsPanelTextMaxHeight,
                        ShowNoRecommendationPlaceholder = true,
                        NoRecommendationPlaceholderText = "No recommendation is available for this issue.",
                        NoRecommendationPlaceholderStyle = SharedStyles.LabelDarkWithDynamicSize,
                        MetadataBottomSpacing = 0f,
                        DescriptionTopSpacing = 6f,
                        RecommendationTopSpacing = 8f,
                        NoRecommendationTopSpacing = 6f
                    };

                    IssueDetailsBody.Draw(options, DrawScopedIssueDetailsMetadataRow);
                }
            }
        }

        void DrawScopedIssueDetailsMetadataRow()
        {
            IssueMetadataRowDrawer.Draw(new IssueMetadataRowOptions
            {
                SeverityLabel = new GUIContent(m_SelectedIssue.Severity.ToString()),
                SeverityLabelWidth = 120f,
                DescriptorId = m_SelectedIssue.Descriptor?.Id ?? string.Empty,
                DrawAfterSeverity = () => EditorGUILayout.LabelField(
                    IssueDetailsUi.GetTitle(m_SelectedIssue),
                    SharedStyles.BoldLabel,
                    GUILayout.ExpandWidth(true)),
                FlexibleSpaceBeforeDescriptorId = false
            });
        }

        void OnIssueAssetLinkClicked(ReportItem item)
        {
            var path = item.Location?.Path;
            if (!string.IsNullOrEmpty(path))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(path);
                if (obj != null)
                {
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                    return;
                }
            }

        }

        void SelectIssue(ReportItem item)
        {
            var selected = false;

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
                        selected = true;
                    }
                }
            }

            if (!selected)
            {
                var path = item.Location?.Path;
                if (!string.IsNullOrEmpty(path))
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(path);
                    if (obj != null)
                        ProjectWindowUtil.ShowCreatedAsset(obj);
                }
            }
        }

        /// <summary>
        /// Finds a GameObject by GlobalObjectId.
        /// </summary>
        /// <param name="globalObjectIdString">The GlobalObjectId string</param>
        /// <returns>The GameObject if found, null otherwise</returns>
        static GameObject FindGameObjectByGlobalObjectId(string globalObjectIdString)
        {
            if (string.IsNullOrEmpty(globalObjectIdString))
                return null;

            if (GlobalObjectId.TryParse(globalObjectIdString, out var globalObjectId))
            {
                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
                return obj as GameObject;
            }

            return null;
        }

        /// <summary>
        /// Finds a GameObject by hierarchy path in a specific scene.
        /// </summary>
        /// <param name="hierarchyPath">The hierarchy path (e.g., "Root/Child/Grandchild")</param>
        /// <param name="sceneName">Scene name (Unity's scene.name property) to search in. If provided, used first to find the scene.</param>
        /// <param name="scenePath">Scene asset path to search in. Used as fallback if sceneName doesn't match or is empty.</param>
        /// <returns>The GameObject if found, null otherwise</returns>
        static GameObject FindGameObjectByPath(string hierarchyPath, string sceneName = null, string scenePath = null)
        {
            if (string.IsNullOrEmpty(hierarchyPath))
                return null;

            // Split the path by "/"
            var pathParts = hierarchyPath.Split('/');
            if (pathParts.Length == 0)
                return null;

            Scene? targetScene = null;

            // First, try to find scene by name if provided
            if (!string.IsNullOrEmpty(sceneName))
            {
                for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                {
                    var scene = SceneManager.GetSceneAt(sceneIndex);
                    if (!scene.IsValid() || !scene.isLoaded)
                        continue;

                    // Match scene by name (case-insensitive comparison)
                    if (string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase))
                    {
                        // If multiple scenes have the same name, use scenePath as tiebreaker
                        if (string.IsNullOrEmpty(scenePath) ||
                            string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                        {
                            targetScene = scene;
                            break;
                        }
                    }
                }
            }

            // Fallback: find scene by path if not found by name or sceneName was empty
            if (!targetScene.HasValue && !string.IsNullOrEmpty(scenePath))
            {
                for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                {
                    var scene = SceneManager.GetSceneAt(sceneIndex);
                    if (!scene.IsValid() || !scene.isLoaded)
                        continue;

                    // Match scene by path (case-insensitive comparison)
                    if (string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                    {
                        targetScene = scene;
                        break;
                    }
                }
            }

            // If scene is not loaded, log warning and return null
            if (!targetScene.HasValue || !targetScene.Value.IsValid())
            {
                var sceneIdentifier = !string.IsNullOrEmpty(sceneName) ? $"'{sceneName}'" :
                                      !string.IsNullOrEmpty(scenePath) ? $"'{scenePath}'" : "unknown";
                Debug.LogWarning($"[{SmartAuditor.DisplayName}] Cannot find GameObject '{hierarchyPath}': scene {sceneIdentifier} is not loaded.");
                return null;
            }

            // Search for the GameObject in the specified scene
            var go = FindGameObjectInScene(targetScene.Value, pathParts);
            if (go == null)
            {
                var sceneIdentifier = !string.IsNullOrEmpty(sceneName) ? $"'{sceneName}'" :
                                      !string.IsNullOrEmpty(scenePath) ? $"'{scenePath}'" : "unknown";
                Debug.LogWarning($"[{SmartAuditor.DisplayName}] GameObject '{hierarchyPath}' not found in scene {sceneIdentifier}.");
            }

            return go;
        }

        /// <summary>
        /// Helper method to find a GameObject by path parts within a specific scene.
        /// </summary>
        static GameObject FindGameObjectInScene(Scene scene, string[] pathParts)
        {
            // Find root GameObject with the first part of the path
            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                if (root.name == pathParts[0])
                {
                    // If it's just the root, return it
                    if (pathParts.Length == 1)
                        return root;

                    // Otherwise, traverse down the hierarchy
                    var current = root.transform;
                    for (int i = 1; i < pathParts.Length; i++)
                    {
                        var found = false;
                        foreach (Transform child in current)
                        {
                            if (child.name == pathParts[i])
                            {
                                current = child;
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                            break;
                    }

                    // If we found the full path, return the GameObject
                    if (current.name == pathParts[pathParts.Length - 1])
                        return current.gameObject;
                }
            }

            return null;
        }
    }
}
