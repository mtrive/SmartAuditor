// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using Debug = UnityEngine.Debug;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class DuplicateUnityGuidAnalyzer : ProjectAssetAnalyzer
    {
        internal const string ASF0004 = nameof(ASF0004);

        internal static readonly Descriptor DuplicateUnityGuidDescriptor = new Descriptor(
            ASF0004,
            "Asset: Duplicate Unity GUID",
            Impact.Correctness,
            "Two or more Unity <b>.meta</b> files declare the same <b>guid</b>. Unity uses GUIDs as the stable identity for serialized asset references, so a collision can make references resolve to the wrong asset or become unstable across imports and version-control checkouts.",
            "Keep the <b>.meta</b> file that belongs to the intended asset. Regenerate or replace the duplicated <b>.meta</b> file for the other asset, then reassign any broken references in Unity."
        )
        {
            DefaultSeverity = Severity.Major,
            MessageFormat = "Unity GUID '{0}' is declared by {1} meta files"
        };

        const string k_MetaExtension = ".meta";

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
            if (!context.IsDescriptorEnabled(DuplicateUnityGuidDescriptor))
                return;

            foreach (var clash in FindDuplicateGuids(context.Options, SmartAuditor.ProjectPath))
            {
                var primary = clash.MetaFiles.First(entry => entry.IsReportable);
                var duplicatePaths = clash.MetaFiles
                    .Select(entry => entry.ProjectRelativePath)
                    .ToArray();

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        DuplicateUnityGuidDescriptor.Id,
                        clash.Guid,
                        clash.MetaFiles.Count)
                    .WithLocation(new Location(primary.ProjectRelativePath, primary.GuidLine))
                    .WithEvidence("Meta files", duplicatePaths);
                context.ReportIssue(diagnostic);
            }
        }

        internal static List<GuidClash> FindDuplicateGuids(AnalysisOptions options, string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                return new List<GuidClash>();

            // Normalize once; every per-file relative-path derivation reuses this string.
            var projectRoot = Path.GetFullPath(projectPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            var metaPaths = EnumerateMetaFiles(projectRoot);

            // Read every meta file in parallel (pure filesystem work, no Unity API).
            // Each slot holds (projectRelativePath, guid, line) or default when skipped.
            var results = new (string RelPath, string Guid, int Line)[metaPaths.Length];
            Parallel.For(0, metaPaths.Length, i =>
            {
                var relPath = ToProjectRelativePath(projectRoot, metaPaths[i]);
                if (string.IsNullOrEmpty(relPath))
                    return;

                if (!TryReadMetaGuid(metaPaths[i], relPath, out var guid, out var line))
                    return;

                results[i] = (relPath, guid, line);
            });

            // Sequential grouping pass — no synchronization needed after the parallel phase.
            var byGuid = new Dictionary<string, List<MetaGuidEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (relPath, guid, line) in results)
            {
                if (relPath == null)
                    continue;

                if (!byGuid.TryGetValue(guid, out var entries))
                {
                    entries = new List<MetaGuidEntry>();
                    byGuid[guid] = entries;
                }

                entries.Add(new MetaGuidEntry(relPath, line, ShouldReportMetaPath(options, relPath)));
            }

            return byGuid
                .Where(kv => kv.Value.Count > 1 && kv.Value.Any(entry => entry.IsReportable))
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new GuidClash(kv.Key, kv.Value.OrderBy(e => e.ProjectRelativePath, StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();
        }

        // Returns an array so the caller can preallocate the parallel result buffer.
        // projectRoot must already be normalized (absolute, trailing separator) so that
        // Directory.GetFiles returns paths consistent with the prefix used in ToProjectRelativePath.
        static string[] EnumerateMetaFiles(string projectRoot)
        {
            var allFiles = new List<string>();

            foreach (var rootName in new[] { "Assets", "Packages" })
            {
                var root = projectRoot + rootName;
                if (!Directory.Exists(root))
                    continue;

                string[] files;
                try
                {
                    files = Directory.GetFiles(root, "*" + k_MetaExtension, SearchOption.AllDirectories);
                }
                catch (IOException e)
                {
                    Debug.LogWarning($"[DuplicateUnityGuidAnalyzer] Could not enumerate '{root}': {e.Message}");
                    continue;
                }
                catch (UnauthorizedAccessException e)
                {
                    Debug.LogWarning($"[DuplicateUnityGuidAnalyzer] Access denied enumerating '{root}': {e.Message}");
                    continue;
                }

                allFiles.AddRange(files);
            }

            return allFiles.ToArray();
        }

        static bool ShouldReportMetaPath(AnalysisOptions options, string projectRelativeMetaPath)
        {
            var assetPath = MetaPathToAssetPath(projectRelativeMetaPath);

            if (!AssetPathUtils.ShouldAnalyzePackagePath(assetPath, options))
                return false;

            var rules = options?.Rules;
            if (rules != null &&
                (rules.IsPathGloballySuppressed(assetPath) || rules.IsPathGloballySuppressed(projectRelativeMetaPath)))
            {
                return false;
            }

            var filter = options?.AssetPathFilter;
            return filter == null || filter(assetPath) || filter(projectRelativeMetaPath);
        }

        static string MetaPathToAssetPath(string metaPath)
        {
            if (string.IsNullOrEmpty(metaPath))
                return string.Empty;

            return metaPath.EndsWith(k_MetaExtension, StringComparison.OrdinalIgnoreCase)
                ? metaPath.Substring(0, metaPath.Length - k_MetaExtension.Length)
                : metaPath;
        }

        static bool TryReadMetaGuid(string fullPath, string projectRelativePath, out string guid, out int line)
        {
            guid = null;
            line = 0;

            try
            {
                var lineNumber = 0;
                foreach (var rawLine in File.ReadLines(fullPath))
                {
                    lineNumber++;
                    var trimmed = rawLine.Trim();
                    if (!trimmed.StartsWith("guid:", StringComparison.Ordinal))
                        continue;

                    var value = trimmed.Substring("guid:".Length).Trim();
                    if (string.IsNullOrEmpty(value))
                        return false;

                    guid = value;
                    line = lineNumber;
                    return true;
                }
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[DuplicateUnityGuidAnalyzer] Could not read '{projectRelativePath}': {e.Message}");
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogWarning($"[DuplicateUnityGuidAnalyzer] Access denied reading '{projectRelativePath}': {e.Message}");
            }

            return false;
        }

        // projectRoot must already be normalized (absolute, trailing separator).
        // Derives the relative path by cheap prefix-strip; no per-call Path.GetFullPath.
        static string ToProjectRelativePath(string projectRoot, string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return string.Empty;

            if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return fullPath.Substring(projectRoot.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        internal readonly struct GuidClash
        {
            public GuidClash(string guid, List<MetaGuidEntry> metaFiles)
            {
                Guid = guid;
                MetaFiles = metaFiles;
            }

            public string Guid { get; }
            public List<MetaGuidEntry> MetaFiles { get; }
        }

        internal readonly struct MetaGuidEntry
        {
            public MetaGuidEntry(string projectRelativePath, int guidLine, bool isReportable)
            {
                ProjectRelativePath = projectRelativePath;
                GuidLine = guidLine;
                IsReportable = isReportable;
            }

            public string ProjectRelativePath { get; }
            public int GuidLine { get; }
            public bool IsReportable { get; }
        }
    }
}
