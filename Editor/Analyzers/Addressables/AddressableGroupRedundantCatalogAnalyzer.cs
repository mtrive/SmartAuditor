// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AddressableGroupRedundantCatalogAnalyzer : AddressableGroupAnalyzer
    {
        internal const string ADR0008 = nameof(ADR0008);

        internal static readonly Descriptor RedundantCatalogDescriptor = new Descriptor(
            ADR0008,
            "Addressable Group: All Catalog Lookup Keys Included",
            Impact.BuildSize | Impact.Memory,
            "An Addressable group's <b>BundledAssetGroupSchema</b> has <b>Include Address In Catalog</b>, <b>Include GUID In Catalog</b>, and <b>Include Labels In Catalog</b> all enabled. These three flags are on by default; leaving every flag enabled publishes three lookup keys per entry into the runtime content catalog and inflates the catalog file even when the project only loads entries by one of them.",
            "Open the group in <b>Window &gt; Asset Management &gt; Addressables &gt; Groups</b>, expand <b>Advanced Options</b>, and disable each <b>Include ... In Catalog</b> flag the project does not use. Keep <b>Include Address In Catalog</b> when loading by <b>Addressables.LoadAssetAsync(string)</b>, <b>Include GUID In Catalog</b> when loading via <b>AssetReference</b>, and <b>Include Labels In Catalog</b> when loading by label."
        )
        {
            MessageFormat = "Addressable group '{0}' includes Address, GUID, and Labels in the catalog",
            DefaultSeverity = Severity.Minor
        };

        public override void AnalyzeGroup(AddressableGroupAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(RedundantCatalogDescriptor))
                return;

            var group = context.Group;

            if (group.entries.Count == 0)
                return;

            var bundledAssetSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundledAssetSchema == null)
                return;

            if (!bundledAssetSchema.IncludeAddressInCatalog ||
                !bundledAssetSchema.IncludeGUIDInCatalog ||
                !bundledAssetSchema.IncludeLabelsInCatalog)
                return;

            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    RedundantCatalogDescriptor.Id,
                    group.Name)
                .WithLocation(new Location(group.Name))
                .WithEvidence("EntryCount", group.entries.Count);
            context.ReportIssue(diagnostic);
        }
    }
}
#endif
