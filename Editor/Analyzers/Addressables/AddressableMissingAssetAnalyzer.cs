// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AddressableMissingAssetAnalyzer : AddressableGroupAnalyzer
    {
        internal const string ADR0001 = nameof(ADR0001);

        internal static readonly Descriptor MissingAssetDescriptor = new Descriptor(
            ADR0001,
            "Addressable: Missing Asset",
            Impact.Correctness,
            "An Addressable entry's referenced asset cannot be resolved by the AssetDatabase. Loading the address at runtime via <b>Addressables.LoadAssetAsync</b> will fail.",
            "Remove the entry from the group, or re-import the missing asset and reassign it to the entry."
        )
        {
            MessageFormat = "Addressable entry with address '{1}' in group '{0}' references a missing asset",
            DefaultSeverity = Severity.Major
        };

        public override void AnalyzeGroup(AddressableGroupAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(MissingAssetDescriptor))
                return;

            var group = context.Group;
            foreach (var entry in group.entries)
            {
                if (entry.MainAsset != null)
                    continue;
                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        MissingAssetDescriptor.Id,
                        group.Name,
                        entry.address)
                    .WithLocation(new Location(group.Name));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
#endif
