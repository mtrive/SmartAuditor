using System;
using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class Physics2DAnalyzer : SettingsAnalyzer
    {
#if !UNITY_6000_3_OR_NEWER
        internal const string PHY0003 = nameof(PHY0003);
#endif
        internal const string PHY0004 = nameof(PHY0004);
        internal const string PHY0006 = nameof(PHY0006);

#if !UNITY_6000_3_OR_NEWER
        static readonly Descriptor AutoSyncTransformsDescriptor = new Descriptor(
            PHY0003,
            "Physics 2D: Auto Sync Transforms Enabled",
            Impact.Performance,
            "<b>Auto Sync Transforms</b> is enabled in Physics 2D Settings. Every Transform mutation triggers an immediate 2D physics-state sync before the next query or read, defeating the per-frame batching that newer Unity versions use to amortize the cost.",
            "Disable <b>Project Settings &gt; Physics 2D &gt; Auto Sync Transforms</b> and play-test. If a specific call site needs the older synchronous behavior, call <b>Physics2D.SyncTransforms()</b> explicitly at that site instead."
        );
#endif

        static readonly Descriptor DefaultLayerCollisionMatrixDescriptor = new Descriptor(
            PHY0004,
            "Physics 2D: All Layer Collision Pairs Enabled",
            Impact.Performance,
            "Every pair in the <b>Layer Collision Matrix</b> in Physics 2D Settings is ticked. The 2D physics engine considers every layer-against-every-layer collision, costing CPU on broad-phase checks that the project's actual collision design doesn't need.",
            "In <b>Project Settings &gt; Physics 2D</b>, un-tick the matrix entries for layer pairs that should never collide."
        );

        static readonly Descriptor SimulationModeDescriptor = new Descriptor(
            PHY0006,
            "Physics 2D: Simulation Mode Drives Automatic Updates",
            Impact.Performance,
            "Physics 2D <b>Simulation Mode</b> is set to <b>FixedUpdate</b> or <b>Update</b>. The 2D physics simulation runs every tick of the chosen schedule whether or not the project actually needs physics that frame.",
            "Set <b>Project Settings &gt; Physics 2D &gt; Simulation Mode</b> to <b>Script</b> and call <b>Physics2D.Simulate(deltaTime)</b> only when the project needs to advance the 2D physics simulation.")
        {
            MinimumVersion = "2020.2"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
#if !UNITY_6000_3_OR_NEWER
            if (context.IsDescriptorEnabled(AutoSyncTransformsDescriptor) &&
                Physics2D.autoSyncTransforms)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, AutoSyncTransformsDescriptor.Id)
                    .WithLocation(new Location("Project/Physics 2D"));
                context.ReportIssue(diagnostic);
            }
#endif

            if (context.IsDescriptorEnabled(DefaultLayerCollisionMatrixDescriptor) &&
                IsDefaultLayerCollisionMatrix())
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, DefaultLayerCollisionMatrixDescriptor.Id)
                    .WithLocation(new Location("Project/Physics 2D"));
                context.ReportIssue(diagnostic);
            }
            if (context.IsDescriptorEnabled(SimulationModeDescriptor) &&
                IsNotUsingSimulationModeScript())
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, SimulationModeDescriptor.Id)
                    .WithLocation(new Location("Project/Physics 2D"));
                context.ReportIssue(diagnostic);
            }
        }

        internal static bool IsDefaultLayerCollisionMatrix()
        {
            const int numLayers = 32;
            for (var i = 0; i < numLayers; ++i)
                for (var j = i; j < numLayers; ++j)
                    if (Physics2D.GetIgnoreLayerCollision(i, j))
                        return false;
            return true;
        }

        static bool IsNotUsingSimulationModeScript()
        {
            return Physics2D.simulationMode != SimulationMode2D.Script;
        }
    }
}
