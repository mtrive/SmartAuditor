// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.IO;

namespace SmartAuditor.Editor.UI.Framework
{
    internal static class CodeSnippetCache
    {
        const long k_MaxFileSizeBytes = 200 * 1024;

        static readonly HashSet<string> s_TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".shader",
            ".cginc",
            ".hlsl",
            ".compute",
            ".uss",
            ".uxml",
            ".asset",
            ".meta",
            ".prefab",
            ".txt",
            ".json",
            ".xml",
            ".yaml",
            ".yml",
            ".md"
        };

        struct CacheEntry
        {
            public DateTime LastWriteTimeUtc;
            public string[] Lines;
        }

        static readonly Dictionary<string, CacheEntry> s_Cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        // Resolves a virtual Unity asset path to a real on-disk path.
        //
        // ReportItem.RelativePath uses Unity's virtual roots (Assets/..., Packages/<name>/...).
        // Combining with the project root works for Assets/ and for embedded/local packages
        // (whose physical files live at <project>/Packages/<name>/...) but NOT for registry
        // packages: those live at <project>/Library/PackageCache/<name>@<hash>/... and only
        // appear under Packages/<name>/ as a Unity-virtual mapping. PackageInfo.FindForAssetPath
        // returns the real resolved path for any kind of package, so use it for Packages/...
        // paths and fall back to project-root combining for Assets/ and root-level files.
        static string ResolveFullPath(string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
                return relativePath;

            var normalised = relativePath.Replace('\\', '/');
            if (normalised.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(normalised);
                if (package != null)
                {
                    var prefix = "Packages/" + package.name;
                    if (normalised.Length > prefix.Length &&
                        normalised[prefix.Length] == '/' &&
                        normalised.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var remainder = normalised.Substring(prefix.Length + 1);
                        return Path.Combine(package.resolvedPath, remainder);
                    }
                }
            }

            return Path.Combine(SmartAuditor.ProjectPath, relativePath);
        }

        public static bool TryGetLines(string relativePath, out string[] lines)
        {
            lines = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            var fullPath = ResolveFullPath(relativePath);
            if (!File.Exists(fullPath))
                return false;

            var extension = Path.GetExtension(fullPath);
            if (string.IsNullOrEmpty(extension) || !s_TextExtensions.Contains(extension))
                return false;

            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(fullPath);
            if (s_Cache.TryGetValue(fullPath, out var cached) && cached.LastWriteTimeUtc == lastWriteTimeUtc)
            {
                lines = cached.Lines;
                return lines.Length > 0;
            }

            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > k_MaxFileSizeBytes)
                return false;

