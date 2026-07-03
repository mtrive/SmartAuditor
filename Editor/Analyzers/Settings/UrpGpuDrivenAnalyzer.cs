// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_URP

using SmartAuditor.Editor.Core;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Checks GPU Resident Drawer and GPU Occlusion Culling configuration in URP assets.
    /// </summary>
    /// <remarks>
    /// All three diagnostics require Unity 6 / URP 17 or newer. On earlier versions the GRD and
    /// GPU Occlusion Culling features do not exist; the serialized properties are absent from the
    /// URP asset and the analyzer produces no findings.
    /// </remarks>
    sealed class UrpGpuDrivenAnalyzer : SettingsAnalyzer
    {
        internal const string GFX0020 = nameof(GFX0020);
        internal const string GFX0021 = nameof(GFX0021);
        internal const string GFX0022 = nameof(GFX0022);

        static readonly Descriptor GrdWithoutForwardPlusDescriptor = new Descriptor(
            GFX0020,
            "URP: GPU Resident Drawer Enabled Without Forward+ or Deferred+",
            Impact.Performance,
            "<b>GPU Resident Drawer</b> is enabled on a URP asset, but at least one renderer in its list is not configured for <b>Forward+</b> or <b>Deferred+</b>. Unity requires every renderer in the asset to use one of these paths; renderers on Forward or Deferred silently fall back to the standard draw path and bypass GPU instancing.",
            "Open each <b>Universal Renderer Data</b> asset referenced in the URP Renderer List and set its <b>Rendering Mode</b> to <b>Forward+</b>. Profile on target hardware — Forward+ has its own trade-offs for sparse-light scenes and some mobile targets.")
        {
            MessageFormat = "URP asset '{0}' (quality level '{1}') has GPU Resident Drawer enabled but renderer '{2}' uses {3} instead of Forward+ or Deferred+",
            MinimumVersion = "6000.0"
        };

        static readonly Descriptor BrgVariantsNotKeptDescriptor = new Descriptor(
            GFX0021,
            "URP: GPU Resident Drawer Enabled but BatchRendererGroup Variants Not Kept",
            Impact.Quality,
            "<b>GPU Resident Drawer</b> is enabled on a URP asset, but <b>Project Settings &gt; Graphics &gt; Shader Stripping &gt; BatchRendererGroup Variants</b> is not set to <b>Keep All</b>. Unity strips the BatchRendererGroup shader variants from the player build, so objects converted by GPU Resident Drawer render incorrectly or fall back to the standard path.",
            "Go to <b>Project Settings &gt; Graphics &gt; Shader Stripping</b> and set <b>BatchRendererGroup Variants</b> to <b>Keep All</b>. This increases shader compilation time because Unity compiles all BRG variants into the build.")
        {
            MinimumVersion = "6000.0"
        };

        static readonly Descriptor OcclusionWithoutGrdDescriptor = new Descriptor(
            GFX0022,
            "URP: GPU Occlusion Culling Enabled Without GPU Resident Drawer",
            Impact.Performance,
            "<b>GPU Occlusion Culling</b> is enabled on a Universal Renderer Data asset, but the parent URP asset has <b>GPU Resident Drawer</b> disabled. GPU Occlusion Culling operates on the BatchRendererGroup draw commands produced by GPU Resident Drawer; without it the occlusion pass runs but produces no CPU savings.",
            "Enable <b>GPU Resident Drawer</b> (set to <b>Instanced Drawing</b>) on the URP asset, ensure all renderers use <b>Forward+</b> or <b>Deferred+</b>, and set <b>BatchRendererGroup Variants</b> to <b>Keep All</b> in <b>Project Settings &gt; Graphics</b>.")
        {
            MessageFormat = "Renderer '{0}' on URP asset '{1}' (quality level '{2}') has GPU Occlusion Culling enabled but GPU Resident Drawer is disabled on the asset",
            MinimumVersion = "6000.0"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            var assets = UrpRenderPathInventory.CollectAll();
            if (assets.Count == 0)
                return;

            var grdEnabledAnywhere = false;
            var grdEnabled0020 = context.IsDescriptorEnabled(GrdWithoutForwardPlusDescriptor);
            var brgEnabled0021 = context.IsDescriptorEnabled(BrgVariantsNotKeptDescriptor);
            var occEnabled0022 = context.IsDescriptorEnabled(OcclusionWithoutGrdDescriptor);

            foreach (var asset in assets)
            {
                if (asset.GpuResidentDrawerMode == UrpGpuResidentDrawerMode.Unknown)
                    continue; // GRD feature not present in this URP version

                if (asset.GpuResidentDrawerActive)
                {
                    grdEnabledAnywhere = true;

                    if (grdEnabled0020)
                        ReportGrdWithoutForwardPlus(context, asset);
                }
                else
                {
                    if (occEnabled0022)
                        ReportOcclusionWithoutGrd(context, asset);
                }
            }

            if (grdEnabledAnywhere && brgEnabled0021)
                ReportBrgVariantsIfNeeded(context);
        }

        static void ReportGrdWithoutForwardPlus(SettingsAnalysisContext context, UrpAssetInfo asset)
        {
            var qualityLabel = RenderPipelineUtils.GetQualityLabel(asset.QualityLevel);
            var location = new Location(asset.QualityLevel == -1 ? "Project/Graphics" : "Project/Quality");

            foreach (var renderer in asset.Renderers)
            {
                if (renderer.IsForwardPlusOrDeferredPlus)
                    continue;

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.ProjectSetting,
                        GrdWithoutForwardPlusDescriptor.Id,
                        asset.Name,
                        qualityLabel,
                        renderer.Name,
                        renderer.PathKind.ToString())
                    .WithEvidence("UrpAssetName", asset.Name)
                    .WithEvidence("QualityLevelIndex", asset.QualityLevel)
                    .WithEvidence("RendererName", renderer.Name)
                    .WithEvidence("RendererPath", renderer.PathKind.ToString())
                    .WithLocation(location);
                context.ReportIssue(diagnostic);
            }
        }

        static void ReportBrgVariantsIfNeeded(SettingsAnalysisContext context)
        {
            if (IsBatchRendererGroupVariantsKeptAll())
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, BrgVariantsNotKeptDescriptor.Id)
                .WithLocation(new Location("Project/Graphics"));
            context.ReportIssue(diagnostic);
        }

        static void ReportOcclusionWithoutGrd(SettingsAnalysisContext context, UrpAssetInfo asset)
        {
            var qualityLabel = RenderPipelineUtils.GetQualityLabel(asset.QualityLevel);
            var location = new Location(asset.QualityLevel == -1 ? "Project/Graphics" : "Project/Quality");

            foreach (var renderer in asset.Renderers)
            {
                if (!renderer.GpuOcclusionCullingEnabled)
                    continue;

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.ProjectSetting,
                        OcclusionWithoutGrdDescriptor.Id,
                        renderer.Name,
                        asset.Name,
                        qualityLabel)
                    .WithEvidence("RendererName", renderer.Name)
                    .WithEvidence("UrpAssetName", asset.Name)
                    .WithEvidence("QualityLevelIndex", asset.QualityLevel)
                    .WithLocation(location);
                context.ReportIssue(diagnostic);
            }
        }

        static bool IsBatchRendererGroupVariantsKeptAll()
        {
            // Read BatchRendererGroup shader stripping mode from the GraphicsSettings singleton
            // via SerializedObject. m_BatchRendererGroupShaderStrippingMode values:
            //   0 = Automatic (Unity may strip BRG variants — bad for GRD builds)
            //   non-zero = Keep All — good for GRD
            // When the property is absent (pre-Unity 6), the feature doesn't exist; return true
            // (no issue to report).
            var so = new UnityEditor.SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var prop = so.FindProperty("m_BatchRendererGroupShaderStrippingMode");
            if (prop == null)
                return true;

            return prop.enumValueIndex != 0;
        }
    }
}

#endif
