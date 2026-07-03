// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using SmartAuditor.Editor.Utils;
using UnityEngine.AddressableAssets;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MissingAssetReferenceAnalyzer : GameObjectAnalyzer
    {
        internal const string ADR0010 = nameof(ADR0010);

        static readonly Descriptor MissingAssetReferenceDescriptor = new Descriptor(
            ADR0010,
            "Addressables: Unassigned AssetReference",
            Impact.Correctness,
            "A serialized <b>AssetReference</b> field on the component is null or has an empty <b>AssetGUID</b>. Loading the reference at runtime via <b>Addressables.LoadAssetAsync</b> returns a failed AsyncOperationHandle, and any code that awaits its <b>Result</b> throws.",
            "Assign a valid asset to the AssetReference field in the Inspector, or delete the field declaration if the reference is unused."
        )
        {
            MessageFormat = "AssetReference field '{0}' on '{1}' is unassigned"
        };

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(MissingAssetReferenceDescriptor, context.AssetPath))
                return;

            var component = context.Component;
            var fields = AssetReferenceFieldCache.GetFields(component.GetType());

            foreach (var field in fields)
            {
                var assetRef = field.GetValue(component) as AssetReference;

                // Unassigned: either the field itself is null, or the AssetReference exists
                // but has no asset GUID.
                if (assetRef != null && !string.IsNullOrEmpty(assetRef.AssetGUID))
                    continue;

                var diagnostic = Diagnostic.Create(
                    AnalysisCategory.GameObjectIssue,
                    MissingAssetReferenceDescriptor.Id,
                    field.Name,
                    component.name)
                    .WithLocation(new Location(context.AssetPath))
                    .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                    .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                    .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
                context.ReportIssue(diagnostic);
            }
        }
    }
}
#endif
