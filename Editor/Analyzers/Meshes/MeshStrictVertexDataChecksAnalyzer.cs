// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if UNITY_6000_4_OR_NEWER
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MeshStrictVertexDataChecksAnalyzer : MeshAnalyzer
    {
        internal const string MSH0002 = nameof(MSH0002);

        internal static readonly Descriptor StrictVertexDataChecksEnabledDescriptor = new Descriptor(
            MSH0002,
            "Mesh: Strict Vertex Data Checks Enabled",
            Impact.EditorWorkflow,
            "The <b>Strict Vertex Data Checks</b> option is enabled in the Model Import Settings. When active, Unity validates every vertex on import, discards invalid data, and logs a warning. This adds an import-time performance penalty to every reimport of the model.",
            "Disable <b>Strict Vertex Data Checks</b> in the Model Import Settings unless you specifically need validation of incoming vertex data (for example, to diagnose a model that is crashing or producing corrupted output during import)."
        )
        {
            MessageFormat = "Model '{0}' has Strict Vertex Data Checks enabled",
            DocumentationUrl = "https://docs.unity3d.com/ScriptReference/ModelImporter-strictVertexDataChecks.html",
            MinimumVersion = "6000.4"
        };

        public override void AnalyzeModel(ModelAnalysisContext context)
        {
            if (!context.TryGetModelImporter(out var modelImporter))
                return;

            if (!context.IsDescriptorEnabled(StrictVertexDataChecksEnabledDescriptor, context.AssetPath))
                return;

            if (!modelImporter.strictVertexDataChecks)
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, StrictVertexDataChecksEnabledDescriptor.Id, context.Name)
                .WithLocation(new Location(context.AssetPath));
            context.ReportIssue(diagnostic);
        }
    }
}
#endif
