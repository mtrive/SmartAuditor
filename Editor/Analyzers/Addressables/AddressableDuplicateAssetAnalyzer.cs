// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using System.Collections.Generic;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AddressableDuplicateAssetAnalyzer : AddressableGroupAnalyzer
    {
        internal const string ADR0004 = nameof(ADR0004);

        readonly Dictionary<UnityEngine.Object, List<string>> m_AssetToGroups = new Dictionary<UnityEngine.Object, List<string>>();

        internal static readonly Descriptor DuplicateAssetDescriptor = new Descriptor(
            ADR0004,
            "Addressable: Duplicate Asset",
            Impact.Correctness,
            "The same asset is referenced from more than one Addressable group. Duplicate references ship the asset in multiple bundles, inflate the build, and create ambiguity about which bundle the runtime loader picks.",
            "Keep the asset's reference in one group and remove it from the others."
        )
        {
            MessageFormat = "Asset '{0}' is referenced from multiple Addressable groups"
        };

        public override void AnalyzeGroup(AddressableGroupAnalysisContext context)
        {
            var group = context.Group;
            if (group == null)
                return;

            foreach (var entry in group.entries)
            {
                if (entry.MainAsset == null)
                    continue;

                if (!m_AssetToGroups.ContainsKey(entry.MainAsset))
                {
                    m_AssetToGroups[entry.MainAsset] = new List<string>();
                }
                m_AssetToGroups[entry.MainAsset].Add(group.Name);
            }
        }

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
            foreach (var kvp in m_AssetToGroups)
            {
                if (kvp.Value.Count <= 1)
                    continue;
                var asset = kvp.Key;
                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        DuplicateAssetDescriptor.Id,
                        asset.name)
                    .WithLocation(new Location(kvp.Value[0]))
                    .WithEvidence("AffectedGroups", kvp.Value.ToArray())
                    .WithEvidence("DuplicateCount", kvp.Value.Count);
                context.ReportIssue(diagnostic);
            }

            // Clear the dictionary for the next analysis
            m_AssetToGroups.Clear();
        }
    }
}
#endif
