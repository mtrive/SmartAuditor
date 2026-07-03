// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if DIAGNOSTICS_INSPECTOR

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.SceneManagement;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;

namespace SmartAuditor.Editor.UI
{
    [InitializeOnLoad]
    class DiagnosticsInspectorExtension
    {
        static DiagnosticsInspectorExtension()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
        }

        static void OnPostHeaderGUI(UnityEditor.Editor editor)
        {
            if (editor.targets == null || editor.targets.Length == 0)
                return;

            var selectionAssetPaths = editor.targets
                .Select(AssetDatabase.GetAssetPath)
                .ToArray();

            // Check if this is a GameObject selection in the hierarchy
            if (selectionAssetPaths.Length == 0 || selectionAssetPaths.All(string.IsNullOrEmpty))
            {
                // Try to analyze hierarchy window selection
                AnalyzeGameObjectSelection(editor.targets);
                return;
            }

            var assetPaths = selectionAssetPaths.Where(path => !string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                .ToArray();

            if (assetPaths.Length == 0)
                return;

            var selectionDiagnostics = DiagnosticsCache.GetIssues(assetPaths);
            if (!selectionDiagnostics.Any())
                return;

            DrawDiagnosticButtons(selectionDiagnostics, () => ShowDiagnosticsWindow(assetPaths, selectionDiagnostics.ToArray()));
        }

        static void DrawDiagnosticButtons(IReadOnlyList<ReportItem> diagnostics, System.Action onShowWindow)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return;

            var errorCount = 0;
            var warningCount = 0;
            var infoCount = 0;

            foreach (var item in diagnostics)
            {
                switch (item.Severity)
                {
                    case Severity.Error:
                        errorCount++;
                        break;
                    case Severity.Warning:
                        warningCount++;
                        break;
                    case Severity.Info:
                        infoCount++;
                        break;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (errorCount > 0)
                    DrawDiagnosticButton(EditorGUIUtility.IconContent("console.erroricon"), errorCount, Color.red, onShowWindow);

                if (warningCount > 0)
                    DrawDiagnosticButton(EditorGUIUtility.IconContent("console.warnicon"), warningCount, Color.yellow, onShowWindow);

                if (infoCount > 0)
                    DrawDiagnosticButton(EditorGUIUtility.IconContent("console.infoicon"), infoCount, Color.white, onShowWindow);
            }
        }

        static void DrawDiagnosticButton(GUIContent icon, int count, Color color, System.Action onClick)
        {
            var originalColor = GUI.contentColor;
            GUI.contentColor = color;

            var content = new GUIContent(icon) { text = count.ToString() };
            if (GUILayout.Button(content, EditorStyles.miniButton, GUILayout.MaxWidth(GetMaxWidth(count))))
            {
                EditorApplication.delayCall += () => onClick?.Invoke();
            }

            GUI.contentColor = originalColor;

            int GetMaxWidth(int number)
            {
                if (number < 10)
                    return 33;
                return number < 100 ? 40 : 47;
            }
        }

        static void ShowDiagnosticsWindow(string[] displayPaths, ReportItem[] diagnostics)
        {
            var report = new Report();
            report.AddIssues(diagnostics);

            ScopedAnalysisWindow.CreateWindowWithReport(report, displayPaths);
        }

        static void AnalyzeGameObjectSelection(UnityEngine.Object[] targets)
        {
            // Extract GameObjects from the targets
            var gameObjects = targets.OfType<GameObject>().Where(go => go != null).ToArray();

            if (gameObjects.Length == 0)
                return;

            // Check if all GameObjects are from the currently loaded scene
            var currentScene = SceneManager.GetActiveScene();
            if (!currentScene.IsValid() || !currentScene.isLoaded)
                return;

            var validGameObjects = gameObjects.Where(go => go.scene == currentScene).ToArray();
            if (validGameObjects.Length == 0)
                return;

            // Get GlobalObjectIds for selected GameObjects
            var globalObjectIds = validGameObjects
                .Select(go =>
                {
                    var gid = GlobalObjectId.GetGlobalObjectIdSlow(go);
                    return gid.ToString();
                })
                .ToArray();

            // Get hierarchy paths for display purposes
            var hierarchyPaths = validGameObjects.Select(GetGameObjectPath).ToArray();

            // Get diagnostics for these GameObjects
            var allDiagnostics = DiagnosticsCache.GetGameObjectIssues(currentScene.name, hierarchyPaths);

            // Filter diagnostics by GlobalObjectId for accurate matching
            var selectionDiagnostics = allDiagnostics
                .Where(item => item.Properties.ContainsKey(GameObjectIssueColumns.GlobalObjectId))
                .Where(item =>
                {
                    var itemGlobalObjectId = item.GetProperty(GameObjectIssueColumns.GlobalObjectId);
                    return !string.IsNullOrEmpty(itemGlobalObjectId) &&
                           globalObjectIds.Any(gid => itemGlobalObjectId == gid);
                })
                .ToList();

            if (!selectionDiagnostics.Any())
                return;

            DrawDiagnosticButtons(selectionDiagnostics, () => ShowDiagnosticsWindow(hierarchyPaths, selectionDiagnostics.ToArray()));
        }


        static string GetGameObjectPath(GameObject obj)
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
