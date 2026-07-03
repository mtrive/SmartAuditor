// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using SmartAuditor.Editor.Utils;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class LodTriangleRatioAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0007 = nameof(OBJ0007);

        static readonly Descriptor LodTriangleRatioDescriptor = new Descriptor(
            OBJ0007,
            "LOD Group: Insufficient Triangle Reduction",
            Impact.Performance,
            "A LOD level's mesh doesn't have enough fewer triangles than the previous LOD to justify the LOD switch. LOD switching pays CPU cost (frustum culling, distance check, draw-call setup) and GPU memory cost (two similar meshes loaded), and produces no rendering saving when the reduction is small.",
            "Reduce the triangle count of the lower-quality LOD mesh below the configured minimum reduction percentage, or remove the LOD level entirely if the source mesh isn't suitable for further decimation."
        )
        {
            MessageFormat = "LOD{0} on '{1}' has insufficient triangle reduction"
        };

        [DiagnosticParameter("LODMinimumReductionThreshold", 20)]
        int m_MinimumReductionPercent; // Minimum reduction percentage required (e.g., 30 means at least 30% reduction)

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(LodTriangleRatioDescriptor, context.AssetPath))
                return;

            var component = context.Component;
            if (component is not LODGroup lodGroup)
                return;

            var lods = lodGroup.GetLODs();

            // Need at least 2 LODs to compare reduction
            if (lods.Length < 2)
                return;

            // Check each LOD level against its previous (higher quality) level
            for (int i = 1; i < lods.Length; i++)
            {
                var previousLOD = lods[i - 1];
                var currentLOD = lods[i];

                var previousTriangles = GetTotalTriangles(previousLOD);
                var currentTriangles = GetTotalTriangles(currentLOD);

                // Skip if previous LOD has no triangles (prevents division by zero)
                if (previousTriangles == 0)
                    continue;

                // Lower LODs should have fewer triangles than higher LODs
                // Report if the current LOD has not been reduced enough compared to the previous LOD
                // Calculate reduction percentage: how much was reduced from previous LOD
                float reductionPercentage = ((previousTriangles - currentTriangles) * 100f) / previousTriangles;

                // Report if the reduction is below the threshold (insufficient reduction)
                if (reductionPercentage >= m_MinimumReductionPercent)
                    continue;

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.GameObjectIssue,
                        LodTriangleRatioDescriptor.Id,
                        i,
                        lodGroup.gameObject.name)
                    .WithLocation(new Location(context.AssetPath))
                    .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                    .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                    .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId)
                    .WithEvidence("LodIndex", i)
                    .WithEvidence("TriangleCount", currentTriangles)
                    .WithEvidence("PreviousTriangleCount", previousTriangles)
                    .WithEvidence("ReductionPercent", reductionPercentage)
                    .WithEvidence("MinimumReductionPercent", m_MinimumReductionPercent);
                context.ReportIssue(diagnostic);
            }
        }

        int GetTotalTriangles(LOD lod)
        {
            var totalTriangles = 0;
            foreach (var renderer in lod.renderers)
            {
                if (renderer == null)
                    continue;

                if (!TryGetSharedMesh(renderer, out var mesh))
                    continue;

                totalTriangles += MeshUtils.GetTriangleCount(mesh);
            }
            return totalTriangles;
        }

        static bool TryGetSharedMesh(Renderer renderer, out Mesh mesh)
        {
            mesh = null;
            switch (renderer)
            {
                case SkinnedMeshRenderer skinnedMeshRenderer:
                    mesh = skinnedMeshRenderer.sharedMesh;
                    break;
                default:
                    if (renderer.TryGetComponent<MeshFilter>(out var meshFilter))
                        mesh = meshFilter.sharedMesh;
                    break;
            }

            return mesh != null;
        }
    }
}
