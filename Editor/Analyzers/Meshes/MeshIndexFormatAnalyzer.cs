using SmartAuditor.Editor.Core;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MeshIndexFormatAnalyzer : MeshAnalyzer
    {
        internal const string MSH0001 = nameof(MSH0001);

        internal static readonly Descriptor Mesh32BitIndexFormatUsedDescriptor = new Descriptor(
            MSH0001,
            "Mesh: 32-bit Index Format on a Sub-65k Mesh",
            Impact.Memory,
            "The <b>Index Format</b> in the Model Import Settings is set to <b>32 bit</b>, but the mesh has fewer than 65,536 vertices and would fit in a 16-bit index buffer. A 32-bit index buffer doubles the GPU memory cost of the indices and is not supported on some older mobile GPUs.",
            "Set <b>Index Format</b> in the Model Import Settings to <b>16 bits</b> or <b>Auto</b>."
        )
        {
            MessageFormat = "Mesh '{0}' uses 32-bit Index Format but fits in 16 bits",
            DocumentationUrl = "https://docs.unity3d.com/Manual/FBXImporter-Model.html"
        };

        public override void AnalyzeMesh(MeshAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(Mesh32BitIndexFormatUsedDescriptor, context.Importer.assetPath))
                return;

            var mesh = context.Mesh;
            if (mesh.indexFormat != IndexFormat.UInt32 ||
                mesh.vertexCount > 65535)
                return;
            var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, Mesh32BitIndexFormatUsedDescriptor.Id, context.Name)
                .WithLocation(new Location(context.Importer.assetPath));
            context.ReportIssue(diagnostic);
        }
    }
}
