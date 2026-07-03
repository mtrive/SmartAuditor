using System;
using System.Text.RegularExpressions;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes shader source for pragma directives that may negatively affect performance or build size.
    /// </summary>
    sealed class ShaderPragmaAnalyzer : ShaderAnalyzer
    {
        internal const string SHD0004 = nameof(SHD0004);
        internal const string SHD0005 = nameof(SHD0005);
        internal const string SHD0006 = nameof(SHD0006);

        internal static readonly Descriptor DebugSymbolsDescriptor = new Descriptor(
            SHD0004,
            "Shader: Debug Symbols Pragma",
            Impact.BuildSize,
            "The shader contains a <b>#pragma enable_d3d11_debug_symbols</b> directive. The directive embeds debug information in the compiled shader, inflating the shipped build and slowing compilation. It is intended for local debugging only and should not appear in a shipping build.",
            "Remove the <b>#pragma enable_d3d11_debug_symbols</b> line from the shader before releasing.")
        {
            MessageFormat = "Shader '{0}' contains #pragma enable_d3d11_debug_symbols",
            DocumentationUrl = "https://docs.unity3d.com/Manual/SL-PragmaDirectives.html"
        };

        internal static readonly Descriptor MultiCompileDescriptor = new Descriptor(
            SHD0005,
            "Shader: multi_compile Inflates Variant Count",
            Impact.BuildSize,
            "<b>#pragma multi_compile</b> compiles every keyword combination into the build, whether or not a material actually uses each combination. <b>#pragma shader_feature</b> compiles only the combinations that are enabled on materials referenced by the build, which can dramatically shrink shader build size and runtime memory.",
            "Replace <b>#pragma multi_compile</b> with <b>#pragma shader_feature</b> when the keywords are toggled per-material and not switched at runtime from script.")
        {
            MessageFormat = "Shader '{0}' uses #pragma multi_compile '{1}'",
            DocumentationUrl = "https://docs.unity3d.com/Manual/SL-MultipleProgramVariants.html"
        };

        internal static readonly Descriptor GlobalKeywordDescriptor = new Descriptor(
            SHD0006,
            "Shader: Global Keyword Space Consumed",
            Impact.Memory,
            "<b>#pragma multi_compile</b> and <b>#pragma shader_feature</b> (without the <b>_local</b> suffix) consume entries in the project-wide global keyword space, which is capped at 384 entries. The <b>_local</b> variants confine keywords to the per-shader keyword space and do not contribute to that cap.",
            "Replace <b>#pragma multi_compile</b> with <b>#pragma multi_compile_local</b>, or <b>#pragma shader_feature</b> with <b>#pragma shader_feature_local</b>, when the keyword does not need to be set globally from script.")
        {
            IsEnabledByDefault = false,
            MessageFormat = "Shader '{0}' uses global keyword pragma '{1}'",
            DocumentationUrl = "https://docs.unity3d.com/Manual/shader-keywords.html"
        };

        // Matches #pragma enable_d3d11_debug_symbols (allowing optional leading whitespace before #)
        static readonly Regex s_DebugSymbolsRegex = new Regex(
            @"^\s*#\s*pragma\s+enable_d3d11_debug_symbols",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches #pragma multi_compile followed by a whitespace-delimited keyword list. The
        // `\s+` after `multi_compile` rejects `multi_compile_local` and `multi_compile_instancing`
        // implicitly (they have `_` immediately after, not whitespace), so no separate "is this
        // a local variant?" check is needed downstream — but built-in shorthands still need to
        // be filtered out via s_BuiltinMultiCompileRegex.
        // Group 1 captures the keyword list (everything after 'multi_compile' on the same line).
        static readonly Regex s_MultiCompileRegex = new Regex(
            @"^\s*#\s*pragma\s+multi_compile\s+(.+)$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches #pragma shader_feature followed by a keyword list. Same `\s+` rejection of
        // `shader_feature_local` applies.
        // Group 1 captures the keyword list.
        static readonly Regex s_ShaderFeatureRegex = new Regex(
            @"^\s*#\s*pragma\s+shader_feature\s+(.+)$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches the Unity built-in multi_compile shorthand pragmas that should not trigger SHD0005.
        // These expand to well-known built-in keyword sets and are not user-defined multi_compile lists.
        static readonly Regex s_BuiltinMultiCompileRegex = new Regex(
            @"^\s*#\s*pragma\s+multi_compile_(?:instancing|fog|fwdbase|fwdadd(?:_fullshadows)?|shadowcaster|shadowcollector|prepassfinal|particles)\b",
            RegexOptions.Multiline | RegexOptions.Compiled);

        public override void AnalyzeShader(ShaderAnalysisContext context)
        {
            var strippedSource = context.StrippedSourceCode;
            if (string.IsNullOrEmpty(strippedSource))
                return;

            CheckDebugSymbols(context, strippedSource);
            CheckMultiCompile(context, strippedSource);
            CheckGlobalKeywords(context, strippedSource);
        }

        void CheckDebugSymbols(ShaderAnalysisContext context, string strippedSource)
        {
            if (!context.IsDescriptorEnabled(DebugSymbolsDescriptor, context.AssetPath))
                return;

            var match = s_DebugSymbolsRegex.Match(strippedSource);
            if (!match.Success)
                return;

            var line = GetLineNumber(strippedSource, match.Index);
            var diagnostic = Diagnostic.Create(
                AnalysisCategory.AssetIssue,
                DebugSymbolsDescriptor.Id,
                context.Shader.name)
                .WithLocation(new Location(context.AssetPath, line));
            context.ReportIssue(diagnostic);
        }

        void CheckMultiCompile(ShaderAnalysisContext context, string strippedSource)
        {
            if (!context.IsDescriptorEnabled(MultiCompileDescriptor, context.AssetPath))
                return;

            foreach (Match match in s_MultiCompileRegex.Matches(strippedSource))
            {
                var fullLine = match.Value.Trim();

                // Skip built-in Unity shorthand pragmas (multi_compile_instancing, multi_compile_fog, etc.)
                if (s_BuiltinMultiCompileRegex.IsMatch(fullLine))
                    continue;

                var keywordList = match.Groups[1].Value.Trim();
                var keywords = SplitKeywords(keywordList);
                var line = GetLineNumber(strippedSource, match.Index);
                var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    MultiCompileDescriptor.Id,
                    context.Shader.name,
                    keywordList)
                    .WithLocation(new Location(context.AssetPath, line))
                    .WithEvidence("Keywords", keywords);
                context.ReportIssue(diagnostic);
            }
        }

        void CheckGlobalKeywords(ShaderAnalysisContext context, string strippedSource)
        {
            if (!context.IsDescriptorEnabled(GlobalKeywordDescriptor, context.AssetPath))
                return;

            foreach (Match match in s_MultiCompileRegex.Matches(strippedSource))
            {
                var fullLine = match.Value.Trim();

                // Skip built-in Unity shorthand pragmas
                if (s_BuiltinMultiCompileRegex.IsMatch(fullLine))
                    continue;

                var keywordList = match.Groups[1].Value.Trim();
                var keywords = SplitKeywords(keywordList);
                var line = GetLineNumber(strippedSource, match.Index);
                var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    GlobalKeywordDescriptor.Id,
                    context.Shader.name,
                    $"multi_compile {keywordList}")
                    .WithLocation(new Location(context.AssetPath, line))
                    .WithEvidence("PragmaType", "multi_compile")
                    .WithEvidence("Keywords", keywords);
                context.ReportIssue(diagnostic);
            }

            foreach (Match match in s_ShaderFeatureRegex.Matches(strippedSource))
            {
                var keywordList = match.Groups[1].Value.Trim();
                var keywords = SplitKeywords(keywordList);
                var line = GetLineNumber(strippedSource, match.Index);
                var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    GlobalKeywordDescriptor.Id,
                    context.Shader.name,
                    $"shader_feature {keywordList}")
                    .WithLocation(new Location(context.AssetPath, line))
                    .WithEvidence("PragmaType", "shader_feature")
                    .WithEvidence("Keywords", keywords);
                context.ReportIssue(diagnostic);
            }
        }

        static readonly char[] s_KeywordSeparators = { ' ', '\t' };

        static string[] SplitKeywords(string keywordList)
        {
            if (string.IsNullOrWhiteSpace(keywordList))
                return Array.Empty<string>();
            return keywordList.Split(s_KeywordSeparators, StringSplitOptions.RemoveEmptyEntries);
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
