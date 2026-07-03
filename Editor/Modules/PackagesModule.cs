using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor.PackageManager;

namespace SmartAuditor.Editor.Modules
{
    sealed class PackagesModule : AnalysisModule<PackageAnalyzer>
    {
        internal static readonly InsightSchema k_PackageInsightSchema = new InsightSchema(
            new InsightColumn(PackageColumns.Name, "Name", PropertyFormat.Text, isHidden: true),
            new InsightColumn(PackageColumns.Version, "Version", PropertyFormat.Text),
            new InsightColumn(PackageColumns.Source, "Source", PropertyFormat.Text, ColumnHints.Categorical, isDefaultGroup: true));

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.Package, k_PackageInsightSchema },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override string Name => "Packages";

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.Package,
        };

        public override IReadOnlyCollection<AnalysisCategory> AdditionalEmittedCategories =>
            new[] { AnalysisCategory.ProjectSetting };

        public override Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var analyzers = GetCompatibleAnalyzers(options);

            if (analyzers.Length == 0)
                return Task.FromResult(AnalysisResult.Success);

            var installedPackages = PackageUtils.GetClientPackages();

            progress?.Start("Analyzing Packages", string.Empty, installedPackages.Length);

            using var context = new PackageAnalysisContext(options, session);

            foreach (var package in installedPackages)
            {
                if (cancellationToken.IsCancellationRequested)
                    return Task.FromResult(AnalysisResult.Cancelled);

                progress?.Advance(package.displayName);

                context.PackageInfo = package;
                EnumerateInstalledPackages(context);

                foreach (var analyzer in analyzers)
                    analyzer.Analyze(context);
            }

            FinalizeAnalyzers(session, analyzers);
            return Task.FromResult(AnalysisResult.Success);
        }

        void EnumerateInstalledPackages(PackageAnalysisContext context)
        {
            var package = context.PackageInfo;
            var table = context.GetInsightTable(AnalysisCategory.Package, k_PackageInsightSchema);
            table.AddRow(package.assetPath, new Dictionary<string, object>
            {
                [PackageColumns.Name] = package.name,
                [PackageColumns.Version] = package.version,
                [PackageColumns.Source] = package.source.ToString(),
            });
        }
    }
}
