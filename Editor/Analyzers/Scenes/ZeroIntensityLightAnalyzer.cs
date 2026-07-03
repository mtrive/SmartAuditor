// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzer that detects Light components with zero intensity, which contribute no visual output
    /// but still incur rendering overhead (shadow calculations, draw calls, etc.).
    /// </summary>
    sealed class ZeroIntensityLightAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0017 = nameof(OBJ0017);

        internal static readonly Descriptor ZeroIntensityDescriptor = new Descriptor(
            OBJ0017,
            "Light: Zero Intensity",
            Impact.Quality,
            "The Light component's <b>Intensity</b> is zero. The light contributes no illumination to the scene but still pays culling, shadow-map allocation, and per-frame setup cost.",
            "Set <b>Intensity</b> to a non-zero value, or disable / delete the Light component if it is not meant to contribute."
        )
        {
            MessageFormat = "Light '{0}' has zero intensity"
        };

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(ZeroIntensityDescriptor, context.AssetPath))
                return;

            if (context.Component is not Light light)
                return;

            if (light.intensity != 0f)
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.GameObjectIssue, ZeroIntensityDescriptor.Id, light.gameObject.name)
                .WithLocation(new Location(context.AssetPath))
                .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
            context.ReportIssue(diagnostic);
        }
    }
}
