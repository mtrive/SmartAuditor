// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if UNITY_2020_1_OR_NEWER
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    internal sealed class AnisotropicFilteringAnalyzer : SettingsAnalyzer
    {
        public const string QLT0006 = nameof(QLT0006);

        static readonly Descriptor AnisotropicTexturesForcedOnDescriptor = new Descriptor(
            QLT0006,
            "Quality: Anisotropic Texture Filtering Forced On",
            Impact.Performance | Impact.Quality,
            "A Quality Level has <b>Anisotropic Textures</b> set to <b>Forced On</b> in <b>Project Settings &gt; Quality</b>. The setting forces anisotropic filtering on every texture regardless of import setting, adding GPU sample cost per fragment to textures that may not need it.",
            "Change <b>Anisotropic Textures</b> to <b>Per Texture</b> in <b>Project Settings &gt; Quality</b> so anisotropy is controlled per texture import."
        )
        {
            Platforms = new[] { BuildTarget.Android, BuildTarget.iOS, BuildTarget.Switch },
            MessageFormat = "Quality Level '{0}' has Anisotropic Texture Filtering forced on"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(AnisotropicTexturesForcedOnDescriptor))
                return;

            var originalLevel = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                if (QualitySettings.anisotropicFiltering == AnisotropicFiltering.ForceEnable)
                {
                    var diagnostic = Diagnostic.Create(
                        AnalysisCategory.ProjectSetting,
                        AnisotropicTexturesForcedOnDescriptor.Id,
                        names[i])
                        .WithLocation(new Location("Project/Quality"))
                        .WithEvidence("QualityLevelIndex", i);
                    context.ReportIssue(diagnostic);
                }
            }

            // Restore the original quality level
            QualitySettings.SetQualityLevel(originalLevel, applyExpensiveChanges: false);
        }
    }
}
#endif
