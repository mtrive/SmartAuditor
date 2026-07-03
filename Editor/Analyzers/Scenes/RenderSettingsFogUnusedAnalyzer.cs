// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Reports scenes whose Fog setting is enabled with a degenerate configuration that produces
    /// no visible fog (linear mode with end &lt;= start, or exponential mode with zero density).
    /// </summary>
    sealed class RenderSettingsFogUnusedAnalyzer : SceneAnalyzer
    {
        internal const string SCS0001 = nameof(SCS0001);

        static readonly Descriptor FogMisconfiguredDescriptor = new Descriptor(
            SCS0001,
            "RenderSettings: Fog Enabled With Degenerate Configuration",
            Impact.Performance,
            "The scene has <b>Fog</b> enabled but its range or density produces no visible result (linear mode with <b>End Distance</b> not greater than <b>Start Distance</b>, or exponential mode with <b>Density</b> at zero). Shaders still pay the fog-evaluation cost for every fragment with no visible benefit.",
            "Disable <b>Fog</b> in <b>Window &gt; Rendering &gt; Lighting &gt; Environment</b>, or set a non-degenerate <b>Start</b>/<b>End</b> range (linear) or non-zero <b>Density</b> (exponential)."
        )
        {
            MessageFormat = "Scene '{0}' has Fog enabled with a degenerate configuration"
        };

        public override void Analyze(SceneAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(FogMisconfiguredDescriptor, context.HierarchyPath))
                return;

            if (!RenderSettings.fog)
                return;

            var degenerate = IsDegenerate();
            if (!degenerate)
                return;

            var sceneName = context.Scene.IsValid() ? context.Scene.name : "<unknown scene>";
            var scenePath = context.Scene.IsValid() ? context.Scene.path : string.Empty;
            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.SceneSettings,
                    FogMisconfiguredDescriptor.Id,
                    sceneName)
                .WithLocation(new Location(scenePath))
                .WithEvidence("FogMode", RenderSettings.fogMode.ToString())
                .WithEvidence("FogStartDistance", RenderSettings.fogStartDistance)
                .WithEvidence("FogEndDistance", RenderSettings.fogEndDistance)
                .WithEvidence("FogDensity", RenderSettings.fogDensity);
            context.ReportIssue(diagnostic);
        }

        static bool IsDegenerate()
        {
            switch (RenderSettings.fogMode)
            {
                case FogMode.Linear:
                    return RenderSettings.fogEndDistance <= RenderSettings.fogStartDistance;
                case FogMode.Exponential:
                case FogMode.ExponentialSquared:
                    return RenderSettings.fogDensity == 0f;
                default:
                    return false;
            }
        }
    }
}
