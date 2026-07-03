// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartAuditor.Editor.Modules
{
    /// <summary>
    /// Module for analyzing Unity scenes.
    /// </summary>
    internal sealed class ScenesModule : AnalysisModule<GameObjectAnalyzer>
    {
        public override string Name => "Scenes";

        // Scene-level analyzers (one Analyze() call per scene) are discovered alongside the
        // per-GameObject analyzers the base class manages. The base class is generic over
        // GameObjectAnalyzer; SceneAnalyzer-derived types are tracked manually here.
        SceneAnalyzer[] m_SceneAnalyzers = Array.Empty<SceneAnalyzer>();

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.Scene, new InsightSchema() },
                { AnalysisCategory.SceneSettings, new InsightSchema() },
                { AnalysisCategory.GameObjectIssue, SharedSchemas.GameObjectIssue },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.Scene,
            AnalysisCategory.SceneSettings,
            AnalysisCategory.GameObjectIssue,
        };

        public override AnalysisSource? SupportedAnalysisSource => null;

        public override void Initialize()
        {
            base.Initialize();

            // Manual discovery for SceneAnalyzer-derived types (the base AnalysisModule<T> only
            // walks T = GameObjectAnalyzer). Mirror the base's instantiation + Initialize sweep
            // so DiagnosticParameter caching and any analyzer-side setup runs the same way.
            var sceneAnalyzers = new List<SceneAnalyzer>();
            foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(SceneAnalyzer)))
            {
                if (type.IsAbstract)
                    continue;
                var analyzer = (SceneAnalyzer)Activator.CreateInstance(type);
                analyzer.Initialize();
                sceneAnalyzers.Add(analyzer);
            }
            m_SceneAnalyzers = sceneAnalyzers.ToArray();
        }

        SceneAnalyzer[] GetCompatibleSceneAnalyzers(AnalysisOptions options)
        {
            var analyzers = new List<SceneAnalyzer>();
            foreach (var analyzer in m_SceneAnalyzers)
            {
                if (CoreUtils.SupportsPlatform(analyzer.GetType(), options.Platform))
                {
                    analyzer.CacheParameters(options.DiagnosticParams);
                    analyzers.Add(analyzer);
                }
            }
            return analyzers.ToArray();
        }

        void RunSceneAnalyzers(SceneAnalyzer[] sceneAnalyzers, AnalysisSession session, Scene scene, string assetPath, GameObject[] hierarchyRoots, SceneAnalysisMode mode)
        {
            if (sceneAnalyzers.Length == 0)
                return;

            using var context = new SceneAnalysisContext(session.Options, session)
            {
                Scene = scene,
                HierarchyPath = assetPath ?? string.Empty,
                HierarchyRoots = hierarchyRoots,
                AnalysisMode = mode,
            };

            foreach (var analyzer in sceneAnalyzers)
                analyzer.Analyze(context);
        }

        public override async Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var analyzers = GetCompatibleAnalyzers(options);
            var sceneAnalyzers = GetCompatibleSceneAnalyzers(options);

            if (analyzers.Length == 0 && sceneAnalyzers.Length == 0)
                return AnalysisResult.Success;

            AnalysisResult result;
            if (ShouldUseCurrentSceneMode(options))
            {
                result = AnalyzeSceneObjects(session, analyzers, sceneAnalyzers, cancellationToken);
            }
            else
            {
                if (Application.isPlaying)
                    return AnalysisResult.Success;

                result = await AnalyzeSceneAssets(session, analyzers, sceneAnalyzers, cancellationToken);
            }

            if (result != AnalysisResult.Success)
                return result;

            FinalizeAnalyzers(session, analyzers, sceneAnalyzers);
            return AnalysisResult.Success;
        }

        AnalysisResult AnalyzeSceneObjects(AnalysisSession session, GameObjectAnalyzer[] analyzers, SceneAnalyzer[] sceneAnalyzers, CancellationToken cancellationToken)
        {
            var options = session.Options;
            var scene = HierarchyTraversal.FindScene(options.SceneName);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                var sceneName = options.SceneName ?? "active scene";
                throw new InvalidOperationException($"Scene '{sceneName}' is not found or not loaded. Cannot analyze scene.");
            }

            var result = TraverseScene(scene, (go, hierarchyPath) =>
            {
                if (options.HierarchyPaths == null || options.HierarchyPaths.Length == 0)
                {
                    GameObjectAnalyzer.ProcessGameObjectAndComponents(go, hierarchyPath, string.Empty, analyzers, options, session);
                    return true;
                }

                if (options.HierarchyPaths.All(p => !p.StartsWith(hierarchyPath, StringComparison.Ordinal)))
                    return false;

                var isExactMatch = options.HierarchyPaths.Any(p => hierarchyPath.Equals(p, StringComparison.Ordinal));
                if (isExactMatch)
                {
                    GameObjectAnalyzer.ProcessGameObjectAndComponents(go, hierarchyPath, string.Empty, analyzers, options, session);
                    return false;
                }

                return true;
            }, session.Progress, cancellationToken);

            if (result == AnalysisResult.Success)
                RunSceneAnalyzers(sceneAnalyzers, session, scene, string.Empty, hierarchyRoots: null, SceneAnalysisMode.CurrentSceneMode);

            return result;
        }

        async Task<AnalysisResult> AnalyzeSceneAssets(AnalysisSession session, GameObjectAnalyzer[] analyzers, SceneAnalyzer[] sceneAnalyzers, CancellationToken cancellationToken)
        {
            var options = session.Options;
            var progress = session.Progress;
            var sceneAssetPaths = GetScenePaths(options);
            var originalScenePath = SceneManager.GetActiveScene().path;
            var analysisResult = AnalysisResult.Success;

            try
            {
                for (var i = 0; i < sceneAssetPaths.Length; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        analysisResult = AnalysisResult.Cancelled;
                        break;
                    }
                    var sceneAssetPath = sceneAssetPaths[i];

                    var scene = OpenSceneForAnalysis(sceneAssetPath);
                    if (!scene.IsValid())
                    {
                        Debug.LogError($"[ScenesModule] Failed to load scene: {sceneAssetPath}. " +
                                     "Possible causes: corrupted file, missing dependencies, compilation errors, or memory issues.");
                        continue;
                    }

                    try
                    {
                        var traversalResult = TraverseScene(scene, (go, hierarchyPath) =>
                        {
                            if (options.HierarchyPaths == null ||
                                options.HierarchyPaths.Length == 0 ||
                                options.HierarchyPaths.Any(p => hierarchyPath.StartsWith(p, StringComparison.Ordinal)))
                            {
                                GameObjectAnalyzer.ProcessGameObjectAndComponents(go, hierarchyPath, sceneAssetPath, analyzers, options, session);
                            }
                            return true;
                        }, progress, cancellationToken);

                        if (traversalResult != AnalysisResult.Success)
                        {
                            analysisResult = traversalResult;
                            break;
                        }

                        RunSceneAnalyzers(sceneAnalyzers, session, scene, sceneAssetPath, hierarchyRoots: null, SceneAnalysisMode.AssetMode);
                    }
                    finally
                    {
                        CloseAnalyzedScene();
                    }

                    // Loading and traversing a scene is several seconds of main-thread work;
                    // yield after every scene so the in-window progress overlay can repaint
                    // and the user can hit Cancel between scenes.
                    await Task.Yield();
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalScenePath))
                    OpenSceneForAnalysis(originalScenePath);
            }

            return analysisResult;
        }

        string[] GetScenePaths(AnalysisOptions options)
        {
            var allScenePaths = AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .Where(path => IsSceneAccessible(path, options))
                .ToArray();

            if (options.AssetPathFilter != null)
                allScenePaths = allScenePaths.Where(path => options.AssetPathFilter(path)).ToArray();

            // Drop scenes the user has globally suppressed via a no-Id rule. Scenes don't flow
            // through AssetPathUtils, so mirror its suppression filter here.
            if (options.Rules != null)
                allScenePaths = allScenePaths.Where(path => !options.Rules.IsPathGloballySuppressed(path)).ToArray();

            return allScenePaths;
        }

        bool ShouldUseCurrentSceneMode(AnalysisOptions options)
        {
            if (options == null)
                return false;

            if (options.AnalysisSource == AnalysisSource.LoadedScene)
                return true;

            if (options.AssetPathFilter != null)
            {
                var testPaths = new[] { "LoadedScene", "currentscene", "CURRENTSCENE" };
                foreach (var testPath in testPaths)
                {
                    if (options.AssetPathFilter(testPath))
                        return true;
                }
            }

            return false;
        }

        Scene OpenSceneForAnalysis(string scenePath)
        {
            try
            {
                return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScenesModule] Failed to open scene {scenePath}: {e.Message}");
                return default;
            }
        }

        void CloseAnalyzedScene()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScenesModule] Failed to close analyzed scene: {e.Message}");
            }
        }

        bool IsSceneAccessible(string scenePath, AnalysisOptions options)
        {
            if (!AssetPathUtils.ShouldAnalyzePackagePath(scenePath, options))
            {
                //Debug.Log($"[ScenesModule] Skipping read-only package scene: {scenePath}");
                return false;
            }

            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogWarning($"[ScenesModule] Scene file not found: {scenePath}");
                return false;
            }

            return true;
        }

        static AnalysisResult TraverseScene(Scene scene, Func<GameObject, string, bool> action, IProgress progress = null, CancellationToken cancellationToken = default)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return AnalysisResult.Failure;

            progress?.Start("Analyzing Scene", scene.name, scene.GetRootGameObjects().Length);

            try
            {
                foreach (var rootObject in scene.GetRootGameObjects())
                {
                    if (cancellationToken.IsCancellationRequested)
                        return AnalysisResult.Cancelled;

                    progress?.Advance(rootObject.name);
                    HierarchyTraversal.TraverseGameObjectHierarchy(rootObject, string.Empty, action);
                }
            }
            finally
            {
                progress?.Clear();
            }

            return cancellationToken.IsCancellationRequested ? AnalysisResult.Cancelled : AnalysisResult.Success;
        }
    }
}
