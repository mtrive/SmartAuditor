// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SmartAuditor.Editor.Core;
using Debug = UnityEngine.Debug;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Checks Unity text-serialized assets for structural YAML problems that can prevent
    /// Unity from importing or deserializing the file reliably.
    /// </summary>
    sealed class MalformedUnityYamlAnalyzer : AssetAnalyzer
    {
        internal const string ASF0003 = nameof(ASF0003);

        internal static readonly Descriptor MalformedUnityYamlDescriptor = new Descriptor(
            ASF0003,
            "Asset: Malformed Unity YAML",
            Impact.Correctness,
            "The asset's text serialization does not follow Unity's expected YAML document structure. Bad document markers, duplicate file IDs, tab indentation, or structurally invalid object data can make Unity fail to import the asset or deserialize it inconsistently.",
            "Open the asset in a text editor, fix the reported YAML structure, then reimport the asset. If the file was hand-edited or merged, regenerate it from Unity when possible."
        )
        {
            DefaultSeverity = Severity.Major,
            MessageFormat = "Unity YAML asset '{0}' is malformed: {1}"
        };

        static readonly HashSet<string> s_ValidExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".asset",
            ".prefab",
            ".yaml",
            ".yml"
        };

        static readonly Regex s_DocumentMarkerRegex = new Regex(
            @"^---\s+!u!(\d+)\s+&(-?\d+)(?:\s+stripped)?\s*$",
            RegexOptions.Compiled);

        static readonly Regex s_RootObjectRegex = new Regex(
            @"^[A-Za-z_][A-Za-z0-9_]*:\s*$",
            RegexOptions.Compiled);

        public override void Analyze(AssetAnalysisContext context)
        {
            if (!s_ValidExtensions.Contains(Path.GetExtension(context.AssetPath)))
                return;

            if (!context.IsDescriptorEnabled(MalformedUnityYamlDescriptor, context.AssetPath))
                return;

            string fileText;
            try
            {
                fileText = File.ReadAllText(context.AssetPath);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[MalformedUnityYamlAnalyzer] Could not read '{context.AssetPath}': {e.Message}");
                return;
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogWarning($"[MalformedUnityYamlAnalyzer] Access denied reading '{context.AssetPath}': {e.Message}");
                return;
            }

            if (!TryFindProblem(fileText, out var problem))
                return;

            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    MalformedUnityYamlDescriptor.Id,
                    Path.GetFileName(context.AssetPath),
                    problem.Message)
                .WithLocation(new Location(context.AssetPath, problem.Line, column: problem.Column))
                .WithEvidence("YAML problem", problem.Message);
            context.ReportIssue(diagnostic);
        }

        internal static bool TryFindProblem(string fileText, out YamlProblem problem)
        {
            problem = new YamlProblem();

            if (string.IsNullOrEmpty(fileText))
                return false;

            var lines = SplitLines(fileText);
            if (!LooksLikeUnityYaml(lines))
                return false;

            var fileIds = new HashSet<string>(StringComparer.Ordinal);
            var seenDocument = false;
            var seenRoot = false;
            var inObjectBody = false;
            var blockScalarParentIndent = -1;
            var quotedScalar = '\0';

            for (var i = 0; i < lines.Length; i++)
            {
                var lineNumber = i + 1;
                var line = StripBom(lines[i]);
                var trimmed = line.Trim();

                if (quotedScalar != '\0')
                {
                    if (!ContainsClosingQuote(line, quotedScalar, startIndex: 0))
                        continue;

                    quotedScalar = '\0';
                    continue;
                }

                if (blockScalarParentIndent >= 0)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var indentInBlock = CountLeadingSpaces(line);
                    if (indentInBlock > blockScalarParentIndent)
                        continue;

                    blockScalarParentIndent = -1;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (inObjectBody && HasMoreObjectDataBeforeNextDocument(lines, i + 1))
                        return CreateProblem(out problem, "unexpected blank line inside Unity object data", lineNumber, 1);
                    continue;
                }

                var tabColumn = FirstIndentTabColumn(line);
                if (tabColumn > 0)
                    return CreateProblem(out problem, "tab character used for indentation", lineNumber, tabColumn);

                if (trimmed.StartsWith("%", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("---", StringComparison.Ordinal))
                {
                    var markerMatch = s_DocumentMarkerRegex.Match(line);
                    if (!markerMatch.Success)
                        return CreateProblem(out problem, "invalid Unity YAML document marker", lineNumber, 1);

                    var fileId = markerMatch.Groups[2].Value;
                    if (!fileIds.Add(fileId))
                        return CreateProblem(out problem, $"duplicate Unity fileID '{fileId}'", lineNumber, 1);

                    seenDocument = true;
                    seenRoot = false;
                    inObjectBody = false;
                    continue;
                }

                if (!seenDocument)
                    return CreateProblem(out problem, "content appears before the first Unity YAML document marker", lineNumber, 1);

                var indent = CountLeadingSpaces(line);
                if (!seenRoot)
                {
                    if (indent != 0)
                        return CreateProblem(out problem, "Unity object root is indented", lineNumber, indent + 1);

                    if (!s_RootObjectRegex.IsMatch(line))
                        return CreateProblem(out problem, "missing Unity object root after document marker", lineNumber, 1);

                    seenRoot = true;
                    inObjectBody = true;
                    continue;
                }

                if (indent == 0)
                    return CreateProblem(out problem, "unindented field inside Unity object data", lineNumber, 1);

                if ((indent % 2) != 0)
                    return CreateProblem(out problem, "indentation is not a multiple of two spaces", lineNumber, indent + 1);

                if (StartsBlockScalar(line))
                    blockScalarParentIndent = indent;
                else if (StartsMultilineQuotedScalar(line, out var quote))
                    quotedScalar = quote;
            }

            if (!seenDocument)
                return CreateProblem(out problem, "missing Unity YAML document marker", 1, 1);

            if (!seenRoot)
                return CreateProblem(out problem, "missing Unity object root after document marker", lines.Length, 1);

            return false;
        }

        static bool CreateProblem(out YamlProblem problem, string message, int line, int column)
        {
            problem = new YamlProblem
            {
                Message = message,
                Line = Math.Max(1, line),
                Column = Math.Max(1, column)
            };
            return true;
        }

        static string[] SplitLines(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        static string StripBom(string line)
        {
            if (string.IsNullOrEmpty(line) || line[0] != '\uFEFF')
                return line;
            return line.Substring(1);
        }

        static bool LooksLikeUnityYaml(string[] lines)
        {
            for (var i = 0; i < lines.Length && i < 16; i++)
            {
                var trimmed = StripBom(lines[i]).Trim();
                if (trimmed.Length == 0)
                    continue;

                if (trimmed.StartsWith("%YAML", StringComparison.Ordinal))
                    continue;

                if (trimmed.StartsWith("%TAG !u!", StringComparison.Ordinal) ||
                    trimmed.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    return true;
                }

                return false;
            }

            return false;
        }

        static bool HasMoreObjectDataBeforeNextDocument(string[] lines, int startIndex)
        {
            for (var i = startIndex; i < lines.Length; i++)
            {
                var trimmed = StripBom(lines[i]).Trim();
                if (trimmed.Length == 0)
                    continue;
                return !trimmed.StartsWith("---", StringComparison.Ordinal);
            }

            return false;
        }

        static int CountLeadingSpaces(string line)
        {
            var count = 0;
            while (count < line.Length && line[count] == ' ')
                count++;
            return count;
        }

        static int FirstIndentTabColumn(string line)
        {
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] == ' ')
                    continue;
                if (line[i] == '\t')
                    return i + 1;
                return 0;
            }

            return 0;
        }

        static bool StartsBlockScalar(string line)
        {
            var trimmed = line.TrimEnd();
            return trimmed.EndsWith(": |", StringComparison.Ordinal) ||
                   trimmed.EndsWith(": |-", StringComparison.Ordinal) ||
                   trimmed.EndsWith(": |+", StringComparison.Ordinal) ||
                   trimmed.EndsWith(": >", StringComparison.Ordinal) ||
                   trimmed.EndsWith(": >-", StringComparison.Ordinal) ||
                   trimmed.EndsWith(": >+", StringComparison.Ordinal);
        }

        static bool StartsMultilineQuotedScalar(string line, out char quote)
        {
            quote = '\0';

            var valueStart = FindQuotedScalarValueStart(line);
            if (valueStart < 0)
                return false;

            var first = line[valueStart];
            if (first != '"' && first != '\'')
                return false;

            if (ContainsClosingQuote(line, first, valueStart + 1))
                return false;

            quote = first;
            return true;
        }

        static int FindQuotedScalarValueStart(string line)
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex >= 0)
                return SkipSpaces(line, colonIndex + 1);

            var valueStart = SkipSpaces(line, 0);
            if (valueStart >= 0 && valueStart < line.Length && line[valueStart] == '-')
                return SkipSpaces(line, valueStart + 1);

            return -1;
        }

        static int SkipSpaces(string line, int startIndex)
        {
            var index = Math.Max(0, startIndex);
            while (index < line.Length && line[index] == ' ')
                index++;
            return index < line.Length ? index : -1;
        }

        static bool ContainsClosingQuote(string line, char quote, int startIndex)
        {
            for (var i = Math.Max(0, startIndex); i < line.Length; i++)
            {
                if (line[i] != quote)
                    continue;

                if (quote == '"' && IsEscapedDoubleQuote(line, i))
                    continue;

                if (quote == '\'' && i + 1 < line.Length && line[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                return true;
            }

            return false;
        }

        static bool IsEscapedDoubleQuote(string line, int quoteIndex)
        {
            var slashCount = 0;
            for (var i = quoteIndex - 1; i >= 0 && line[i] == '\\'; i--)
                slashCount++;

            return (slashCount % 2) == 1;
        }

        internal struct YamlProblem
        {
            public string Message;
            public int Line;
            public int Column;
        }
    }
}
