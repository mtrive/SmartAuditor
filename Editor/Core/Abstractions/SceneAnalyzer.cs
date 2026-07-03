// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using SmartAuditor.Editor.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Analysis mode for scene analysis
    /// </summary>
    public enum SceneAnalysisMode
    {
        /// <summary>
        /// Analyze scene as an asset by loading it
        /// </summary>
        AssetMode,
        /// <summary>
        /// Analyze the currently loaded scene without loading/closing
        /// </summary>
        CurrentSceneMode
    }

    /// <summary>
    /// A context object passed by ScenesModule
    /// </summary>
    public class SceneAnalysisContext : AnalysisContext
    {
        public SceneAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        public string HierarchyPath;
        public Scene Scene;

        /// <summary>
        /// Optional hierarchy roots for partial analysis. If null or empty, analyzes full scene.
        /// </summary>
        public GameObject[] HierarchyRoots;

        /// <summary>
        /// The analysis mode being used
        /// </summary>
        public SceneAnalysisMode AnalysisMode;
    }

    /// <summary>
    /// Abstract base class for an analyzer invoked once per scene by
    /// <c>SmartAuditor.Editor.Modules.ScenesModule</c>. Use for scene-level findings (structure,
    /// RenderSettings, LightmapSettings, NavMesh build config) — for per-GameObject component
    /// findings, derive from <c>GameObjectAnalyzer</c> instead.
    /// </summary>
    internal abstract class SceneAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Implement to detect scene-level issues and report them via
        /// <see cref="AnalysisContext.ReportIssue"/>.
        /// </summary>
        public abstract void Analyze(SceneAnalysisContext context);

        /// <summary>
        /// Returns every GameObject reachable from <paramref name="context"/>'s hierarchy roots
        /// (or every root in the scene if no roots are specified). Subtrees of inactive parents
        /// are included.
        /// </summary>
        protected static GameObject[] GetAllGameObjectsInScene(SceneAnalysisContext context)
        {
            var all = new List<GameObject>();
            TraverseSceneHierarchy(context, go => all.Add(go));
            return all.ToArray();
        }

        /// <summary>
        /// Walks every GameObject under <paramref name="context"/>'s hierarchy roots (or every
        /// root in the scene if no roots are specified) and invokes <paramref name="action"/> on
        /// each.
        /// </summary>
        protected static void TraverseSceneHierarchy(SceneAnalysisContext context, Action<GameObject> action)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var roots = (context.HierarchyRoots != null && context.HierarchyRoots.Length > 0)
                ? context.HierarchyRoots
                : (context.Scene.IsValid() ? context.Scene.GetRootGameObjects() : Array.Empty<GameObject>());

            foreach (var root in roots)
            {
                if (root == null)
                    continue;
                HierarchyTraversal.TraverseGameObjectHierarchy(root, string.Empty, (go, _) =>
                {
                    action(go);
                    return true;
                });
            }
        }

        /// <summary>
        /// Counts every GameObject reachable from <paramref name="context"/>'s hierarchy roots.
        /// </summary>
        protected static int CountGameObjectsInScene(SceneAnalysisContext context)
        {
            var count = 0;
            TraverseSceneHierarchy(context, _ => count++);
            return count;
        }
    }
}
