using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Modules
{
    /// <summary>
    /// Dispatches <see cref="ScriptableObjectAnalyzer"/>-derived analyzers over every ScriptableObject
    /// asset in the project, loading each asset once and providing it via
    /// <see cref="ScriptableObjectAnalysisContext.ScriptableObject"/>.
    /// Findings are emitted under the shared <see cref="AnalysisCategory.AssetIssue"/> category,
    /// whose schema is owned by <c>AssetsModule</c>.
    /// </summary>
    sealed class ScriptableObjectsModule : AnalysisModule<ScriptableObjectAnalyzer>
    {
        public override string Name => "Scriptable Objects";

        public override IReadOnlyCollection<AnalysisCategory> AdditionalEmittedCategories =>
            new[] { AnalysisCategory.AssetIssue };

        public override AnalysisSource? SupportedAnalysisSource => AnalysisSource.Assets;

        const int k_YieldEveryNAssets = 32;

        public override async Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var analyzers = GetCompatibleAnalyzers(options);

            if (analyzers.Length == 0)
                return AnalysisResult.Success;

            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(ScriptableObject)}, a:assets", options);

            using var context = new ScriptableObjectAnalysisContext(options, session);

            progress?.Start("Analyzing ScriptableObjects", string.Empty, assetPaths.Length);

            for (var i = 0; i < assetPaths.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AnalysisResult.Cancelled;

                var assetPath = assetPaths[i];
                progress?.Advance(assetPath);

                var scriptableObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (scriptableObject == null)
                    continue;

                context.AssetPath = assetPath;
                context.ScriptableObject = scriptableObject;

                foreach (var analyzer in analyzers)
                    analyzer.Analyze(context);

                if ((i + 1) % k_YieldEveryNAssets == 0)
                    await Task.Yield();
            }

            FinalizeAnalyzers(session, analyzers);
            return AnalysisResult.Success;
        }
    }
}
