// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class SceneHierarchyDepthAnalyzer : SceneAnalyzer
    {
        internal const string SCN0001 = nameof(SCN0001);

        static readonly Descriptor DeepHierarchyDescriptor = new Descriptor(
            SCN0001,
            "Scene: Deep Hierarchy",
            Impact.Performance,
            "The scene contains a GameObject whose hierarchy depth exceeds the configured threshold. Deep hierarchies pay transform-recomputation cost on every move and fragment memory layout for child Transforms.",
            "Flatten the hierarchy by reparenting deeply-nested children to higher-level parents, or split the subtree into a prefab that contains its own pruned hierarchy."
        )
        {
            MessageFormat = "GameObject '{0}' is {1} levels deep"
        };

        [DiagnosticParameter("MaxHierarchyDepth", 8)]
        int m_MaxHierarchyDepth;

        public override void Analyze(SceneAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(DeepHierarchyDescriptor, context.HierarchyPath))
                return;

            var scenePath = context.Scene.IsValid() ? context.Scene.path : string.Empty;

            TraverseSceneHierarchy(context, go =>
            {
                var depth = GetDepth(go.transform);
                if (depth < m_MaxHierarchyDepth)
                    return;

                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.Scene,
                        DeepHierarchyDescriptor.Id,
                        GetHierarchyPath(go),
                        depth)
                    .WithLocation(new Location(scenePath))
                    .WithEvidence("HierarchyDepth", depth)
                    .WithEvidence("MaxHierarchyDepth", m_MaxHierarchyDepth);
                context.ReportIssue(diagnostic);
            });
        }

        static int GetDepth(Transform t)
        {
            var depth = 0;
            var parent = t.parent;
            while (parent != null)
            {
                depth++;
                parent = parent.parent;
            }
            return depth;
        }

        static string GetHierarchyPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
