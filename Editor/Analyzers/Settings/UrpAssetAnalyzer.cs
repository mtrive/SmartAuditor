#if PACKAGE_URP

using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Checks Universal Render Pipeline asset assignment and per-asset mobile settings (HDR, MSAA).
    /// </summary>
    sealed class UrpAssetAnalyzer : SettingsAnalyzer
    {
        internal const string GFX0008 = nameof(GFX0008);
        internal const string GFX0009 = nameof(GFX0009);
        internal const string GFX0010 = nameof(GFX0010);

        static readonly Descriptor URPAssetDescriptor = new Descriptor(
            GFX0008,
            "URP: Active Render Pipeline Asset is Not a URP Asset",
            Impact.Performance | Impact.Quality,
            "<b>Project Settings &gt; Graphics &gt; Scriptable Render Pipeline Settings</b> doesn't reference a Universal Render Pipeline Asset. URP-only features (renderer features, URP post-processing, URP-specific shaders) don't run, and the project falls back to the Built-in renderer.",
            "Assign a Universal Render Pipeline Asset under <b>Project Settings &gt; Graphics &gt; Scriptable Render Pipeline Settings</b>, and pair it with the URP Asset slot on every <b>Quality Level</b>.")
        {
            MessageFormat = "Active render pipeline is '{0}', not a Universal Render Pipeline Asset"
        };

        static readonly Descriptor HdrSettingDescriptor = new Descriptor(
            GFX0009,
            "URP: HDR Enabled on Mobile Platform",
            Impact.Performance | Impact.Quality,
            "A URP Asset targets a mobile platform with <b>HDR</b> enabled. HDR uses a wider floating-point color buffer, which increases memory bandwidth and can be costly on low-end mobile GPUs. However, HDR is required for correct tone mapping, high-dynamic-range bloom, and overbright art direction.",
            "If the project does not rely on HDR post-processing effects (bloom on highlights, tone-mapping with overbright source values), disable <b>HDR</b> on the URP Asset for this platform. If HDR effects are intentional, keep HDR enabled and profile the GPU cost on target hardware.")
        {
            Platforms = new[] { BuildTarget.Android, BuildTarget.iOS, BuildTarget.Switch },
            MessageFormat = "URP Asset '{0}' has HDR enabled on quality level '{1}'"
        };

        static readonly Descriptor MsaaSampleCountSettingDescriptor = new Descriptor(
            GFX0010,
            "URP: MSAA Sample Count is 4x or 8x on Mobile",
            Impact.Performance | Impact.Quality,
            "<b>Anti Aliasing (MSAA)</b> on a URP Asset is set to <b>4x</b> or <b>8x</b> for a mobile platform. Tile-based deferred-rendering (TBDR) mobile GPUs resolve MSAA on-tile at low bandwidth cost, but 4x/8x still increases tile memory pressure and can cause register spills on low-end hardware. On non-TBDR or bandwidth-constrained targets the cost is higher.",
            "Try reducing <b>Anti Aliasing (MSAA)</b> to <b>2x</b> on the URP Asset for mobile platforms and profile the visual quality difference on target hardware. If high-quality anti-aliasing is important and the rendering path is <b>Forward+</b>, consider whether a post-process technique (FXAA, SMAA, or TAA) meets quality requirements at lower sample count.")
        {
            Platforms = new[] { BuildTarget.Android, BuildTarget.iOS, BuildTarget.Switch },
            MessageFormat = "URP Asset '{0}' has high MSAA sample count on quality level '{1}'"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            var renderPipeline = GraphicsSettings.currentRenderPipeline;
            if (renderPipeline == null || renderPipeline is not UniversalRenderPipelineAsset)
            {
                if (context.IsDescriptorEnabled(URPAssetDescriptor))
                {
                    var pipelineLabel = renderPipeline == null
                        ? "None (Built-in Render Pipeline)"
                        : renderPipeline.name;

                    var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, URPAssetDescriptor.Id, pipelineLabel)
                        .WithEvidence("ConfiguredRenderPipeline",
                            renderPipeline == null ? "None (Built-in Render Pipeline)" : renderPipeline.GetType().Name)
                        .WithLocation(new Location("Project/Graphics"));

                    if (renderPipeline != null)
                        diagnostic = diagnostic.WithEvidence("RenderPipelineAssetName", renderPipeline.name);

                    context.ReportIssue(diagnostic);
                }
            }

            RenderPipelineUtils.AnalyzePipelineAssets(context, AnalyzeRenderPipelineAsset);
        }

        void AnalyzeRenderPipelineAsset(SettingsAnalysisContext context, RenderPipelineAsset renderPipeline, int qualityLevel)
        {
            if (renderPipeline is not UniversalRenderPipelineAsset)
                return;

            if (context.IsDescriptorEnabled(HdrSettingDescriptor) &&
                HdrSettingDescriptor.IsApplicable(context.Options) &&
                GetHdrEnabled(renderPipeline))
            {
                RenderPipelineUtils.ReportPipelineAssetIssue(context, qualityLevel, renderPipeline, HdrSettingDescriptor.Id);
            }

            if (context.IsDescriptorEnabled(MsaaSampleCountSettingDescriptor) &&
                MsaaSampleCountSettingDescriptor.IsApplicable(context.Options))
            {
                var msaaSampleCount = GetMsaaSampleCount(renderPipeline);
                if (msaaSampleCount >= 4)
                {
                    var diagnostic = Diagnostic.Create(
                            AnalysisCategory.ProjectSetting,
                            MsaaSampleCountSettingDescriptor.Id,
                            renderPipeline.name,
                            RenderPipelineUtils.GetQualityLabel(qualityLevel))
                        .WithEvidence("QualityLevelIndex", qualityLevel)
                        .WithEvidence("MsaaSampleCount", msaaSampleCount)
                        .WithLocation(new Location(qualityLevel == -1 ? "Project/Graphics" : "Project/Quality"));
                    context.ReportIssue(diagnostic);
                }
            }
        }

        internal static bool GetHdrEnabled(RenderPipelineAsset renderPipeline)
        {
            return renderPipeline is UniversalRenderPipelineAsset urpAsset && urpAsset.supportsHDR;
        }

        internal static void SetHdrEnabled(RenderPipelineAsset renderPipeline, bool value)
        {
            if (renderPipeline is UniversalRenderPipelineAsset urpAsset)
                urpAsset.supportsHDR = value;
        }

        internal static int GetMsaaSampleCount(RenderPipelineAsset renderPipeline)
        {
            return renderPipeline is UniversalRenderPipelineAsset urpAsset ? urpAsset.msaaSampleCount : -1;
        }

        internal static void SetMsaaSampleCount(RenderPipelineAsset renderPipeline, int value)
        {
            if (renderPipeline is UniversalRenderPipelineAsset urpAsset)
                urpAsset.msaaSampleCount = value;
        }
    }
}
#endif
