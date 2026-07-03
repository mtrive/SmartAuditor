// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.
// SHD0010

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Flags shaders whose declared <c>shader_feature_local*</c> binary toggles span a much larger
    /// combinatorial surface than the materials in the project actually exercise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The analyzer parses each shader's <c>#pragma shader_feature_local*</c> directives for binary
    /// (on/off) keyword toggles, then walks every project material using that shader to count how
    /// many distinct combinations of those toggles are enabled. When the observed combinations are
    /// substantially less than <c>2^K</c> — where <c>K</c> is the number of binary axes — the surface
    /// is over-broad: the material set has an implicit constraint that the shader doesn't encode.
    /// </para>
    /// <para>
    /// This is design-intent guidance, not a build-size win: <c>shader_feature</c> already strips
    /// unused combinations from the variant set. The fix is to collapse mutually-exclusive axes into
    /// a single multi-state keyword so material authors can't introduce a new accidental combination
    /// that adds variants in the future.
    /// </para>
    /// </remarks>
    sealed class ShaderFeatureCoverageAnalyzer : ShaderAnalyzer
    {
        internal const string SHD0010 = nameof(SHD0010);

        // Minimum binary axis count below which the analyzer stays silent — with fewer than three
        // axes, mutual exclusivity isn't structurally interesting and an enum collapse would gain
        // nothing.
        const int k_MinAxes = 3;

        // Coverage threshold: observed combinations divided by 2^K. At or below this fraction the
        // axis surface is judged over-broad enough to flag. 0.5 catches FMUber's 7-of-16 pattern
        // (43.75%) while ignoring shaders whose materials happen to use most combinations.
        const double k_CoverageThreshold = 0.5;

        // Matches #pragma shader_feature_local with optional stage / keyword suffix, captures the keyword list.
        static readonly Regex s_ShaderFeatureLocalRegex = new Regex(
            @"^\s*#\s*pragma\s+shader_feature_local(?:_fragment|_vertex|_compute|_keyword)?\s+(.+)$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        static readonly char[] s_KeywordSeparators = { ' ', '\t' };

        internal static readonly Descriptor CoverageDescriptor = new Descriptor(
            SHD0010,
            "Shader: shader_feature Axes Over-Broad for Material Usage",
            Impact.Quality,
            "The shader declares N independent binary <b>shader_feature_local</b> toggles, but materials in the project exercise only a fraction of the <b>2^N</b> possible combinations. The implicit constraint between toggles (e.g. pairwise mutual exclusivity) isn't encoded in the shader, so a future material can accidentally enable a forbidden combination and grow the compiled variant set.",
            "Collapse mutually-exclusive toggles into a single multi-state <b>shader_feature_local</b> enum keyword (<c>_MODE_NONE _MODE_A _MODE_B _MODE_C</c>). Variant stripping is unchanged; the design contract is now explicit."
        )
        {
            DefaultSeverity = Severity.Info,
            // Advisory only — no variant-count win. Off by default so reports stay focused on
            // actionable issues; enable explicitly for design-review passes.
            IsEnabledByDefault = false,
            MessageFormat = "Shader '{0}' has {1} binary shader_feature axes but materials use {2} of {3} combinations ({4:P0} coverage)"
        };

        // Cached project-wide mapping from shader instance ID to the materials that reference it.
        // Built once per analysis run on first invocation; cleared in Initialize.
        Dictionary<int, List<Material>> m_MaterialsByShader;

        public override void Initialize()
        {
            m_MaterialsByShader = null;
        }

        public override void AnalyzeShader(ShaderAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(CoverageDescriptor, context.AssetPath))
            {
                return;
            }

            var shader = context.Shader;
            if (!shader)
            {
                return;
            }

            var source = context.StrippedSourceCode;
            if (string.IsNullOrEmpty(source))
            {
                // Shadergraphs and built-in shaders don't expose source; their toggle metadata
                // would need a different parser. Skip silently.
                return;
            }

            var binaryAxes = ExtractBinaryShaderFeatureAxes(source);
            if (binaryAxes.Count < k_MinAxes)
            {
                return;
            }

            EnsureMaterialIndex();

            if (!m_MaterialsByShader.TryGetValue(shader.GetInstanceID(), out var materials) || materials.Count == 0)
            {
                return;
            }

            var observed = new HashSet<long>();
            foreach (var material in materials)
            {
                if (!material)
                {
                    continue;
                }

                observed.Add(BuildSignature(material, binaryAxes));
            }

            var axisCount = binaryAxes.Count;
            var total = 1L << axisCount;
            var coverage = (double)observed.Count / total;

            if (coverage > k_CoverageThreshold)
            {
                return;
            }

            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    CoverageDescriptor.Id,
                    shader.name,
                    axisCount,
                    observed.Count,
                    total,
                    coverage)
                .WithLocation(new Location(context.AssetPath))
                .WithEvidence("BinaryAxisCount", axisCount)
                .WithEvidence("ObservedCombinations", observed.Count)
                .WithEvidence("PossibleCombinations", total)
                .WithEvidence("Keywords", binaryAxes.ToArray())
                .WithEvidence("MaterialCount", materials.Count);
            context.ReportIssue(diagnostic);
        }

        // Returns the list of keyword names that participate in BINARY shader_feature_local axes
        // (i.e. declarations of shape `_ _SOME_KEYWORD`). Multi-state axes (`_ _A _B _C`) are
        // skipped — collapsing them is a different conversation.
        internal static List<string> ExtractBinaryShaderFeatureAxes(string strippedSource)
        {
            var result = new List<string>();

            foreach (Match match in s_ShaderFeatureLocalRegex.Matches(strippedSource))
            {
                var keywords = match.Groups[1].Value.Trim()
                    .Split(s_KeywordSeparators, StringSplitOptions.RemoveEmptyEntries);

                // Binary axis shape: "_ KEYWORD" (off + one keyword). Length must be exactly 2 and the
                // first token must be the underscore-only "off" sentinel.
                if (keywords.Length != 2 || keywords[0] != "_")
                {
                    continue;
                }

                result.Add(keywords[1]);
            }

            return result;
        }

        // Encodes the on/off state of each axis as a bit position. Up to 63 axes fit in a long;
        // realistic shaders never approach that limit.
        static long BuildSignature(Material material, List<string> binaryAxes)
        {
            long signature = 0;
            for (int i = 0; i < binaryAxes.Count; i++)
            {
                if (material.IsKeywordEnabled(binaryAxes[i]))
                {
                    signature |= 1L << i;
                }
            }
            return signature;
        }

        void EnsureMaterialIndex()
        {
            if (m_MaterialsByShader != null)
            {
                return;
            }

            m_MaterialsByShader = new Dictionary<int, List<Material>>();

            var guids = AssetDatabase.FindAssets("t:Material");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                {
                    continue;
                }

                var shader = material.shader;
                if (!shader)
                {
                    continue;
                }

                var key = shader.GetInstanceID();
                if (!m_MaterialsByShader.TryGetValue(key, out var list))
                {
                    list = new List<Material>();
                    m_MaterialsByShader[key] = list;
                }
                list.Add(material);
            }
        }
    }
}
