using SmartAuditor.Editor.Core;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class OptimizationSettingsAnalyzer : SettingsAnalyzer
    {
        internal const string PLY0007 = nameof(PLY0007);
        internal const string PLY0008 = nameof(PLY0008);

        static readonly Descriptor PrebakeCollisionMeshesDescriptor = new Descriptor(
            PLY0007,
            "Player: Prebake Collision Meshes Disabled",
            Impact.BuildSize | Impact.LoadTime,
            "<b>Prebake Collision Meshes</b> in Player Settings is off. Collision data is baked at scene load instead of at build time, lengthening application startup and scene-load times when physics is in use.",
            "Enable <b>Project Settings &gt; Player &gt; Other Settings &gt; Prebake Collision Meshes</b> for release and profiling builds. Baking moves the cost from runtime to build time, trading larger build size and longer builds for shorter scene loads.");

        static readonly Descriptor OptimizeMeshDataDescriptor = new Descriptor(
            PLY0008,
            "Player: Optimize Mesh Data Disabled",
            Impact.BuildSize | Impact.LoadTime | Impact.Performance,
            "<b>Optimize Mesh Data</b> in Player Settings is off. Unused vertex channels stay in the shipped meshes — they inflate build size, slow mesh loads, and cost GPU bandwidth at draw time.",
            "Enable <b>Project Settings &gt; Player &gt; Other Settings &gt; Optimize Mesh Data</b>. Note: it strips channels based on the material assigned at build time, so swapping a material at runtime to one that needs a stripped channel will break that mesh.");

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (context.IsDescriptorEnabled(PrebakeCollisionMeshesDescriptor) &&
                !PlayerSettings.bakeCollisionMeshes)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, PrebakeCollisionMeshesDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(OptimizeMeshDataDescriptor) &&
                !PlayerSettings.stripUnusedMeshComponents)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, OptimizeMeshDataDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
