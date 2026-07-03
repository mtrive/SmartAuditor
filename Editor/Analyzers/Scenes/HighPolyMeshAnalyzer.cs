// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using SmartAuditor.Editor.Utils;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class HighPolyMeshAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0005 = nameof(OBJ0005);

        static readonly Descriptor HighPolyNoLODDescriptor = new Descriptor(
            OBJ0005,
            "Mesh: High Triangle Count Without LOD Group",
            Impact.Performance,
            "A renderable mesh has more triangles than the configured threshold but no <b>LODGroup</b> component in its hierarchy. Every fragment of the mesh renders at full quality regardless of how small the object appears on screen, costing GPU time disproportionate to the rendered size.",
            "Add a <b>LODGroup</b> component to the GameObject (or an ancestor) and produce lower-polygon LOD meshes in the modeling tool, or accept the cost if the mesh is consistently rendered close to the camera."
        )
        {
            MessageFormat = "Mesh '{0}' on '{1}' has {2} triangles but no LOD Group"
        };

        [DiagnosticParameter("HighPolyMeshTriangleThreshold", 10000)]
        int m_TriangleThreshold;

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(HighPolyNoLODDescriptor, context.AssetPath))
                return;

            var component = context.Component;
            if (!TryGetSharedMesh(component, out var mesh))
                return;

            var triangleCount = MeshUtils.GetTriangleCount(mesh);

            if (triangleCount < m_TriangleThreshold)
                return;
            // Check if there's an LOD Group in the parent hierarchy
            var lodGroup = component.GetComponentInParent<LODGroup>();

            if (lodGroup != null)
                return;

            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.GameObjectIssue,
                    HighPolyNoLODDescriptor.Id,
                    mesh.name,
                    component.gameObject.name,
                    triangleCount.ToString("N0")) // Format with thousands separator
                .WithLocation(new Location(context.AssetPath))
                .WithEvidence("TriangleCount", triangleCount)
                .WithEvidence("TriangleThreshold", m_TriangleThreshold)
                .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
            context.ReportIssue(diagnostic);
        }

        static bool TryGetSharedMesh(Component component, out Mesh mesh)
        {
            mesh = null;
            switch (component)
            {
                case MeshFilter meshFilter:
                    mesh = meshFilter.sharedMesh;
                    break;
                case SkinnedMeshRenderer skinnedMeshRenderer:
                    mesh = skinnedMeshRenderer.sharedMesh;
                    break;
            }

            return mesh != null;
        }
    }
}
