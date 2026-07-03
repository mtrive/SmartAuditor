// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityEditor.Compilation;
using SmartAuditor.Editor.Settings;
using CompilerMessageType = SmartAuditor.Editor.CodeAnalysis.Compilation.CompilerMessageType;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation.Roslyn
{
    // Per-assembly state machine for the Roslyn pipeline. Exposes
    // CompilationStatus, OnCompilationFinished, and IsCompletedSuccessfully
    // so the orchestrator can treat each assembly uniformly.
    //
    // Roslyn's Emit is synchronous, so Run() completes the whole compile in
    // one call. The orchestrator iterates in topological order so dependencies
    // are guaranteed built when this task starts.
    internal sealed class RoslynCompilationTask
    {
        // Unity-specific default warning suppressions. These match what
        // Unity's normal compile silently applies (visible in normal builds
        // because csc emits no warning, but Unity does this via internal
        // BeeDriver flags that don't show up in
        // assembly.compilerOptions.AdditionalCompilerArguments). Mirroring
        // them here avoids surfacing Unity-irrelevant warnings as Errors when
        // the project's csc.rsp has -warnaserror+.
        //   CS0649: "Field is never assigned to" -- false-positive on
        //           [SerializeField] private fields populated by Unity's
        //           serializer, not by a constructor.
        //   CS8618: "Non-nullable field must contain a non-null value when
        //           exiting constructor" -- the NRT-aware version of CS0649,
        //           same root cause when nullable is enabled.
        static readonly string[] s_UnityDefaultSuppressions =
        {
            "CS0649",
            "CS8618",
        };

        // Cached per CoreModule path: whether that UnityEngine.CoreModule.dll was built with collections
        // checks (i.e. defines AtomicSafetyHandle). The CoreModule is the same across every assembly in
        // a run, so the metadata probe runs at most once per path.
        static readonly Dictionary<string, bool> s_CollectionsChecksByCoreModule =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        readonly Assembly m_Assembly;
        readonly string m_OutputFolder;
        readonly string m_AssemblyPath;
        readonly string m_PdbPath;
        readonly CompilationMode m_CompilationMode;
        readonly bool m_RunDiagnosticAnalyzers;

        RoslynCompilationTask[] m_Dependencies = Array.Empty<RoslynCompilationTask>();
        CompilationStatus m_Status = CompilationStatus.NotStarted;
        CompilerMessage[] m_Messages = Array.Empty<CompilerMessage>();
        DateTime m_StartTimeUtc;
        DateTime m_EndTimeUtc;
        long m_DurationMs;

        public string AssemblyName => Path.GetFileNameWithoutExtension(m_AssemblyPath);
        public string AssemblyPath => m_AssemblyPath;
        public bool IsCompletedSuccessfully => m_Status == CompilationStatus.Success;
        public CompilationStatus Status => m_Status;
        public Action<AssemblyCompilationResult> OnCompilationFinished;

        // Mark this task as satisfied by a cache hit. The orchestrator copies
        // the cached DLL+PDB into m_AssemblyPath / m_PdbPath before calling
        // this; we just transition state and fire the same callback the
        // compile path would have. DurationInMs == 0 is the cache-hit signal
        // that consumers (and tests) check.
        public void MarkAsCached()
        {
            if (m_Status != CompilationStatus.NotStarted)
                return;
            m_Status = CompilationStatus.Success;
            m_StartTimeUtc = DateTime.UtcNow;
            m_EndTimeUtc = m_StartTimeUtc;
            m_DurationMs = 0;
            m_Messages = Array.Empty<CompilerMessage>();
            FireCallback();
        }

        public RoslynCompilationTask(
            Assembly assembly,
            string outputFolder,
            CompilationMode compilationMode,
            bool runDiagnosticAnalyzers)
        {
            m_Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            m_OutputFolder = outputFolder;
            m_CompilationMode = compilationMode;
            m_RunDiagnosticAnalyzers = runDiagnosticAnalyzers;

            var filename = Path.GetFileName(assembly.outputPath);
            m_AssemblyPath = Path.Combine(outputFolder, filename);
            m_PdbPath = Path.ChangeExtension(m_AssemblyPath, ".pdb");
        }

        public void AddDependencies(RoslynCompilationTask[] dependencies)
        {
            m_Dependencies = dependencies ?? Array.Empty<RoslynCompilationTask>();
        }

        // Synchronous compile + emit. The orchestrator must call this only
        // after all dependencies have been Run() so their rebuilt-temp DLLs
        // exist in m_OutputFolder.
        public void Run()
        {
            if (m_Status != CompilationStatus.NotStarted)
                return;

            m_StartTimeUtc = DateTime.UtcNow;
            m_EndTimeUtc = m_StartTimeUtc;

            // If any dependency failed, propagate MissingDependency without
            // attempting to compile.
            if (m_Dependencies.Any(d => d.Status != CompilationStatus.Success))
            {
                m_Status = CompilationStatus.MissingDependency;
                m_EndTimeUtc = DateTime.UtcNow;
                FireCallback();
                return;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                m_Status = CompilationStatus.IsCompiling;
                CompileAndEmit();
                sw.Stop();
                m_EndTimeUtc = DateTime.UtcNow;
                m_DurationMs = sw.ElapsedMilliseconds;
                m_Status = m_Messages.Any(m => m.Type == CompilerMessageType.Error)
                    ? CompilationStatus.CompilerError
                    : CompilationStatus.Success;
            }
            catch (Exception ex)
            {
                sw.Stop();
                m_EndTimeUtc = DateTime.UtcNow;
                m_DurationMs = sw.ElapsedMilliseconds;
                m_Messages = new[]
                {
                    new CompilerMessage
                    {
                        Code = "<Roslyn>",
                        Type = CompilerMessageType.Error,
                        Message = ex.Message,
                        File = m_Assembly.sourceFiles?.FirstOrDefault() ?? string.Empty,
                        Line = 0,
                    }
                };
                m_Status = CompilationStatus.CompilerError;
            }

            FireCallback();
        }

        void CompileAndEmit()
        {
            var responseFile = LoadResponseFile();
            var overrides = SmartAuditorSettings.instance.Compilation;
            var userDefines = overrides?.GetValidDefines();
            var removedDefines = overrides?.GetValidRemovedDefines();

            // Resolve references first: ENABLE_UNITY_COLLECTIONS_CHECKS must match the referenced
            // UnityEngine.CoreModule variant (editor build = checks-on, release player = checks-off),
            // otherwise Unity.Collections / AtomicSafetyHandle-guarded code won't compile.
            var refPaths = RoslynReferenceResolver.Resolve(m_Assembly, m_OutputFolder);
            var collectionsChecks = ResolveCollectionsChecks(refPaths);

            var defines = RoslynDefineResolver.Resolve(m_Assembly.defines, m_CompilationMode, responseFile, userDefines, removedDefines, collectionsChecks);
            var parseOptions = RoslynApi.CreateParseOptions(responseFile.LangVersion, defines);

            var trees = new List<SyntaxTree>(m_Assembly.sourceFiles.Length);
            foreach (var src in m_Assembly.sourceFiles)
            {
                var text = File.ReadAllText(src);
                trees.Add(RoslynApi.ParseSyntaxTree(text, src, parseOptions));
            }

            var refs = new List<MetadataReference>(refPaths.Count);
            foreach (var p in refPaths)
            {
                if (File.Exists(p))
                    refs.Add(RoslynApi.CreateMetadataReferenceFromFile(p));
            }

            var allowUnsafe = m_Assembly.compilerOptions.AllowUnsafeCode || (responseFile.AllowUnsafe ?? false);
            var generalOption = (responseFile.WarningsAsErrors ?? false)
                ? RoslynReportDiagnostic.Error
                : RoslynReportDiagnostic.Default;

            // Build per-diagnostic overrides. Order matters: Unity defaults
            // are applied first, then csc.rsp + AdditionalCompilerArguments
            // overrides. A user who genuinely wants -warnaserror:CS8618 in
            // their own code can still set it; the Unity default suppression
            // only wins when the user hasn't said anything.
            //
            // CS0649 / CS8618 are Unity-specific false positives:
            // [SerializeField] private fields are populated by Unity's
            // serializer, not by C# constructors. Unity's normal compile
            // suppresses these via internal csc invocation flags that
            // AdditionalCompilerArguments doesn't expose to us.
            var specific = new Dictionary<string, RoslynReportDiagnostic>();
            foreach (var id in s_UnityDefaultSuppressions)
                specific[id] = RoslynReportDiagnostic.Suppress;
            foreach (var id in responseFile.SpecificWarningsAsErrors)
                specific[id] = RoslynReportDiagnostic.Error;
            foreach (var id in responseFile.SpecificNoWarn)
                specific[id] = RoslynReportDiagnostic.Suppress;

            // Apply Unity-package warnaserror neutralisation: any com.unity.*
            // or read-only package keeps warnings as warnings even if the
            // project's csc.rsp asks for -warnaserror+. Mirrors the existing
            // AssemblyCompilation behaviour without needing a -warnaserror-
            // flag round-trip.
            if (!ScopeAllowsWarningsAsErrors())
                generalOption = RoslynReportDiagnostic.Default;

            var options = RoslynApi.CreateCompilationOptions(allowUnsafe, generalOption, specific);
            var compilation = RoslynApi.CreateCompilation(AssemblyName, trees, refs, options);

            // Run source generators (which may add synthesised members the
            // emit step depends on) and analyzers (whose diagnostics merge
            // into the per-assembly Messages alongside the compiler's own).
            // The DLL list comes from Unity's per-assembly compilerOptions --
            // same list Unity's normal build uses, so generators are scoped
            // to the assemblies that actually reference them. Generators
            // always run; m_RunDiagnosticAnalyzers controls only the
            // diagnostic analyzer step (perf escape hatch for the user pref).
            var analyzerPaths = m_Assembly.compilerOptions.RoslynAnalyzerDllPaths
                                ?? Array.Empty<string>();
            var analyzerRun = RoslynAnalyzerDriver.Run(
                compilation,
                parseOptions,
                analyzerPaths,
                m_RunDiagnosticAnalyzers);
            compilation = analyzerRun.UpdatedCompilation;

            var emit = RoslynApi.Emit(compilation, m_AssemblyPath, m_PdbPath);

            m_Messages = ConvertDiagnostics(emit.Diagnostics
                .Concat(analyzerRun.Diagnostics)
                .ToList());
            if (!emit.Success)
            {
                // Make sure the failure is visible even if Roslyn returned
                // success=false without explicit diagnostics (rare, but
                // defensive).
                if (!m_Messages.Any(m => m.Type == CompilerMessageType.Error))
                {
                    m_Messages = m_Messages.Concat(new[]
                    {
                        new CompilerMessage
                        {
                            Code = "<Roslyn>",
                            Type = CompilerMessageType.Error,
                            Message = "Emit returned success=false without an error diagnostic.",
                            File = m_Assembly.sourceFiles?.FirstOrDefault() ?? string.Empty,
                            Line = 0,
                        }
                    }).ToArray();
                }
            }
        }

        // Whether ENABLE_UNITY_COLLECTIONS_CHECKS should be defined for this compile, matching the
        // referenced UnityEngine.CoreModule. Null when no CoreModule is referenced (leave it untouched).
        static bool? ResolveCollectionsChecks(IReadOnlyList<string> refPaths)
        {
            var coreModule = refPaths.FirstOrDefault(
                p => Path.GetFileName(p).Equals("UnityEngine.CoreModule.dll", StringComparison.OrdinalIgnoreCase));
            return coreModule == null ? (bool?)null : CollectionsChecksAvailable(coreModule);
        }

        // Probes whether the referenced CoreModule defines the collections-checks-only type
        // Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle, via a metadata-only Roslyn compilation
        // (no parsing, no emit). Result cached per path.
        static bool CollectionsChecksAvailable(string coreModulePath)
        {
            if (s_CollectionsChecksByCoreModule.TryGetValue(coreModulePath, out var cached))
                return cached;

            var reference = RoslynApi.CreateMetadataReferenceFromFile(coreModulePath);
            var probe = CSharpCompilation.Create("SmartAuditor.CoreModuleProbe", references: new[] { reference });
            var hasChecks =
                probe.GetTypeByMetadataName("Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle") != null;

            s_CollectionsChecksByCoreModule[coreModulePath] = hasChecks;
            return hasChecks;
        }

        CscResponseFile LoadResponseFile()
        {
            // Two sources contribute compiler flags for an assembly:
            //   1. csc.rsp adjacent to the asmdef (or Assets/csc.rsp for
            //      predefined assemblies).
            //   2. Per-assembly AdditionalCompilerArguments that Unity
            //      computes -- includes Unity-default suppressions like
            //      -nowarn:0649 (so [SerializeField] private fields don't
            //      trigger CS0649 "Field never assigned"). Without forwarding
            //      these, project assemblies fail with errors that don't
            //      appear in a normal Unity compile.
            var rsp = LoadFileBasedRsp();
            MergeAdditionalCompilerArguments(rsp);
            return rsp;
        }

        CscResponseFile LoadFileBasedRsp()
        {
            // Predefined assemblies (Assembly-CSharp, Assembly-CSharp-Editor)
            // pick up Assets/csc.rsp; asmdef-based assemblies pick up a
            // csc.rsp adjacent to the asmdef. We don't know the asmdef path
            // here, so probe the source-file's directory upward looking for
            // a sibling csc.rsp. For predefined assemblies, fall through to
            // Assets/csc.rsp.
            if (m_Assembly.sourceFiles == null || m_Assembly.sourceFiles.Length == 0)
                return new CscResponseFile();

            var firstSrc = m_Assembly.sourceFiles[0];
            var dir = Path.GetDirectoryName(firstSrc);
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, "csc.rsp");
                if (File.Exists(candidate))
                    return CscResponseFileParser.ParseFile(candidate);
                var parent = Path.GetDirectoryName(dir);
                if (parent == dir)
                    break;
                dir = parent;
            }
            return new CscResponseFile();
        }

        void MergeAdditionalCompilerArguments(CscResponseFile target)
        {
            var args = m_Assembly.compilerOptions.AdditionalCompilerArguments;
            if (args == null || args.Length == 0)
                return;

            // CscResponseFileParser.Parse handles tokens separated by
            // whitespace or newlines, so joining is sufficient.
            var fromArgs = CscResponseFileParser.Parse(string.Join("\n", args));

            // Merge into target. Later sources override earlier scalar values
            // (langversion, allowUnsafe, warnaserror+/-, nullable); set/list
            // fields union.
            if (fromArgs.LangVersion != null)
                target.LangVersion = fromArgs.LangVersion;
            if (fromArgs.AllowUnsafe.HasValue)
                target.AllowUnsafe = fromArgs.AllowUnsafe;
            if (fromArgs.WarningsAsErrors.HasValue)
                target.WarningsAsErrors = fromArgs.WarningsAsErrors;
            if (fromArgs.Nullable != null)
                target.Nullable = fromArgs.Nullable;
            foreach (var id in fromArgs.SpecificWarningsAsErrors) target.SpecificWarningsAsErrors.Add(id);
            foreach (var id in fromArgs.SpecificNoWarn) target.SpecificNoWarn.Add(id);
            foreach (var d in fromArgs.Defines) target.Defines.Add(d);
        }

        bool ScopeAllowsWarningsAsErrors()
        {
            // Read-only and com.unity.* packages keep warnings as warnings.
            if (AssemblyResolver.IsReadOnlyAssembly(m_Assembly.name))
                return false;

            if (m_Assembly.sourceFiles == null || m_Assembly.sourceFiles.Length == 0)
                return true;

            var src = m_Assembly.sourceFiles[0];
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(src);
            if (pkg != null && pkg.name.StartsWith("com.unity.", StringComparison.OrdinalIgnoreCase))
                return false;

            var n = src.Replace('\\', '/');
            if (n.IndexOf("/com.unity.", StringComparison.OrdinalIgnoreCase) >= 0
                || n.StartsWith("com.unity.", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        static CompilerMessage[] ConvertDiagnostics(IReadOnlyList<RoslynDiagnosticInfo> diags)
        {
            if (diags == null || diags.Count == 0)
                return Array.Empty<CompilerMessage>();

            var arr = new CompilerMessage[diags.Count];
            for (int i = 0; i < diags.Count; i++)
            {
                var d = diags[i];
                arr[i] = new CompilerMessage
                {
                    Code = d.Id ?? "<Roslyn>",
                    Type = SeverityToType(d.Severity),
                    Message = d.Message ?? string.Empty,
                    File = string.IsNullOrEmpty(d.FilePath) ? SmartAuditor.ProjectPath : d.FilePath,
                    Line = d.Line,
                };
            }
            return arr;
        }

        static CompilerMessageType SeverityToType(RoslynDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case RoslynDiagnosticSeverity.Error: return CompilerMessageType.Error;
                case RoslynDiagnosticSeverity.Warning: return CompilerMessageType.Warning;
                case RoslynDiagnosticSeverity.Info: return CompilerMessageType.Info;
                default: return CompilerMessageType.Info;
            }
        }

        void FireCallback()
        {
            OnCompilationFinished?.Invoke(new AssemblyCompilationResult
            {
                AssemblyName = AssemblyName,
                AssemblyPath = m_AssemblyPath,
                DependentAssemblyNames = m_Dependencies.Select(d => d.AssemblyName).ToArray(),
                StartTime = m_StartTimeUtc,
                EndTime = m_EndTimeUtc,
                DurationInMs = m_DurationMs,
                Messages = m_Messages,
                Status = m_Status,
            });
        }
    }
}
