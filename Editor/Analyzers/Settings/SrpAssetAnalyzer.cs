using SmartAuditor.Editor.Core;
using UnityEngine.Rendering;
#if PACKAGE_URP
using UnityEngine.Rendering.Universal;
#endif

#if PACKAGE_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace SmartAuditor.Editor.Analyzers
{
    internal sealed class SrpAssetAnalyzer : SettingsAnalyzer
    {
        internal const string GFX0007 = nameof(GFX0007);

        static readonly Descriptor SRPBatcherSettingDescriptor = new Descriptor(
            GFX0007,
            "SRP Asset: SRP Batcher Disabled",
            Impact.Performance,
            "<b>SRP Batcher</b> is disabled on a Render Pipeline Asset. Without the batcher, the renderer rebuilds per-object material data on the CPU for every draw call instead of reusing a persistent GPU buffer, raising the CPU cost of submitting draws. In URP, SRP Batcher is also a prerequisite for enabling <b>GPU Resident Drawer</b>.",
            "Enable <b>SRP Batcher</b> on the Render Pipeline Asset. If the field isn't visible in the inspector, open the More (⋮) menu and choose <b>Show All Advanced Properties</b>. The batcher reduces CPU draw-call submission cost for materials that share a shader variant, and is required before <b>GPU Resident Drawer</b> can be enabled.")
        {
            MessageFormat = "SRP Batcher is disabled on '{0}' (quality level '{1}')"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            RenderPipelineUtils.AnalyzePipelineAssets(context, Analyze);
        }

        void Analyze(SettingsAnalysisContext context, RenderPipelineAsset renderPipeline, int qualityLevel)
        {
            if (!context.IsDescriptorEnabled(SRPBatcherSettingDescriptor))
                return;

            var srpBatcherEnabled = GetSrpBatcherSetting(renderPipeline);
            if (srpBatcherEnabled != null && !srpBatcherEnabled.Value)
                RenderPipelineUtils.ReportPipelineAssetIssue(context, qualityLevel, renderPipeline, SRPBatcherSettingDescriptor.Id);
        }

        internal static bool? GetSrpBatcherSetting(RenderPipelineAsset renderPipeline)
        {
            if (renderPipeline == null)
                return null;

#if PACKAGE_URP
            if (renderPipeline is UniversalRenderPipelineAsset urpAsset)
                return urpAsset.useSRPBatcher;
#endif

#if PACKAGE_HDRP
            var enableSrpBatcherField = GetSrpBatcherField(renderPipeline, out var hdrpAsset);
            if (enableSrpBatcherField != null)
                return (bool)enableSrpBatcherField.GetValue(hdrpAsset);
#endif

            return null;
        }

#if PACKAGE_HDRP
        static System.Reflection.FieldInfo GetSrpBatcherField(RenderPipelineAsset renderPipeline,
            out HDRenderPipelineAsset hdrpAsset)
        {
            hdrpAsset = null;
            if (renderPipeline is HDRenderPipelineAsset asset)
            {
                hdrpAsset = asset;
                return hdrpAsset.GetType()
                    .GetField("enableSRPBatcher", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }

            return null;
        }
#endif
    }
}
