// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Reports scenes whose ambient mode is Skybox but no Skybox Material is assigned.
    /// </summary>
    sealed class RenderSettingsMissingSkyboxAnalyzer : SceneAnalyzer
    {
        internal const string SCS0000 = nameof(SCS0000);

        static readonly Descriptor MissingSkyboxDescriptor = new Descriptor(
            SCS0000,
            "RenderSettings: Skybox Material Missing",
            Impact.Quality,
            "The scene's <b>Ambient Source</b> is <b>Skybox</b> but no Skybox Material is assigned. Ambient lighting will fall back to the default cubemap and the scene's skybox shader will not contribute to the lighting solution.",
            "Assign a Skybox Material in <b>Window &gt; Rendering &gt; Lighting &gt; Environment</b>, or switch <b>Ambient Source</b> to <b>Gradient</b> or <b>Color</b>."
        )
        {
            MessageFormat = "Scene '{0}' uses Skybox ambient mode but has no Skybox Material assigned"
        };

        public override void Analyze(SceneAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(MissingSkyboxDescriptor, context.HierarchyPath))
                return;

            if (RenderSettings.ambientMode != AmbientMode.Skybox)
                return;
            if (RenderSettings.skybox != null)
                return;

            var sceneName = context.Scene.IsValid() ? context.Scene.name : "<unknown scene>";
            var scenePath = context.Scene.IsValid() ? context.Scene.path : string.Empty;
            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.SceneSettings,
                    MissingSkyboxDescriptor.Id,
                    sceneName)
                .WithLocation(new Location(scenePath));
            context.ReportIssue(diagnostic);
        }
    }
}
