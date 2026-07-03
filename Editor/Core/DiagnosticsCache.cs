// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace SmartAuditor.Editor.Core
{
    internal static class DiagnosticsCache
    {
        static readonly Dictionary<string, ReportItem[]> s_Cache = new Dictionary<string, ReportItem[]>();

        static DiagnosticsCache()
        {
            EditorApplication.projectChanged += Clear;
        }

        public static List<ReportItem> GetIssues(IReadOnlyList<string> assetPaths)
        {
            var diagnostics = new List<ReportItem>();
            foreach (var assetPath in assetPaths)
            {
                GetIssues(assetPath, ref diagnostics);
            }
            return diagnostics;
        }

        static void GetIssues(string assetPath, ref List<ReportItem> result)
        {
            if (!s_Cache.TryGetValue(assetPath, out var diagnostics))
            {
                diagnostics = Analyze(assetPath);
                s_Cache[assetPath] = diagnostics;
            }
            result.AddRange(diagnostics);
        }

        public static List<ReportItem> GetGameObjectIssues(string sceneName, string[] hierarchyPaths)
        {
            if (string.IsNullOrEmpty(sceneName) || hierarchyPaths == null || hierarchyPaths.Length == 0)
                return new List<ReportItem>();

            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
                return new List<ReportItem>();

            // Create a cache key based on scene name and hierarchy paths
            // Note: For now we don't cache per-hierarchy-path combinations, we just analyze on demand
            // This could be optimized in the future if needed
            var diagnostics = AnalyzeScene(sceneName, hierarchyPaths);

            return diagnostics.ToList();
        }

        public static void InvalidateAsset(string assetPath)
        {
            // remove key if it exists
            s_Cache.Remove(assetPath);
        }

        static void Clear()
        {
            s_Cache.Clear();
        }

        static ReportItem[] Analyze(string assetPath)
        {
            var report = SmartAuditor.Analyze(new AnalysisOptions
            {
                Categories = new[] { AnalysisCategory.AssetIssue, AnalysisCategory.GameObjectIssue },
                AssetPathFilter = (path) => path == assetPath // TODO optimize so filter is passed to FindAssets
            });

            return report?.Issues;// Issues.ToArray();
        }

        static ReportItem[] AnalyzeScene(string sceneName, string[] hierarchyPaths = null)
        {
            var report = SmartAuditor.Analyze(new AnalysisOptions
            {
                Categories = new[] { AnalysisCategory.GameObjectIssue },
                AnalysisSource = AnalysisSource.LoadedScene,
                SceneName = sceneName,
                HierarchyPaths = hierarchyPaths
            });

            return report?.Issues ?? System.Array.Empty<ReportItem>();
        }
    }

    internal class DiagnosticsAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Invalidate cache for imported assets
            foreach (var assetPath in importedAssets)
            {
                DiagnosticsCache.InvalidateAsset(assetPath);
            }

            // Invalidate cache for moved assets
            foreach (var assetPath in movedAssets)
            {
                DiagnosticsCache.InvalidateAsset(assetPath);
            }

            // Invalidate cache for deleted assets
            foreach (var assetPath in deletedAssets)
            {
                DiagnosticsCache.InvalidateAsset(assetPath);
            }
        }
    }
}
