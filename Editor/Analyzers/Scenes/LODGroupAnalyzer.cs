// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class LODGroupAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0004 = nameof(OBJ0004);
        internal const string OBJ0012 = nameof(OBJ0012);
        internal const string OBJ0013 = nameof(OBJ0013);

        static readonly Descriptor EmptyLODGroupDescriptor = new Descriptor(
            OBJ0004,
            "LOD Group: No Renderers Assigned",
            Impact.Quality,
            "The LOD Group has no non-null renderers across any of its LOD levels. The component still pays the per-frame screen-space check and culling-decision cost, while contributing no rendering or culling benefit.",
            "Assign at least one renderer to one of the LOD levels in the LOD Group Inspector, or remove the LOD Group component."
        )
        {
            MessageFormat = "LOD Group on '{0}' has no renderers across its {1} LOD levels"
        };

        static readonly Descriptor InsufficientLODLevelsDescriptor = new Descriptor(
            OBJ0012,
            "LOD Group: Fewer Than Recommended LOD Levels",
            Impact.Quality,
            "The LOD Group has fewer LOD levels than the configured minimum. A single-LOD group still pays the LOD Group's per-frame screen-space cost but cannot actually switch between meshes.",
            "Add LOD levels with progressively lower polygon counts in the LOD Group Inspector, or remove the LOD Group component if a single mesh suffices."
        )
        {
            MessageFormat = "LOD Group on '{0}' has only {1} LOD level(s)"
        };

        [DiagnosticParameter("LODGroupMinimumLevels", 2)]
        int m_MinimumLODLevels;

        static readonly Descriptor NullRendererLODDescriptor = new Descriptor(
            OBJ0013,
            "LOD Group: Null Renderer in Non-Lowest LOD",
            Impact.Quality,
            "An LOD level above the lowest has a null entry in its renderer list. Unity pops out the slot when the LOD transitions in, producing visual gaps and runtime warnings.",
            "Assign a valid renderer to the empty slot, or remove the slot from the LOD level in the LOD Group Inspector."
        )
        {
            MessageFormat = "LOD{0} on '{1}' has a null renderer entry"
        };

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            var component = context.Component;
            if (component is not LODGroup lodGroup)
                return;

            var lods = lodGroup.GetLODs();

            // Count non-null renderers across all LOD levels. A level with one empty slot has
            // Length == 1 but contributes nothing visually. This count is shared by OBJ0004 and OBJ0013.
            int renderersCount = 0;
            foreach (var lod in lods)
            {
                foreach (var renderer in lod.renderers)
                {
                    if (renderer != null)
                        renderersCount++;
                }
            }

            // Check if any LOD level has renderers.
            if (context.IsDescriptorEnabled(EmptyLODGroupDescriptor, context.AssetPath))
            {
                if (renderersCount == 0)
                {
                    var diagnostic = Diagnostic.Create(
                        AnalysisCategory.GameObjectIssue,
                        EmptyLODGroupDescriptor.Id,
                        component.gameObject.name,
                        lods.Length)
                        .WithLocation(new Location(context.AssetPath))
                        .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                        .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                        .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
                    context.ReportIssue(diagnostic);
                }
            }

            // Check if LODGroup has fewer than the minimum recommended LOD levels
            if (context.IsDescriptorEnabled(InsufficientLODLevelsDescriptor, context.AssetPath))
            {
                if (lods.Length < m_MinimumLODLevels)
                {
                    var diagnostic = Diagnostic.Create(
                        AnalysisCategory.GameObjectIssue,
                        InsufficientLODLevelsDescriptor.Id,
                        component.gameObject.name,
                        lods.Length)
                        .WithLocation(new Location(context.AssetPath))
                        .WithEvidence("LODLevelCount", lods.Length)
                        .WithEvidence("MinimumLODLevels", m_MinimumLODLevels)
                        .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                        .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                        .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
                    context.ReportIssue(diagnostic);
                }
            }

            // Check for null renderers in non-lowest LOD levels
            if (!context.IsDescriptorEnabled(NullRendererLODDescriptor, context.AssetPath))
                return;
            if (lods.Length <= 0)
                return;
            // A fully-empty group is OBJ0004's territory; skip OBJ0013 to avoid double-reporting.
            if (renderersCount == 0)
                return;
            int lowestLodIndex = lods.Length - 1;
            for (int i = 0; i < lods.Length; i++)
            {
                var lod = lods[i];
                // Only check non-lowest LODs
                if (i >= lowestLodIndex)
                    continue;
                foreach (var renderer in lod.renderers)
                {
                    if (renderer != null)
                        continue;

                    var diagnostic = Diagnostic.Create(
                            AnalysisCategory.GameObjectIssue,
                            NullRendererLODDescriptor.Id,
                            i,
                            component.gameObject.name)
                        .WithLocation(new Location(context.AssetPath))
                        .WithEvidence("LodIndex", i)
                        .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                        .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                        .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
                    context.ReportIssue(diagnostic);
                    break; // Report once per LOD level
                }
            }
        }
    }
}
