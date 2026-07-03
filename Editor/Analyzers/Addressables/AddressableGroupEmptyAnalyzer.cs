// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEditor.AddressableAssets;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AddressableGroupEmptyAnalyzer : AddressableGroupAnalyzer
    {
        internal const string ADR0003 = nameof(ADR0003);

        internal static readonly Descriptor EmptyGroupDescriptor = new Descriptor(
            ADR0003,
            "Addressable Group: Empty",
            Impact.Correctness,
            "An Addressable group contains no entries. The group adds nothing to the build and may indicate a deserialization error or an in-progress refactor that left the group orphaned.",
            "Delete the group from the Addressables window if it is no longer needed, or add the entries that were expected to be in it."
        )
        {
            MessageFormat = "Addressable group '{0}' has no entries",
            DefaultSeverity = Severity.Minor
        };

        public override void AnalyzeGroup(AddressableGroupAnalysisContext context)
        {
            var group = context.Group;
            if (group == null)
                return;

            if (!context.IsDescriptorEnabled(EmptyGroupDescriptor))
                return;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null && settings.DefaultGroup != null && group == settings.DefaultGroup)
                return;

            if (group.entries.Count != 0)
                return;
            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    EmptyGroupDescriptor.Id,
                    group.Name)
                .WithLocation(new Location(group.Name));
            context.ReportIssue(diagnostic);
        }
    }
}
#endif
