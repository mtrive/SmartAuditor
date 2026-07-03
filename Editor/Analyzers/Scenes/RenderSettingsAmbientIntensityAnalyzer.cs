// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Reports scenes whose <c>RenderSettings.ambientIntensity</c> is set above a configured
    /// upper bound. The default in Unity is 1.0; values significantly above that are usually
    /// authoring artifacts (slider dragged or a unit-conversion bug).
    /// </summary>
    sealed class RenderSettingsAmbientIntensityAnalyzer : SceneAnalyzer
    {
        internal const string SCS0002 = nameof(SCS0002);

        static readonly Descriptor HighAmbientIntensityDescriptor = new Descriptor(
            SCS0002,
            "RenderSettings: Ambient Intensity Unusually High",
            Impact.Quality,
            "The scene's <b>Ambient Intensity Multiplier</b> is above the configured threshold. Unity's default is 1.0; values significantly above that are usually authoring artifacts (slider mis-drag, unit-conversion error) and produce washed-out shading.",
            "Reset <b>Ambient Intensity Multiplier</b> to 1.0 in <b>Window &gt; Rendering &gt; Lighting &gt; Environment</b>, or confirm the higher value is intentional."
        )
        {
            MessageFormat = "Scene '{0}' has Ambient Intensity Multiplier of {1:F2}",
            IsEnabledByDefault = false
        };

        [DiagnosticParameter("AmbientIntensityThreshold", 1.5f)]
        float m_Threshold;

        public override void Analyze(SceneAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(HighAmbientIntensityDescriptor, context.HierarchyPath))
                return;

            var intensity = RenderSettings.ambientIntensity;
            if (intensity <= m_Threshold)
                return;

            var sceneName = context.Scene.IsValid() ? context.Scene.name : "<unknown scene>";
            var scenePath = context.Scene.IsValid() ? context.Scene.path : string.Empty;
            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.SceneSettings,
                    HighAmbientIntensityDescriptor.Id,
                    sceneName,
                    intensity)
                .WithLocation(new Location(scenePath))
                .WithEvidence("AmbientIntensity", intensity)
                .WithEvidence("Threshold", m_Threshold);
            context.ReportIssue(diagnostic);
        }
    }
}
