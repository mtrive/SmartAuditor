// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor.AddressableAssets;
using UnityEngine.AddressableAssets;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ScriptableObjectAssetReferenceAnalyzer : ScriptableObjectAnalyzer
    {
        internal const string ADR0009 = nameof(ADR0009);

        static readonly Descriptor UnregisteredAssetReferenceDescriptor = new Descriptor(
            ADR0009,
            "Addressables: ScriptableObject AssetReference Not In Any Group",
            Impact.Correctness,
            "A serialized <b>AssetReference</b> field on the ScriptableObject has a valid <b>AssetGUID</b>, but the referenced asset belongs to no Addressable group. <b>AssetReference.RuntimeKeyIsValid</b> only parses the GUID, so the field looks valid in the editor, yet loading it at runtime via <b>Addressables.LoadAssetAsync</b> / <b>InstantiateAsync</b> returns a failed AsyncOperationHandle and any code awaiting its <b>Result</b> throws.",
            "Add the referenced asset to an Addressable group, or clear the field if the reference is no longer needed."
        )
        {
            MessageFormat = "AssetReference field '{0}' on ScriptableObject '{1}' references an asset that is in no Addressable group"
        };

        public override void Analyze(ScriptableObjectAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(UnregisteredAssetReferenceDescriptor, context.AssetPath))
                return;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var fields = AssetReferenceFieldCache.GetFields(context.ScriptableObject.GetType());

            foreach (var field in fields)
            {
                var assetRef = field.GetValue(context.ScriptableObject) as AssetReference;
                if (!AddressableReferenceValidation.IsAssignedButUnregistered(assetRef, settings))
                    continue;

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        UnregisteredAssetReferenceDescriptor.Id,
                        field.Name,
                        context.ScriptableObject.name)
                    .WithLocation(new Location(context.AssetPath));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
#endif
