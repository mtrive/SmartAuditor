// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_URP

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class UrpCameraAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0001 = nameof(OBJ0001);

        static readonly Descriptor StopNaNDescriptor = new Descriptor(
            OBJ0001,
            "Camera: Stop NaNs Enabled",
            Impact.Performance,
            "The Camera's <b>Stop NaNs</b> option is on. The setting adds a fullscreen GPU pass that replaces NaN values in the render output with black — it's a workaround for shader NaN propagation, not a feature, and should not be on in shipping builds.",
            "Disable <b>Stop NaNs</b> on the Camera and fix the shader or material that produces NaN values at the source."
        )
        {
            MessageFormat = "Camera on '{0}' has Stop NaNs enabled"
        };

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            if (context.Component is not Camera camera)
                return;

            // GetUniversalAdditionalCameraData() AddComponents the data if it's missing, which
            // would dirty the scene/prefab during a read-only analysis. TryGetComponent is the
            // safe read; an absent component means the (default false) stopNaN is in effect.
            if (!camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
                return;
            if (!cameraData.stopNaN)
                return;

            if (!context.IsDescriptorEnabled(StopNaNDescriptor, context.AssetPath))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.GameObjectIssue, StopNaNDescriptor.Id, camera.gameObject.name)
                .WithLocation(new Location(context.AssetPath))
                .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
            context.ReportIssue(diagnostic);
        }
    }
}
#endif
