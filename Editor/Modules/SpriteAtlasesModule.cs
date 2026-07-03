using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Utils;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine.U2D;

namespace SmartAuditor.Editor.Modules
{
    sealed class SpriteAtlasesModule : AnalysisModule<SpriteAtlasAnalyzer>
    {
        internal static readonly InsightSchema k_SpriteAtlasInsightSchema = new InsightSchema(
            new InsightColumn(SpriteAtlasColumns.SpriteCount, "Sprite Count", PropertyFormat.Number, ColumnHints.Aggregatable),
            new InsightColumn(SpriteAtlasColumns.Resolution, "Resolution", PropertyFormat.Resolution, longName: "Atlas Texture Resolution"));

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.SpriteAtlas, k_SpriteAtlasInsightSchema },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override string Name => "Sprite Atlases";

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.SpriteAtlas,
        };

        public override IReadOnlyCollection<AnalysisCategory> AdditionalEmittedCategories =>
            new[] { AnalysisCategory.AssetIssue };

        // Yield to the message loop every N atlases so the in-window progress overlay can
        // repaint and the user can hit Cancel. AssetDatabase is main-thread-only.
        const int k_YieldEveryNAtlases = 16;

        public override async Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var analyzers = GetCompatibleAnalyzers(options);
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(SpriteAtlas)}, a:assets", options);

            using var context = new SpriteAtlasAnalysisContext(options, session);

            progress?.Start("Analyzing Sprite Atlases", string.Empty, assetPaths.Length);

            for (var i = 0; i < assetPaths.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AnalysisResult.Cancelled;

                var assetPath = assetPaths[i];
                progress?.Advance(assetPath);

                context.AssetPath = assetPath;

                var spriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(assetPath);
                if (spriteAtlas == null)
                    continue;

                var spriteCount = spriteAtlas.spriteCount;
                var resolution = TextureUtils.GetAtlasResolution(spriteAtlas);

                var table = context.GetInsightTable(AnalysisCategory.SpriteAtlas, k_SpriteAtlasInsightSchema);
                table.AddRow(assetPath, new Dictionary<string, object>
                {
                    [SpriteAtlasColumns.SpriteCount] = spriteCount,
                    [SpriteAtlasColumns.Resolution] = resolution,
                });

                foreach (var analyzer in analyzers)
                    analyzer.Analyze(context);

                if ((i + 1) % k_YieldEveryNAtlases == 0)
                    await Task.Yield();
            }

            FinalizeAnalyzers(session, analyzers);
            return AnalysisResult.Success;
        }
    }
}
