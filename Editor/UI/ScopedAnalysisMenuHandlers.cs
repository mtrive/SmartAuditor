// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartAuditor.Editor.UI
{
    [InitializeOnLoad]
    static class ScopedAnalysisMenuHandlers
    {
        static ScopedAnalysisMenuHandlers()
        {
            SceneHierarchyHooks.addItemsToSceneHeaderContextMenu += (menu, scene) =>
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent($"Analyze {scene.name}..."), on: false, () =>
                {
                    // note that selecting multiple scene does not seem to be supported by SceneHierarchyHooks
                    AnalyzeSceneFromContext(scene.name);
                });
            };
            SceneHierarchyHooks.addItemsToGameObjectContextMenu += (menu, o) =>
            {
                if (o is GameObject go)
                {
                    menu.AddSeparator("");
                    var selectedGameObjects = Selection.gameObjects;
                    var validSelection = selectedGameObjects != null && selectedGameObjects.Length > 0 &&
                                        selectedGameObjects.Any(g => g != null);

                    if (validSelection)
                    {
                        var count = selectedGameObjects.Length;
                        var menuText = count == 1 ? $"Analyze {go.name}" : $"Analyze {count} selected objects";
                        menu.AddItem(new GUIContent($"{menuText}..."), on: false, () =>
                        {
                            AnalyzeGameObjectFromContext(includeChildren: false);
                        });
                        menu.AddItem(new GUIContent($"{menuText} and children..."), on: false, () =>
                        {
                            AnalyzeGameObjectFromContext(includeChildren: true);
                        });
                    }
                    else
                    {
                        menu.AddItem(new GUIContent($"Analyze {go.name}..."), on: false, () =>
                        {
                            AnalyzeGameObjectFromContext(includeChildren: false);
                        });
                        menu.AddItem(new GUIContent($"Analyze {go.name} and children..."), on: false, () =>
                        {
                            AnalyzeGameObjectFromContext(includeChildren: true);
                        });
                    }
                }
            };
        }

        static void AnalyzeSceneFromContext(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("Scene path is empty. Cannot analyze scene.");
                return;
            }

            AnalysisOptions options;
            options = new AnalysisOptions
            {
                Categories = new[] { AnalysisCategory.GameObjectIssue },
                AnalysisSource = AnalysisSource.LoadedScene, // Excludes PrefabsModule (AssetAnalysis only)
                SceneName = sceneName
            };

            ScopedAnalysisWindow.BeginScopedAnalysis(options, new[] { sceneName });
        }

        static void AnalyzeGameObjectFromContext(bool includeChildren)
        {
            var selectedGameObjects = Selection.gameObjects;
            if (selectedGameObjects == null || selectedGameObjects.Length == 0)
            {
                Debug.LogWarning("No GameObjects selected in the hierarchy.");
                return;
            }

            // Filter out null GameObjects
            var validGameObjects = selectedGameObjects.Where(go => go != null).ToArray();
            if (validGameObjects.Length == 0)
            {
                Debug.LogWarning("No valid GameObjects selected.");
                return;
            }

            var hierarchyPaths = new HashSet<string>();

            // Collect hierarchy paths for all selected GameObjects
            foreach (var go in validGameObjects)
            {
                var goPath = GetGameObjectPath(go);
                hierarchyPaths.Add(goPath);

                if (includeChildren)
                {
                    CollectChildrenPaths(go, hierarchyPaths);
                }
            }

            // Check if all selected GameObjects are from the currently loaded scene
            var currentScene = SceneManager.GetActiveScene();
            var allFromCurrentScene = currentScene.IsValid() && currentScene.isLoaded &&
                validGameObjects.All(go => go.scene == currentScene);

            AnalysisOptions options;

            if (allFromCurrentScene)
            {
                // Analyze current scene with selected hierarchy root paths
                options = new AnalysisOptions
                {
                    Categories = new[] { AnalysisCategory.GameObjectIssue },
                    AnalysisSource = AnalysisSource.LoadedScene, // Excludes PrefabsModule (AssetAnalysis only)
                    HierarchyPaths = hierarchyPaths.ToArray(),
                    SceneName = currentScene.name
                };
            }
            else
            {
                // Get unique scene paths from selected GameObjects (asset-based analysis)
                var scenePaths = new HashSet<string>();

                foreach (var go in validGameObjects)
                {
                    var scene = go.scene;
                    if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                    {
                        Debug.LogWarning($"Selected GameObject '{go.name}' is not part of a saved scene.");
                        continue;
                    }

                    scenePaths.Add(scene.path);
                }

                if (scenePaths.Count == 0)
                {
                    Debug.LogWarning("No valid scenes found for selected GameObjects.");
                    return;
                }

                options = new AnalysisOptions
                {
                    Categories = new[] { AnalysisCategory.GameObjectIssue },
                    AnalysisSource = AnalysisSource.Assets, // Analyzing scene as asset
                    AssetPathFilter = (path) =>
                    {
                        return scenePaths.Contains(path);
                    },
                    HierarchyPaths = hierarchyPaths.ToArray()
                };
            }

            // Create a filter function to only include issues for selected GameObjects (and optionally their children)
            Func<ReportItem, bool> hierarchyFilter = (item) =>
            {
                if (item.Location == null || string.IsNullOrEmpty(item.Location.Path))
                    return true;

                var itemPath = item.Location.Path;

                // Check if the item path matches any of our hierarchy paths
                // It should either match exactly or be a child (start with the path + "/")
                return hierarchyPaths.Any(hierarchyPath =>
                    itemPath == hierarchyPath ||
                    (includeChildren && itemPath.StartsWith(hierarchyPath + "/", System.StringComparison.Ordinal)));
            };

            // Create display names for selected objects
            var displayPaths = validGameObjects.Select(GetGameObjectPath).ToArray();

            ScopedAnalysisWindow.BeginScopedAnalysis(options, displayPaths, hierarchyFilter);
        }

        [MenuItem("Assets/Analyze Assets...", isValidateFunction: false, priority: 1000)]
        static void AnalyzeProjectWindowSelection()
        {
            var selectedPaths = new List<string>();

            foreach (var guid in Selection.assetGUIDs)
            {
                selectedPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            ScopedAnalysisWindow.BeginScopedAnalysis(new AnalysisOptions
            {
                Categories = new [] { AnalysisCategory.AssetIssue, AnalysisCategory.GameObjectIssue }, // so it does not analyze code
                AnalysisSource = AnalysisSource.Assets, // Excludes modules that only support LoadedScene
                AssetPathFilter = (path) =>
                {
                    return selectedPaths.Any(p => path.StartsWith(p, System.StringComparison.Ordinal));
                }
            }, selectedPaths.ToArray());
        }

        static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null)
                return string.Empty;

            string path = obj.name;
            Transform parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        static void CollectChildrenPaths(GameObject root, HashSet<string> paths)
        {
            if (root == null)
                return;

            foreach (Transform child in root.transform)
            {
                var childPath = GetGameObjectPath(child.gameObject);
                paths.Add(childPath);
                CollectChildrenPaths(child.gameObject, paths);
            }
        }
    }
}
