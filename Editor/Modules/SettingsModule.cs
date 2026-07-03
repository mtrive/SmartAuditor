using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Modules
{
    sealed class SettingsModule : AnalysisModule<SettingsAnalyzer>
    {
        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.ProjectSetting, SharedSchemas.ProjectSetting },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override string Name => "Settings";

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.ProjectSetting,
        };

        public override Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var analyzers = GetCompatibleAnalyzers(options);

            using var context = new SettingsAnalysisContext(options, session);

            progress?.Start("Analyzing Settings", string.Empty, analyzers.Length);

            foreach (var analyzer in analyzers)
            {
                if (cancellationToken.IsCancellationRequested)
                    return Task.FromResult(AnalysisResult.Cancelled);

                analyzer.Analyze(context);
            }

            FinalizeAnalyzers(session, analyzers);
            return Task.FromResult(AnalysisResult.Success);
        }
    }
}
