using SmartAuditor.Editor.CodeAnalysis.Compilation;
using SmartAuditor.Editor.Core;
using UnityEditor.Compilation;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Flags asmdefs that contain test code but aren't marked as Test Assemblies.
    /// Detection is asmdef-JSON-based (see <see cref="AssemblyResolver.IsTestAssembly"/>):
    /// an assembly survives <see cref="AssembliesType.PlayerWithoutTestAssemblies"/> when its
    /// asmdef has no <c>UNITY_INCLUDE_TESTS</c> define constraint, yet its asmdef still
    /// explicitly lists <c>nunit.framework.dll</c> in <c>precompiledReferences</c> or names
    /// <c>UnityEngine.TestRunner</c> / <c>UnityEditor.TestRunner</c> in <c>references</c>.
    /// Such asmdefs ship with player builds and aren't picked up by the Unity Test Runner —
    /// both consequences are usually unintended.
    /// </summary>
    /// <remarks>
    /// We can't infer this from the runtime <see cref="UnityEditor.Compilation.Assembly"/>
    /// because Unity auto-injects <c>nunit.framework</c> as a precompiled reference into
    /// every asmdef when the test framework package is installed; reading the asmdef file
    /// directly is the only reliable signal.
    /// </remarks>
    sealed class MisconfiguredTestAssemblyAnalyzer : ProjectAssetAnalyzer
    {
        internal const string ASM0001 = nameof(ASM0001);

        static readonly Descriptor MisconfiguredTestAssemblyDescriptor = new Descriptor(
            ASM0001,
            "Assembly Definition: Test References Without Test-Assembly Flag",
            Impact.Correctness | Impact.BuildSize,
            "An <b>.asmdef</b> file explicitly references <b>nunit.framework</b> or <b>UnityEngine.TestRunner</b> / <b>UnityEditor.TestRunner</b>, but the file is not marked as a Test Assembly. The assembly ships inside player builds (bloating the binary) and is invisible to the Unity Test Runner (so the tests it contains never execute).",
            "Open the .asmdef file in the Inspector and tick the <b>Test Assemblies</b> checkbox, or move the test code into a dedicated test .asmdef file. If the assembly genuinely needs the test framework at runtime, suppress this rule for that .asmdef file."
        )
        {
            MessageFormat = "Assembly '{0}' references the test framework but its .asmdef file is not marked as a Test Assembly",
        };

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
            if (!context.IsDescriptorEnabled(MisconfiguredTestAssemblyDescriptor))
                return;

            // PlayerWithoutTestAssemblies excludes asmdefs that already declare
            // UNITY_INCLUDE_TESTS, so any survivor whose asmdef JSON still names a test
            // framework reference is misconfigured by definition.
            foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
            {
                var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                if (!AssemblyResolver.IsTestAsmdef(asmdefPath))
                    continue;

                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, MisconfiguredTestAssemblyDescriptor.Id,
                    assembly.name);
                if (!string.IsNullOrEmpty(asmdefPath))
                    diagnostic = diagnostic.WithLocation(new Location(asmdefPath));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
