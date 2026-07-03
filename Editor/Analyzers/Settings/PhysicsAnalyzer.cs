using System;
using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class PhysicsAnalyzer : SettingsAnalyzer
    {
#if !UNITY_6000_3_OR_NEWER
        internal const string PHY0001 = nameof(PHY0001);
#endif
        internal const string PHY0002 = nameof(PHY0002);
        internal const string PHY0005 = nameof(PHY0005);

#if !UNITY_6000_3_OR_NEWER
        static readonly Descriptor AutoSyncTransformsDescriptor = new Descriptor(
            PHY0001,
            "Physics: Auto Sync Transforms Enabled",
            Impact.Performance,
            "<b>Auto Sync Transforms</b> is enabled in Physics Settings. Every Transform mutation triggers an immediate physics-state sync before the next physics query or read, defeating the per-frame batching that newer Unity versions use to amortize the cost.",
            "Disable <b>Project Settings &gt; Physics &gt; Auto Sync Transforms</b> and play-test. If a specific call site needs the older synchronous behavior, call <b>Physics.SyncTransforms()</b> explicitly at that site instead."
        );
#endif

        static readonly Descriptor DefaultLayerCollisionMatrixDescriptor = new Descriptor(
            PHY0002,
            "Physics: All Layer Collision Pairs Enabled",
            Impact.Performance,
            "Every pair in the <b>Layer Collision Matrix</b> in Physics Settings is ticked. The 3D physics engine considers every layer-against-every-layer collision, costing CPU on broad-phase checks that the project's actual collision design doesn't need.",
            "In <b>Project Settings &gt; Physics</b>, un-tick the matrix entries for layer pairs that should never collide."
        );

        static readonly Descriptor ReuseCollisionCallbacksDescriptor = new Descriptor(
            PHY0005,
            "Physics: Reuse Collision Callbacks Disabled",
            Impact.Memory,
            "<b>Reuse Collision Callbacks</b> in Physics Settings is off. Every <b>OnCollision*</b> callback allocates a new managed <b>Collision</b> object, generating per-collision GC pressure during gameplay.",
            "Enable <b>Project Settings &gt; Physics &gt; Reuse Collision Callbacks</b>. Unity then reuses a single <b>Collision</b> instance across callbacks, eliminating the allocation but requiring callbacks to not store the reference for use after the call returns.")
        {
            MinimumVersion = "2018.3"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
#if !UNITY_6000_3_OR_NEWER
            if (context.IsDescriptorEnabled(AutoSyncTransformsDescriptor) &&
                Physics.autoSyncTransforms)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, AutoSyncTransformsDescriptor.Id)
                    .WithLocation(new Location("Project/Physics"));
                context.ReportIssue(diagnostic);
            }
#endif

            if (context.IsDescriptorEnabled(DefaultLayerCollisionMatrixDescriptor) &&
                IsDefaultLayerCollisionMatrix())
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, DefaultLayerCollisionMatrixDescriptor.Id)
                    .WithLocation(new Location("Project/Physics"));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(ReuseCollisionCallbacksDescriptor) &&
                ReuseCollisionCallbacksDescriptor.IsApplicable(context.Options) &&
                !Physics.reuseCollisionCallbacks)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, ReuseCollisionCallbacksDescriptor.Id)
                    .WithLocation(new Location("Project/Physics"));
                context.ReportIssue(diagnostic);
            }
        }

        internal static bool IsDefaultLayerCollisionMatrix()
        {
            const int numLayers = 32;
            for (var i = 0; i < numLayers; ++i)
                for (var j = i; j < numLayers; ++j)
                    if (Physics.GetIgnoreLayerCollision(i, j))
                        return false;
            return true;
        }
    }
}
