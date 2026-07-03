// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_URP

using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Emits URP render-path and GPU Resident Drawer diagnostics grounded in per-scene evidence:
    /// realtime light count, repeated MeshRenderer groups, and GRD-incompatible scene features.
    /// Runs once per scene via the <c>SceneAnalyzer</c> base.
    /// </summary>
    /// <remarks>
    /// Diagnostics in this analyzer require Unity 6 / URP 17 or newer, as indicated by
    /// <c>MinimumVersion = "6000.0"</c> on each descriptor.
    /// </remarks>
    sealed class UrpRenderPathSceneAnalyzer : SceneAnalyzer
    {
        internal const string GFX0023 = nameof(GFX0023);
        internal const string GFX0024 = nameof(GFX0024);
        internal const string GFX0025 = nameof(GFX0025);

        static readonly Descriptor ForwardPlusCandidateDescriptor = new Descriptor(
            GFX0023,
            "URP: Forward Renderer Under Light and Instancing Pressure",
            Impact.Performance,
            "The active URP renderer uses <b>Forward</b>, but this scene shows signs of rendering pressure that <b>Forward+</b> addresses more efficiently: high realtime/mixed light count, many MeshRenderers sharing the same mesh and materials, or both. Forward imposes a per-object additional-light limit (default 8) and cannot use <b>GPU Resident Drawer</b> for draw-call batching.",
            "First reduce or bake lights where possible, and minimize additional-light shadow casters. Then enable <b>SRP Batcher</b> on the URP asset, set <b>GPU Resident Drawer</b> to <b>Instanced Drawing</b>, switch the renderer to <b>Forward+</b>, and set <b>BatchRendererGroup Variants</b> to <b>Keep All</b> in <b>Project Settings &gt; Graphics</b>. Profile on target hardware — Forward+ can increase GPU memory usage for the tile light grid.")
        {
            MessageFormat = "Scene '{0}' has {1} realtime/mixed lights and {2} instancing-candidate renderers; renderer uses Forward (not Forward+)",
            MinimumVersion = "6000.0"
        };

        static readonly Descriptor GrdBlockersDescriptor = new Descriptor(
            GFX0024,
            "URP: GPU Resident Drawer Blocked by Scene Features",
            Impact.Performance,
            "<b>GPU Resident Drawer</b> is enabled on the active URP asset, but this scene contains features that prevent some MeshRenderers from using the GPU Resident Drawer fast path. Affected renderers silently fall back to the standard draw path, reducing the effectiveness of the feature.",
            "Remove or reduce the blocking features on a per-renderer basis: avoid <b>MaterialPropertyBlock</b> writes (prefer per-material properties or GPU instancing), switch <b>Light Probes</b> from <b>Use Proxy Volume</b> to <b>Blend Probes</b> where acceptable. Profile the batch count before and after to confirm the improvement.")
        {
            MessageFormat = "Scene '{0}' has {1} renderers blocked from GPU Resident Drawer ({2})",
            MinimumVersion = "6000.0"
        };

        static readonly Descriptor OcclusionDisabledDescriptor = new Descriptor(
            GFX0025,
            "URP: GPU Occlusion Culling Disabled for Large Renderer Set",
            Impact.Performance,
            "<b>GPU Resident Drawer</b> is active and <b>Forward+</b> or <b>Deferred+</b> is the rendering path, but <b>GPU Occlusion Culling</b> is disabled on the renderer. This scene has many eligible MeshRenderers; enabling GPU Occlusion Culling lets the GPU discard occluded draw commands early, reducing GPU rendering work.",
            "Open the <b>Universal Renderer Data</b> asset and enable <b>GPU Occlusion Culling</b>. Disable <b>Compatibility Mode</b> (enable Render Graph) on the URP asset if it is active, as GPU Occlusion Culling requires the Render Graph path. Profile on target hardware.")
        {
            MessageFormat = "Scene '{0}' has {1} GPU Resident Drawer-eligible renderers but GPU Occlusion Culling is disabled",
            MinimumVersion = "6000.0"
        };

        [DiagnosticParameter("MinAdditionalLightsForForwardPlus", 8)]
        int m_MinAdditionalLightsForForwardPlus;

        [DiagnosticParameter("MinRepeatedRenderersForInstancing", 50)]
        int m_MinRepeatedRenderersForInstancing;

        [DiagnosticParameter("MinEligibleRenderersForOcclusion", 100)]
        int m_MinEligibleRenderersForOcclusion;

        public override void Analyze(SceneAnalysisContext context)
        {
            var assets = UrpRenderPathInventory.CollectAll();
            if (assets.Count == 0)
                return; // not a URP project

            // Determine the aggregate renderer-path state and GRD state across all URP assets.
            // For scene diagnostics, "any Forward asset" is the trigger for GFX0023.
            bool anyForwardRenderer = false;
            bool grdActive = false;
            bool occlusionEnabled = false;
            bool forwardPlusOrDeferredPlus = false;

            foreach (var asset in assets)
            {
                foreach (var renderer in asset.Renderers)
                {
                    if (renderer.PathKind == UrpRendererPathKind.Forward)
                        anyForwardRenderer = true;
                    if (renderer.IsForwardPlusOrDeferredPlus)
                        forwardPlusOrDeferredPlus = true;
                    if (renderer.GpuOcclusionCullingEnabled)
                        occlusionEnabled = true;
                }

                if (asset.GpuResidentDrawerActive)
                    grdActive = true;
            }

            var forwardPlusCandidate = context.IsDescriptorEnabled(ForwardPlusCandidateDescriptor);
            var grdBlockers = context.IsDescriptorEnabled(GrdBlockersDescriptor);
            var occlusionCheck = context.IsDescriptorEnabled(OcclusionDisabledDescriptor);

            if (!forwardPlusCandidate && !grdBlockers && !occlusionCheck)
                return;

            // Walk the scene once, collecting all evidence needed by the three diagnostics.
            int realtimeMixedLightCount = 0;
            var rendererGroups = new Dictionary<int, int>(); // mesh+material hash → count
            int materialPropertyBlockCount = 0;
            int lightProbeProxyVolumeCount = 0;
            int eligibleRendererCount = 0;

            TraverseSceneHierarchy(context, go =>
            {
                CollectLightFacts(go, ref realtimeMixedLightCount);
                CollectRendererFacts(
                    go,
                    rendererGroups,
                    ref materialPropertyBlockCount,
                    ref lightProbeProxyVolumeCount,
                    ref eligibleRendererCount);
            });

            int repeatedRendererCount = CountRepeatedRenderers(rendererGroups);
            var sceneName = context.Scene.IsValid() ? context.Scene.name : string.Empty;
            var scenePath = context.Scene.IsValid() ? context.Scene.path : string.Empty;

            if (forwardPlusCandidate && anyForwardRenderer)
            {
                bool lightPressure = realtimeMixedLightCount >= m_MinAdditionalLightsForForwardPlus;
                bool instancingPressure = repeatedRendererCount >= m_MinRepeatedRenderersForInstancing;

                if (lightPressure || instancingPressure)
                {
                    var diagnostic = Diagnostic.Create(
                            AnalysisCategory.Scene,
                            ForwardPlusCandidateDescriptor.Id,
                            sceneName,
                            realtimeMixedLightCount,
                            repeatedRendererCount)
                        .WithEvidence("RealtimeMixedLightCount", realtimeMixedLightCount)
                        .WithEvidence("RepeatedRendererCount", repeatedRendererCount)
                        .WithEvidence("LightPressure", lightPressure)
                        .WithEvidence("InstancingPressure", instancingPressure)
                        .WithLocation(new Location(scenePath));
                    context.ReportIssue(diagnostic);
                }
            }

            if (grdBlockers && grdActive)
            {
                int totalBlockers = materialPropertyBlockCount + lightProbeProxyVolumeCount;
                if (totalBlockers > 0)
                {
                    var blockerSummary = BuildBlockerSummary(materialPropertyBlockCount, lightProbeProxyVolumeCount);
                    var diagnostic = Diagnostic.Create(
                            AnalysisCategory.Scene,
                            GrdBlockersDescriptor.Id,
                            sceneName,
                            totalBlockers,
                            blockerSummary)
                        .WithEvidence("MaterialPropertyBlockCount", materialPropertyBlockCount)
                        .WithEvidence("LightProbeProxyVolumeCount", lightProbeProxyVolumeCount)
                        .WithLocation(new Location(scenePath));
                    context.ReportIssue(diagnostic);
                }
            }

            if (occlusionCheck && grdActive && forwardPlusOrDeferredPlus && !occlusionEnabled)
            {
                if (eligibleRendererCount >= m_MinEligibleRenderersForOcclusion)
                {
                    var diagnostic = Diagnostic.Create(
                            AnalysisCategory.Scene,
                            OcclusionDisabledDescriptor.Id,
                            sceneName,
                            eligibleRendererCount)
                        .WithEvidence("EligibleRendererCount", eligibleRendererCount)
                        .WithLocation(new Location(scenePath));
                    context.ReportIssue(diagnostic);
                }
            }
        }

        static void CollectLightFacts(GameObject go, ref int realtimeMixedLightCount)
        {
            if (!go.TryGetComponent<Light>(out var light))
                return;
            if (!light.enabled)
                return;
            // Directional lights count as additional lights in URP terms when there is more
            // than one; non-directional realtime/mixed lights directly stress the per-object limit.
            if (light.type == LightType.Rectangle)
                return; // baked-only in URP
            if (light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked)
                return;

            realtimeMixedLightCount++;
        }

        static void CollectRendererFacts(
            GameObject go,
            Dictionary<int, int> rendererGroups,
            ref int materialPropertyBlockCount,
            ref int lightProbeProxyVolumeCount,
            ref int eligibleRendererCount)
        {
            if (!go.TryGetComponent<MeshRenderer>(out var renderer))
                return;

            if (!go.TryGetComponent<MeshFilter>(out var meshFilter))
                return;

            var mesh = meshFilter.sharedMesh;
            if (!mesh)
                return;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return;

            // Track repeated mesh+material groups for instancing candidates.
            var groupKey = ComputeGroupKey(mesh.GetInstanceID(), materials);
            if (!rendererGroups.TryGetValue(groupKey, out var count))
                count = 0;
            rendererGroups[groupKey] = count + 1;

            // Detect GRD blockers: MaterialPropertyBlock.
            if (renderer.HasPropertyBlock())
                materialPropertyBlockCount++;

            // Detect GRD blockers: Light Probe Proxy Volume.
            if (renderer.lightProbeUsage == LightProbeUsage.UseProxyVolume ||
                renderer.probeAnchor != null)
                lightProbeProxyVolumeCount++;

            // Count renderers eligible for GPU Resident Drawer (no known blockers from this renderer).
            if (!renderer.HasPropertyBlock() &&
                renderer.lightProbeUsage != LightProbeUsage.UseProxyVolume &&
                renderer.probeAnchor == null)
                eligibleRendererCount++;
        }

        static int ComputeGroupKey(int meshInstanceId, Material[] materials)
        {
            var hash = meshInstanceId;
            foreach (var mat in materials)
            {
                if (mat != null)
                    hash = hash * 397 ^ mat.GetInstanceID();
            }
            return hash;
        }

        static int CountRepeatedRenderers(Dictionary<int, int> groups)
        {
            int total = 0;
            foreach (var pair in groups)
            {
                if (pair.Value >= 2)
                    total += pair.Value;
            }
            return total;
        }

        static string BuildBlockerSummary(int mpbCount, int lppvCount)
        {
            if (mpbCount > 0 && lppvCount > 0)
                return $"{mpbCount} with MaterialPropertyBlock, {lppvCount} with Light Probe Proxy Volume";
            if (mpbCount > 0)
                return $"{mpbCount} with MaterialPropertyBlock";
            return $"{lppvCount} with Light Probe Proxy Volume";
        }
    }
}

#endif
