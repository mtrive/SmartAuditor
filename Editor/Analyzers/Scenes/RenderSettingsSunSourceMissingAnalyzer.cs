// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Reports scenes that contain at least one directional Light but do not assign a
    /// <c>RenderSettings.sun</c> source. Procedural skybox shaders use the sun source for
    /// sun-disk placement; without it the skybox falls back to a default direction that does
    /// not align with the scene's lighting.
    /// </summary>
    sealed class RenderSettingsSunSourceMissingAnalyzer : SceneAnalyzer
    {
        internal const string SCS0003 = nameof(SCS0003);

        static readonly Descriptor SunSourceMissingDescriptor = new Descriptor(
            SCS0003,
            "RenderSettings: Sun Source Unset",
            Impact.Quality,
            "The scene contains a directional Light but <b>RenderSettings.sun</b> is unset. Procedural skybox shaders use the sun source to place the sun disk; without it the skybox falls back to a default direction that does not match the scene's directional light.",
            "Set the scene's directional Light as the <b>Sun Source</b> in <b>Window &gt; Rendering &gt; Lighting &gt; Environment</b>."
        )
        {
            MessageFormat = "Scene '{0}' has a directional Light but no Sun Source assigned"
        };

        public override void Analyze(SceneAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(SunSourceMissingDescriptor, context.HierarchyPath))
                return;

            if (RenderSettings.sun != null)
                return;

            var hasDirectional = false;
            TraverseSceneHierarchy(context, go =>
            {
                if (hasDirectional)
                    return;
                if (go.TryGetComponent<Light>(out var light) && light.type == LightType.Directional)
                    hasDirectional = true;
            });

            if (!hasDirectional)
                return;

            var sceneName = context.Scene.IsValid() ? context.Scene.name : "<unknown scene>";
            var scenePath = context.Scene.IsValid() ? context.Scene.path : string.Empty;
            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.SceneSettings,
                    SunSourceMissingDescriptor.Id,
                    sceneName)
                .WithLocation(new Location(scenePath));
            context.ReportIssue(diagnostic);
        }
    }
}
