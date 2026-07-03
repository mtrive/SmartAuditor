// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Utils;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Modules
{
    sealed class LightingModule : AnalysisModule<LightingAnalyzer>
    {
        public override string Name => "Lighting";

        public override IReadOnlyCollection<AnalysisCategory> AdditionalEmittedCategories =>
            new[] { AnalysisCategory.AssetIssue };

        public override Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var analyzers = GetCompatibleAnalyzers(options);

            var probePaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(ReflectionProbe)}, a:assets", options);
            var lightingSettingsPaths = AssetPathUtils.GetAssetPathsByFilter($"t:LightingSettings, a:assets", options);

            progress?.Start("Analyzing Lighting Assets", string.Empty,
                probePaths.Length + lightingSettingsPaths.Length);

            foreach (var probePath in probePaths)
            {
                if (cancellationToken.IsCancellationRequested)
                    return Task.FromResult(AnalysisResult.Cancelled);
                progress?.Advance(probePath);

                var probe = AssetDatabase.LoadAssetAtPath<ReflectionProbe>(probePath);
                if (probe == null)
                    continue;

                using var context = new ReflectionProbeAnalysisContext(options, session)
                {
                    Name = probe.name,
                    AssetPath = probePath,
                    Probe = probe,
                    Resolution = probe.resolution,
                };

                foreach (var analyzer in analyzers)
                    analyzer.AnalyzeReflectionProbe(context);
            }

            foreach (var lightingSettingsPath in lightingSettingsPaths)
            {
                if (cancellationToken.IsCancellationRequested)
                    return Task.FromResult(AnalysisResult.Cancelled);
                progress?.Advance(lightingSettingsPath);

                var lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(lightingSettingsPath);
                if (lightingSettings == null)
                    continue;

                using var context = new LightingSettingsAnalysisContext(options, session)
                {
                    Name = $"Lighting Settings ({Path.GetFileNameWithoutExtension(lightingSettingsPath)})",
                    AssetPath = lightingSettingsPath,
                    Settings = lightingSettings,
                };

                foreach (var analyzer in analyzers)
                    analyzer.AnalyzeLightingSettings(context);
            }

            FinalizeAnalyzers(session, analyzers);
            return Task.FromResult(AnalysisResult.Success);
        }
    }
}
