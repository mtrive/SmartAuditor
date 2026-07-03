using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MeshReadWriteAnalyzer : MeshAnalyzer
    {
        internal const string MSH0000 = nameof(MSH0000);

        internal static readonly Descriptor MeshReadWriteEnabledDescriptor = new Descriptor(
            MSH0000,
            "Mesh: Read/Write Enabled",
            Impact.Memory,
            "The <b>Read/Write Enabled</b> flag in the Model Import Settings is on. Read/Write keeps a CPU-side copy of the mesh data alongside the GPU copy, doubling memory cost for any mesh that does not need scripted access at runtime.",
            "Disable the <b>Read/Write Enabled</b> option in the Model Import Settings unless the mesh is read or modified from script at runtime."
        )
        {
            MessageFormat = "Mesh '{0}' Read/Write is enabled",
            DocumentationUrl = "https://docs.unity3d.com/Manual/FBXImporter-Model.html"
        };

        public override void AnalyzeMesh(MeshAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(MeshReadWriteEnabledDescriptor, context.Importer.assetPath))
                return;

            var mesh = context.Mesh;
            if (!mesh.isReadable)
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, MeshReadWriteEnabledDescriptor.Id, context.Name)
                .WithLocation(new Location(context.Importer.assetPath));
            context.ReportIssue(diagnostic);
        }
    }
}
