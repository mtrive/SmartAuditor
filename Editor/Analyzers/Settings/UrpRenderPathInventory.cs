// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_URP

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SmartAuditor.Editor.Analyzers
{
    internal enum UrpRendererPathKind
    {
        Forward,
        ForwardPlus,
        Deferred,
        DeferredPlus,
        Unknown,
    }

    internal enum UrpGpuResidentDrawerMode
    {
        /// <summary>GPU Resident Drawer is off.</summary>
        Disabled,
        /// <summary>GPU Resident Drawer uses instanced drawing.</summary>
        InstancedDrawing,
        /// <summary>The URP version in use does not expose this setting.</summary>
        Unknown,
    }

    /// <summary>
    /// Render-path and feature facts for a single Universal Renderer Data asset.
    /// </summary>
    internal readonly struct UrpRendererInfo
    {
        internal readonly string Name;
        internal readonly UrpRendererPathKind PathKind;
        internal readonly bool GpuOcclusionCullingEnabled;

        internal UrpRendererInfo(string name, UrpRendererPathKind pathKind, bool gpuOcclusionCullingEnabled)
        {
            Name = name;
            PathKind = pathKind;
            GpuOcclusionCullingEnabled = gpuOcclusionCullingEnabled;
        }

        internal bool IsForwardPlusOrDeferredPlus =>
            PathKind == UrpRendererPathKind.ForwardPlus || PathKind == UrpRendererPathKind.DeferredPlus;

        internal bool IsDeferred =>
            PathKind == UrpRendererPathKind.Deferred || PathKind == UrpRendererPathKind.DeferredPlus;
    }

    /// <summary>
    /// Render-pipeline facts for a single (URP asset, quality level) pair.
    /// </summary>
    internal readonly struct UrpAssetInfo
    {
        internal readonly string Name;

        /// <summary>-1 for the default Graphics setting; 0-based index into QualitySettings.names otherwise.</summary>
        internal readonly int QualityLevel;

        internal readonly IReadOnlyList<UrpRendererInfo> Renderers;
        internal readonly bool HdrEnabled;
        internal readonly int MsaaSampleCount;
        internal readonly bool SrpBatcherEnabled;
        internal readonly UrpGpuResidentDrawerMode GpuResidentDrawerMode;

        /// <summary>True when render graph is disabled (compatibility / legacy mode).</summary>
        internal readonly bool CompatibilityModeEnabled;

        internal UrpAssetInfo(
            string name,
            int qualityLevel,
            IReadOnlyList<UrpRendererInfo> renderers,
            bool hdrEnabled,
            int msaaSampleCount,
            bool srpBatcherEnabled,
            UrpGpuResidentDrawerMode gpuResidentDrawerMode,
            bool compatibilityModeEnabled)
        {
            Name = name;
            QualityLevel = qualityLevel;
            Renderers = renderers;
            HdrEnabled = hdrEnabled;
            MsaaSampleCount = msaaSampleCount;
            SrpBatcherEnabled = srpBatcherEnabled;
            GpuResidentDrawerMode = gpuResidentDrawerMode;
            CompatibilityModeEnabled = compatibilityModeEnabled;
        }

        internal bool GpuResidentDrawerActive => GpuResidentDrawerMode == UrpGpuResidentDrawerMode.InstancedDrawing;

        internal bool AllRenderersForwardPlusOrDeferredPlus()
        {
            if (Renderers.Count == 0)
                return false;
            foreach (var r in Renderers)
            {
                if (!r.IsForwardPlusOrDeferredPlus)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Collects render-path facts from every URP asset reachable via Graphics and Quality settings.
    /// All reads are non-mutating: uses <c>QualitySettings.GetRenderPipelineAssetAt</c> rather than
    /// <c>SetQualityLevel</c> toggling.
    /// </summary>
    internal static class UrpRenderPathInventory
    {
        /// <summary>
        /// Returns one entry per (URP asset, quality level) pair. The same physical asset referenced
        /// at multiple quality levels produces multiple entries, mirroring the convention in
        /// <c>RenderPipelineUtils.AnalyzePipelineAssets</c> so diagnostics can cite the specific level.
        /// </summary>
        internal static List<UrpAssetInfo> CollectAll()
        {
            var result = new List<UrpAssetInfo>();
            TryCollect(GraphicsSettings.defaultRenderPipeline, -1, result);
            for (int i = 0; i < QualitySettings.names.Length; i++)
                TryCollect(QualitySettings.GetRenderPipelineAssetAt(i), i, result);
            return result;
        }

        /// <summary>
        /// Returns <c>true</c> when deferred passes may execute anywhere in the project, or when no
        /// URP assets are configured at all (non-URP project — caller should stay quiet).
        /// Returns <c>false</c> only when URP assets exist and none of their renderers uses a deferred
        /// path, meaning deferred passes will never run.
        /// </summary>
        internal static bool DeferredPassesMayRun()
        {
            var assets = CollectAll();
            if (assets.Count == 0)
                return true; // non-URP project — stay quiet

            foreach (var asset in assets)
                foreach (var renderer in asset.Renderers)
                    if (renderer.IsDeferred)
                        return true;

            return false;
        }

        // ---------------------------------------------------------------------------------
        // Implementation
        // ---------------------------------------------------------------------------------

        static void TryCollect(RenderPipelineAsset asset, int qualityLevel, List<UrpAssetInfo> result)
        {
            if (!(asset is UniversalRenderPipelineAsset urpAsset))
                return;

            var so = new SerializedObject(urpAsset);
            var renderers = CollectRenderers(so);
            var grdMode = ReadGpuResidentDrawerMode(so);
            var compatMode = ReadCompatibilityMode(so);

            result.Add(new UrpAssetInfo(
                urpAsset.name,
                qualityLevel,
                renderers,
                urpAsset.supportsHDR,
                urpAsset.msaaSampleCount,
                urpAsset.useSRPBatcher,
                grdMode,
                compatMode));
        }

        static List<UrpRendererInfo> CollectRenderers(SerializedObject urpAssetSo)
        {
            var result = new List<UrpRendererInfo>();
            var list = urpAssetSo.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray)
                return result;

            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                if (element == null)
                    continue;

                var dataRef = element.objectReferenceValue;
                if (!dataRef)
                    continue;

                var dataSo = new SerializedObject(dataRef);
                var renderingMode = dataSo.FindProperty("m_RenderingMode");
                if (renderingMode == null)
                    continue;

                var pathKind = ParsePathKind(renderingMode);
                var occlusion = ReadGpuOcclusionCulling(dataSo);
                result.Add(new UrpRendererInfo(dataRef.name, pathKind, occlusion));
            }

            return result;
        }

        static UrpRendererPathKind ParsePathKind(SerializedProperty renderingMode)
        {
            // Compare by enum name rather than integer index so newly added enum values (e.g.
            // Deferred+) are handled without requiring an index update in this file.
            var names = renderingMode.enumNames;
            var index = renderingMode.enumValueIndex;
            if (index < 0 || names == null || index >= names.Length)
                return UrpRendererPathKind.Unknown;

            return ParseRendererPathKind(names[index]);
        }

        /// <summary>
        /// Maps a serialized <c>m_RenderingMode</c> enum-name string to the corresponding
        /// <see cref="UrpRendererPathKind"/>. Exposed as <c>internal</c> for unit-test access.
        /// </summary>
        internal static UrpRendererPathKind ParseRendererPathKind(string enumName)
        {
            if (string.Equals(enumName, "Forward", StringComparison.OrdinalIgnoreCase))
                return UrpRendererPathKind.Forward;
            if (string.Equals(enumName, "ForwardPlus", StringComparison.OrdinalIgnoreCase))
                return UrpRendererPathKind.ForwardPlus;
            if (string.Equals(enumName, "Deferred", StringComparison.OrdinalIgnoreCase))
                return UrpRendererPathKind.Deferred;
            // Unity may spell Deferred+ as "DeferredPlus" or "Deferred+" depending on version.
            if (string.Equals(enumName, "DeferredPlus", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(enumName, "Deferred+", StringComparison.OrdinalIgnoreCase))
                return UrpRendererPathKind.DeferredPlus;

            return UrpRendererPathKind.Unknown;
        }

        static UrpGpuResidentDrawerMode ReadGpuResidentDrawerMode(SerializedObject urpAssetSo)
        {
            // m_GpuResidentDrawerMode: 0=Disabled, 1=InstancedDrawing. Absent on URP versions
            // that predate the GPU Resident Drawer feature (pre-Unity 6).
            var prop = urpAssetSo.FindProperty("m_GpuResidentDrawerMode");
            if (prop == null)
                return UrpGpuResidentDrawerMode.Unknown;

            return prop.enumValueIndex == 1
                ? UrpGpuResidentDrawerMode.InstancedDrawing
                : UrpGpuResidentDrawerMode.Disabled;
        }

        static bool ReadGpuOcclusionCulling(SerializedObject rendererDataSo)
        {
            // m_UseGPUOcclusionCulling lives on UniversalRendererData; absent on older URP versions.
            var prop = rendererDataSo.FindProperty("m_UseGPUOcclusionCulling");
            return prop != null && prop.boolValue;
        }

        static bool ReadCompatibilityMode(SerializedObject urpAssetSo)
        {
            // URP 14+ added Render Graph. Compatibility mode = render graph disabled.
            // m_UseRenderGraph: true = render graph active, false = compatibility / legacy mode.
            var prop = urpAssetSo.FindProperty("m_UseRenderGraph");
            if (prop != null)
                return !prop.boolValue;

            return false; // field absent → render graph not yet available in this URP version
        }
    }
}

#endif
