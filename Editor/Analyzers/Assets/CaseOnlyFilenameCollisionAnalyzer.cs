// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using Debug = UnityEngine.Debug;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class CaseOnlyFilenameCollisionAnalyzer : ProjectAssetAnalyzer
    {
        internal const string ASF0005 = nameof(ASF0005);

        internal static readonly Descriptor CaseOnlyFilenameCollisionDescriptor = new Descriptor(
            ASF0005,
            "Asset: Case-Only Filename Collision",
            Impact.Correctness,
            "Two or more asset files have project-relative paths that differ only by letter casing. This diagnostic only runs outside the Windows Editor because Windows cannot represent the conflicting files reliably; the collision can still break imports, hide assets, or make version-control operations unstable for Windows users.",
            "Rename one of the colliding files so the project-relative path is unique without relying on letter case, then let Unity regenerate or update the corresponding <b>.meta</b> file."
        )
        {
            DefaultSeverity = Severity.Major,
            MessageFormat = "Asset path '{0}' differs only by case from {1} other file(s)"
        };

        const string k_MetaExtension = ".meta";

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
#if UNITY_EDITOR_WIN
            return;
#else
            if (!context.IsDescriptorEnabled(CaseOnlyFilenameCollisionDescriptor))
                return;

            foreach (var collision in FindCaseOnlyCollisions(context.Options, SmartAuditor.ProjectPath))
            {
                var primary = collision.Files.First(entry => entry.IsReportable);
                var collidingPaths = collision.Files
                    .Select(entry => entry.ProjectRelativePath)
                    .ToArray();

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        CaseOnlyFilenameCollisionDescriptor.Id,
                        primary.ProjectRelativePath,
                        collision.Files.Count - 1)
                    .WithLocation(new Location(primary.ProjectRelativePath))
                    .WithEvidence("Colliding paths", collidingPaths);
                context.ReportIssue(diagnostic);
            }
#endif
        }

        internal static List<PathCollision> FindCaseOnlyCollisions(AnalysisOptions options, string projectPath)
        {
            var paths = new List<string>();

            foreach (var fullPath in EnumerateAssetFiles(projectPath))
            {
                var projectRelativePath = ToProjectRelativePath(projectPath, fullPath);
                if (string.IsNullOrEmpty(projectRelativePath))
                    continue;

                paths.Add(projectRelativePath);
            }

            return FindCaseOnlyCollisions(options, paths);
        }

        internal static List<PathCollision> FindCaseOnlyCollisions(AnalysisOptions options, IEnumerable<string> projectRelativePaths)
        {
            var byCaseFoldedPath = new Dictionary<string, List<FileEntry>>(StringComparer.Ordinal);

            foreach (var projectRelativePath in projectRelativePaths ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrEmpty(projectRelativePath) ||
                    projectRelativePath.EndsWith(k_MetaExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var key = projectRelativePath.ToUpperInvariant();
                if (!byCaseFoldedPath.TryGetValue(key, out var entries))
                {
                    entries = new List<FileEntry>();
                    byCaseFoldedPath[key] = entries;
                }

                entries.Add(new FileEntry(projectRelativePath, ShouldReportPath(options, projectRelativePath)));
            }

            return byCaseFoldedPath
                .Where(kv => HasCaseOnlyCollision(kv.Value) && kv.Value.Any(entry => entry.IsReportable))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new PathCollision(kv.Value.OrderBy(e => e.ProjectRelativePath, StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();
        }

        static bool HasCaseOnlyCollision(List<FileEntry> entries)
        {
            if (entries == null || entries.Count < 2)
                return false;

            return entries
                .Select(entry => entry.ProjectRelativePath)
                .Distinct(StringComparer.Ordinal)
                .Count() > 1;
        }

        static IEnumerable<string> EnumerateAssetFiles(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                yield break;

            foreach (var rootName in new[] { "Assets", "Packages" })
            {
                var root = Path.Combine(projectPath, rootName);
                if (!Directory.Exists(root))
                    continue;

                string[] files;
                try
                {
                    files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                }
                catch (IOException e)
                {
                    Debug.LogWarning($"[CaseOnlyFilenameCollisionAnalyzer] Could not enumerate '{root}': {e.Message}");
                    continue;
                }
                catch (UnauthorizedAccessException e)
                {
                    Debug.LogWarning($"[CaseOnlyFilenameCollisionAnalyzer] Access denied enumerating '{root}': {e.Message}");
                    continue;
                }

                foreach (var file in files)
                    yield return file;
            }
        }

        static bool ShouldReportPath(AnalysisOptions options, string projectRelativePath)
        {
            if (!AssetPathUtils.ShouldAnalyzePackagePath(projectRelativePath, options))
                return false;

            var rules = options?.Rules;
            if (rules != null && rules.IsPathGloballySuppressed(projectRelativePath))
                return false;

            var filter = options?.AssetPathFilter;
            return filter == null || filter(projectRelativePath);
        }

        static string ToProjectRelativePath(string projectPath, string fullPath)
        {
            if (string.IsNullOrEmpty(projectPath) || string.IsNullOrEmpty(fullPath))
                return string.Empty;

            var projectRoot = Path.GetFullPath(projectPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var absolutePath = Path.GetFullPath(fullPath);

            if (!absolutePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var relative = absolutePath.Substring(projectRoot.Length);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        internal readonly struct PathCollision
        {
            public PathCollision(List<FileEntry> files)
            {
                Files = files;
            }

            public List<FileEntry> Files { get; }
        }

        internal readonly struct FileEntry
        {
            public FileEntry(string projectRelativePath, bool isReportable)
            {
                ProjectRelativePath = projectRelativePath;
                IsReportable = isReportable;
            }

            public string ProjectRelativePath { get; }
            public bool IsReportable { get; }
        }
    }
}
