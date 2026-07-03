// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

namespace SmartAuditor.Editor.CodeAnalysis.Compilation
{
    // Factory contract used by PlatformAssemblyProvider to construct an
    // IAssemblyCompilation. The main asmdef ships only this interface and
    // a static registry slot; concrete implementations register themselves
    // from a separate asmdef whose compilation is gated by a Unity version
    // constraint (e.g. the Roslyn pipeline asmdef registers a factory only
    // on Unity 6000.0+ where the vendored Microsoft.CodeAnalysis DLLs load
    // cleanly). On unsupported Unity versions the slot stays null and
    // compilation is unavailable.
    //
    // This decoupling avoids a circular asmdef reference: the Roslyn
    // sub-asmdef references the main, but the main does not need to
    // reference the sub.
    internal interface IAssemblyCompilationFactory
    {
        IAssemblyCompilation Create();
    }

    // Static slot for the optional Roslyn-pipeline factory. Populated by
    // RoslynAssemblyCompilationFactory's [InitializeOnLoad] hook in the
    // Roslyn sub-asmdef when that asmdef compiles. Left null otherwise.
    internal static class AssemblyCompilationFactoryRegistry
    {
        public static IAssemblyCompilationFactory RoslynFactory;
    }
}
