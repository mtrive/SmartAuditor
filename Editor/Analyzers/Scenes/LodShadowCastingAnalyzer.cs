// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzer that detects LOD Groups whose shadow casting turns on as the LOD gets more distant:
    /// a nearer, higher-detail LOD level casts no shadows while a more distant, lower-detail level does.
    /// The object's shadow then disappears as the camera approaches and the nearer LOD takes over,
    /// the reverse of the intended optimization (drop shadows on distant levels, not near ones).
    /// </summary>
    sealed class LodShadowCastingAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0020 = nameof(OBJ0020);

        internal static readonly Descriptor LodShadowCastingDescriptor = new Descriptor(
            OBJ0020,
            "LOD Group: Shadow Casting Increases With Distance",
            Impact.Quality,
            "A more distant, lower-detail LOD level has a renderer whose <b>Cast Shadows</b> is enabled while a nearer, higher-detail level casts no shadows at all. The object's shadow disappears as the camera approaches and the nearer LOD takes over, the reverse of the intended optimization where shadow casting is dropped on distant levels.",
            "Set <b>Cast Shadows</b> to <b>On</b> on the nearer LOD's renderers so the shadow is consistent across the transition, or set it to <b>Off</b> on every LOD level if the object is intentionally shadowless. Shadow casting should never turn on as a LOD becomes more distant."
        )
        {
            MessageFormat = "LOD{0} on '{1}' casts shadows while nearer LOD{2} does not"
        };

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(LodShadowCastingDescriptor, context.AssetPath))
            {
                return;
            }

            if (context.Component is not LODGroup lodGroup)
            {
                return;
            }

            var lods = lodGroup.GetLODs();

            // Walk the chain from nearest (LOD0) to most distant, remembering the nearest level that
            // has renderers but casts no shadows. A later level that does cast is the backwards case.
            // Levels with no renderers are skipped so a gap (OBJ0004 / OBJ0013's job) can't pose as a
            // casting transition.
            int nearestNonCastingLod = -1;
            for (int i = 0; i < lods.Length; i++)
            {
                if (!TryGetLodCastsShadows(lods[i], out bool castsShadows))
                {
                    continue;
                }

                if (!castsShadows)
                {
                    if (nearestNonCastingLod < 0)
                    {
                        nearestNonCastingLod = i;
                    }
                    continue;
                }

                if (nearestNonCastingLod < 0)
                {
                    continue;
                }

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.GameObjectIssue,
                        LodShadowCastingDescriptor.Id,
                        i,
                        lodGroup.gameObject.name,
                        nearestNonCastingLod)
                    .WithLocation(new Location(context.AssetPath))
                    .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                    .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                    .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId)
                    .WithEvidence("CastingLodIndex", i)
                    .WithEvidence("NearestNonCastingLodIndex", nearestNonCastingLod);
                context.ReportIssue(diagnostic);
                return; // One finding per LOD Group: the transition is a group-level property.
            }
        }

        // A LOD casts shadows if any of its non-null renderers has a shadowCastingMode other than Off.
        // The return value reports whether the level is judgeable at all (has at least one renderer);
        // an unpopulated level returns false so the caller skips it rather than treating it as non-casting.
        static bool TryGetLodCastsShadows(LOD lod, out bool castsShadows)
        {
            castsShadows = false;
            bool hasRenderer = false;
            foreach (var renderer in lod.renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                hasRenderer = true;
                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    castsShadows = true;
                    return true;
                }
            }

            return hasRenderer;
        }
    }
}
