using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ShaderInstancingAnalyzer : ShaderAnalyzer
    {
        internal const string SHD0007 = nameof(SHD0007);

        internal static readonly Descriptor ShaderInstancingDescriptor = new Descriptor(
            SHD0007,
            "Shader: GPU Instancing Not Supported",
            Impact.Performance,
            "The shader does not declare <b>#pragma multi_compile_instancing</b>. Materials using the shader cannot be batched into instanced draw calls, so every renderer produces a separate draw call.",
            "Add <b>#pragma multi_compile_instancing</b> to each surface or vertex/fragment program in the shader so materials can opt in to GPU Instancing."
        )
        {
            MessageFormat = "Shader '{0}' does not support GPU Instancing"
        };

        public override void AnalyzeShader(ShaderAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(ShaderInstancingDescriptor, context.AssetPath))
                return;

            var shader = context.Shader;

            var hasInstancing = ShaderUtilProxy.HasInstancing(shader);
            if (hasInstancing != false)
                return;
            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    ShaderInstancingDescriptor.Id,
                    context.Shader.name)
                .WithLocation(new Location(context.AssetPath));
            context.ReportIssue(diagnostic);
        }
    }
}
