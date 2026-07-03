using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class TextureUtilizationAnalyzer : TextureAnalyzer
    {
        internal const string TEX0005 = nameof(TEX0005);
        internal const string TEX0007 = nameof(TEX0007);
        internal const string TEX0009 = nameof(TEX0009);
        internal const string TEX0010 = nameof(TEX0010);

        internal static readonly Descriptor TextureSolidColorDescriptor = new Descriptor(
            TEX0005,
            "Texture: Solid Color Above 1x1 Size",
            Impact.Memory,
            "The texture is a single solid color but is larger than 1x1 pixels. Every texel of the source asset encodes the same RGBA value, so the texture costs memory and disk space that a 1x1 representative would not.",
            "Shrink the texture to 1x1, or replace its references with a Material color parameter (`Color` property on the shader) and drop the texture asset entirely."
        )
        {
            IsEnabledByDefault = false,
            MessageFormat = "Texture '{0}' is a solid color but larger than 1x1"
        };

        internal static readonly Descriptor TextureTooMuchEmptySpaceDescriptor = new Descriptor(
            TEX0007,
            "Texture: High Empty-Space Ratio",
            Impact.Memory,
            "The texture (typically a sprite atlas) has more transparent pixels than the configured threshold. Empty space costs the same texture memory and bandwidth as visible content.",
            "Repack the atlas more tightly, or split it into smaller atlases that each pack a related group of sprites."
        )
        {
            IsEnabledByDefault = false,
            MessageFormat = "Texture '{0}' has high empty-space ratio ({1})"
        };

        internal static readonly Descriptor TextureWithNoAlphaChannelDescriptor = new Descriptor(
            TEX0009,
            "Texture: Alpha-Capable Format on Source Without Alpha",
            Impact.Memory,
            "The texture's compression format reserves bits for an alpha channel, but the source asset has no alpha. Memory is spent storing a constant 1.0 alpha that no shader samples.",
            "Switch the texture's compression format to an opaque-only variant (e.g. <b>DXT1</b> on desktop, <b>ETC2 RGB</b> on mobile)."
        )
        {
            IsEnabledByDefault = true,
            MessageFormat = "Texture '{0}' uses an alpha-capable format but the source has no alpha"
        };

        internal static readonly Descriptor TextureEmptyAlphaDescriptor = new Descriptor(
            TEX0010,
            "Texture: Source Has Empty Alpha Channel",
            Impact.Memory,
            "The texture's source asset has an alpha channel where every pixel is fully opaque. The channel costs storage in both the source file and the compressed runtime texture without contributing to rendering.",
            "Re-export the source asset without an alpha channel, or strip the alpha at import via a custom AssetPostprocessor."
        )
        {
            IsEnabledByDefault = false,
            MessageFormat = "Texture '{0}' has an empty alpha channel"
        };

        [DiagnosticParameter("TextureEmptySpaceLimit", 50)]
        int m_EmptySpaceLimit;

        public override void AnalyzeTexture2D(Texture2DAnalysisContext context)
        {
            if (context.Texture.width == 1 && context.Texture.height == 1)
            {
                return;
            }

//            should skip textures which are child assets (fonts, embedded textures, etc.) ?
//            if (!AssetDatabase.IsMainAsset(texture))
//                return;

            if (context.IsDescriptorEnabled(TextureSolidColorDescriptor, context.Importer.assetPath) &&
                TextureUtils.IsSolidColorWithDimensionHandling(context.Importer, context.Texture))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, TextureSolidColorDescriptor.Id, context.Name)
                    .WithLocation(new Location(context.Importer.assetPath));
                context.ReportIssue(diagnostic);
            }

            if (TextureUtils.DoesImporterPreserveAlpha(context.Importer, context.ImporterPlatformSettings))
            {
                if (context.Importer.DoesSourceTextureHaveAlpha())
                {
                    if (context.IsDescriptorEnabled(TextureEmptyAlphaDescriptor, context.Importer.assetPath) &&
                        TextureUtils.IsAlphaChannelEmpty(context.Texture))
                    {
                        var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, TextureEmptyAlphaDescriptor.Id, context.Name)
                            .WithLocation(new Location(context.Importer.assetPath));
                        context.ReportIssue(diagnostic);
                    }
                }
                else
                {
                    if (context.Importer.textureType != TextureImporterType.NormalMap)
                    {
                        var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, TextureWithNoAlphaChannelDescriptor.Id, context.Name)
                            .WithLocation(new Location(context.Importer.assetPath));
                        context.ReportIssue(diagnostic);
                    }
                }
            }

            if (context.IsDescriptorEnabled(TextureTooMuchEmptySpaceDescriptor, context.Importer.assetPath))
            {
                var emptyPercent = TextureUtils.GetEmptySpacePercentage(context.Texture, context.Importer);
                if (emptyPercent is { } percent && percent > m_EmptySpaceLimit)
                {
                    var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, TextureTooMuchEmptySpaceDescriptor.Id, context.Name, Formatting.FormatPercentage(percent / 100.0f))
                        .WithLocation(new Location(context.Importer.assetPath))
                        .WithEvidence("EmptySpacePercent", percent)
                        .WithEvidence("EmptySpaceLimit", m_EmptySpaceLimit);
                    context.ReportIssue(diagnostic);
                }
            }
        }
    }
}






