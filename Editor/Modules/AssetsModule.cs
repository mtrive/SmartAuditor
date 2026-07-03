using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Runs <see cref="AssetAnalyzer"/>-derived analyzers. Per-asset analyzers
    /// (<see cref="PsdFileAnalyzer"/>, <see cref="MalformedUnityYamlAnalyzer"/>,
    /// <see cref="OrphanedSerializedFieldAnalyzer"/>, <see cref="AssetImportMessageAnalyzer"/>)
    /// receive a context for every asset path. Whole-project analyzers derived from
    /// <see cref="ProjectAssetAnalyzer"/> only run in the finalize phase. Type-specific analyzers
    /// live in their per-type modules (<c>TexturesModule</c>, <c>MeshesModule</c>,
    /// <c>ScriptableObjectsModule</c>, …). The shared <see cref="AnalysisCategory.AssetIssue"/> and
    /// <see cref="AnalysisCategory.AssetImportMessage"/> schemas live in <see cref="SharedSchemas"/>;
    /// this module is just one of the producers that emits findings under those categories.
    /// </summary>
    internal sealed class AssetsModule : AnalysisModule<AssetAnalyzer>
    {
        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.AssetIssue, SharedSchemas.AssetIssue },
                { AnalysisCategory.AssetImportMessage, SharedSchemas.AssetImportMessage },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override string Name => "Assets";

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.AssetIssue,
            AnalysisCategory.AssetImportMessage,
        };

        public override AnalysisSource? SupportedAnalysisSource => AnalysisSource.Assets;

        // Yield to the message loop after this many assets so the in-window progress overlay
        // can repaint and the user can hit Cancel. Asset analyzers touch AssetDatabase / asset
        // importers (main-thread-only), so we use await Task.Yield to keep execution on the
        // main thread rather than offloading to a worker via Task.Run.
        const int k_YieldEveryNAssets = 32;

        public override async Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var analyzers = GetCompatibleAnalyzers(options);

            if (analyzers.Length == 0)
                return AnalysisResult.Success;

            // Partition: per-asset analyzers run once per path; ProjectAssetAnalyzers only run in Finalize.
            var perAssetAnalyzers = new List<AssetAnalyzer>();
            foreach (var analyzer in analyzers)
            {
                if (!(analyzer is ProjectAssetAnalyzer))
                    perAssetAnalyzers.Add(analyzer);
            }

            if (perAssetAnalyzers.Count > 0)
            {
                var assetPaths = AssetPathUtils.GetAssetPaths(options);

                progress?.Start("Analyzing Assets", string.Empty, assetPaths.Length);

                for (var i = 0; i < assetPaths.Length; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return AnalysisResult.Cancelled;

                    var assetPath = assetPaths[i];
                    progress?.Advance(assetPath);

                    using var assetAnalysisContext = new AssetAnalysisContext(options, session)
                    {
                        AssetPath = assetPath,
                    };

                    foreach (var analyzer in perAssetAnalyzers)
                        analyzer.Analyze(assetAnalysisContext);

                    if ((i + 1) % k_YieldEveryNAssets == 0)
                        await Task.Yield();
                }
            }

            FinalizeAnalyzers(session, analyzers);
            return AnalysisResult.Success;
        }
    }
}
