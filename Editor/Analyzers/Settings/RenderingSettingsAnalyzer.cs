using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class RenderingSettingsAnalyzer : SettingsAnalyzer
    {
        internal const string GFX0013 = nameof(GFX0013);
        internal const string GFX0014 = nameof(GFX0014);
        internal const string GFX0017 = nameof(GFX0017);
        internal const string GFX0019 = nameof(GFX0019);

        static readonly Descriptor MetalApiValidationDescriptor = new Descriptor(
            GFX0013,
            "Graphics (iOS): Metal API Validation Enabled",
            Impact.Performance,
            "<b>Metal API Validation</b> is enabled in iOS Player Settings. This option spends extra GPU time checking Metal command buffers for invalid usage and skews profiling results in test builds.",
            "Disable <b>Project Settings &gt; Player &gt; iOS &gt; Other Settings &gt; Metal API Validation</b>. The check only applies to Xcode-launched builds and rarely surfaces errors a developer can act on.")
        {
            Platforms = new[] { BuildTarget.iOS, BuildTarget.StandaloneOSX, BuildTarget.tvOS },
            MinimumVersion = "2018.1"
        };

        static readonly Descriptor GraphicsJobsDescriptor = new Descriptor(
            GFX0014,
            "Graphics: Graphics Jobs Disabled",
            Impact.Performance,
            "<b>Graphics Jobs</b> in Player Settings is off. The render-command buffer is built entirely on the render thread, which is frequently the CPU bottleneck on draw-heavy projects.",
            "Enable <b>Project Settings &gt; Player &gt; Other Settings &gt; Graphics Jobs</b> and profile. The option spreads command-buffer construction across worker threads, but it is experimental on some Unity versions — test for new crashes before shipping.");

        static readonly Descriptor MipStrippingDescriptor = new Descriptor(
            GFX0017,
            "Graphics: Texture Mipmap Stripping Disabled",
            Impact.BuildSize,
            "<b>Texture Mipmap Stripping</b> in Player Settings is off. The build ships every mip of every mipmapped texture, even mips that no Quality Level on the target platform can ever request.",
            "Enable <b>Project Settings &gt; Player &gt; Other Settings &gt; Texture Mipmap Stripping</b>. Stripping has no effect if at least one Quality Level on the target platform uses the highest mip; it also breaks runtime overrides of <b>masterTextureLevel</b> that request a stripped mip.")
        {
            MinimumVersion = "2020.2"
        };

        static readonly Descriptor LightmapStreamingDescriptor = new Descriptor(
            GFX0019,
            "Lighting: Lightmap Streaming is Disabled",
            Impact.Memory,
            "<b>Lightmap Streaming</b> in Player Settings is off while <b>Texture Streaming</b> is enabled on at least one Quality Level. Every mip level of every lightmap stays resident in GPU memory, even when the camera is far from the lightmapped geometry.",
            "Enable <b>Project Settings &gt; Player &gt; Other Settings &gt; Rendering &gt; Lightmap Streaming</b> so Unity discards higher-resolution mip levels when they're not visible. Lightmap streaming only takes effect when <b>Texture Streaming</b> is also enabled in <b>Project Settings &gt; Quality</b>.")
        {
            MinimumVersion = "2020.1"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (context.IsDescriptorEnabled(MetalApiValidationDescriptor) &&
                MetalApiValidationDescriptor.IsApplicable(context.Options) &&
                PlayerSettings.enableMetalAPIValidation)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, MetalApiValidationDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(GraphicsJobsDescriptor) &&
                !PlayerSettings.graphicsJobs)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, GraphicsJobsDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(MipStrippingDescriptor) &&
                MipStrippingDescriptor.IsApplicable(context.Options) &&
                !PlayerSettings.mipStripping)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, MipStrippingDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }

            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(context.Options.Platform);
            if (context.IsDescriptorEnabled(LightmapStreamingDescriptor) &&
                PlayerSettingsUtil.IsLightmapStreamingSupported &&
                QualitySettingsAnalyzer.IsTextureStreamingEnabledOnAnyQualityLevel() &&
                !PlayerSettingsUtil.IsLightmapStreamingEnabled(buildTargetGroup))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, LightmapStreamingDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
