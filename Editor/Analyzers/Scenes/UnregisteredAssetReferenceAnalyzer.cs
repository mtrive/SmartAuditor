// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using SmartAuditor.Editor.Utils;
using UnityEditor.AddressableAssets;
using UnityEngine.AddressableAssets;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class UnregisteredAssetReferenceAnalyzer : GameObjectAnalyzer
    {
        internal const string ADR0012 = nameof(ADR0012);

        static readonly Descriptor UnregisteredAssetReferenceDescriptor = new Descriptor(
            ADR0012,
            "Addressables: AssetReference Not In Any Group",
            Impact.Correctness,
            "A serialized <b>AssetReference</b> field on the component has a valid <b>AssetGUID</b>, but the referenced asset belongs to no Addressable group. <b>AssetReference.RuntimeKeyIsValid</b> only parses the GUID, so the field looks valid in the editor, yet loading it at runtime via <b>Addressables.LoadAssetAsync</b> / <b>InstantiateAsync</b> returns a failed AsyncOperationHandle and any code awaiting its <b>Result</b> throws.",
            "Add the referenced asset to an Addressable group, or clear the field if the reference is no longer needed."
        )
        {
            MessageFormat = "AssetReference field '{0}' on '{1}' references an asset that is in no Addressable group"
        };

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(UnregisteredAssetReferenceDescriptor, context.AssetPath))
                return;

            var component = context.Component;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var fields = AssetReferenceFieldCache.GetFields(component.GetType());

            foreach (var field in fields)
            {
                var assetRef = field.GetValue(component) as AssetReference;
                if (!AddressableReferenceValidation.IsAssignedButUnregistered(assetRef, settings))
                    continue;

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.GameObjectIssue,
                        UnregisteredAssetReferenceDescriptor.Id,
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
