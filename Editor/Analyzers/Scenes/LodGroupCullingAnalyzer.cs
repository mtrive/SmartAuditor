// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    internal sealed class LodGroupCullingAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0015 = nameof(OBJ0015);

        static readonly Descriptor LodGroupZeroScreenPercentageDescriptor = new Descriptor
            (
            OBJ0015,
            "LOD Group: Zero Culling Screen Percentage",
            Impact.Performance,
            "The lowest-quality LOD level's <b>Screen Percentage</b> is at 0. The LOD Group never culls the object — it continues to render the lowest-LOD mesh regardless of how small the object appears on screen.",
            "Set the lowest LOD level's <b>Screen Percentage</b> to a small positive value (typically 2–5%) in the LOD Group Inspector so the object can be culled when too small to contribute meaningfully."
            )
        {
            MessageFormat = "LOD Group on '{0}' has a culling threshold of 0"
        };

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(LodGroupZeroScreenPercentageDescriptor, context.AssetPath))
                return;

            var component = context.Component;
            if (component is not LODGroup lodGroup)
                return;

            var lods = lodGroup.GetLODs();
            if (lods.Length == 0)
                return;

            // Check if the last LOD (lowest quality) has a screen percentage of 0
            var lastLod = lods[lods.Length - 1];
            if (!Mathf.Approximately(lastLod.screenRelativeTransitionHeight, 0f))
                return;

            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.GameObjectIssue,
                    LodGroupZeroScreenPercentageDescriptor.Id,
                    component.gameObject.name)
                .WithLocation(new Location(context.AssetPath))
                .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
            context.ReportIssue(diagnostic);
        }
    }
}
