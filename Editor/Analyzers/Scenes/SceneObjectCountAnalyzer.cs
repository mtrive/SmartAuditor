// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Reports scenes with a GameObject count exceeding the configured threshold.
    /// </summary>
    sealed class SceneObjectCountAnalyzer : SceneAnalyzer
    {
        internal const string SCN0002 = nameof(SCN0002);

        static readonly Descriptor TooManyObjectsDescriptor = new Descriptor(
            SCN0002,
            "Scene: GameObject Count Above Threshold",
            Impact.Performance,
            "The scene contains a GameObject count above the configured threshold. Large scenes pay enumeration, broadcast, and lookup costs proportional to the count even when most objects are inactive.",
            "Split the scene into smaller scenes loaded additively, or replace static-object-heavy regions with merged combined meshes and prefabs that pool at runtime."
        )
        {
            MessageFormat = "Scene contains {0} GameObjects"
        };

        [DiagnosticParameter("MaxGameObjectCount", 1000)]
        int m_MaxGameObjectCount;

        public override void Analyze(SceneAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(TooManyObjectsDescriptor, context.HierarchyPath))
                return;

            var count = CountGameObjectsInScene(context);
            if (count < m_MaxGameObjectCount)
                return;

            var scenePath = context.Scene.IsValid() ? context.Scene.path : string.Empty;
            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.Scene,
                    TooManyObjectsDescriptor.Id,
                    count)
                .WithLocation(new Location(scenePath))
                .WithEvidence("GameObjectCount", count)
                .WithEvidence("MaxGameObjectCount", m_MaxGameObjectCount);
            context.ReportIssue(diagnostic);
        }
    }
}
