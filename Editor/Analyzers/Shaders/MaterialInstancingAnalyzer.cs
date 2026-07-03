// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MaterialInstancingAnalyzer : ShaderAnalyzer
    {
        internal const string MAT0001 = nameof(MAT0001);

        internal static readonly Descriptor MaterialInstancingDescriptor = new Descriptor(
            MAT0001,
            "Material: GPU Instancing Not Enabled",
            Impact.Performance,
            "The material's <b>Enable GPU Instancing</b> checkbox is off, but the assigned shader supports instancing. Without instancing, every renderer using this material emits a separate draw call instead of being batched into one instanced draw.",
            "Tick <b>Enable GPU Instancing</b> in the Material Inspector when the material is used on multiple renderers."
        )
        {
            DefaultSeverity = Severity.Minor, // We need more context to be able to bump this
            MessageFormat = "Material '{0}' does not have GPU Instancing enabled"
        };

        public override void AnalyzeMaterial(MaterialAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(MaterialInstancingDescriptor, context.AssetPath))
                return;

            var material = context.Material;
            var shader = material.shader;

            var hasInstancing = ShaderUtilProxy.HasInstancing(shader);
            if (hasInstancing != true || material.enableInstancing)
                return;
            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    MaterialInstancingDescriptor.Id,
                    material.name)
                .WithLocation(new Location(context.AssetPath));
            context.ReportIssue(diagnostic);
        }
    }
}
