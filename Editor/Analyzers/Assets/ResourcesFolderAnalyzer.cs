using System;
using System.Collections.Generic;
using System.IO;
using SmartAuditor.Editor.Core;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    internal sealed class ResourcesFolderAnalyzer : ProjectAssetAnalyzer
    {
        internal const string RES0002 = nameof(RES0002);
        internal const string RES0003 = nameof(RES0003);

        static readonly Descriptor AssetInResourcesFolderDescriptor = new Descriptor
            (
            RES0002,
            "Resources Folder: Asset Direct Reference",
            Impact.BuildSize,
            "The asset is stored under a <b>Resources/</b> folder. Every asset under <b>Resources/</b> is bundled into the build's <b>resources.assets</b> file and loaded eagerly at startup, regardless of whether the game actually uses it at runtime.",
            "Move the asset out of <b>Resources/</b> and load it on demand via <b>AssetBundles</b> or the <b>Addressables</b> package."
            )
        {
            MessageFormat = "Asset '{0}' is in a Resources folder"
        };

        static readonly Descriptor AssetInResourcesFolderDependencyDescriptor = new Descriptor
            (
            RES0003,
            "Resources Folder: Asset Dependency",
            Impact.BuildSize,
            "The asset is referenced by another asset that lives under <b>Resources/</b>. Unity's dependency resolver pulls it into the <b>resources.assets</b> bundle alongside the referencing asset, even though this asset isn't directly under <b>Resources/</b>.",
            "Break the reference from the <b>Resources/</b> asset, or move both assets to an <b>AssetBundle</b> or <b>Addressables</b> group so the dependency loads on demand."
            )
        {
            MessageFormat = "Asset '{0}' is pulled into Resources by another asset's reference"
        };

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
            var assetPathsDict = new Dictionary<string, DependencyNode>();

            foreach (var path in AssetPathUtils.GetAssetPaths(context.Options))
            {
                if (path.IndexOf("/resources/", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                    continue;

                var root = ProcessResourceAsset(context, path, assetPathsDict, null);
                foreach (var depAssetPath in AssetDatabase.GetDependencies(path, recursive: true))
                {
                    // skip self
                    if (depAssetPath.Equals(path))
                        continue;

                    ProcessResourceAsset(context, depAssetPath, assetPathsDict, root);
                }
            }
        }

        static DependencyNode ProcessResourceAsset(AnalysisContext context,
            string assetPath, Dictionary<string, DependencyNode> assetPathsDict, DependencyNode parent)
        {
            // skip C# scripts
            if (Path.GetExtension(assetPath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                return null;

            // Don't surface findings for paths the user has globally suppressed, even when they
            // are pulled in as dependencies of a non-suppressed Resources asset.
            var rules = context.Options.Rules;
            if (rules != null && rules.IsPathGloballySuppressed(assetPath))
                return null;

            if (assetPathsDict.TryGetValue(assetPath, out var existing))
            {
                if (parent != null)
                    existing.AddChild(parent);
                return existing;
            }

            var location = new Location(assetPath);
            var dependencyNode = new AssetDependencyNode
            {
                Location = location
            };
            if (parent != null)
                dependencyNode.AddChild(parent);

            var isInResources = assetPath.IndexOf("/resources/", StringComparison.OrdinalIgnoreCase) >= 0;

            var diagnostic = Diagnostic.Create(
                AnalysisCategory.AssetIssue,
                isInResources ? AssetInResourcesFolderDescriptor.Id : AssetInResourcesFolderDependencyDescriptor.Id,
                Path.GetFileName(assetPath))
                .WithDependencies(dependencyNode)
                .WithLocation(location);
            context.ReportIssue(diagnostic);

            assetPathsDict.Add(assetPath, dependencyNode);

            return dependencyNode;
        }
    }
}
