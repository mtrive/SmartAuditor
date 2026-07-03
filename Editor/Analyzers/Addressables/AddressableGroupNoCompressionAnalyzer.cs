// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AddressableGroupNoCompressionAnalyzer : AddressableGroupAnalyzer
    {
        internal const string ADR0006 = nameof(ADR0006);

        internal static readonly Descriptor NoCompressionDescriptor = new Descriptor(
            ADR0006,
            "Addressable Group: No Compression",
            Impact.BuildSize,
            "An Addressable group has its <b>Compression</b> schema setting set to <b>Uncompressed</b>. Uncompressed bundles produce larger builds and slower remote downloads than the LZ4 or LZMA alternatives.",
            "Set <b>Compression</b> on the group's <b>BundledAssetGroupSchema</b> to <b>LZ4</b> for runtime-loaded content or <b>LZMA</b> for remote-only content."
        )
        {
            MessageFormat = "Addressable group '{0}' has compression disabled"
        };

        public override void AnalyzeGroup(AddressableGroupAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(NoCompressionDescriptor))
                return;

            var group = context.Group;

            // Ignore empty groups
            if (group.entries.Count == 0)
                return;

            var bundledAssetSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundledAssetSchema == null)
                return;

            // Check if compression is disabled
            if (bundledAssetSchema.Compression != BundledAssetGroupSchema.BundleCompressionMode.Uncompressed)
                return;
            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    NoCompressionDescriptor.Id,
                    group.Name)
                .WithLocation(new Location(group.Name));
            context.ReportIssue(diagnostic);
        }
    }
}
#endif
