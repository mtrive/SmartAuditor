using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Modules
{
    sealed class TexturesModule : AnalysisModule<TextureAnalyzer>
    {
        // Texture insight schema. ColumnHints
        // drive InsightSummary computation: Size is Aggregatable + TopNMetric (total memory
        // footprint + top-N largest); Format / Compression are Categorical (distribution).
        internal static readonly InsightSchema k_TextureInsightSchema = new InsightSchema(
            new InsightColumn(TextureColumns.Shape, "Shape", PropertyFormat.Text, ColumnHints.Categorical, longName: "Texture Shape"),
            new InsightColumn(TextureColumns.ImporterType, "Importer Type", PropertyFormat.Text, ColumnHints.Categorical, longName: "Texture Importer Type"),
            new InsightColumn(TextureColumns.Format, "Format", PropertyFormat.Text, ColumnHints.Categorical, longName: "Texture Format"),
            new InsightColumn(TextureColumns.Compression, "Compression", PropertyFormat.Text, ColumnHints.Categorical, longName: "Texture Compression"),
            new InsightColumn(TextureColumns.MipmapEnabled, "Mipmaps", PropertyFormat.Boolean, longName: "Texture Mipmaps Enabled"),
            new InsightColumn(TextureColumns.Readable, "Readable", PropertyFormat.Boolean, longName: "Read/Write Enabled"),
            new InsightColumn(TextureColumns.Resolution, "Resolution", PropertyFormat.Resolution, longName: "Source Texture Resolution"),
            new InsightColumn(TextureColumns.Size, "Size", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Texture Size in Memory"),
            new InsightColumn(TextureColumns.StreamingMipmap, "Streaming", PropertyFormat.Boolean, longName: "Streaming Mipmaps"),
            new InsightColumn(TextureColumns.sRGB, "sRGB", PropertyFormat.Boolean, longName: "sRGB (Color Texture)"),
            new InsightColumn(TextureColumns.Filtering, "Filter Mode", PropertyFormat.Text, ColumnHints.Categorical, longName: "Texture Filter Mode"));

        internal static readonly InsightSchema k_SpriteInsightSchema = new InsightSchema(
            new InsightColumn(SpriteColumns.SpriteMode, "Sprite Mode", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(SpriteColumns.PixelsPerUnit, "PPU", PropertyFormat.Number, longName: "Pixels Per Unit"),
            new InsightColumn(SpriteColumns.MeshType, "Mesh Type", PropertyFormat.Text, ColumnHints.Categorical, longName: "Sprite Mesh Type"),
            new InsightColumn(SpriteColumns.ExtrudeEdges, "Extrude", PropertyFormat.Number, longName: "Extrude Edges"),
            new InsightColumn(SpriteColumns.Format, "Format", PropertyFormat.Text, ColumnHints.Categorical, longName: "Texture Format"),
            new InsightColumn(SpriteColumns.Compression, "Compression", PropertyFormat.Text, ColumnHints.Categorical, longName: "Texture Compression"),
            new InsightColumn(SpriteColumns.Readable, "Readable", PropertyFormat.Boolean, longName: "Read/Write Enabled"),
            new InsightColumn(SpriteColumns.Resolution, "Resolution", PropertyFormat.Resolution, longName: "Source Texture Resolution"),
            new InsightColumn(SpriteColumns.Size, "Size", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Texture Size in Memory"));

        public override string Name => "Textures";

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.Texture, k_TextureInsightSchema },
                { AnalysisCategory.Sprite, k_SpriteInsightSchema },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.Texture,
            AnalysisCategory.Sprite,
        };

        public override IReadOnlyCollection<AnalysisCategory> AdditionalEmittedCategories =>
            new[] { AnalysisCategory.AssetIssue };

        // Yield to the message loop after this many assets so the in-window progress overlay
        // can repaint and the user can hit Cancel. Texture analyzers touch AssetDatabase /
        // AssetImporter (main-thread-only), so we use await Task.Yield rather than offloading
        // to a worker via Task.Run.
        const int k_YieldEveryNAssets = 32;

        public override async Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var analyzers = GetCompatibleAnalyzers(options);
            var platformString = options.PlatformAsString;
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(Texture)}, a:assets", options);

            progress?.Start("Analyzing Textures and Sprites", string.Empty, assetPaths.Length);

            var cancelled = false;
            try
            {
                for (var i = 0; i < assetPaths.Length; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    var assetPath = assetPaths[i];
                    progress?.Advance(assetPath);

                    var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (textureImporter == null)
                    {
                        continue; // skip render textures
                    }

                    var platformSettings = textureImporter.GetPlatformTextureSettings(platformString);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);

                    if (texture == null)
                        continue;

                    var name = string.IsNullOrEmpty(texture.name) ?
                        Path.GetFileNameWithoutExtension(assetPath) : texture.name;

                    var context = CreateAppropriateContext(
                        texture,
                        textureImporter,
                        platformSettings,
                        name,
                        options,
                        session);

                    if (context == null)
                        continue;

                    using (context)
                    {
                        // Run analyzers
                        foreach (var analyzer in analyzers)
                        {
                            analyzer.Analyze(context);
                        }

                        var resolution = Formatting.FormatResolution(texture.width, texture.height);
                        var isSprite = textureImporter.textureType == TextureImporterType.Sprite;

                        if (isSprite)
                        {
                            var importerSettings = new TextureImporterSettings();
                            textureImporter.ReadTextureSettings(importerSettings);

                            var table = context.GetInsightTable(AnalysisCategory.Sprite, k_SpriteInsightSchema);
                            table.AddRow(assetPath, new Dictionary<string, object>
                            {
                                [SpriteColumns.SpriteMode] = textureImporter.spriteImportMode.ToString(),
                                [SpriteColumns.PixelsPerUnit] = textureImporter.spritePixelsPerUnit,
                                [SpriteColumns.MeshType] = importerSettings.spriteMeshType.ToString(),
                                [SpriteColumns.ExtrudeEdges] = (int)importerSettings.spriteExtrude,
                                [SpriteColumns.Format] = context.ImporterPlatformSettings.format.ToString(),
                                [SpriteColumns.Compression] = context.ImporterPlatformSettings.textureCompression.ToString(),
                                [SpriteColumns.Readable] = context.Importer.isReadable,
                                [SpriteColumns.Resolution] = resolution,
                                [SpriteColumns.Size] = context.Size,
                            });
                        }
                        else
                        {
                            var table = context.GetInsightTable(AnalysisCategory.Texture, k_TextureInsightSchema);
                            table.AddRow(assetPath, new Dictionary<string, object>
                            {
                                [TextureColumns.Shape] = context.Importer.textureShape.ToString(),
                                [TextureColumns.ImporterType] = context.Importer.textureType.ToString(),
                                [TextureColumns.Format] = context.ImporterPlatformSettings.format.ToString(),
                                [TextureColumns.Compression] = context.ImporterPlatformSettings.textureCompression.ToString(),
                                [TextureColumns.MipmapEnabled] = context.Importer.mipmapEnabled,
                                [TextureColumns.Readable] = context.Importer.isReadable,
                                [TextureColumns.Resolution] = resolution,
                                [TextureColumns.Size] = context.Size,
                                [TextureColumns.StreamingMipmap] = context.Importer.streamingMipmaps,
                                [TextureColumns.sRGB] = context.Importer.sRGBTexture,
                                [TextureColumns.Filtering] = context.Importer.filterMode.ToString(),
                            });
                        }

                    }

                    if ((i + 1) % k_YieldEveryNAssets == 0)
                        await Task.Yield();
                }
            }
            finally
            {
                FinalizeAnalyzers(session, analyzers);
            }

            return cancelled ? AnalysisResult.Cancelled : AnalysisResult.Success;
        }

        TextureAnalysisContext CreateAppropriateContext(
            Texture texture,
            TextureImporter importer,
            TextureImporterPlatformSettings platformSettings,
            string name,
            AnalysisOptions options,
            IFindingSink sink)
        {
            TextureAnalysisContext context = null;

            switch (texture)
            {
                case Texture2D tex2D:
                    context = new Texture2DAnalysisContext(options, sink)
                    {
                        Texture = tex2D,
                        Pixels = tex2D.isReadable ? tex2D.GetPixels() : null
                    };
                    break;

                case Texture3D tex3D:
                    context = new Texture3DAnalysisContext(options, sink)
                    {
                        Texture = tex3D,
                        Pixels = tex3D.isReadable ? tex3D.GetPixels() : null,
                        Depth = tex3D.depth
                    };
                    break;

                case Cubemap cubemap:
                    var facePixels = new Dictionary<CubemapFace, Color[]>();
                    if (cubemap.isReadable)
                    {
                        foreach (CubemapFace face in System.Enum.GetValues(typeof(CubemapFace)))
                        {
                            facePixels[face] = cubemap.GetPixels(face);
                        }
                    }
                    context = new CubemapAnalysisContext(options, sink)
                    {
                        Texture = cubemap,
                        FacePixels = facePixels
                    };
                    break;

                case RenderTexture renderTex:
                    context = new RenderTextureAnalysisContext(options, sink)
                    {
                        Texture = renderTex,
                        Format = renderTex.format
                    };
                    break;
            }

            if (context != null)
            {
                context.Name = name;
                context.Importer = importer;
                context.ImporterPlatformSettings = platformSettings;
                context.Size = CalculateTextureSize(texture, importer, platformSettings, options.PlatformAsString);
            }

            return context;
        }

        long CalculateTextureSize(Texture texture, TextureImporter importer, TextureImporterPlatformSettings settings, string platform)
        {
            // Implement your texture size calculation logic here
            // You might want to handle different texture types differently
    #if SMARTAUDITOR_CAN_USE_COMPUTEMIPCHAINSIZE
			if (texture.dimension != UnityEngine.Rendering.TextureDimension.Tex2D)
				return 0;

            var format = (TextureFormat)settings.format;
            if (settings.format == TextureImporterFormat.Automatic)
            {
                format = (TextureFormat)importer.GetAutomaticFormat(platform);
            }

            return UnityEngine.Experimental.Rendering.GraphicsFormatUtility.ComputeMipChainSize(texture.width, texture.height, TextureUtils.GetTextureDepth(texture), format, texture.mipmapCount);
    #else
            return UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
    #endif
        }
    }
}
