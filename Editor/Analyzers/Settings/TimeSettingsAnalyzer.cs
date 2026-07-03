using System;
using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class TimeSettingsAnalyzer : SettingsAnalyzer
    {
        internal const string PHY0007 = nameof(PHY0007);
        internal const string PHY0008 = nameof(PHY0008);

        static readonly Descriptor FixedTimestepDescriptor = new Descriptor(
            PHY0007,
            "Time: Fixed Timestep Is the Default Value",
            Impact.Performance,
            "Time Settings has <b>Fixed Timestep</b> at the Unity default of <b>0.02</b> (50 Hz). At a 30 FPS render target, FixedUpdate must run twice per render frame to keep up, and any slow frame triggers a cascade of catch-up FixedUpdate calls (the \"spiral of death\").",
            "Set <b>Fixed Timestep</b> in <b>Project Settings &gt; Time</b> slightly below the inverse of the target frame rate — <b>0.04</b> for 30 FPS (25 Hz), <b>0.0167</b> for 60 FPS (60 Hz) — so FixedUpdate runs at most once per render frame."
        );

        static readonly Descriptor MaximumAllowedTimestepDescriptor = new Descriptor(
            PHY0008,
            "Time: Maximum Allowed Timestep Is the Default Value",
            Impact.Performance,
            "Time Settings has <b>Maximum Allowed Timestep</b> at the Unity default of <b>0.1</b> (100 ms). After a slow frame, the Time Manager can spend up to 100 ms in catch-up FixedUpdate calls — pushing the next frame's time over budget and reinforcing the cycle (the \"spiral of death\").",
            "Reduce <b>Maximum Allowed Timestep</b> in <b>Project Settings &gt; Time</b> to a value the target frame rate can absorb (typically 1.5–2× the target frame time)."
        );

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (context.IsDescriptorEnabled(FixedTimestepDescriptor) &&
                UnityEngine.Time.fixedDeltaTime - 0.02f < Mathf.Epsilon)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, FixedTimestepDescriptor.Id)
                    .WithLocation(new Location("Project/Time"));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(MaximumAllowedTimestepDescriptor) &&
                UnityEngine.Time.maximumDeltaTime - 0.1f < Mathf.Epsilon)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, MaximumAllowedTimestepDescriptor.Id)
                    .WithLocation(new Location("Project/Time"));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
