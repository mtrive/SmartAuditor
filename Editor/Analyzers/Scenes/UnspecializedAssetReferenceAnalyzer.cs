// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using SmartAuditor.Editor.Utils;
using UnityEngine.AddressableAssets;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class UnspecializedAssetReferenceAnalyzer : GameObjectAnalyzer
    {
        internal const string ADR0011 = nameof(ADR0011);

        static readonly Descriptor UnspecializedAssetReferenceDescriptor = new Descriptor(
            ADR0011,
            "Addressables: Unspecialized AssetReference",
            Impact.Quality,
            "The serialized field uses the base <b>AssetReference</b> type rather than a strongly-typed subclass. The Inspector accepts assets of any type, and <b>LoadAssetAsync&lt;T&gt;</b> calls at runtime can fail with a type mismatch instead of producing a compile-time error.",
            "Change the field's type to a typed AssetReference subclass (e.g. <b>AssetReferenceTexture2D</b>, <b>AssetReferenceGameObject</b>, or a custom <b>AssetReferenceT&lt;T&gt;</b>)."
        )
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "AssetReference field '{0}' on '{1}' is not specialized"
        };

        public override void AnalyzeComponent(ComponentAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(UnspecializedAssetReferenceDescriptor, context.AssetPath))
                return;

            var component = context.Component;
            var fields = AssetReferenceFieldCache.GetFields(component.GetType());

            foreach (var field in fields)
            {
                // Cache returns AssetReference and all subclasses; this analyzer only fires on
                // fields whose declared type is exactly AssetReference (not AssetReference<T>).
                if (field.FieldType != typeof(AssetReference))
                    continue;

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.GameObjectIssue,
                        UnspecializedAssetReferenceDescriptor.Id,
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
