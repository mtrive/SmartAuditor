// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Utils
{
    internal static class GraphicsTiers
    {
        static readonly GraphicsTier[] s_GraphicsTiers = { GraphicsTier.Tier1, GraphicsTier.Tier2, GraphicsTier.Tier3 };

        internal static bool IsMixedStandardShaderQuality(BuildTarget platform)
        {
            var buildGroup = BuildPipeline.GetBuildTargetGroup(platform);
            var firstQuality = EditorGraphicsSettings.GetTierSettings(buildGroup, s_GraphicsTiers[0]).standardShaderQuality;

            for (var i = 1; i < s_GraphicsTiers.Length; i++)
            {
                var quality = EditorGraphicsSettings.GetTierSettings(buildGroup, s_GraphicsTiers[i]).standardShaderQuality;
                if (quality != firstQuality)
                    return true;
            }

            return false;
        }

        internal static bool IsMixedRenderingPath(BuildTarget platform)
        {
            return !TryGetUniformRenderingPath(platform, out _);
        }

        internal static bool IsUsingForwardRendering(BuildTarget platform)
        {
            return TryGetUniformRenderingPath(platform, out var path) && path == RenderingPath.Forward;
        }

        internal static bool IsUsingDeferredRendering(BuildTarget platform)
        {
            return TryGetUniformRenderingPath(platform, out var path) && path == RenderingPath.DeferredShading;
        }

        static bool TryGetUniformRenderingPath(BuildTarget platform, out RenderingPath path)
        {
            var buildGroup = BuildPipeline.GetBuildTargetGroup(platform);
            path = EditorGraphicsSettings.GetTierSettings(buildGroup, s_GraphicsTiers[0]).renderingPath;

            for (var i = 1; i < s_GraphicsTiers.Length; i++)
            {
                var tierPath = EditorGraphicsSettings.GetTierSettings(buildGroup, s_GraphicsTiers[i]).renderingPath;
                if (tierPath != path)
                {
                    path = default;
                    return false;
                }
            }

            return true;
        }
    }
}
