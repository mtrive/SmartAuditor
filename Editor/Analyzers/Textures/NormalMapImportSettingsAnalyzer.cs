using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzer that detects normal maps with improper import settings.
    /// </summary>
    sealed class NormalMapImportSettingsAnalyzer : TextureAnalyzer
    {
        internal const string TEX0006 = nameof(TEX0006);
        internal const string TEX0023 = nameof(TEX0023);
        internal const string TEX0024 = nameof(TEX0024);
        internal const string TEX0025 = nameof(TEX0025);

        internal static readonly Descriptor NormalMapSrgbEnabledDescriptor = new Descriptor(
            TEX0006,
            "Normal Map: sRGB Enabled",
            Impact.Quality,
            "The normal map texture has <b>sRGB (Color Texture)</b> enabled in its Import Settings. Normal maps encode surface vector data and must be sampled in linear space; sRGB decoding distorts the values and produces incorrect lighting.",
            "Disable <b>sRGB (Color Texture)</b> in the texture's Import Settings."
        )
        {
            DefaultSeverity = Severity.Critical,
            IsEnabledByDefault = true,
            MessageFormat = "Normal map '{0}' has sRGB (Color Texture) enabled"
        };

        internal static readonly Descriptor NormalMapMipmapsNotEnabledDescriptor = new Descriptor(
            TEX0023,
            "Normal Map: Mipmaps Not Enabled",
            Impact.Quality | Impact.Performance,
            "The normal map texture has <b>Generate Mip Maps</b> disabled. Without mipmaps, the normals shimmer and alias when sampled at low screen coverage, producing visible noise in distant lighting.",
            "Enable <b>Generate Mip Maps</b> under <b>Advanced</b> in the texture's Import Settings."
        )
        {
            DefaultSeverity = Severity.Major,
            IsEnabledByDefault = true,
            MessageFormat = "Normal map '{0}' has mipmaps disabled"
        };

        internal static readonly Descriptor NormalMapPointFilteringDescriptor = new Descriptor(
            TEX0024,
            "Normal Map: Point Filtering",
            Impact.Quality,
            "The normal map texture's <b>Filter Mode</b> is <b>Point</b>. Point sampling reads the nearest texel without interpolation, producing faceted lighting on smooth surfaces that normal maps are meant to make appear curved.",
            "Set <b>Filter Mode</b> to <b>Bilinear</b> or <b>Trilinear</b> in the texture's Import Settings."
        )
        {
            DefaultSeverity = Severity.Moderate,
            IsEnabledByDefault = true,
            MessageFormat = "Normal map '{0}' uses Point filtering"
        };

        internal static readonly Descriptor NormalMapInappropriateCompressionDescriptor = new Descriptor(
            TEX0025,
            "Normal Map: Inappropriate Compression Format",
            Impact.Quality | Impact.Memory,
            "The normal map uses a generic color-texture compression format rather than a normal-map-optimized variant. Generic formats either quantize XYZ channels uniformly (losing normal precision) or carry an unused alpha channel.",
            "Switch the normal map's compression format to a normal-map variant (<b>BC5</b> or <b>BC7</b> on desktop; <b>ASTC</b> on mobile)."
        )
        {
            DefaultSeverity = Severity.Moderate,
            IsEnabledByDefault = true,
            MessageFormat = "Normal map '{0}' uses a color-texture compression format"
        };

        public override void AnalyzeTexture2D(Texture2DAnalysisContext context)
        {
            var importer = context.Importer;

            if (importer.textureType != TextureImporterType.NormalMap)
                return;

            var assetPath = importer.assetPath;

            // TEX0006: sRGB enabled on normal map
            if (context.IsDescriptorEnabled(NormalMapSrgbEnabledDescriptor, assetPath))
            {
                // TextureImporter.sRGBTexture is available in editor APIs; prefer direct access.
                if (importer.sRGBTexture)
                {
                    var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, NormalMapSrgbEnabledDescriptor.Id, context.Name)
                        .WithLocation(new Location(assetPath));
                    context.ReportIssue(diagnostic);
                }
            }

            // TEX0023: mipmaps not enabled for normal map
            if (context.IsDescriptorEnabled(NormalMapMipmapsNotEnabledDescriptor, assetPath))
            {
                if (!importer.mipmapEnabled)
                {
                    var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, NormalMapMipmapsNotEnabledDescriptor.Id, context.Name)
                        .WithLocation(new Location(assetPath));
                    context.ReportIssue(diagnostic);
                }
            }

            // TEX0024: point filtering on normal map
            if (context.IsDescriptorEnabled(NormalMapPointFilteringDescriptor, assetPath))
            {
                if (importer.filterMode == FilterMode.Point)
                {
                    var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, NormalMapPointFilteringDescriptor.Id, context.Name)
                        .WithLocation(new Location(assetPath));
                    context.ReportIssue(diagnostic);
                }
            }

            // TEX0025: inappropriate compression format
            if (context.IsDescriptorEnabled(NormalMapInappropriateCompressionDescriptor, assetPath))
            {
                if (IsInappropriateNormalMapFormat(importer, context.ImporterPlatformSettings))
                {
                    var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, NormalMapInappropriateCompressionDescriptor.Id, context.Name)
                        .WithLocation(new Location(assetPath));
                    context.ReportIssue(diagnostic);
                }
            }
        }

        static bool IsInappropriateNormalMapFormat(TextureImporter importer, TextureImporterPlatformSettings platformSettings)
        {
            // Heuristic: If texture compression is disabled and format is an uncompressed color format,
            // or if the chosen format is a generic color format instead of a normal-map-optimized format,
            // we flag it as potentially inappropriate.

            var format = platformSettings.format;

            // If format is Automatic, ask Unity which format it will actually use.
            if (format != TextureImporterFormat.Automatic)
                return TextureFormatUtils.IsLikelyInappropriateNormalMapFormat((TextureFormat)format);
            var automatic = (TextureFormat)importer.GetAutomaticFormat(platformSettings.name);
            return TextureFormatUtils.IsLikelyInappropriateNormalMapFormat(automatic);

        }
    }
}

