using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes ShaderLab <c>Tags { ... }</c> blocks for tag keys that are not recognized by Unity.
    /// Unknown tag keys are silently ignored, so a typo such as <c>RenderingPipeline</c> for
    /// <c>RenderPipeline</c> compiles cleanly while dropping the behaviour the author intended.
    /// </summary>
    sealed class ShaderTagsAnalyzer : ShaderAnalyzer
    {
        internal const string SHD0011 = nameof(SHD0011);
        internal const string SHD0012 = nameof(SHD0012);

        internal static readonly Descriptor MisspelledTagKeyDescriptor = new Descriptor(
            SHD0011,
            "Shader: Misspelled Tag Key",
            Impact.Correctness,
            "A tag key in a <b>Tags</b> block closely matches a built-in ShaderLab tag but is not spelled exactly, so Unity silently ignores it. For example <b>RenderingPipeline</b> instead of <b>RenderPipeline</b> leaves the SubShader with no render-pipeline tag and breaks pipeline matching.",
            "Correct the tag key to the suggested spelling. If it is an intentional custom tag, rename it so it does not resemble a built-in key.")
        {
            DefaultSeverity = Severity.Major,
            MessageFormat = "Shader '{0}' tag key '{1}' looks like a typo of '{2}'",
            DocumentationUrl = "https://docs.unity3d.com/Manual/SL-SubShaderTags.html"
        };

        internal static readonly Descriptor UnknownTagKeyDescriptor = new Descriptor(
            SHD0012,
            "Shader: Unrecognized Tag Key",
            Impact.Correctness,
            "A tag key in a <b>Tags</b> block is not one of Unity's built-in ShaderLab tags. Unity silently ignores unrecognized keys, so a mistyped key has no effect at runtime. Custom tags are legal and read back via <b>Material.GetTag</b>, which is why this check is disabled by default.",
            "Enable this diagnostic only when auditing for stray tag keys. Remove or correct keys that are not intentional custom tags.")
        {
            IsEnabledByDefault = false,
            MessageFormat = "Shader '{0}' has unrecognized {1}-level tag key '{2}'",
            DocumentationUrl = "https://docs.unity3d.com/Manual/SL-SubShaderTags.html"
        };

        // Built-in SubShader-level tag keys (https://docs.unity3d.com/Manual/SL-SubShaderTags.html).
        static readonly HashSet<string> s_SubShaderTagKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CanUseSpriteAtlas",
            "DisableBatching",
            "ForceNoShadowCasting",
            "IgnoreProjector",
            "PreviewType",
            "Queue",
            "RenderPipeline",
            "RenderType",
        };

        // Built-in Pass-level tag keys (https://docs.unity3d.com/Manual/SL-PassTags.html). Passes also
        // permit arbitrary user-defined tags, so the unknown-key check is intentionally lenient here:
        // a Pass key is only ever surfaced as a near-miss of one of these, never as a bare unknown.
        static readonly HashSet<string> s_PassTagKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LightMode",
            "PassFlags",
            "RequireOptions",
        };

        enum TagScope
        {
            Other,
            SubShader,
            Pass,
        }

        // Shader program bodies are blanked out (newlines preserved) before scanning so their HLSL
        // braces never disturb the ShaderLab brace tracking below.
        static readonly Regex s_ProgramBlockRegex = new Regex(
            @"(?:CGPROGRAM|HLSLPROGRAM|CGINCLUDE|HLSLINCLUDE).*?(?:ENDCG|ENDHLSL)",
            RegexOptions.Singleline | RegexOptions.Compiled);

        static readonly Regex s_NonNewlineRegex = new Regex(@"[^\n]", RegexOptions.Compiled);

        // One pass over the structural tokens: a block opener that carries its own brace, the opening
        // of a Tags block, or a bare brace. Matching `keyword{` (rather than the bare keyword) keeps a
        // keyword inside a string literal -- e.g. Shader "Custom/Pass" -- from being read as a block.
        static readonly Regex s_StructureRegex = new Regex(
            @"(?<sskw>SubShader|GrabPass|Pass|Category|Properties)\s*\{|(?<tags>Tags\s*\{)|(?<open>\{)|(?<close>\})",
            RegexOptions.Compiled);

        // A tag key, optionally quoted, immediately followed by '='.
        static readonly Regex s_TagKeyRegex = new Regex(
            "\"?(?<key>[A-Za-z_][A-Za-z0-9_]*)\"?\\s*=",
            RegexOptions.Compiled);

        public override void AnalyzeShader(ShaderAnalysisContext context)
        {
            var source = context.StrippedSourceCode;
            if (string.IsNullOrEmpty(source))
                return;

            var nearMissEnabled = context.IsDescriptorEnabled(MisspelledTagKeyDescriptor, context.AssetPath);
            var unknownEnabled = context.IsDescriptorEnabled(UnknownTagKeyDescriptor, context.AssetPath);
            if (!nearMissEnabled && !unknownEnabled)
                return;

            var shaderLab = BlankProgramBlocks(source);

            var scopes = new Stack<TagScope>();
            var skipUntil = -1;

            foreach (Match match in s_StructureRegex.Matches(shaderLab))
            {
                // Tokens inside a Tags block we already consumed (e.g. its closing brace) are skipped
                // so they never push or pop the scope stack.
                if (match.Index <= skipUntil)
                    continue;

                if (match.Groups["sskw"].Success)
                {
                    scopes.Push(ScopeForKeyword(match.Value));
                }
                else if (match.Groups["tags"].Success)
                {
                    var scope = scopes.Count > 0 ? scopes.Peek() : TagScope.Other;
                    var bodyStart = match.Index + match.Length;
                    var bodyEnd = shaderLab.IndexOf('}', bodyStart);
                    if (bodyEnd < 0)
                        break; // unterminated Tags block: nothing reliable left to parse

                    AnalyzeTagsBlock(context, shaderLab, bodyStart, bodyEnd, scope, nearMissEnabled, unknownEnabled);
                    skipUntil = bodyEnd;
                }
                else if (match.Groups["open"].Success)
                {
                    scopes.Push(TagScope.Other);
                }
                else // close
                {
                    if (scopes.Count > 0)
                        scopes.Pop();
                }
            }
        }

        void AnalyzeTagsBlock(ShaderAnalysisContext context, string shaderLab, int bodyStart, int bodyEnd,
            TagScope scope, bool nearMissEnabled, bool unknownEnabled)
        {
            // Tags are only meaningful directly under a SubShader or a Pass; anything else is skipped
            // rather than guessed at.
            if (scope == TagScope.Other)
                return;

            var knownKeys = scope == TagScope.Pass ? s_PassTagKeys : s_SubShaderTagKeys;
            var body = shaderLab.Substring(bodyStart, bodyEnd - bodyStart);

            foreach (Match keyMatch in s_TagKeyRegex.Matches(body))
            {
                var key = keyMatch.Groups["key"].Value;
                if (knownKeys.Contains(key))
                    continue;

                var line = GetLineNumber(shaderLab, bodyStart + keyMatch.Index);
                var nearest = NearestKey(key, knownKeys, out var distance);

                if (nearest != null && nearMissEnabled)
                {
                    var diagnostic = Diagnostic.Create(
                            AnalysisCategory.AssetIssue,
                            MisspelledTagKeyDescriptor.Id,
                            context.Shader.name,
                            key,
                            nearest)
                        .WithLocation(new Location(context.AssetPath, line))
                        .WithEvidence("TagScope", scope.ToString())
                        .WithEvidence("EditDistance", distance);
                    context.ReportIssue(diagnostic);
                }
                else if (nearest == null && unknownEnabled && scope == TagScope.SubShader)
                {
                    // Pass-level Tags blocks may carry arbitrary user-defined keys (read back via
                    // Shader.FindPassTagValue), so an unrecognized Pass key is never surfaced here --
                    // only a near-miss of a built-in Pass key is.
                    var diagnostic = Diagnostic.Create(
                            AnalysisCategory.AssetIssue,
                            UnknownTagKeyDescriptor.Id,
                            context.Shader.name,
                            scope.ToString(),
                            key)
                        .WithLocation(new Location(context.AssetPath, line))
                        .WithEvidence("TagScope", scope.ToString());
                    context.ReportIssue(diagnostic);
                }
            }
        }

        static TagScope ScopeForKeyword(string match)
        {
            // `match` is the keyword plus its trailing whitespace and brace (e.g. "SubShader {").
            if (match.StartsWith("SubShader", StringComparison.Ordinal))
                return TagScope.SubShader;
            if (match.StartsWith("Pass", StringComparison.Ordinal) || match.StartsWith("GrabPass", StringComparison.Ordinal))
                return TagScope.Pass;
            return TagScope.Other;
        }

        // Returns the closest built-in key when `key` is a near-miss of one, otherwise null. A
        // near-miss is a small edit distance relative to length, so a genuine custom tag (far from
        // every built-in key) returns null and is left to the unknown-key check.
        static string NearestKey(string key, HashSet<string> knownKeys, out int distance)
        {
            string best = null;
            var bestDistance = int.MaxValue;
            foreach (var candidate in knownKeys)
            {
                var d = Levenshtein(key, candidate);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = candidate;
                }
            }

            distance = bestDistance;
            if (best == null)
                return null;

            var longer = Math.Max(key.Length, best.Length);
            var isNearMiss = bestDistance >= 1 && bestDistance <= 3 && bestDistance * 3 <= longer;
            return isNearMiss ? best : null;
        }

        static int Levenshtein(string a, string b)
        {
            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();

            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];

            for (var j = 0; j <= b.Length; j++)
                previous[j] = j;

            for (var i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }

                (previous, current) = (current, previous);
            }

            return previous[b.Length];
        }

        static string BlankProgramBlocks(string source)
        {
            return s_ProgramBlockRegex.Replace(source, m => s_NonNewlineRegex.Replace(m.Value, " "));
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
    }
}
