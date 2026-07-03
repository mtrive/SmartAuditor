using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor.Compilation;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation
{
    internal class EditorAssemblyProvider : IAssemblyProvider
    {
        public void Dispose()
        {
            // No-op: this provider only forwards Unity's already-compiled editor-assembly
            // paths and holds no resources of its own. The IDisposable contract is required
            // by IAssemblyProvider so that providers which DO own resources (e.g.
            // PlatformAssemblyProvider with its IAssemblyCompilation) can clean up.
        }

        public IReadOnlyList<CompiledAssemblyInfo> GetAssemblies(IProgress progress = null, CancellationToken cancellationToken = default)
        {
            // The full editor compilation set: pure editor asmdefs *and* player asmdefs in
            // their editor-define form (so #if UNITY_EDITOR content inside player asmdefs
            // is reachable). Filter out test asmdefs for parity with the player path
            // (RoslynAssemblyCompilation applies the same filter on top of
            // PlayerWithoutTestAssemblies).
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Where(a => !AssemblyResolver.IsTestAssembly(a))
                .ToArray();

            return assemblies
                .Select(a => AssemblyResolver.GetAssemblyInfoFromAssemblyPath(a.outputPath))
                .ToArray();
        }
    }
}
