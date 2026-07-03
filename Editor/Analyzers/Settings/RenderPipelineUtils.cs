using System;
using SmartAuditor.Editor.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    internal static class RenderPipelineUtils
    {
        internal static void AnalyzePipelineAssets(
            SettingsAnalysisContext context,
            Action<SettingsAnalysisContext, RenderPipelineAsset, int> analyze)
        {
            analyze(context, GraphicsSettings.defaultRenderPipeline, -1);

            var initialQualityLevel = QualitySettings.GetQualityLevel();
            try
            {
                for (var i = 0; i < QualitySettings.names.Length; ++i)
                {
                    QualitySettings.SetQualityLevel(i);
                    analyze(context, QualitySettings.renderPipeline, i);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(initialQualityLevel);
            }
        }

        internal static void ReportPipelineAssetIssue(AnalysisContext context, int qualityLevel, RenderPipelineAsset renderPipeline, string id)
        {
            if (renderPipeline == null)
                return;

            var qualityLabel = GetQualityLabel(qualityLevel);
            var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, id, renderPipeline.name, qualityLabel)
                .WithEvidence("QualityLevelIndex", qualityLevel)
                .WithLocation(new Location(qualityLevel == -1 ? "Project/Graphics" : "Project/Quality"));
            context.ReportIssue(diagnostic);
        }

        internal static string GetQualityLabel(int qualityLevel) =>
            qualityLevel == -1 ? "Default" : QualitySettings.names[qualityLevel];
    }
}
