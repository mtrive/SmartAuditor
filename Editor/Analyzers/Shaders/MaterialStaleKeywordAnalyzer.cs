// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes materials for shader keyword entries that the currently-assigned shader does not declare.
    /// </summary>
    /// <remarks>
    /// Unity stores three keyword arrays in <c>m_SavedProperties</c>'s sibling fields:
    /// <list type="bullet">
    ///   <item><description><c>m_ValidKeywords</c> — keywords currently enabled on the material.</description></item>
    ///   <item><description><c>m_DisabledKeywords</c> — keywords known to the material but disabled.</description></item>
    ///   <item><description><c>m_ShaderKeywords</c> — legacy whitespace-delimited list (older asset format).</description></item>
    /// </list>
    /// When a shader keyword is renamed or removed, the corresponding entries in these arrays are
    /// orphaned but not cleaned up. The analyzer cross-references each entry against
    /// <see cref="Shader.keywordSpace"/> and reports the entries that no longer match.
    /// </remarks>
    sealed class MaterialStaleKeywordAnalyzer : ShaderAnalyzer
    {
        internal const string MAT0005 = nameof(MAT0005);

        internal static readonly Descriptor StaleKeywordDescriptor = new Descriptor(
            MAT0005,
            "Material: Stale Shader Keyword",
            Impact.Correctness | Impact.BuildSize,
            "The material's keyword arrays hold an entry that the currently-assigned shader does not declare. Stale entries accumulate when a shader's <b>#pragma multi_compile</b> or <b>#pragma shader_feature</b> directive is renamed or removed, and they can re-enter the build through shader-variant collection while doing nothing at runtime.",
            "Reassign the shader in the Material Inspector to refresh the keyword arrays, or strip the stale entries programmatically via <b>SerializedObject</b> on the material asset."
        )
        {
            DefaultSeverity = Severity.Minor,
            // Gated off by default: Shader.keywordSpace only enumerates local keywords. Materials may
            // legitimately enable global keywords set from script (Shader.SetGlobalKeyword) that won't
            // appear in the local space; flagging those as stale would be a false positive. Verify
            // on a broad sample before flipping to enabled by default.
            IsEnabledByDefault = false,
            MessageFormat = "Material '{0}' has stale shader keyword '{1}' not declared by shader '{2}'"
        };

        public override void AnalyzeMaterial(MaterialAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(StaleKeywordDescriptor, context.AssetPath))
            {
                return;
            }

            var material = context.Material;
            var shader = material.shader;

            if (!shader)
            {
                return;
            }

            var keywordSpace = shader.keywordSpace;

            var serializedMaterial = new SerializedObject(material);
            CheckArrayKeywords(context, material, shader, serializedMaterial, "m_ValidKeywords", keywordSpace);
            CheckArrayKeywords(context, material, shader, serializedMaterial, "m_DisabledKeywords", keywordSpace);
            CheckLegacyKeywordString(context, material, shader, serializedMaterial, keywordSpace);
        }

        static void CheckArrayKeywords(
            MaterialAnalysisContext context,
            Material material,
            Shader shader,
            SerializedObject serializedMaterial,
            string propertyName,
            LocalKeywordSpace keywordSpace)
        {
            var property = serializedMaterial.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                var entry = property.GetArrayElementAtIndex(i);
                if (entry == null)
                {
                    continue;
                }

                var keywordName = entry.stringValue;
                if (string.IsNullOrEmpty(keywordName))
                {
                    continue;
                }

                ReportIfStale(context, material, shader, keywordName, keywordSpace);
            }
        }

        static void CheckLegacyKeywordString(
            MaterialAnalysisContext context,
            Material material,
            Shader shader,
            SerializedObject serializedMaterial,
            LocalKeywordSpace keywordSpace)
        {
            var legacyProperty = serializedMaterial.FindProperty("m_ShaderKeywords");
            if (legacyProperty == null || legacyProperty.propertyType != SerializedPropertyType.String)
            {
                return;
            }

            var combined = legacyProperty.stringValue;
            if (string.IsNullOrEmpty(combined))
            {
                return;
            }

            // Legacy format is a space-delimited keyword list (e.g. "_ALPHATEST_ON _EMISSION").
            foreach (var keywordName in combined.Split(s_LegacySeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                ReportIfStale(context, material, shader, keywordName, keywordSpace);
            }
        }

        static void ReportIfStale(
            MaterialAnalysisContext context,
            Material material,
            Shader shader,
            string keywordName,
            LocalKeywordSpace keywordSpace)
        {
            if (keywordSpace.FindKeyword(keywordName).isValid)
            {
                return;
            }

            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    StaleKeywordDescriptor.Id,
                    material.name,
                    keywordName,
                    shader.name)
                .WithLocation(new Location(context.AssetPath))
                .WithEvidence("KeywordName", keywordName)
                .WithEvidence("ShaderName", shader.name);
            context.ReportIssue(diagnostic);
        }

        static readonly char[] s_LegacySeparators = { ' ', '\t' };
    }
}
