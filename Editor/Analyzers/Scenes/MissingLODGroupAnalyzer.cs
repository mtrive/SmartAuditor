// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MissingLODGroupAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0014 = nameof(OBJ0014);

        static readonly Descriptor MissingLODGroupDescriptor = new Descriptor(
            OBJ0014,
            "LOD Group: Missing for LOD-Named Children",
            Impact.Quality,
            "This GameObject has child renderers whose names include <b>_LOD</b>, but neither it nor any ancestor has a <b>LODGroup</b> component. The naming suggests LOD setup was started but never finished, so every LOD mesh renders at once.",
            "Add a <b>LODGroup</b> component to this GameObject and assign each LOD-named child renderer to the correct LOD level in the Inspector."
        )
        {
            MessageFormat = "GameObject '{0}' with LOD-named child renderers needs a LOD Group"
        };

        public override void AnalyzeGameObject(GameObjectAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(MissingLODGroupDescriptor, context.AssetPath))
                return;

            var gameObject = context.GameObject;
            if (gameObject == null)
                return;

            // Check if this GameObject has children
            var childCount = gameObject.transform.childCount;
            if (childCount == 0)
                return;

            // Children must have "_LOD" in their name AND a Renderer to qualify.
            // The name alone is a weak signal: unrelated empty/structural objects sometimes
            // contain "_LOD" without being renderable LOD candidates.
            bool hasLODChild = false;
            int lodChildCount = 0;
            for (int i = 0; i < childCount; i++)
            {
                var child = gameObject.transform.GetChild(i);
                if (child != null &&
                    child.gameObject.name.Contains("_LOD") &&
                    child.GetComponent<Renderer>())
                {
                    hasLODChild = true;
                    lodChildCount++;
                }
            }

            if (!hasLODChild)
                return;

            // Traverse up the hierarchy to check if any ancestor has a LODGroup component
            var currentTransform = gameObject.transform;
            while (currentTransform != null)
            {
                var lodGroup = currentTransform.GetComponent<LODGroup>();
                if (lodGroup != null)
                    return; // Found a LODGroup in the hierarchy, no issue

                currentTransform = currentTransform.parent;
            }

            // No LODGroup found in the hierarchy, report the issue
            var diagnostic = Diagnostic.Create(
                AnalysisCategory.GameObjectIssue,
                MissingLODGroupDescriptor.Id,
                gameObject.name)
                .WithEvidence("LodChildCount", lodChildCount)
                .WithLocation(new Location(context.AssetPath))
                .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
            context.ReportIssue(diagnostic);
        }
    }
}

