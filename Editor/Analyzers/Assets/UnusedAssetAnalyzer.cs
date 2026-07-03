// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.IO;
using SmartAuditor.Editor;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
#if PACKAGE_ADDRESSABLES
using UnityEditor.AddressableAssets;
#endif

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Flags assets not linked (via Unity references) from enabled build scenes, Resources, optional Addressables, or every scene file.
    /// </summary>
    sealed class UnusedAssetAnalyzer : ProjectAssetAnalyzer
    {
        internal const string CLN0001 = nameof(CLN0001);

        internal static readonly Descriptor PossiblyUnusedAssetDescriptor = new Descriptor(
            CLN0001,
            "Asset: Possibly Unused",
            Impact.BuildSize,
            "No chain of Unity references reaches this asset from enabled Build Settings scenes, Resources folders, or Addressables entries. The asset still ships in the project (and contributes to its size) without any static path linking it to a runtime load.",
            "Confirm the asset is unreachable before deleting. Runtime loading via <b>Resources.Load</b>, AssetBundles, editor-only references, or addresses constructed dynamically can still require an asset that fails this static check."
        )
        {
            MessageFormat = "Asset '{0}' is not reachable from build scenes, Resources, or Addressables",
            DefaultSeverity = Severity.Info,
            IsEnabledByDefault = false
        };

        [DiagnosticParameter("UnusedAssetCheckBuildScenes", 1)]
        int m_CheckBuildScenes;

        [DiagnosticParameter("UnusedAssetCheckResourcesFolder", 1)]
        int m_CheckResourcesFolder;

        [DiagnosticParameter("UnusedAssetCheckAllSceneFiles", 0)]
        int m_CheckAllSceneFiles;

#if PACKAGE_ADDRESSABLES
        [DiagnosticParameter("UnusedAssetCheckAddressablesGroups", 1)]
        int m_CheckAddressablesGroups;
#endif

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
            if (!context.IsDescriptorEnabled(PossiblyUnusedAssetDescriptor))
                return;

            var options = context.Options;
            // Fetch the full universe without suppression so suppressed Resources/scene assets can still
            // anchor reachability for the non-suppressed assets they reference. Individual paths are
            // skipped in the reporting loop below.
            var allPaths = AssetPathUtils.GetAssetPaths(options, applyGlobalSuppression: false);
            var startingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (m_CheckBuildScenes != 0)
                AddBuildSettingsScenePaths(startingPaths);

            if (m_CheckResourcesFolder != 0)
                AddResourcesAssetPaths(startingPaths, allPaths);

            if (m_CheckAllSceneFiles != 0)
                AddAllScenePaths(startingPaths);

#if PACKAGE_ADDRESSABLES
            if (m_CheckAddressablesGroups != 0)
                AddAddressablesEntryPaths(startingPaths);
#endif

            if (startingPaths.Count == 0)
                return;

            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var startPath in startingPaths)
            {
                if (!IsValidStartingAssetPath(startPath))
                    continue;

                reachable.Add(startPath);
                foreach (var dep in AssetDatabase.GetDependencies(startPath, recursive: true))
                {
                    if (!string.IsNullOrEmpty(dep))
                        reachable.Add(dep);
                }
            }

            foreach (var path in allPaths)
            {
                if (reachable.Contains(path))
                    continue;

                // The universe is unfiltered so suppressed paths anchor reachability, but we
                // must not report them as unused.
                if (options.Rules != null && options.Rules.IsPathGloballySuppressed(path))
                    continue;

                if (!UnusedAssetPathExclusions.ShouldReportUnusedCandidate(path))
                    continue;

                if (!context.IsDescriptorEnabled(PossiblyUnusedAssetDescriptor, path))
                    continue;

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        PossiblyUnusedAssetDescriptor.Id,
                        path)
                    .WithLocation(new Location(path));
                context.ReportIssue(diagnostic);
            }
        }

        static void AddBuildSettingsScenePaths(HashSet<string> startingPaths)
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                    continue;

                if (string.IsNullOrEmpty(scene.path))
                    continue;

                startingPaths.Add(scene.path);
            }
        }

        static void AddResourcesAssetPaths(HashSet<string> startingPaths, string[] allPaths)
        {
            foreach (var path in allPaths)
            {
                if (path.IndexOf("/resources/", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                    continue;

                startingPaths.Add(path);
            }
        }

        static void AddAllScenePaths(HashSet<string> startingPaths)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    startingPaths.Add(path);
            }
        }

#if PACKAGE_ADDRESSABLES
        static void AddAddressablesEntryPaths(HashSet<string> startingPaths)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            foreach (var group in settings.groups)
            {
                if (group == null)
                    continue;

                foreach (var entry in group.entries)
                {
                    if (entry?.MainAsset == null)
                        continue;

                    var path = AssetDatabase.GetAssetPath(entry.MainAsset);
                    if (!string.IsNullOrEmpty(path))
                        startingPaths.Add(path);
                }
            }
        }
#endif

        static bool IsValidStartingAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return false;

            return AssetImporter.GetAtPath(path) != null;
        }
    }
}
