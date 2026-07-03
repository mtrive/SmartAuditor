// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Detects missing (null) component slots on GameObjects, which occur when a script asset was deleted or the reference is broken.
    /// </summary>
    sealed class MissingComponentAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0018 = nameof(OBJ0018);

        internal static readonly Descriptor MissingComponentDescriptor = new Descriptor(
            OBJ0018,
            "GameObject: Missing Component",
            Impact.Correctness,
            "A GameObject has a component slot pointing to a MonoScript that the AssetDatabase cannot resolve — the script was deleted, renamed, or moved between assemblies. The Inspector still renders the slot as <b>Missing (Mono Script)</b>, but the GameObject behaves as if the component were absent.",
            "Remove the missing-component slot in the Inspector, or restore the deleted/renamed script and reassign it."
        )
        {
            MessageFormat = "GameObject '{0}' has a missing component",
            DefaultSeverity = Severity.Major
        };

        public override void AnalyzeGameObject(GameObjectAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(MissingComponentDescriptor, context.AssetPath))
                return;

            var gameObject = context.GameObject;
            if (gameObject == null)
                return;

            var components = new List<Component>(gameObject.GetComponentCount());
            gameObject.GetComponents(components);

            for (var i = 0; i < components.Count; i++)
            {
                if (components[i])
                    continue;

                var diagnostic = Diagnostic.Create(
                    AnalysisCategory.GameObjectIssue,
                    MissingComponentDescriptor.Id,
                    gameObject.name)
                    .WithLocation(new Location(context.AssetPath))
                    .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                    .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                    .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
                context.ReportIssue(diagnostic);
            }
        }
    }
}
