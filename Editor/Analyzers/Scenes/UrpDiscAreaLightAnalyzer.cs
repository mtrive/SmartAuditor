// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_URP

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class UrpDiscAreaLightAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0011 = nameof(OBJ0011);

        static readonly Descriptor DiscAreaLightDescriptor = new Descriptor(
            OBJ0011,
            "URP: Disc Area Light Unsupported",
            Impact.Compatibility | Impact.Quality,
            "The Light component is configured as a <b>Disc</b> area light, but URP only supports <b>Rectangle</b> area lights and only in <b>Baked</b> mode. The light either renders incorrectly or fails to render at all on URP targets.",
            "Change the area-light <b>Shape</b> to <b>Rectangle</b> on the Light component, or switch the light type to one URP supports at runtime (Directional, Point, Spot)."
        )
        {
            MessageFormat = "Light '{0}' is a Disc Area Light, which URP does not support"
        };

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            // Type-test first so the pipeline query only runs on Lights, not every component.
            if (context.Component is not Light light)
                return;

            if (light.type != LightType.Disc)
                return;

            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset)
                return;

            if (!context.IsDescriptorEnabled(DiscAreaLightDescriptor, context.AssetPath))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.GameObjectIssue, DiscAreaLightDescriptor.Id, light.gameObject.name)
                .WithLocation(new Location(context.AssetPath))
                .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
            context.ReportIssue(diagnostic);
        }
    }
}
#endif
