// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes how textures are used in materials based on shader property names,
    /// and reports when import settings don't match the inferred usage.
    /// Each offending texture is reported once per rule, with the full list of
    /// affected material paths surfaced as diagnostic evidence.
    /// </summary>
    sealed class MaterialTextureUsageAnalyzer : ShaderAnalyzer
    {
        internal const string TEX0027 = nameof(TEX0027);
        internal const string TEX0028 = nameof(TEX0028);
        internal const string TEX0029 = nameof(TEX0029);
        internal const string TEX0030 = nameof(TEX0030);

        internal static readonly Descriptor TextureUsedAsNormalMapWrongTypeDescriptor = new Descriptor(
            TEX0027,
            "Texture: Used as Normal Map With Wrong Import Type",
            Impact.Quality,
            "The texture is assigned to a normal-map property on a material, but its <b>Texture Type</b> import setting is not <b>Normal map</b>. Unity's normal-map sampling path expects normal-map-encoded import data; reading a non-normal-map texture through it produces incorrect lighting.",
            "Set <b>Texture Type</b> to <b>Normal map</b> in the texture's Import Settings."
        )
        {
            DefaultSeverity = Severity.Major,
            IsEnabledByDefault = true,
            MessageFormat = "Texture '{0}' is used as a normal map but its Texture Type is not Normal map"
        };

        internal static readonly Descriptor TextureUsedAsNormalMapSrgbEnabledDescriptor = new Descriptor(
            TEX0028,
            "Texture: Used as Normal Map With sRGB Enabled",
            Impact.Quality,
            "The texture is assigned to a normal-map property on a material but has <b>sRGB (Color Texture)</b> enabled in its Import Settings. Normal maps encode vector data and must be sampled in linear space; sRGB-decoded samples produce incorrect lighting.",
            "Disable <b>sRGB (Color Texture)</b> in the texture's Import Settings."
        )
        {
            DefaultSeverity = Severity.Critical,
            IsEnabledByDefault = true,
            MessageFormat = "Texture '{0}' is used as a normal map but has sRGB enabled"
        };

        internal static readonly Descriptor TextureUsedAsColorMapSrgbDisabledDescriptor = new Descriptor(
            TEX0029,
            "Texture: Used as Color Map Without sRGB",
            Impact.Quality,
            "The texture is assigned to a color map property on a material but has <b>sRGB (Color Texture)</b> disabled in its Import Settings. In Linear color space, Unity applies an sRGB-to-linear conversion when sampling color textures; without it, colors are read too dark and the rendered result will appear incorrect.",
            "Enable <b>sRGB (Color Texture)</b> in the texture's Import Settings."
        )
        {
            DefaultSeverity = Severity.Major,
            IsEnabledByDefault = true,
            MessageFormat = "Texture '{0}' is used as a color map but has sRGB disabled"
        };

        internal static readonly Descriptor TextureUsedAsDataMapSrgbEnabledDescriptor = new Descriptor(
            TEX0030,
            "Texture: Used as Data Map With sRGB Enabled",
            Impact.Quality,
            "The texture is assigned to a data map property on a material but has <b>sRGB (Color Texture)</b> enabled in its Import Settings. In Linear color space, Unity applies an sRGB-to-linear conversion when sampling sRGB textures; for data maps (metallic, occlusion, masks, height), this distorts the encoded values and produces incorrect shader results.",
            "Disable <b>sRGB (Color Texture)</b> in the texture's Import Settings."
        )
        {
            DefaultSeverity = Severity.Major,
            IsEnabledByDefault = true,
            MessageFormat = "Texture '{0}' is used as a data map but has sRGB enabled"
        };

        // Shader property names are case-sensitive in HLSL/ShaderLab, so an exact-string match
        // captures the convention without false-positives on names like "_NotBumpMap" that a
        // substring check would catch.
        static readonly HashSet<string> s_NormalMapPropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "_BumpMap",
            "_NormalMap",
            "_DetailNormalMap",
            "_SecondNormalMap",
        };

        // Color maps encode perceptual color data authored in sRGB space. Unity applies an
        // sRGB→linear conversion at sample time when sRGB (Color Texture) is enabled, which is
        // required for correct results in a Linear color space project.
        static readonly HashSet<string> s_ColorMapPropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "_MainTex",
            "_BaseMap",
            "_BaseColorMap",
            "_EmissionMap",
            "_EmissiveColorMap",
            "_DetailAlbedoMap",
        };

        // Data maps store non-color scalar or packed-channel data (metallic, roughness, occlusion,
        // height, etc.). The sRGB decode curve must NOT be applied to them — enabling sRGB corrupts
        // the encoded values. Normal-map slots are deliberately excluded here; they are handled by
        // the dedicated normal-map rules (TEX0028).
        static readonly HashSet<string> s_DataMapPropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "_MetallicGlossMap",
            "_SpecGlossMap",
            "_MetallicMap",
            "_MaskMap",
            "_OcclusionMap",
            "_ParallaxMap",
            "_HeightMap",
            "_DetailMask",
            "_SmoothnessMap",
            "_RoughnessMap",
            "_AOMap",
        };

        // Accumulator: keyed by (descriptorId, texturePath). Each entry holds the texture display
        // name and the ordered, deduplicated set of material asset paths that triggered the rule.
        // Cleared in Finalize so re-runs don't bleed into each other (analyzers are reused).
        readonly Dictionary<(string DescriptorId, string TexturePath), TextureFinding> m_Findings =
            new Dictionary<(string, string), TextureFinding>();

        public override void AnalyzeTextureUsage(TextureUsageAnalysisContext context)
        {
            if (context.TextureImporter == null || string.IsNullOrEmpty(context.TexturePath))
                return;

            var usage = InferUsageFromPropertyName(context.ShaderPropertyName);
            if (usage == TextureUsage.Unknown)
                return;

            var importer = context.TextureImporter;
            var textureName = context.Texture != null ? context.Texture.name : System.IO.Path.GetFileNameWithoutExtension(context.TexturePath);
            var materialPath = context.MaterialPath ?? string.Empty;

            if (usage == TextureUsage.NormalMap)
            {
                if (context.IsDescriptorEnabled(TextureUsedAsNormalMapWrongTypeDescriptor, context.TexturePath) &&
                    importer.textureType != TextureImporterType.NormalMap)
                {
                    RecordFinding(TEX0027, context.TexturePath, textureName, materialPath);
                }

                if (context.IsDescriptorEnabled(TextureUsedAsNormalMapSrgbEnabledDescriptor, context.TexturePath) &&
                    importer.sRGBTexture)
                {
                    RecordFinding(TEX0028, context.TexturePath, textureName, materialPath);
                }

                return;
            }

            // The sRGB (Color Texture) flag only has a meaningful effect in Linear color space.
            // In Gamma space Unity ignores it for the sampling path, so mismatches cause no
            // visible error and should not be reported.
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                return;

            if (usage == TextureUsage.ColorMap &&
                context.IsDescriptorEnabled(TextureUsedAsColorMapSrgbDisabledDescriptor, context.TexturePath) &&
                !importer.sRGBTexture)
            {
                RecordFinding(TEX0029, context.TexturePath, textureName, materialPath);
            }

            if (usage == TextureUsage.DataMap &&
                context.IsDescriptorEnabled(TextureUsedAsDataMapSrgbEnabledDescriptor, context.TexturePath) &&
                importer.sRGBTexture)
            {
                RecordFinding(TEX0030, context.TexturePath, textureName, materialPath);
            }
        }

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
            foreach (var kvp in m_Findings)
            {
                var finding = kvp.Value;
                finding.MaterialPaths.Sort(StringComparer.Ordinal);

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        kvp.Key.DescriptorId,
                        finding.TextureName)
                    .WithLocation(new Location(finding.TexturePath))
                    .WithEvidence("AffectedMaterials", finding.MaterialPaths.ToArray())
                    .WithEvidence("MaterialCount", finding.MaterialPaths.Count);

                context.ReportIssue(diagnostic);
            }

            m_Findings.Clear();
        }

        void RecordFinding(string descriptorId, string texturePath, string textureName, string materialPath)
        {
            var key = (descriptorId, texturePath);
            if (!m_Findings.TryGetValue(key, out var finding))
            {
                finding = new TextureFinding { TextureName = textureName, TexturePath = texturePath };
                m_Findings[key] = finding;
            }

            if (!string.IsNullOrEmpty(materialPath) && !finding.MaterialPaths.Contains(materialPath))
                finding.MaterialPaths.Add(materialPath);
        }

        static TextureUsage InferUsageFromPropertyName(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return TextureUsage.Unknown;

            if (s_NormalMapPropertyNames.Contains(propertyName))
                return TextureUsage.NormalMap;
            if (s_ColorMapPropertyNames.Contains(propertyName))
                return TextureUsage.ColorMap;
            if (s_DataMapPropertyNames.Contains(propertyName))
                return TextureUsage.DataMap;
            return TextureUsage.Unknown;
        }

        enum TextureUsage
        {
            Unknown,
            NormalMap,
            ColorMap,
            DataMap
        }

        sealed class TextureFinding
        {
            public string TextureName;
            public string TexturePath;
            public readonly List<string> MaterialPaths = new List<string>();
        }
    }
}
