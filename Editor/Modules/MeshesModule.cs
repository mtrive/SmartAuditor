using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Utils;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace SmartAuditor.Editor.Modules
{
    /// <summary>
    /// Analyzes Unity mesh assets and the source models that contain them.
    /// </summary>
    /// <remarks>
    /// Discovery enumerates assets that contain <see cref="Mesh"/> sub-assets. For each source asset,
    /// analyzers run in two passes: first a model-wide pass (<see cref="MeshAnalyzer.AnalyzeModel"/>)
    /// for importer- and file-level diagnostics, then a per-mesh pass (<see cref="MeshAnalyzer.AnalyzeMesh"/>)
    /// for geometry- and individual-mesh diagnostics. The mesh pass also populates the
    /// <see cref="AnalysisCategory.Mesh"/> insight table.
    ///
    /// The module owns <see cref="AnalysisCategory.Mesh"/>. Some analyzers additionally emit findings
    /// under <see cref="AnalysisCategory.AssetIssue"/>.
    /// </remarks>
    sealed class MeshesModule : AnalysisModule<MeshAnalyzer>
    {
        internal static readonly InsightSchema k_MeshInsightSchema = new InsightSchema(
            new InsightColumn(MeshColumns.VertexCount, "Vertex Count", PropertyFormat.Number, ColumnHints.Aggregatable | ColumnHints.TopNMetric),
            new InsightColumn(MeshColumns.TriangleCount, "Triangle Count", PropertyFormat.Number, ColumnHints.Aggregatable | ColumnHints.TopNMetric),
            new InsightColumn(MeshColumns.SubMeshCount, "Sub Mesh Count", PropertyFormat.Number),
            new InsightColumn(MeshColumns.Size, "Size", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Mesh Size"));

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.Mesh, k_MeshInsightSchema },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override string Name => "Meshes";

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.Mesh,
        };

        public override IReadOnlyCollection<AnalysisCategory> AdditionalEmittedCategories =>
            new[] { AnalysisCategory.AssetIssue };

        // Yield to the message loop every N assets so the in-window progress overlay can
        // repaint and the user can hit Cancel. AssetImporter / AssetDatabase are
        // main-thread-only, so we use await Task.Yield rather than offloading to a worker.
        const int k_YieldEveryNAssets = 32;

        public override async Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var analyzers = GetCompatibleAnalyzers(options);
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(Mesh)}, a:assets", options);

            using var meshContext = new MeshAnalysisContext(options, session);
            using var modelContext = new ModelAnalysisContext(options, session);

            progress?.Start("Analyzing Meshes", string.Empty, assetPaths.Length);

            for (var i = 0; i < assetPaths.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AnalysisResult.Cancelled;

                var assetPath = assetPaths[i];
                progress?.Advance(assetPath);

                // Skip paths the user has globally suppressed via a no-Id rule.
                if (options.Rules != null && options.Rules.IsPathGloballySuppressed(assetPath))
                    continue;

                var assetImporter = AssetImporter.GetAtPath(assetPath);
                // Not all meshes use the ModelImporter, which is why we just pass the AssetImporter to the analyzers to figure out.
                if (assetImporter == null)
                    continue;

                var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                var meshes = new List<Mesh>();
                foreach (var subAsset in subAssets)
                {
                    if (subAsset is Mesh mesh)
                        meshes.Add(mesh);
                }

                if (meshes.Count == 0)
                    continue;

                var modelName = Path.GetFileNameWithoutExtension(assetPath);

                // Model-level pass: invoked once per source asset for importer / model-wide diagnostics.
                modelContext.Name = modelName;
                modelContext.AssetPath = assetPath;
                modelContext.Importer = assetImporter;
                modelContext.Meshes = meshes;

                foreach (var analyzer in analyzers)
                {
                    analyzer.AnalyzeModel(modelContext);
                }

                // Per-mesh pass: invoked once per Mesh sub-asset for geometry / per-mesh diagnostics.
                foreach (var mesh in meshes)
                {
                    var meshName = mesh.name;
                    if (string.IsNullOrEmpty(meshName))
                        meshName = modelName;

                    // TODO: the size returned by the profiler is not the exact size on the target platform. Needs to be fixed.
                    var size = Profiler.GetRuntimeMemorySizeLong(mesh);

                    meshContext.Name = meshName;
                    meshContext.Mesh = mesh;
                    meshContext.Importer = assetImporter;
                    meshContext.Size = size;

                    var table = meshContext.GetInsightTable(AnalysisCategory.Mesh, k_MeshInsightSchema);
                    table.AddRow(assetPath, new Dictionary<string, object>
                    {
                        [MeshColumns.VertexCount] = mesh.vertexCount,
                        [MeshColumns.TriangleCount] = MeshUtils.GetTriangleCount(mesh),
                        [MeshColumns.SubMeshCount] = mesh.subMeshCount,
                        [MeshColumns.Size] = size,
                    });

                    foreach (var analyzer in analyzers)
                    {
                        analyzer.AnalyzeMesh(meshContext);
                    }
                }

                if ((i + 1) % k_YieldEveryNAssets == 0)
                    await Task.Yield();
            }

            FinalizeAnalyzers(session, analyzers);
            return AnalysisResult.Success;
        }
    }
}
