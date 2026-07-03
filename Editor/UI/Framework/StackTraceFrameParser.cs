// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Parses Unity stack trace text into clickable frames.
    /// </summary>
    internal static class StackTraceFrameParser
    {
        static readonly Regex s_AtFileLineRegex = new Regex(
            @"\(at\s+(?<path>.+?):(?<line>\d+)\)",
            RegexOptions.Compiled);

        static readonly Regex s_GenericFileLineRegex = new Regex(
            @"(?<path>(?:Assets|Packages)/[^:\r\n]+):(?<line>\d+)",
            RegexOptions.Compiled);

        internal readonly struct StackTraceFrame
        {
            public StackTraceFrame(string rawLine, string path, int line)
            {
                RawLine = rawLine ?? string.Empty;
                Path = path ?? string.Empty;
                Line = line;
            }

            public string RawLine { get; }
            public string Path { get; }
            public int Line { get; }
            public bool IsOpenable => !string.IsNullOrEmpty(Path) && Line > 0;
        }

        public static List<StackTraceFrame> Parse(string stackTrace)
        {
            var results = new List<StackTraceFrame>();
            if (string.IsNullOrEmpty(stackTrace))
                return results;

            var lines = stackTrace.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                if (TryParseFrame(rawLine, out var frame))
                    results.Add(frame);
                else
                    results.Add(new StackTraceFrame(rawLine, string.Empty, 0));
            }

            return results;
        }

        static bool TryParseFrame(string rawLine, out StackTraceFrame frame)
        {
            var match = s_AtFileLineRegex.Match(rawLine);
            if (!match.Success)
                match = s_GenericFileLineRegex.Match(rawLine);

            if (match.Success &&
                int.TryParse(match.Groups["line"].Value, out var line))
            {
                var path = NormalizePath(match.Groups["path"].Value);
                frame = new StackTraceFrame(rawLine, path, line);
                return true;
            }

            frame = default;
            return false;
        }

        static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            return path.Replace('\\', '/');
        }
    }
}
