// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using UnityEditor.AddressableAssets;

namespace SmartAuditor.Editor.Modules
{
    internal sealed class AddressablesModule : AnalysisModule<AddressableGroupAnalyzer>
    {
        public override string Name => "Addressables";

        public override IReadOnlyCollection<AnalysisCategory> AdditionalEmittedCategories => new[]
        {
            AnalysisCategory.AssetIssue,
        };

        public override Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return Task.FromResult(AnalysisResult.Success);

            var analyzers = GetCompatibleAnalyzers(options);
            if (analyzers.Length == 0)
                return Task.FromResult(AnalysisResult.Success);

            progress?.Start("Analyzing Addressables", "Checking asset references", settings.groups.Count);

            foreach (var group in settings.groups)
            {
                if (cancellationToken.IsCancellationRequested)
                    return Task.FromResult(AnalysisResult.Cancelled);

                if (group == null)
                    continue;

                progress?.Advance(group.Name);

                using var context = new AddressableGroupAnalysisContext(options, session)
                {
                    Group = group,
                };

                foreach (var analyzer in analyzers)
                    analyzer.AnalyzeGroup(context);
            }

            FinalizeAnalyzers(session, analyzers);
            return Task.FromResult(AnalysisResult.Success);
        }
    }
}
#endif
