// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class RendererMaterialAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0009 = nameof(OBJ0009);
        internal const string OBJ0010 = nameof(OBJ0010);

        static readonly Descriptor NoMaterialsDescriptor = new Descriptor(
            OBJ0009,
            "Renderer: No Materials Assigned",
            Impact.Quality,
            "The renderer's <b>Materials</b> array is null or empty. The renderer participates in scene traversal and culling but produces no draw call — and in the Editor, Unity renders the magenta error material in its place.",
            "Assign at least one Material to the renderer in the Inspector, or delete the renderer component if no draw call is wanted."
        )
        {
            MessageFormat = "{0} on '{1}' has no materials assigned"
        };

        static readonly Descriptor UnassignedMaterialDescriptor = new Descriptor(
            OBJ0010,
            "Renderer: Material Slot Unassigned",
            Impact.Quality,
            "The renderer's <b>Materials</b> array has a null entry. Unity draws the magenta error material into that slot at runtime — typically because the material asset was deleted, or because the slot was never assigned to the corresponding submesh.",
            "Assign a valid Material to the empty slot in the Inspector, or remove the slot if the mesh has fewer submeshes than the renderer expects."
        )
        {
            MessageFormat = "{0} on '{1}' has an unassigned material in slot {2}"
        };

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            // Only MeshRenderer and SkinnedMeshRenderer carry material slots in this analysis;
            // other Renderers (e.g. ParticleSystemRenderer) have their own conventions.
            var component = context.Component;
            if (component is not (MeshRenderer or SkinnedMeshRenderer))
                return;

            var noMaterialsEnabled = context.IsDescriptorEnabled(NoMaterialsDescriptor, context.AssetPath);
            var unassignedEnabled = context.IsDescriptorEnabled(UnassignedMaterialDescriptor, context.AssetPath);
            if (!noMaterialsEnabled && !unassignedEnabled)
                return;

            var renderer = (Renderer)component;
            // Renderer.sharedMaterials allocates a new Material[] on every access; cache once.
            var materials = renderer.sharedMaterials;
            var typeName = component.GetType().Name;

            if (materials == null || materials.Length == 0)
            {
                if (!noMaterialsEnabled)
                    return;
                ReportIssue(context, NoMaterialsDescriptor, typeName, component.gameObject.name);
                return;
            }

            if (!unassignedEnabled)
                return;
            for (var i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null)
                    continue;
                ReportIssue(context, UnassignedMaterialDescriptor, typeName, component.gameObject.name, i.ToString());
            }
        }

        static void ReportIssue(ComponentAnalysisContext context, Descriptor descriptor, params object[] messageArgs)
        {
            var diagnostic = Diagnostic.Create(AnalysisCategory.GameObjectIssue, descriptor.Id, messageArgs)
                .WithLocation(new Location(context.AssetPath))
                .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
            context.ReportIssue(diagnostic);
        }
    }
}
