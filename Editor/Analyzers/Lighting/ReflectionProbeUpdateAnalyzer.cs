// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if UNITY_2020_1_OR_NEWER

using SmartAuditor.Editor.Core;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ReflectionProbeUpdateAnalyzer : LightingAnalyzer
    {
        internal const string GFX0012 = nameof(GFX0012);

        static readonly Descriptor ReflectionProbeRealtimeUpdateDescriptor = new Descriptor(
            GFX0012,
            "Reflection Probe: Realtime Every-Frame Update",
            Impact.Performance,
            "The Reflection Probe's <b>Type</b> is <b>Realtime</b> and its <b>Refresh Mode</b> is <b>Every frame</b>. Re-rendering the probe's cubemap every frame is one of the most expensive things a probe can do, especially at higher resolutions.",
            "Set <b>Refresh Mode</b> to <b>On Awake</b> for static lighting, or to <b>Via scripting</b> if the probe only needs to update on demand."
        )
        {
            MessageFormat = "Reflection Probe '{0}' updates every frame in realtime"
        };

        public override void AnalyzeReflectionProbe(ReflectionProbeAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(ReflectionProbeRealtimeUpdateDescriptor))
                return;

            if (context.Probe.mode != ReflectionProbeMode.Realtime ||
                context.Probe.refreshMode != ReflectionProbeRefreshMode.EveryFrame)
                return;
            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    ReflectionProbeRealtimeUpdateDescriptor.Id,
                    context.Probe.name)
                .WithLocation(new Location(context.AssetPath));
            context.ReportIssue(diagnostic);
        }
    }
}
#endif
