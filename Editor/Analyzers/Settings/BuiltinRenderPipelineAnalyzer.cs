using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class BuiltinRenderPipelineAnalyzer : SettingsAnalyzer
    {
        internal const string GFX0001 = nameof(GFX0001);
        internal const string GFX0002 = nameof(GFX0002);
        internal const string GFX0003 = nameof(GFX0003);
        internal const string GFX0011 = nameof(GFX0011);

        static readonly Descriptor ShaderQualityDescriptor = new Descriptor(
            GFX0001,
            "Graphics: Mixed Standard Shader Quality Across Tiers",
            Impact.BuildSize,
            "The build target's Graphics Tier Settings use a mixture of values (Low/Medium/High) for <b>Standard Shader Quality</b>. Mixed quality settings produce more shader variants, inflating both build time and the shipped build's shader payload.",
            "Edit the platform's <b>Graphics Tier Settings</b> in <b>Project Settings &gt; Graphics</b> to use the same Standard Shader Quality across all tiers, unless the target devices genuinely span a wide capability range.");

        static readonly Descriptor ForwardRenderingDescriptor = new Descriptor(
            GFX0002,
            "Graphics: Forward Rendering Path",
            Impact.Performance,
            "The build target uses <b>Forward</b> rendering, configured in <b>Project Settings &gt; Graphics &gt; Tier Settings</b>. Forward is appropriate for simple-lighting projects but may become CPU-bound in dynamic-light-heavy scenes.",
            "Keep Forward for simple-lighting projects (2D, mostly-baked lighting). If the project uses many dynamic lights, switch the tier's <b>Rendering Path</b> to <b>Deferred</b> and profile on target hardware.")
        {
            IsEnabledByDefault = false,
            DefaultSeverity = Severity.Info,
        };

        static readonly Descriptor DeferredRenderingDescriptor = new Descriptor(
            GFX0003,
            "Graphics: Deferred Rendering Path",
            Impact.Performance,
            "The build target uses <b>Deferred</b> rendering, configured in <b>Project Settings &gt; Graphics &gt; Tier Settings</b>. Deferred fills a G-buffer every frame and can be overkill for scenes with few dynamic lights or simple post-processing.",
            "Keep Deferred when the project has many dynamic lights or screen-space effects that benefit from the G-buffer. For simpler projects, consider switching the tier's <b>Rendering Path</b> to <b>Forward</b> and profiling on target hardware.")
        {
            IsEnabledByDefault = false,
            DefaultSeverity = Severity.Info,
        };

        static readonly Descriptor MixedRenderingPathDescriptor = new Descriptor(
            GFX0011,
            "Graphics: Mixed Rendering Path Across Tiers",
            Impact.BuildSize,
            "The build target's <b>Graphics Tier Settings</b> use different <b>Rendering Path</b> values across tiers (for example, Forward on one tier and Deferred on another). Mixed paths increase shader variant count and make runtime rendering behavior harder to predict.",
            "Edit the platform's <b>Graphics Tier Settings</b> in <b>Project Settings &gt; Graphics</b> to use the same <b>Rendering Path</b> across all tiers, unless tiers intentionally target devices with different rendering needs.");

        public override void Analyze(SettingsAnalysisContext context)
        {
            // Only check for Built-In Rendering Pipeline
            if (!IsUsingBuiltinRenderPipeline())
                return;

            if (context.IsDescriptorEnabled(ShaderQualityDescriptor) &&
                GraphicsTiers.IsMixedStandardShaderQuality(context.Options.Platform))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, ShaderQualityDescriptor.Id)
                    .WithLocation(new Location("Project/Graphics"));
                context.ReportIssue(diagnostic);
            }
            if (context.IsDescriptorEnabled(MixedRenderingPathDescriptor) &&
                GraphicsTiers.IsMixedRenderingPath(context.Options.Platform))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, MixedRenderingPathDescriptor.Id)
                    .WithLocation(new Location("Project/Graphics"));
                context.ReportIssue(diagnostic);
            }
            if (context.IsDescriptorEnabled(ForwardRenderingDescriptor) &&
                GraphicsTiers.IsUsingForwardRendering(context.Options.Platform))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, ForwardRenderingDescriptor.Id)
                    .WithLocation(new Location("Project/Graphics"));
                context.ReportIssue(diagnostic);
            }
            if (context.IsDescriptorEnabled(DeferredRenderingDescriptor) &&
                GraphicsTiers.IsUsingDeferredRendering(context.Options.Platform))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, DeferredRenderingDescriptor.Id)
                    .WithLocation(new Location("Project/Graphics"));
                context.ReportIssue(diagnostic);
            }
        }

        static bool IsUsingBuiltinRenderPipeline()
        {
            return GraphicsSettings.defaultRenderPipeline == null;
        }
    }
}
