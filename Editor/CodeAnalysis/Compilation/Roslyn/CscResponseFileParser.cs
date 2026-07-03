// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.IO;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation.Roslyn
{
    // Parsed representation of a csc.rsp response file. Captures only the
    // flags the Roslyn pipeline currently maps into CSharpParseOptions /
    // CSharpCompilationOptions; unknown flags are recorded but not acted on
    // so the orchestrator can log them once.
    internal sealed class CscResponseFile
    {
        public string LangVersion;
        public bool? AllowUnsafe;
        public bool? WarningsAsErrors;
        public HashSet<string> SpecificWarningsAsErrors { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> SpecificNoWarn { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public List<string> Defines { get; } = new List<string>();
        public string Nullable;
        public List<string> UnknownFlags { get; } = new List<string>();

        public static CscResponseFile Empty => new CscResponseFile();
    }

    internal static class CscResponseFileParser
    {
        // Returns Empty if the path is null/missing; never throws on file IO.
        public static CscResponseFile ParseFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return CscResponseFile.Empty;

            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch (IOException) { return CscResponseFile.Empty; }
            catch (UnauthorizedAccessException) { return CscResponseFile.Empty; }

            return Parse(content);
        }

        public static CscResponseFile Parse(string content)
        {
            var result = new CscResponseFile();
            if (string.IsNullOrEmpty(content))
                return result;

            foreach (var token in Tokenise(content))
                ApplyToken(result, token);

            return result;
        }

        // csc.rsp tokens: one or more flags per line, '#' starts a line
        // comment, quoted strings preserve embedded whitespace. csc itself
        // parses similarly via System.CommandLine; the subset we honour is
        // small enough that we don't need that dependency.
        static IEnumerable<string> Tokenise(string content)
        {
            using var reader = new StringReader(content);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var hashIndex = line.IndexOf('#');
                if (hashIndex >= 0)
                    line = line.Substring(0, hashIndex);

                line = line.Trim();
                if (line.Length == 0)
                    continue;

                foreach (var token in SplitRespectingQuotes(line))
                    yield return token;
            }
        }

        static IEnumerable<string> SplitRespectingQuotes(string line)
        {
            var start = -1;
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    if (start < 0)
                        start = i;
                    continue;
                }

                if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (start >= 0)
                    {
                        yield return Unquote(line.Substring(start, i - start));
                        start = -1;
                    }
                    continue;
                }

                if (start < 0)
                    start = i;
            }
            if (start >= 0)
                yield return Unquote(line.Substring(start));
        }

        static string Unquote(string s) =>
            s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"'
                ? s.Substring(1, s.Length - 2)
                : s;

        static void ApplyToken(CscResponseFile result, string token)
        {
            // csc accepts both "-flag" and "/flag".
            if (token.Length < 2 || (token[0] != '-' && token[0] != '/'))
            {
                result.UnknownFlags.Add(token);
                return;
            }

            var body = token.Substring(1);

            // -warnaserror+ / -warnaserror- (no colon)
            if (StartsWithIgnoreCase(body, "warnaserror"))
            {
                var rest = body.Substring("warnaserror".Length);
                if (rest.Length == 0 || rest == "+")
                    result.WarningsAsErrors = true;
                else if (rest == "-")
                    result.WarningsAsErrors = false;
                else if (rest.StartsWith(":"))
                {
                    foreach (var id in SplitList(rest.Substring(1)))
                        result.SpecificWarningsAsErrors.Add(NormaliseDiagnosticId(id));
                }
                else
                    result.UnknownFlags.Add(token);
                return;
            }

            if (StartsWithIgnoreCase(body, "nowarn:"))
            {
                foreach (var id in SplitList(body.Substring("nowarn:".Length)))
                    result.SpecificNoWarn.Add(NormaliseDiagnosticId(id));
                return;
            }

            if (StartsWithIgnoreCase(body, "langversion:"))
            {
                result.LangVersion = body.Substring("langversion:".Length).Trim();
                return;
            }

            if (StartsWithIgnoreCase(body, "define:") || StartsWithIgnoreCase(body, "d:"))
            {
                var sep = body.IndexOf(':');
                foreach (var d in SplitList(body.Substring(sep + 1)))
                {
                    var trimmed = d.Trim();
                    if (trimmed.Length > 0)
                        result.Defines.Add(trimmed);
                }
                return;
            }

            if (StartsWithIgnoreCase(body, "nullable"))
            {
                var rest = body.Substring("nullable".Length);
                if (rest.Length == 0)
                    result.Nullable = "enable";
                else if (rest.StartsWith(":") && rest.Length > 1)
                    result.Nullable = rest.Substring(1).Trim().ToLowerInvariant();
                else
                    result.UnknownFlags.Add(token);
                return;
            }

            if (string.Equals(body, "unsafe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(body, "unsafe+", StringComparison.OrdinalIgnoreCase))
            {
                result.AllowUnsafe = true;
                return;
            }

            if (string.Equals(body, "unsafe-", StringComparison.OrdinalIgnoreCase))
            {
                result.AllowUnsafe = false;
                return;
            }

            // Recognised but no-op for the analysis pipeline:
            //   -noconfig (we already build args explicitly)
            //   -nostdlib (we resolve mscorlib explicitly)
            if (string.Equals(body, "noconfig", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(body, "nostdlib", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(body, "nostdlib+", StringComparison.OrdinalIgnoreCase))
                return;

            result.UnknownFlags.Add(token);
        }

        static bool StartsWithIgnoreCase(string s, string prefix) =>
            s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        static IEnumerable<string> SplitList(string list)
        {
            foreach (var part in list.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                yield return part.Trim();
        }

        // Normalise diagnostic IDs: csc accepts both "0219" and "CS0219".
        // Roslyn's diagnostic options always use the prefixed form.
        static string NormaliseDiagnosticId(string id)
        {
            id = id.Trim();
            if (id.Length == 0)
                return id;
            if (id.Length >= 2 && (char.IsLetter(id[0]) && char.IsLetter(id[1])))
                return id.ToUpperInvariant();
            return "CS" + id;
        }
    }
}
