// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using UnityEngine;

namespace SmartAuditor.Editor.Utils
{
    internal static class MeshUtils
    {
        /// <summary>
        /// Returns the total triangle count across all triangle-topology submeshes of the mesh.
        /// Allocation-free: uses <see cref="Mesh.GetIndexCount"/> rather than <see cref="Mesh.triangles"/>,
        /// which would allocate a managed array on every access.
        /// </summary>
        public static int GetTriangleCount(Mesh mesh)
        {
            var triangleCount = 0;
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) != MeshTopology.Triangles)
                    continue;

                triangleCount += (int)(mesh.GetIndexCount(i) / 3);
            }
            return triangleCount;
        }
    }
}
