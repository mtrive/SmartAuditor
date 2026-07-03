// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AddressableDuplicateAddressAnalyzer : AddressableGroupAnalyzer
    {
        internal const string ADR0005 = nameof(ADR0005);

        internal static readonly Descriptor DuplicateAddressDescriptor = new Descriptor(
            ADR0005,
            "Addressable: Duplicate Address",
            Impact.Correctness,
            "The same string address appears on entries in more than one Addressable group. <b>Addressables.LoadAssetAsync</b> resolves the address by first match, so duplicates produce non-deterministic load behavior depending on group order.",
            "Rename the entries so each address is unique across all groups."
        )
        {
            MessageFormat = "Address '{0}' is used by entries in multiple Addressable groups"
        };

        readonly Dictionary<string, List<string>> m_AddressToGroups = new Dictionary<string, List<string>>();

        public override void AnalyzeGroup(AddressableGroupAnalysisContext context)
        {
            var group = context.Group;
            if (group == null)
                return;

            foreach (var entry in group.entries)
            {
                if (string.IsNullOrEmpty(entry.address))
                    continue;

                if (!m_AddressToGroups.ContainsKey(entry.address))
                {
                    m_AddressToGroups[entry.address] = new List<string>();
                }
                m_AddressToGroups[entry.address].Add(group.Name);
            }
        }

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
            foreach (var kvp in m_AddressToGroups)
            {
                if (kvp.Value.Count <= 1)
                    continue;
                var address = kvp.Key;
                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        DuplicateAddressDescriptor.Id,
                        address)
                    .WithLocation(new Location(kvp.Value[0]))
                    .WithEvidence("AffectedGroups", kvp.Value.ToArray())
                    .WithEvidence("DuplicateCount", kvp.Value.Count);
                context.ReportIssue(diagnostic);
            }

            // Clear the dictionary for the next analysis
            m_AddressToGroups.Clear();
        }
    }
}
#endif
