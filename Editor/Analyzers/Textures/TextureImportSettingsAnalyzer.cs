using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class TextureImportSettingsAnalyzer : TextureAnalyzer
    {
        internal const string TEX0000 = nameof(TEX0000);
        internal const string TEX0001 = nameof(TEX0001);
        internal const string TEX0003 = nameof(TEX0003);
        internal const string TEX0004 = nameof(TEX0004);

        internal static readonly Descriptor TextureMipmapsNotEnabledDescriptor = new Descriptor(
            TEX0000,
            "Texture: Mipmaps Not Enabled",
            Impact.Performance | Impact.Quality,
            "The texture's <b>Generate Mip Maps</b> option in the Import Settings is off. Without mipmaps, the GPU samples the full-resolution texture for every fragment regardless of screen size, increasing memory bandwidth and producing aliasing artifacts at distance.",
            "Enable <b>Generate Mip Maps</b> under <b>Advanced</b> in the Texture Import Settings."
        )
        {
            MessageFormat = "Texture '{0}' has mipmaps disabled"
        };

        internal static readonly Descriptor TextureMipmapsEnabledDescriptor = new Descriptor(
            TEX0001,
            "Texture: Mipmaps Enabled on Sprite or UI Texture",
            Impact.BuildSize | Impact.Quality,
            "The texture's <b>Generate Mip Maps</b> option is on, but the texture is imported as Sprite or GUI. Sprites and UI render at fixed sizes that never sample lower mip levels, so the mip chain ships in the build with no rendering benefit.",
            "Disable <b>Generate Mip Maps</b> under <b>Advanced</b> in the Texture Import Settings."
        )
        {
            MessageFormat = "Sprite/UI texture '{0}' has mipmaps enabled"
        };

        internal static readonly Descriptor TextureStreamingMipmapEnabledDescriptor = new Descriptor(
            TEX0003,
            "Texture: Streaming Mipmaps Not Enabled",
            Impact.Memory | Impact.Quality,
            "The texture is larger than the configured streaming threshold but <b>Streaming Mipmaps</b> in the Texture Import Settings is off. Every mip level stays resident in GPU memory for the texture's lifetime instead of being paged in on demand.",
            "Enable <b>Streaming Mipmaps</b> in the Texture Import Settings."
        )
        {
            MessageFormat = "Texture '{0}' has Streaming Mipmaps disabled"
        };

        internal static readonly Descriptor TextureAnisotropicLevelDescriptor = new Descriptor(
            TEX0004,
            "Texture: Anisotropic Level Above 1",
            Impact.Performance | Impact.Quality,
            "The texture's <b>Aniso Level</b> in the Import Settings is greater than 1. Anisotropic filtering improves sampling quality at shallow viewing angles but costs additional GPU samples on every draw that uses the texture.",
            "Set <b>Aniso Level</b> to 1 in the Texture Import Settings unless the texture is consistently viewed at shallow angles (ground planes, terrain, long corridors)."
        )
        {
            Platforms = new[] { BuildTarget.Android, BuildTarget.iOS, BuildTarget.Switch},
            MessageFormat = "Texture '{0}' has Aniso Level {1}"
        };

        [DiagnosticParameter("TextureStreamingMipmapsSizeLimit", 4000)]
        int m_StreamingMipmapsSizeLimit;

        // [DiagnosticParameter("TextureSizeLimit", 2048)]
        // int m_SizeLimit;

        public override void Analyze(TextureAnalysisContext context)
        {
            var assetPath = context.Importer.assetPath;

            if (context.IsDescriptorEnabled(TextureMipmapsNotEnabledDescriptor, context.Importer.assetPath) &&
                !context.Importer.mipmapEnabled && context.Importer.textureType == TextureImporterType.Default)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, TextureMipmapsNotEnabledDescriptor.Id, context.Name)
                    .WithLocation(new Location(assetPath));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(TextureMipmapsEnabledDescriptor, context.Importer.assetPath) &&
                context.Importer.mipmapEnabled &&
                (context.Importer.textureType == TextureImporterType.Sprite || context.Importer.textureType == TextureImporterType.GUI))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, TextureMipmapsEnabledDescriptor.Id, context.Name)
                    .WithLocation(new Location(assetPath));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(TextureStreamingMipmapEnabledDescriptor, context.Importer.assetPath) &&
                context.Importer.mipmapEnabled && !context.Importer.streamingMipmaps && context.Size > Mathf.Pow(m_StreamingMipmapsSizeLimit, 2))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, TextureStreamingMipmapEnabledDescriptor.Id, context.Name)
                    .WithLocation(new Location(context.Importer.assetPath))
                    .WithEvidence("TextureSize", context.Size)
                    .WithEvidence("StreamingMipmapsSizeLimit", m_StreamingMipmapsSizeLimit);
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(TextureAnisotropicLevelDescriptor, context.Importer.assetPath) &&
                context.Importer.mipmapEnabled && context.Importer.filterMode != FilterMode.Point && context.Importer.anisoLevel > 1)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, TextureAnisotropicLevelDescriptor.Id, context.Name, context.Importer.anisoLevel)
                    .WithLocation(new Location(context.Importer.assetPath));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
