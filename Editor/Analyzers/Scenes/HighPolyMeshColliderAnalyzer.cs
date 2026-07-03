// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using SmartAuditor.Editor.Utils;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class HighPolyMeshColliderAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0006 = nameof(OBJ0006);

        static readonly Descriptor HighPolyMeshColliderDescriptor = new Descriptor(
            OBJ0006,
            "MeshCollider: High Triangle Count",
            Impact.Performance,
            "A <b>MeshCollider</b> uses a mesh with more triangles than the configured threshold. PhysX builds a BVH over every triangle; raycasts, sweep tests, and overlap queries against the collider get slower as the triangle count grows, and the BVH itself adds memory cost.",
            "Replace the MeshCollider's <b>Mesh</b> with a low-poly collision proxy, or swap the MeshCollider for primitive colliders (Box / Sphere / Capsule) if the geometry allows."
        )
        {
            MessageFormat = "MeshCollider on '{0}' uses mesh '{1}' with {2} triangles"
        };

        [DiagnosticParameter("HighPolyMeshColliderTriangleThreshold", 300)]
        int m_TriangleThreshold;

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(HighPolyMeshColliderDescriptor, context.AssetPath))
                return;

            var component = context.Component;
            if (component is not MeshCollider meshCollider)
                return;
            var mesh = meshCollider.sharedMesh;
            if (mesh == null)
                return;
            var triangleCount = MeshUtils.GetTriangleCount(mesh);

            if (triangleCount < m_TriangleThreshold)
                return;

            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.GameObjectIssue,
                    HighPolyMeshColliderDescriptor.Id,
                    meshCollider.gameObject.name,
                    mesh.name,
                    triangleCount.ToString("N0")) // Format with thousands separator
                .WithLocation(new Location(context.AssetPath))
                .WithEvidence("TriangleCount", triangleCount)
                .WithEvidence("TriangleThreshold", m_TriangleThreshold)
                .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
            context.ReportIssue(diagnostic);
        }
    }
}
