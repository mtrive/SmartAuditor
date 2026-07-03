// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEditor.AddressableAssets;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AddressableDefaultGroupAnalyzer : AddressableGroupAnalyzer
    {
        internal const string ADR0007 = nameof(ADR0007);

        internal static readonly Descriptor DefaultGroupDescriptor = new Descriptor(
            ADR0007,
            "Addressable: Asset in Default Group",
            Impact.BuildSize | Impact.LoadTime,
            "An asset is assigned to the default Addressable group. The default group does not have content-specific schema settings (compression, packing mode, local vs. remote location), so assets placed there ship with one-size-fits-all defaults.",
            "Move the asset to a purpose-configured Addressable group whose schema settings match the asset's use case (local content, remote content, packing strategy, compression)."
        )
        {
            MessageFormat = "Asset '{0}' is in the default Addressable group '{1}'"
        };

        public override void AnalyzeGroup(AddressableGroupAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(DefaultGroupDescriptor))
                return;

            var group = context.Group;
            if (group == null)
                return;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            // Check if this group is the default group
            if (settings.DefaultGroup == null || group != settings.DefaultGroup)
                return;
            // Report all entries in the default group
            foreach (var entry in group.entries)
            {
                if (entry.MainAsset == null)
                    continue;

                // Use asset path if available, otherwise use address
                var assetPath = entry.AssetPath;
                if (string.IsNullOrEmpty(assetPath))
                    assetPath = entry.address;
                if (string.IsNullOrEmpty(assetPath))
                    assetPath = entry.MainAsset.name;

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        DefaultGroupDescriptor.Id,
                        assetPath,
                        group.Name)
                    .WithLocation(new Location(assetPath));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
#endif
