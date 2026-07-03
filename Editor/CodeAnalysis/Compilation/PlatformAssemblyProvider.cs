using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine.Profiling;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation
{
    internal class PlatformAssemblyProvider : IAssemblyProvider
    {
        IAssemblyCompilation m_Compilation;

        BuildTarget m_Platform = EditorUserBuildSettings.activeBuildTarget;
        string[] m_TargetAssemblies;

        public Action<AssemblyCompilationResult> OnAssemblyCompilationFinished;

        public PlatformAssemblyProvider(BuildTarget targetPlatform, string[] targetAssemblies)
        {
            m_Platform = targetPlatform;
            m_TargetAssemblies = targetAssemblies;
        }

        public IReadOnlyList<CompiledAssemblyInfo> GetAssemblies(IProgress progress = null, CancellationToken cancellationToken = default)
        {
            var compilationResults = new List<AssemblyCompilationResult>();
            void OnFinished(AssemblyCompilationResult compilationResult)
            {
                compilationResults.Add(compilationResult);
                OnAssemblyCompilationFinished?.Invoke(compilationResult);
            }

            // Dispatch to the Roslyn pipeline when (a) the user-pref toggle is
            // set AND (b) the optional Roslyn-pipeline asmdef compiled on this
            // Unity version (its [InitializeOnLoad] populates the factory).
            m_Compilation = CreateCompilation();
            m_Compilation.OnAssemblyCompilationFinished = OnFinished;
            m_Compilation.CompilationMode = CompilationMode.Player;
            m_Compilation.Platform = m_Platform;
            m_Compilation.TargetAssemblies = m_TargetAssemblies;

            Profiler.BeginSample("CodeModule.Analyze.Compilation");
            var compiledAssemblyPaths = m_Compilation.Compile(progress, cancellationToken);
            Profiler.EndSample();

            var assembliesWithErrors = compilationResults.Where(r => r.Status == CompilationStatus.CompilerError)
                .Select(r => r.AssemblyName)
                .ToList();
            if (assembliesWithErrors.Any())
                throw new InvalidOperationException(
                    "The following assemblies failed to compile:\n" + string.Join("\n", assembliesWithErrors));
            if (compiledAssemblyPaths.Count == 0)
                throw new InvalidOperationException("No assemblies were compiled");

            return compiledAssemblyPaths.Select(AssemblyResolver.GetAssemblyInfoFromAssemblyPath).ToArray();
        }

        IAssemblyCompilation CreateCompilation()
        {
            if (AssemblyCompilationFactoryRegistry.RoslynFactory != null)
            {
                return AssemblyCompilationFactoryRegistry.RoslynFactory.Create();
            }
            throw new NotSupportedException("Roslyn pipeline not supported");
        }

        public void Dispose()
        {
            m_Compilation?.Dispose();
        }
    }
}
