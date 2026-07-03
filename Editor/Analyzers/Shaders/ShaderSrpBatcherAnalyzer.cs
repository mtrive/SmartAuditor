using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ShaderSrpBatcherAnalyzer : ShaderAnalyzer
    {
        internal const string SHD0000 = nameof(SHD0000);

        internal static readonly Descriptor SrpBatcherDescriptor = new Descriptor(
            SHD0000,
            "Shader: Not Compatible With SRP Batcher",
            Impact.Performance,
            "The shader does not meet the SRP Batcher's compatibility requirements (most often: per-material properties declared outside a single <b>CBUFFER</b> named <b>UnityPerMaterial</b>). The SRP Batcher cannot fast-path draws that use this shader, so the CPU pays full per-material draw-call setup cost.",
            "Move all per-material properties into a <b>CBUFFER_START(UnityPerMaterial) ... CBUFFER_END</b> block in the shader, and avoid sampler state or texture declarations inside that CBUFFER."
        )
        {
            MessageFormat = "Shader '{0}' is not compatible with SRP Batcher",
            DocumentationUrl = "https://docs.unity3d.com/Manual/SRPBatcher.html"
        };

        public override void AnalyzeShader(ShaderAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(SrpBatcherDescriptor, context.AssetPath))
                return;

            if (!IsSrpBatchingEnabled)
                return;

            var shader = context.Shader;
            if (ShaderUtilProxy.IsSRPBatcherCompatible(shader) != false)
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, SrpBatcherDescriptor.Id, shader.name)
                .WithLocation(new Location(context.AssetPath));
            context.ReportIssue(diagnostic);
        }

        internal static bool IsSrpBatchingEnabled => GraphicsSettings.defaultRenderPipeline != null &&
        GraphicsSettings.useScriptableRenderPipelineBatching;
    }
}
