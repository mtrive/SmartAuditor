// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation.Roslyn
{
    // Roslyn-based assembly compilation for SmartAuditor's player-code path.
    //
    // References, defines, and warning-as-error rules are constructed
    // explicitly so compilation matches Unity's normal build without leaking
    // csc defaults. Compile is synchronous (Roslyn.Emit returns a value), so
    // assemblies run in topological order with no poll loop. PDB output is
    // portable (EmitOptions.WithDebugInformationFormat) to avoid routing
    // through Microsoft.DiaSymReader.Native, which crashes Mono via broken
    // COM interop.
    internal sealed class RoslynAssemblyCompilation : IAssemblyCompilation
    {
        // Cache namespace for this pipeline, stamped with the pipeline assembly's module version.
        // The MVID changes whenever this assembly -- or anything it depends on -- is recompiled, so
        // any change to the compilation logic (define resolution, reference resolution, parse/emit
        // options) invalidates DLLs that an earlier build of the pipeline cached. Without this, a
        // read-only package's cached DLL would be reused even after the logic that produced it
        // changed.
        static readonly string s_CachePipelinePrefix =
            "roslyn@" + typeof(RoslynAssemblyCompilation).Assembly.ManifestModule.ModuleVersionId;

        Dictionary<string, RoslynCompilationTask> m_Tasks;
        Dictionary<string, Assembly> m_TaskAssemblies;
        string m_OutputFolder = string.Empty;

        public string[] TargetAssemblies { get; set; }
        public CompilationMode CompilationMode { get; set; } = CompilationMode.Player;
        public BuildTarget Platform { get; set; } = EditorUserBuildSettings.activeBuildTarget;
        public Action<AssemblyCompilationResult> OnAssemblyCompilationFinished { get; set; }

        // Pipeline-specific knob (not on IAssemblyCompilation): when true, the
        // analyzer driver runs CompilationWithAnalyzers; when false it skips
        // the analyzer step (source generators always run). Lets the user
        // disable the (sometimes-slow) analyzer pass via UserPreferences.UseRoslynAnalyzers.
        public bool RunDiagnosticAnalyzers = true;

        public IReadOnlyList<string> Compile(IProgress progress = null, CancellationToken cancellationToken = default)
        {
            if (CompilationMode != CompilationMode.Player)
                throw new InvalidOperationException("RoslynAssemblyCompilation currently supports CompilationMode.Player only.");

            // PlayerWithoutTestAssemblies misses asmdefs that explicitly reference
            // NUnit / TestRunner without setting the test-asmdef flag.
            // AssemblyResolver.IsTestAssembly reads the asmdef JSON to catch them.
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies)
                .Where(a => !AssemblyResolver.IsTestAssembly(a))
                .ToArray();

            // Honour TargetAssemblies + transitive deps.
            if (TargetAssemblies != null && TargetAssemblies.Length > 0)
            {
                var collected = new List<Assembly>();
                foreach (var a in assemblies.Where(a => TargetAssemblies.Contains(a.name)))
                    CollectDependencies(a, collected);
                assemblies = collected.ToArray();
            }

            WarnIfEditorReferenceFallback(assemblies);

            return CompilePlayerAssemblies(assemblies, progress, cancellationToken);
        }

        // When the active build target's player module isn't installed, Unity can't supply player
        // reference assemblies and falls back to the editor's own UnityEngine.CoreModule (built with
        // collections checks on). Code then compiles against editor, not release-player, semantics --
        // #if ENABLE_UNITY_COLLECTIONS_CHECKS code is analysed that a shipped build strips. The define
        // resolver still makes it compile either way; this only makes the loss of fidelity visible.
        void WarnIfEditorReferenceFallback(Assembly[] assemblies)
        {
            var coreModule = assemblies
                .SelectMany(a => a.compiledAssemblyReferences ?? Array.Empty<string>())
                .FirstOrDefault(p => Path.GetFileName(p).Equals("UnityEngine.CoreModule.dll", StringComparison.OrdinalIgnoreCase));

            // Player reference assemblies live under PlaybackEngines/<platform>/Variations/...; anything
            // else (the editor's bundled managed dir) means the player module wasn't available.
            if (coreModule == null || coreModule.IndexOf("PlaybackEngines", StringComparison.OrdinalIgnoreCase) >= 0)
                return;

            UnityEngine.Debug.LogWarning(
                $"[{SmartAuditor.DisplayName}] Code analysis is using editor reference assemblies " +
                $"(UnityEngine.CoreModule at '{coreModule}') instead of player references for build target '{Platform}'. " +
                "The target's player module (e.g. Standalone IL2CPP) appears not to be installed, so results reflect " +
                "editor (collections-checks-on) semantics rather than a release player. Install the target player module " +
                "for release-faithful analysis.");
        }

        IReadOnlyList<string> CompilePlayerAssemblies(Assembly[] assemblies, IProgress progress, CancellationToken cancellationToken)
        {
            progress?.Start("Compiling Assemblies", "Building project scripts for analysis", assemblies.Length);

            m_OutputFolder = FileUtil.GetUniqueTempPathInProject();
            if (!Directory.Exists(m_OutputFolder))
                Directory.CreateDirectory(m_OutputFolder);

            // First pass: create one task per assembly.
            m_Tasks = new Dictionary<string, RoslynCompilationTask>(assemblies.Length);
            m_TaskAssemblies = new Dictionary<string, Assembly>(assemblies.Length);
            foreach (var asm in assemblies)
            {
                var asmCopy = asm;
                var task = new RoslynCompilationTask(asm, m_OutputFolder, CompilationMode, RunDiagnosticAnalyzers)
                {
                    OnCompilationFinished = result =>
                    {
                        // Save freshly-compiled read-only-package DLLs to the
                        // shared cache so subsequent runs can skip the recompile.
                        if (result.Status == CompilationStatus.Success && result.DurationInMs > 0)
                        {
                            AssemblyCompilationCache.Save(
                                asmCopy, result.AssemblyPath,
                                Platform, CompilationMode, s_CachePipelinePrefix);
                        }

                        OnAssemblyCompilationFinished?.Invoke(result);
                    }
                };
                m_Tasks[asm.name] = task;
                m_TaskAssemblies[asm.name] = asm;
            }

            // Second pass: wire dependencies.
            foreach (var asm in assemblies)
            {
                var deps = new List<RoslynCompilationTask>();
                if (asm.assemblyReferences != null)
                {
                    foreach (var dep in asm.assemblyReferences)
                    {
                        if (m_Tasks.TryGetValue(dep.name, out var depTask))
                            deps.Add(depTask);
                    }
                }
                m_Tasks[asm.name].AddDependencies(deps.ToArray());
            }

            // Topological run order: a task only runs after all its deps.
            // Each task checks the shared cache first; cache hits short-circuit
            // the compile and report DurationInMs == 0 (cache-hit signal tests check).
            //
            // progress.Advance is called BEFORE each task runs so the user
            // sees which assembly is currently being compiled. Roslyn.Emit
            // is a single synchronous blocking call, so the editor can't
            // repaint mid-compile; a post-compile Advance would leave the
            // bar appearing frozen. Advancing first means the bar reflects
            // current activity even if it doesn't tick smoothly within an emit.
            var ordered = TopologicalOrder(assemblies);
            foreach (var asm in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var task = m_Tasks[asm.name];
                progress?.Advance(asm.name);
                if (TryCacheHit(asm, task))
                    continue;
                task.Run();
            }

            progress?.Clear();

            return m_Tasks.Values
                .Where(t => t.IsCompletedSuccessfully)
                .Select(t => t.AssemblyPath)
                .ToList();
        }

        // Cache-hit short circuit. Copies cached DLL+PDB into the task's
        // output path, marks the task as Success with DurationInMs == 0.
        // Returns true on hit so the caller skips the compile.
        bool TryCacheHit(Assembly assembly, RoslynCompilationTask task)
        {
            if (!AssemblyCompilationCache.TryPopulate(
                    assembly, task.AssemblyPath,
                    Platform, CompilationMode, s_CachePipelinePrefix))
                return false;

            task.MarkAsCached();
            return true;
        }

        // Kahn-style topological sort over assembly.assemblyReferences. Cycles
        // are broken by emitting cycle members in their original order
        // (Unity's compilation pipeline does not allow real cycles; this is
        // belt-and-braces).
        static List<Assembly> TopologicalOrder(Assembly[] assemblies)
        {
            var byName = assemblies.ToDictionary(a => a.name);
            var emitted = new HashSet<string>();
            var ordered = new List<Assembly>(assemblies.Length);

            void Visit(Assembly a, HashSet<string> visiting)
            {
                if (emitted.Contains(a.name))
                    return;
                if (!visiting.Add(a.name))
                    return; // cycle, drop

                if (a.assemblyReferences != null)
                {
                    foreach (var dep in a.assemblyReferences)
                    {
                        if (byName.TryGetValue(dep.name, out var depAsm))
                            Visit(depAsm, visiting);
                    }
                }

                emitted.Add(a.name);
                ordered.Add(a);
                visiting.Remove(a.name);
            }

            foreach (var a in assemblies)
                Visit(a, new HashSet<string>());

            return ordered;
        }

        static void CollectDependencies(Assembly assembly, List<Assembly> output)
        {
            if (output.Contains(assembly))
                return;
            output.Add(assembly);
            if (assembly.assemblyReferences == null)
                return;
            foreach (var dep in assembly.assemblyReferences)
                CollectDependencies(dep, output);
        }

        public void Dispose()
        {
            if (m_Tasks == null || string.IsNullOrEmpty(m_OutputFolder) || !Directory.Exists(m_OutputFolder))
                return;

            foreach (var task in m_Tasks.Values.Where(t => t.IsCompletedSuccessfully))
            {
                TryDelete(task.AssemblyPath);
                TryDelete(Path.ChangeExtension(task.AssemblyPath, ".pdb"));
            }
            m_Tasks.Clear();
            m_OutputFolder = string.Empty;
        }

        static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException) { /* best-effort */ }
            catch (UnauthorizedAccessException) { /* best-effort */ }
        }
    }
}
