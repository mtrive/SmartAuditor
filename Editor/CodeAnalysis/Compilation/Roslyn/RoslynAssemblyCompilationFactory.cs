// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using UnityEditor;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation.Roslyn
{
    // Registers the Roslyn-pipeline factory into the main asmdef's slot at
    // editor load. Only compiles when the parent asmdef's defineConstraint
    // (UNITY_6000_0_OR_NEWER) is met; on older Unity the registration simply
    // doesn't happen and compilation is unavailable.
    [InitializeOnLoad]
    internal sealed class RoslynAssemblyCompilationFactory : IAssemblyCompilationFactory
    {
        static RoslynAssemblyCompilationFactory()
        {
            AssemblyCompilationFactoryRegistry.RoslynFactory = new RoslynAssemblyCompilationFactory();
        }

        public IAssemblyCompilation Create()
        {
            return new RoslynAssemblyCompilation
            {
                // Pipeline-specific knob driven by UserPreferences; not part
                // of the shared IAssemblyCompilation surface.
                RunDiagnosticAnalyzers = UserPreferences.UseRoslynAnalyzers,
            };
        }
    }
}
