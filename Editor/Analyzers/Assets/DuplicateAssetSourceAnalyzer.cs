// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Detects asset source files of any type whose on-disk content is identical,
    /// reporting reclaimable space and the duplicate paths in evidence.
    /// </summary>
    sealed class DuplicateAssetSourceAnalyzer : ProjectAssetAnalyzer
    {
        internal const string CLN0003 = nameof(CLN0003);

        internal static readonly Descriptor DuplicateAssetSourceDescriptor = new Descriptor(
            CLN0003,
            "Asset: Duplicate Source File",
            Impact.BuildSize | Impact.EditorWorkflow,
            "Two or more asset source files in the project have identical SHA-256 hashes. The duplicates ship in the build (or, if not referenced, still occupy disk and import time in the project) without contributing anything unique.",
            "Keep one canonical asset, repoint references to it, and delete the redundant source files."
        )
        {
            MessageFormat = "{0} reclaimable across {1} identical source files",
            DefaultSeverity = Severity.Minor
        };

        // Exclude .meta files so we never flag Unity-generated sidecar files.
        static readonly HashSet<string> k_ExcludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".meta"
        };

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
            if (!context.IsDescriptorEnabled(DuplicateAssetSourceDescriptor))
                return;

            var projectPath = SmartAuditor.ProjectPath;
            if (string.IsNullOrEmpty(projectPath))
                return;

            foreach (var cluster in FindDuplicateSourceFiles(context.Options, projectPath))
            {
                var representative = cluster.Paths[0];
                var duplicateCount = cluster.Paths.Count - 1;
                var redundantBytes = checked(cluster.FileSize * duplicateCount);
                var duplicatePaths = cluster.Paths.GetRange(1, duplicateCount).ToArray();

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        DuplicateAssetSourceDescriptor.Id,
                        Formatting.FormatBytes(redundantBytes),
                        cluster.Paths.Count)
                    .WithLocation(new Location(representative))
                    .WithEvidence("DuplicatePaths", duplicatePaths)
                    .WithEvidence("RedundantBytes", redundantBytes)
                    .WithEvidence("DuplicateCount", duplicateCount);

                context.ReportIssue(diagnostic);
            }
        }

        /// <summary>
        /// Walks <c>Assets/</c> and <c>Packages/</c> under <paramref name="projectPath"/>,
        /// groups files by size, hashes size-collision groups with SHA-256, and returns
        /// one <see cref="DuplicateCluster"/> per group of two or more identical files.
        /// </summary>
        internal static List<DuplicateCluster> FindDuplicateSourceFiles(AnalysisOptions options, string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                return new List<DuplicateCluster>();

            var projectRoot = Path.GetFullPath(projectPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            // Collect all asset paths, excluding .meta sidecar files.
            var allPaths = EnumerateAssetFiles(projectRoot);

            // Group paths by file size (cheap first filter before SHA-256).
            var sizeToPaths = new Dictionary<long, List<string>>();
            foreach (var (relPath, size) in allPaths)
            {
                if (!ShouldReport(options, relPath))
                    continue;

                if (!sizeToPaths.TryGetValue(size, out var list))
                {
                    list = new List<string>();
                    sizeToPaths[size] = list;
                }

                list.Add(relPath);
            }

            // For each size-collision group, compute SHA-256 in parallel and cluster by hash.
            var results = new List<DuplicateCluster>();

            foreach (var (size, paths) in sizeToPaths)
            {
                if (paths.Count < 2)
                    continue;

                // Hash every file in the collision group in parallel.
                var hashes = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
                Parallel.ForEach(paths, path =>
                {
                    var fullPath = projectRoot + path.Replace('/', Path.DirectorySeparatorChar);
                    if (!TryComputeSha256Hex(fullPath, out var hex))
                        return;

                    hashes.TryAdd(path, hex);
                });

                // Group by hash.
                var hashToPaths = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var path in paths)
                {
                    if (!hashes.TryGetValue(path, out var hex))
                        continue;

                    if (!hashToPaths.TryGetValue(hex, out var cluster))
                    {
                        cluster = new List<string>();
                        hashToPaths[hex] = cluster;
                    }

                    cluster.Add(path);
                }

                foreach (var cluster in hashToPaths.Values)
                {
                    if (cluster.Count < 2)
                        continue;

                    cluster.Sort(StringComparer.Ordinal);
                    results.Add(new DuplicateCluster(cluster, size));
                }
            }

            results.Sort((a, b) => StringComparer.Ordinal.Compare(a.Paths[0], b.Paths[0]));
            return results;
        }

        static List<(string RelPath, long Size)> EnumerateAssetFiles(string projectRoot)
        {
            var result = new List<(string, long)>();
            foreach (var rootName in new[] { "Assets", "Packages" })
            {
                var root = projectRoot + rootName;
                if (!Directory.Exists(root))
                    continue;

                IEnumerable<FileInfo> files;
                try
                {
                    // EnumerateFiles returns FileInfo objects whose Length is populated by the
                    // OS directory-listing stat — no second stat call per file.
                    files = new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    if (k_ExcludedExtensions.Contains(file.Extension))
                        continue;

                    var relPath = ToProjectRelativePath(projectRoot, file.FullName);
                    if (string.IsNullOrEmpty(relPath))
                        continue;

                    long size;
                    try
                    {
                        size = file.Length;
                    }
                    catch (IOException)
                    {
                        continue;
                    }

                    result.Add((relPath, size));
                }
            }

            return result;
        }

        static bool ShouldReport(AnalysisOptions options, string projectRelativePath)
        {
            if (!AssetPathUtils.ShouldAnalyzePackagePath(projectRelativePath, options))
                return false;

            var rules = options?.Rules;
            if (rules != null && rules.IsPathGloballySuppressed(projectRelativePath))
                return false;

            var filter = options?.AssetPathFilter;
            return filter == null || filter(projectRelativePath);
        }

        static string ToProjectRelativePath(string projectRoot, string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return string.Empty;

            if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return fullPath.Substring(projectRoot.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        static bool TryComputeSha256Hex(string fullPath, out string hex)
        {
            hex = null;
            try
            {
                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sha = SHA256.Create();
                var buffer = new byte[256 * 1024];
                while (true)
                {
                    var n = stream.Read(buffer, 0, buffer.Length);
                    if (n == 0)
                        break;
                    sha.TransformBlock(buffer, 0, n, null, 0);
                }

                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var hash = sha.Hash;
                if (hash == null || hash.Length == 0)
                    return false;

                hex = FingerprintUtils.ToHexLower(hash);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// A group of two or more asset files with identical content.
        /// </summary>
        internal readonly struct DuplicateCluster
        {
            /// <param name="paths">Project-relative paths, sorted ascending. Must contain at least 2 entries.</param>
            /// <param name="fileSize">On-disk size in bytes, shared by all files in the cluster.</param>
            public DuplicateCluster(List<string> paths, long fileSize)
            {
                Paths = paths;
                FileSize = fileSize;
            }

            /// <summary>Project-relative paths of the duplicate files, sorted ascending.</summary>
            public List<string> Paths { get; }

            /// <summary>On-disk size in bytes of each file in the cluster.</summary>
            public long FileSize { get; }
        }
    }
}
