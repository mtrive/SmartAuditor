// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by GameObjectModule
    /// </summary>
    public class GameObjectAnalysisContext : AnalysisContext
    {
        public GameObjectAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// Scene or Prefab path
        /// </summary>
        public string AssetPath;

        /// <summary>
        /// Hierarchy path
        /// </summary>
        public string HierarchyPath;

        /// <summary>
        /// Scene name (Unity's scene.name property). Empty for prefabs.
        /// </summary>
        public string SceneName;

        /// <summary>
        /// Unity GlobalObjectId for unique identification
        /// </summary>
        public string GlobalObjectId;

        /// <summary>
        /// GameObject root
        /// </summary>
        public GameObject GameObject;
    }

    public class ComponentAnalysisContext : AnalysisContext
    {
        public ComponentAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// Scene or Prefab path
        /// </summary>
        public string AssetPath;

        /// <summary>
        /// Hierarchy path
        /// </summary>
        public string HierarchyPath;

        /// <summary>
        /// Scene name (Unity's scene.name property). Empty for prefabs.
        /// </summary>
        public string SceneName;

        /// <summary>
        /// Unity GlobalObjectId for unique identification
        /// </summary>
        public string GlobalObjectId;

        /// <summary>
        /// Component object
        /// </summary>
        public Component Component;
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by PrefabsModule
    /// </summary>
    internal abstract class GameObjectAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Implement this method to detect Issues and report them using Diagnostic.Create and context.ReportIssue.
        /// </summary>
        /// <param name="context">Context object containing information necessary to perform analysis</param>
        /// <remarks>
        /// When GameObject Analyzers detect an issue, they should use <seealso cref="Diagnostic.Create"/> to create
        /// a diagnostic with an <see cref="AnalysisCategory"/> and a DescriptorId, then call <seealso cref="AnalysisContext.ReportIssue"/> to report it.
        /// </remarks>
        public virtual void AnalyzeGameObject(GameObjectAnalysisContext context)
        {
        }

        /// <summary>
        /// Implement this method to detect component-specific issues and report them using Diagnostic.Create and context.ReportIssue.
        /// </summary>
        /// <param name="context">Context object containing information necessary to perform analysis</param>
        /// <remarks>
        /// When Component Analyzers detect an issue, they should use <seealso cref="Diagnostic.Create"/> to create
        /// a diagnostic with an <see cref="AnalysisCategory"/> and a DescriptorId, then call <seealso cref="AnalysisContext.ReportIssue"/> to report it.
        /// </remarks>
        public virtual void AnalyzeComponent(ComponentAnalysisContext context)
        {
        }

        /// <summary>
        /// Processes a GameObject and all its components through the analyzers
        /// </summary>
        /// <param name="go">The GameObject to analyze</param>
        /// <param name="hierarchyPath">The hierarchy path of the GameObject in the scene or prefab</param>
        /// <param name="assetPath">The asset path (scene file path or prefab path)</param>
        /// <param name="analyzers">Array of analyzers to run</param>
        /// <param name="options">Analysis options</param>
        /// <param name="sink">Destination for findings emitted by the analyzers.</param>
        public static void ProcessGameObjectAndComponents(
            GameObject go,
            string hierarchyPath,
            string assetPath,
            GameObjectAnalyzer[] analyzers,
            AnalysisOptions options,
            IFindingSink sink)
        {
            var sceneName = (go != null && go.scene.IsValid()) ? go.scene.name : string.Empty;
            var globalObjectId = string.Empty;
            if (go != null)
                globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();

            using var gameObjectAnalysisContext = new GameObjectAnalysisContext(options, sink)
            {
                AssetPath = assetPath,
                HierarchyPath = hierarchyPath,
                SceneName = sceneName,
                GlobalObjectId = globalObjectId,
                GameObject = go,
            };

            foreach (var analyzer in analyzers)
                analyzer.AnalyzeGameObject(gameObjectAnalysisContext);

            gameObjectAnalysisContext.Flush();

            var components = new List<Component>(go.GetComponentCount());
            go.GetComponents(components);
            foreach (var component in components)
            {
                if (!component)
                {
                    // MissingComponentAnalyzer reports these in AnalyzeGameObject; null cannot be passed to AnalyzeComponent.
                    continue;
                }

                using var componentAnalysisContext = new ComponentAnalysisContext(options, sink)
                {
                    AssetPath = assetPath,
                    HierarchyPath = hierarchyPath,
                    SceneName = sceneName,
                    GlobalObjectId = globalObjectId,
                    Component = component,
                };
                foreach (var analyzer in analyzers)
                    analyzer.AnalyzeComponent(componentAnalysisContext);

                componentAnalysisContext.Flush();
            }
        }
    }
}
