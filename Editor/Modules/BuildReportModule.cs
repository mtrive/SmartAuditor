using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Build;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace SmartAuditor.Editor.Modules
{
    sealed class BuildReportModule : AnalysisModule
    {
        internal static readonly InsightSchema k_BuildFileInsightSchema = new InsightSchema(
            new InsightColumn(BuildFileColumns.Description, "Source Asset", PropertyFormat.Text),
            new InsightColumn(BuildFileColumns.FileType, "File Type", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(BuildFileColumns.ImporterType, "Importer Type", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(BuildFileColumns.RuntimeType, "Runtime Type", PropertyFormat.Text, ColumnHints.Categorical, isDefaultGroup: true),
            new InsightColumn(BuildFileColumns.Size, "Size", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric),
            new InsightColumn(BuildFileColumns.SizePercent, "Size % (of Data)", PropertyFormat.Percentage),
            new InsightColumn(BuildFileColumns.BuildFile, "Build File", PropertyFormat.Text, ColumnHints.Categorical));

        class BuildAnalysisContext : AnalysisContext
        {
            public BuildAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }
            public BuildReport Report;
        }

        const string k_Unknown = "Unknown";

        internal static LastBuildReportProvider BuildReportProvider = new LastBuildReportProvider();

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.BuildFile, k_BuildFileInsightSchema }
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override string Name => "Build Report";

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.BuildFile,
        };

        public override Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(AnalysisResult.Cancelled);

            var buildReport = BuildReportProvider.GetBuildReport(options.Platform);
            if (buildReport != null)
            {
                using var context = new BuildAnalysisContext(options, session)
                {
                    Report = buildReport,
                };

                if (cancellationToken.IsCancellationRequested)
                {
                    context.Discard();
                    return Task.FromResult(AnalysisResult.Cancelled);
                }

                if (!AnalyzePackedAssets(context, cancellationToken))
                {
                    context.Discard();
                    return Task.FromResult(AnalysisResult.Cancelled);
                }

                context.Flush();
            }
            return Task.FromResult(AnalysisResult.Success);
        }

        bool AnalyzePackedAssets(BuildAnalysisContext context, CancellationToken cancellationToken)
        {
            ulong dataSize = 0;
            foreach (var packedAsset in context.Report.packedAssets)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                foreach (var assetInfo in packedAsset.contents)
                    dataSize += assetInfo.packedSize;
            }

            foreach (var packedAsset in context.Report.packedAssets)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                // note that there can be several entries for each source asset (for example, a prefab can reference a Texture, a Material and a shader)
                foreach (var content in packedAsset.contents)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    // sourceAssetPath might contain '|' which is invalid. This is due to compressed texture format names in the asset name such as DXT1|BC1
                    var assetPath = string.IsNullOrEmpty(content.sourceAssetPath) ? k_Unknown : PathUtils.ReplaceInvalidChars(content.sourceAssetPath);
                    var assetImporter = AssetImporter.GetAtPath(assetPath);
                    var description = string.IsNullOrEmpty(assetPath) ? k_Unknown : Path.GetFileNameWithoutExtension(assetPath);

                    var table = context.GetInsightTable(AnalysisCategory.BuildFile, k_BuildFileInsightSchema);
                    table.AddRow(assetPath, new Dictionary<string, object>
                    {
                        [BuildFileColumns.Description] = description,
                        [BuildFileColumns.FileType] = Path.GetExtension(assetPath)?.TrimStart('.') ?? string.Empty,
                        [BuildFileColumns.ImporterType] = assetImporter != null ? assetImporter.GetType().Name : k_Unknown,
                        [BuildFileColumns.RuntimeType] = content.type.Name,
                        [BuildFileColumns.Size] = (long)content.packedSize,
                        [BuildFileColumns.SizePercent] = Math.Round((double)content.packedSize / dataSize, 4),
                        [BuildFileColumns.BuildFile] = packedAsset.shortPath,
                    });
                }
            }

            return true;
        }
    }
}