            try
            {
                lines = File.ReadAllLines(fullPath);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            s_Cache[fullPath] = new CacheEntry
            {
                LastWriteTimeUtc = lastWriteTimeUtc,
                Lines = lines
            };

            return lines.Length > 0;
        }
    }

    internal static class SnippetWindow
    {
        public static SnippetWindowResult Compute(
            int line,
            int? endLine,
            int totalLines,
            int contextBefore,
            int contextAfter,
            int maxInitialLines,
            int maxExpandedLines,
            bool expanded)
        {
            if (line < 1 || totalLines < 1)
                return SnippetWindowResult.Empty;

            var safeMaxInitialLines = Math.Max(1, maxInitialLines);
            var safeMaxExpandedLines = Math.Max(safeMaxInitialLines, maxExpandedLines);
            var issueStart = Math.Max(1, Math.Min(line, totalLines));
            var issueEndCandidate = endLine ?? issueStart;
            var issueEnd = Math.Max(issueStart, Math.Min(issueEndCandidate, totalLines));

            var contextFirst = Math.Max(1, issueStart - Math.Max(0, contextBefore));
            var contextLast = Math.Min(totalLines, issueEnd + Math.Max(0, contextAfter));

            if (expanded)
            {
                var expandedBounds = GetExpandedWindowBounds(
                    issueStart,
                    issueEnd,
                    totalLines,
                    contextFirst,
                    contextLast,
                    safeMaxExpandedLines);

                return new SnippetWindowResult(
                    expandedBounds.First,
                    expandedBounds.Last,
                    expandedBounds.First,
                    expandedBounds.Last,
                    isTruncated: false);
            }

            var fullFirst = contextFirst;
            var fullLast = contextLast;
            var fullCount = fullLast - fullFirst + 1;

            if (fullCount <= safeMaxInitialLines)
            {
                var expandedBounds = GetExpandedWindowBounds(
                    issueStart,
                    issueEnd,
                    totalLines,
                    contextFirst,
                    contextLast,
                    safeMaxExpandedLines);
                var expandedCount = expandedBounds.Last - expandedBounds.First + 1;

                return new SnippetWindowResult(
                    fullFirst,
                    fullLast,
                    fullFirst,
                    fullLast,
                    isTruncated: expandedCount > fullCount);
            }

            var issueCenter = issueStart + (issueEnd - issueStart) / 2;
            var visibleFirst = issueCenter - safeMaxInitialLines / 2;
            var visibleLast = visibleFirst + safeMaxInitialLines - 1;

            if (visibleFirst < fullFirst)
            {
                var shift = fullFirst - visibleFirst;
                visibleFirst += shift;
                visibleLast += shift;
            }

            if (visibleLast > fullLast)
            {
                var shift = visibleLast - fullLast;
                visibleFirst -= shift;
                visibleLast -= shift;
            }

            visibleFirst = Math.Max(fullFirst, visibleFirst);
            visibleLast = Math.Min(fullLast, visibleLast);

            return new SnippetWindowResult(
                visibleFirst,
                visibleLast,
                fullFirst,
                fullLast,
                isTruncated: true);
        }

        static (int First, int Last) GetExpandedWindowBounds(
            int issueStart,
            int issueEnd,
            int totalLines,
            int contextFirst,
            int contextLast,
            int maxExpandedLines)
        {
            var centered = ComputeCenteredLineWindow(issueStart, issueEnd, totalLines, maxExpandedLines);
            return (Math.Min(contextFirst, centered.First), Math.Max(contextLast, centered.Last));
        }

        static (int First, int Last) ComputeCenteredLineWindow(
            int issueStart,
            int issueEnd,
            int totalLines,
            int lineCount)
        {
            var safeCount = Math.Max(1, lineCount);
            var issueSpan = issueEnd - issueStart + 1;
            var surrounding = Math.Max(0, safeCount - issueSpan);
            var before = surrounding / 2 + surrounding % 2;
            var after = surrounding / 2;

            var first = Math.Max(1, issueStart - before);
            var last = Math.Min(totalLines, issueEnd + after);

            if (last - first + 1 < safeCount)
            {
                if (first == 1)
                    last = Math.Min(totalLines, first + safeCount - 1);
                else if (last == totalLines)
                    first = Math.Max(1, last - safeCount + 1);
            }

            return (first, last);
        }
    }

    internal readonly struct SnippetWindowResult
    {
        public static SnippetWindowResult Empty => new SnippetWindowResult(0, 0, 0, 0, isTruncated: false);

        public SnippetWindowResult(int firstLine, int lastLine, int fullFirstLine, int fullLastLine, bool isTruncated)
        {
            FirstLine = firstLine;
            LastLine = lastLine;
            FullFirstLine = fullFirstLine;
            FullLastLine = fullLastLine;
            IsTruncated = isTruncated;
        }

        public int FirstLine { get; }
        public int LastLine { get; }
        public int FullFirstLine { get; }
        public int FullLastLine { get; }
        public bool IsTruncated { get; }
        public int FullLineCount => FullLastLine >= FullFirstLine ? FullLastLine - FullFirstLine + 1 : 0;
        public bool IsValid => FirstLine > 0 && LastLine >= FirstLine;
    }
}
