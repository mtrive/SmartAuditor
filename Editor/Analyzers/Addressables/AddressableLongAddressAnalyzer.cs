// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AddressableLongAddressAnalyzer : AddressableGroupAnalyzer
    {
        internal const string ADR0002 = nameof(ADR0002);

        internal static readonly Descriptor LongAddressDescriptor = new Descriptor(
            ADR0002,
            "Addressable: Long Asset Address",
            Impact.Memory | Impact.BuildSize,
            "An Addressable address is longer than the configured maximum. Long addresses inflate the catalog file, take more runtime memory at load time, and can hit filename-length limits on some installation paths.",
            "Rename the entry to a shorter address. Hierarchical addresses like <b>UI/MainMenu/Title</b> are usually preferable to long flat identifiers."
        )
        {
            MessageFormat = "Address '{0}' in group '{1}' exceeds the configured length limit"
        };

        [DiagnosticParameter("MaxAddressLength", 100)]
        int m_MaxAddressLength;

        public override void AnalyzeGroup(AddressableGroupAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(LongAddressDescriptor))
                return;

            var group = context.Group;
//            if (group == null)
//                return;

//            var bundledAssetSchema = group.GetSchema<BundledAssetGroupSchema>();
//            if (bundledAssetSchema == null)
//                return;

            // Only check groups that pack separately
//            if (bundledAssetSchema.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
  //              return;
//
            foreach (var entry in group.entries)
            {
                if (string.IsNullOrEmpty(entry.address))
                    continue;

                if (entry.address.Length <= m_MaxAddressLength)
                    continue;
                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        LongAddressDescriptor.Id,
                        entry.address,
                        group.Name)
                    .WithLocation(new Location(group.Name))
                    .WithEvidence("AddressLength", entry.address.Length)
                    .WithEvidence("MaxAddressLength", m_MaxAddressLength)
                    .WithEvidence("Excess", entry.address.Length - m_MaxAddressLength);
                context.ReportIssue(diagnostic);
            }
        }
    }
}
#endif
