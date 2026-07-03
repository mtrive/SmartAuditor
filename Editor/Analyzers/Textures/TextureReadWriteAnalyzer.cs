using SmartAuditor.Editor.Core;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzer that detects textures with Read/Write enabled, which causes texture data to be duplicated in memory.
    /// </summary>
    sealed class TextureReadWriteAnalyzer : TextureAnalyzer
    {
        internal const string TEX0002 = nameof(TEX0002);

        internal static readonly Descriptor TextureReadWriteEnabledDescriptor = new Descriptor(
            TEX0002,
            "Texture: Read/Write Enabled",
            Impact.Memory,
            "The texture's <b>Read/Write Enabled</b> flag in the Import Settings is on. Read/Write keeps a CPU-side copy of the texture data alongside the GPU copy, doubling memory cost for any texture that does not need scripted pixel access at runtime.",
            "Disable <b>Read/Write Enabled</b> in the Texture Import Settings unless the texture is sampled or modified from script at runtime."
        )
        {
            MessageFormat = "Texture '{0}' Read/Write is enabled",
            DocumentationUrl = "https://docs.unity3d.com/Manual/class-TextureImporter.html"
        };

        public override void Analyze(TextureAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(TextureReadWriteEnabledDescriptor, context.Importer.assetPath))
                return;

            if (context.Importer.textureType == TextureImporterType.Cursor)
                return;

            if (!context.Importer.isReadable)
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, TextureReadWriteEnabledDescriptor.Id, context.Name)
                .WithLocation(new Location(context.Importer.assetPath));
            context.ReportIssue(diagnostic);
        }
    }
}

