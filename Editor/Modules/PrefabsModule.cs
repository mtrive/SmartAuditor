// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Modules
{
    internal sealed class PrefabsModule : AnalysisModule<GameObjectAnalyzer>
    {
        internal static readonly InsightSchema k_PrefabInsightSchema = new InsightSchema(
            new InsightColumn(PrefabColumns.HierarchySize, "Hierarchy Size", PropertyFormat.Number, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Total GameObjects in hierarchy"),
            new InsightColumn(PrefabColumns.HierarchyDepth, "Hierarchy Depth", PropertyFormat.Number, ColumnHints.TopNMetric, longName: "Maximum depth from root (root = 0)"),
            new InsightColumn(PrefabColumns.ComponentCount, "Components", PropertyFormat.Number, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Total component instances"));

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.Prefab, k_PrefabInsightSchema },
                { AnalysisCategory.GameObjectIssue, SharedSchemas.GameObjectIssue },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override string Name => "Prefabs";

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.Prefab,
            AnalysisCategory.GameObjectIssue,
        };

        public override AnalysisSource? SupportedAnalysisSource => AnalysisSource.Assets; // Prefabs are assets, not current scene

        // Yield to the message loop every N prefabs so the in-window progress overlay can
        // repaint and the user can hit Cancel. AssetDatabase / hierarchy traversal are
        // main-thread-only, so we use await Task.Yield rather than offloading to a worker.
        const int k_YieldEveryNPrefabs = 16;

        public override async Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var analyzers = GetCompatibleAnalyzers(options);
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:Prefab a:assets", options);

            using var prefabInsightContext = new AnalysisContext(options, session);

            progress?.Start("Analyzing Prefabs", string.Empty, assetPaths.Length);

            for (var i = 0; i < assetPaths.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AnalysisResult.Cancelled;

                var assetPath = assetPaths[i];
                progress?.Advance(assetPath);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                    continue;

                CountPrefabHierarchy(prefab.transform, out var nodeCount, out var maxDepth, out var componentCount);

                var table = prefabInsightContext.GetInsightTable(AnalysisCategory.Prefab, k_PrefabInsightSchema);
                table.AddRow(assetPath, new Dictionary<string, object>
                {
                    [PrefabColumns.HierarchySize] = nodeCount,
                    [PrefabColumns.HierarchyDepth] = maxDepth,
                    [PrefabColumns.ComponentCount] = componentCount,
                });

                HierarchyTraversal.TraverseGameObjectHierarchy(prefab, string.Empty, (go, hierarchyPath) =>
                {
                    GameObjectAnalyzer.ProcessGameObjectAndComponents(go, hierarchyPath, assetPath, analyzers, options, session);
                    return true;
                });

                if ((i + 1) % k_YieldEveryNPrefabs == 0)
                    await Task.Yield();
            }

            FinalizeAnalyzers(session, analyzers);
            return AnalysisResult.Success;
        }

        static void CountPrefabHierarchy(Transform root, out int nodeCount, out int maxDepth, out int componentCount)
        {
            nodeCount = 0;
            maxDepth = 0;
            componentCount = 0;
            CountRecursive(root, 0, ref nodeCount, ref maxDepth, ref componentCount);
        }

        static void CountRecursive(Transform t, int depth, ref int nodeCount, ref int maxDepth, ref int componentCount)
        {
            nodeCount++;
            if (depth > maxDepth)
                maxDepth = depth;
            componentCount += t.gameObject.GetComponentCount();
            var n = t.childCount;
            for (var i = 0; i < n; i++)
                CountRecursive(t.GetChild(i), depth + 1, ref nodeCount, ref maxDepth, ref componentCount);
        }
    }
}
