// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class CursorTextureAnalyzer : TextureAnalyzer
    {
        internal const string TEX0019 = nameof(TEX0019);
        internal const string TEX0020 = nameof(TEX0020);
        internal const string TEX0021 = nameof(TEX0021);
        internal const string TEX0022 = nameof(TEX0022);

        internal static readonly Descriptor CursorTextureFormatDescriptor = new Descriptor(
            TEX0019,
            "Cursor Texture: Invalid Format",
            Impact.Correctness,
            "The cursor texture is not in <b>RGBA 32</b> format. Unity's hardware-cursor API requires RGBA32 to upload the texture as an OS cursor; other formats are rejected at runtime and the cursor falls back to the system default.",
            "Set the texture's <b>Format</b> to <b>RGBA 32 bit</b> in the Import Settings and ensure compression is disabled."
        )
        {
            MessageFormat = "Cursor texture '{0}' is not in RGBA32 format"
        };

        internal static readonly Descriptor CursorTextureReadableDescriptor = new Descriptor(
            TEX0020,
            "Cursor Texture: Read/Write Disabled",
            Impact.Correctness,
            "The cursor texture has <b>Read/Write Enabled</b> off. Unity's hardware-cursor API needs CPU access to the pixel data to hand it to the OS; non-readable cursor textures fail to apply at runtime.",
            "Enable <b>Read/Write Enabled</b> in the texture's Import Settings."
        )
        {
            MessageFormat = "Cursor texture '{0}' is not readable"
        };

        internal static readonly Descriptor CursorTextureAlphaDescriptor = new Descriptor(
            TEX0021,
            "Cursor Texture: Alpha Is Transparency Disabled",
            Impact.Quality,
            "The cursor texture has <b>Alpha Is Transparency</b> off. Without it, edge pixels with partial alpha sample neighboring opaque pixels, producing a halo around the cursor instead of clean transparency.",
            "Enable <b>Alpha Is Transparency</b> in the texture's Import Settings."
        )
        {
            MessageFormat = "Cursor texture '{0}' has Alpha Is Transparency disabled"
        };

        internal static readonly Descriptor CursorTextureMipmapsDescriptor = new Descriptor(
            TEX0022,
            "Cursor Texture: Mipmaps Enabled",
            Impact.BuildSize,
            "The cursor texture has <b>Generate Mip Maps</b> enabled. Cursors render at their authored pixel size and never sample lower mip levels, so the mip chain ships in the build with no rendering benefit.",
            "Disable <b>Generate Mip Maps</b> under <b>Advanced</b> in the texture's Import Settings."
        )
        {
            MessageFormat = "Cursor texture '{0}' has mipmaps enabled"
        };

        public override void Analyze(TextureAnalysisContext context)
        {
            var textureImporter = context.Importer;

            if (textureImporter.textureType != TextureImporterType.Cursor)
                return;

            // Check texture format
            if (context.IsDescriptorEnabled(CursorTextureFormatDescriptor, context.Importer.assetPath) &&
                (textureImporter.textureCompression != TextureImporterCompression.Uncompressed ||
                 textureImporter.GetDefaultPlatformTextureSettings().format != TextureImporterFormat.RGBA32))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, CursorTextureFormatDescriptor.Id, context.Name)
                    .WithLocation(new Location(context.Importer.assetPath));
                context.ReportIssue(diagnostic);
            }

            // Check Read/Write enabled
            if (context.IsDescriptorEnabled(CursorTextureReadableDescriptor, context.Importer.assetPath) &&
                !textureImporter.isReadable)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, CursorTextureReadableDescriptor.Id, context.Name)
                    .WithLocation(new Location(context.Importer.assetPath));
                context.ReportIssue(diagnostic);
            }

            // Check Alpha Is Transparency
            if (context.IsDescriptorEnabled(CursorTextureAlphaDescriptor, context.Importer.assetPath) &&
                !textureImporter.alphaIsTransparency)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, CursorTextureAlphaDescriptor.Id, context.Name)
                    .WithLocation(new Location(context.Importer.assetPath));
                context.ReportIssue(diagnostic);
            }

            // Check mipmaps
            if (context.IsDescriptorEnabled(CursorTextureMipmapsDescriptor, context.Importer.assetPath) &&
                textureImporter.mipmapEnabled)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, CursorTextureMipmapsDescriptor.Id, context.Name)
                    .WithLocation(new Location(context.Importer.assetPath));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
