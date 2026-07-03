using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ShaderFallbackAnalyzer : ShaderAnalyzer
    {
        internal const string SHD0003 = nameof(SHD0003);

        internal static readonly Descriptor ShaderFallbackDescriptor = new Descriptor(
            SHD0003,
            "Shader: Fallback Shader Not Found",
            Impact.Correctness,
            "The shader's <b>Fallback</b> directive names a shader that <b>Shader.Find</b> cannot resolve. When the main shader is unsupported on the current target, Unity will render with the magenta error shader instead of the intended fallback.",
            "Update the <b>Fallback</b> name to reference an existing shader, or delete the directive if the project does not need a fallback path.")
        {
            MessageFormat = "Shader '{0}' fallback '{1}' cannot be resolved",
            DocumentationUrl = "https://docs.unity3d.com/Manual/SL-Fallback.html"
        };

        public override void AnalyzeShader(ShaderAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(ShaderFallbackDescriptor, context.AssetPath))
                return;

            if (string.IsNullOrEmpty(context.StrippedSourceCode))
                return;

            foreach (var fallback in ExtractFallbackReferences(context.StrippedSourceCode))
            {
                var fallbackName = fallback.Key;
                if (IsFallbackDisabled(fallbackName))
                    continue;

                // Shader.Find is Unity's own resolver — it knows about project shaders, package
                // shaders, URP/HDRP pipeline shaders, and the legacy built-ins. A Hidden/ shader
                // that doesn't resolve is treated as a built-in we can't see from edit time
                // rather than a missing reference.
                if (Shader.Find(fallbackName) != null)
                    continue;
                if (fallbackName.StartsWith("Hidden/", StringComparison.Ordinal))
                    continue;

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        ShaderFallbackDescriptor.Id,
                        context.Shader.name,
                        fallbackName)
                    .WithLocation(new Location(context.AssetPath, fallback.Value))
                    .WithEvidence("FallbackShaderName", fallbackName);
                context.ReportIssue(diagnostic);
            }
        }

        /// <summary>
        /// Extracts all unique fallback shader names from shader source code, mapped to the line
        /// they appear on.
        /// </summary>
        static Dictionary<string, int> ExtractFallbackReferences(string strippedSource)
        {
            var fallbackReferences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in s_FallbackRegex.Matches(strippedSource))
            {
                var fallbackName = GetFallbackName(match);
                if (string.IsNullOrEmpty(fallbackName))
                    continue;
                if (fallbackReferences.ContainsKey(fallbackName))
                    continue;

                fallbackReferences[fallbackName] = GetLineNumber(strippedSource, match.Index);
            }

            return fallbackReferences;
        }

        static bool IsFallbackDisabled(string fallbackName)
        {
            return fallbackName.Equals("off", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts the fallback name from a regex match. The regex has three alternative capture
        /// groups (double-quoted, single-quoted, or unquoted); only one is populated per match.
        /// </summary>
        static string GetFallbackName(Match match)
        {
            for (var i = 1; i < match.Groups.Count; ++i)
            {
                var value = match.Groups[i].Value;
                if (!string.IsNullOrEmpty(value))
                    return value.Trim();
            }
            return string.Empty;
        }

        static int GetLineNumber(string source, int index)
        {
            if (string.IsNullOrEmpty(source))
                return 1;
            if (index < 0)
                return 1;
            if (index >= source.Length)
                index = source.Length - 1;

            var line = 1;
            for (var i = 0; i < index; i++)
            {
                if (source[i] == '\n')
                    line++;
            }
            return line;
        }

        // Matches ShaderLab Fallback statements in three formats:
        // - Fallback "ShaderName"   (double quotes - Group 1)
        // - Fallback 'ShaderName'   (single quotes - Group 2)
        // - Fallback ShaderName     (no quotes - Group 3, stops at whitespace/braces/newlines)
        static readonly Regex s_FallbackRegex = new Regex(
            @"Fallback\s+(?:""([^""]+)""|'([^']+)'|([^\s{}\n\r]+))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
