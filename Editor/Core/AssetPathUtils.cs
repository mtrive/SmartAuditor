using System;
using System.IO;
using System.Linq;
using SmartAuditor.Editor;
using SmartAuditor.Editor.Utils;
using UnityEditor;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Utility class for finding and filtering asset paths in the project.
    /// </summary>
    internal static class AssetPathUtils
    {
        internal static Func<string, bool> IsReadOnlyPackagePath { get; set; } = PackageUtils.IsAssetInReadOnlyPackage;

        /// <summary>
        /// Resolves a path relative to the project root to an absolute filesystem path.
        /// If <paramref name="relativeToProject"/> is already absolute, returns it unchanged.
        /// </summary>
        internal static string ToAbsoluteProjectPath(string relativeToProject)
        {
            if (string.IsNullOrEmpty(relativeToProject))
                return string.Empty;
            if (Path.IsPathRooted(relativeToProject))
                return relativeToProject;
            return Path.GetFullPath(Path.Combine(SmartAuditor.ProjectPath,
                relativeToProject.Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <summary>
        /// Returns all asset paths, optionally filtered by the predicate in <paramref name="options"/>.
        /// Pass <paramref name="applyGlobalSuppression"/> as <c>false</c> to skip the no-Id suppression
        /// filter — useful when you need the full universe for reachability seeding but still want to
        /// suppress individual results downstream.
        /// </summary>
        public static string[] GetAssetPaths(AnalysisOptions options, bool applyGlobalSuppression = true)
        {
            return GetAssetPathsByFilter("", options, applyGlobalSuppression);
        }

        /// <summary>
        /// Returns asset paths matching the given AssetDatabase filter string, optionally filtered further
        /// by the predicate in <paramref name="options"/>.
        /// Pass <paramref name="applyGlobalSuppression"/> as <c>false</c> to skip the no-Id suppression
        /// filter — useful when you need the full universe for reachability seeding but still want to
        /// suppress individual results downstream.
        /// </summary>
        public static string[] GetAssetPathsByFilter(string filter, AnalysisOptions options, bool applyGlobalSuppression = true)
        {
            var paths = AssetDatabase.FindAssets(filter)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Where(p => !p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Where(p => AssetImporter.GetAtPath(p) != null)
                .Where(p => ShouldAnalyzePackagePath(p, options))
                .Where(p =>
                {
                    var lower = p.ToLower();
                    return !lower.Contains("/editor/") &&
                           !lower.Contains("/editor default resources/") &&
                           !lower.Contains("/editor resources/");
                })
                .ToList();

            if (options?.AssetPathFilter != null)
                paths = paths.Where(p => options.AssetPathFilter(p)).ToList();

            // Drop paths the user has globally suppressed via a no-Id rule. Doing it here means
            // callers never see suppressed paths and don't each need to repeat the check.
            // Callers that need the full universe for reachability seeding can opt out.
            if (applyGlobalSuppression && options?.Rules != null)
                paths = paths.Where(p => !options.Rules.IsPathGloballySuppressed(p)).ToList();

            return paths.ToArray();
        }

        internal static bool ShouldAnalyzePackagePath(string path, AnalysisOptions options)
        {
            if (options == null || options.AnalyzeReadOnlyPackages)
                return true;

            return IsReadOnlyPackagePath == null || !IsReadOnlyPackagePath(path);
        }
    }
}
