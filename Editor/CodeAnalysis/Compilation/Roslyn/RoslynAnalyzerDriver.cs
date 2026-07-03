// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation.Roslyn
{
    // Loads Roslyn analyzer DLLs as AnalyzerFileReferences, runs the source
    // generators they declare, and runs the diagnostic analyzers via
    // CompilationWithAnalyzers.
    //
    // Generators MUST run separately from analyzers: CompilationWithAnalyzers
    // does NOT execute source generators, so without RunGeneratorsAndUpdateCompilation
    // any source-generated members are missing and dependents fail with
    // cascading CS0103 / CS0246 errors.
    //
    // Analyzer DLL paths are filtered to ".dll" up front -- non-DLL assets
    // (.ruleset, .editorconfig) tagged with the RoslynAnalyzer label would
    // otherwise reach AnalyzerFileReference and throw on load.
    internal static class RoslynAnalyzerDriver
    {
        public sealed class AnalyzerRunResult
        {
            public CSharpCompilation UpdatedCompilation;
            public IReadOnlyList<RoslynDiagnosticInfo> Diagnostics;
        }

        public static AnalyzerRunResult Run(
            CSharpCompilation compilation,
            CSharpParseOptions parseOptions,
            IReadOnlyList<string> analyzerDllPaths,
            bool runDiagnosticAnalyzers = true,
            CancellationToken cancellationToken = default)
        {
            var emptyResult = new AnalyzerRunResult
            {
                UpdatedCompilation = compilation,
                Diagnostics = Array.Empty<RoslynDiagnosticInfo>(),
            };

            if (analyzerDllPaths == null || analyzerDllPaths.Count == 0)
                return emptyResult;

            var loader = new SimpleAnalyzerAssemblyLoader();
            var fileRefs = new List<AnalyzerFileReference>();
            foreach (var path in analyzerDllPaths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;
                if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!File.Exists(path))
                    continue;
                // AnalyzerFileReference requires an absolute path and throws
                // "Absolute path expected" otherwise. Unity's analyzer asset
                // paths are project-relative (Packages/..., Assets/...), so
                // resolve here.
                fileRefs.Add(new AnalyzerFileReference(Path.GetFullPath(path), loader));
            }

            if (fileRefs.Count == 0)
                return emptyResult;

            var diagnostics = new List<RoslynDiagnosticInfo>();

            // Source generators first -- the updated compilation is what the
            // analyzers (and ultimately the emit step) operate on. Generators
            // run unconditionally: the runDiagnosticAnalyzers flag only gates
            // the diagnostic analyzer step below. A generator that doesn't run
            // is a correctness bug (downstream code references members the
            // generator was meant to emit), not a perf knob.
            var generators = fileRefs
                .SelectMany(r => SafeGetGenerators(r, diagnostics))
                .ToImmutableArray();

            CSharpCompilation currentCompilation = compilation;
            if (!generators.IsEmpty)
            {
                var driver = CSharpGeneratorDriver.Create(
                    generators: generators,
                    additionalTexts: ImmutableArray<AdditionalText>.Empty,
                    parseOptions: parseOptions,
                    optionsProvider: null);

                driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
                    currentCompilation,
                    out var updated,
                    out var generatorDiagnostics,
                    cancellationToken);

                currentCompilation = (CSharpCompilation)updated;
                diagnostics.AddRange(RoslynApi.ConvertDiagnostics(generatorDiagnostics));
            }

            if (!runDiagnosticAnalyzers)
            {
                return new AnalyzerRunResult
                {
                    UpdatedCompilation = currentCompilation,
                    Diagnostics = diagnostics,
                };
            }

            // Run analyzers against the (possibly generator-updated) compilation.
            var analyzers = fileRefs
                .SelectMany(r => SafeGetAnalyzers(r, diagnostics))
                .ToImmutableArray();

            if (!analyzers.IsEmpty)
            {
                // WithAnalyzers no longer accepts CancellationToken in Roslyn 4.x
                // (the (analyzers, options, cancellationToken) overload is marked
                // obsolete). Cancellation is plumbed through GetAnalyzerDiagnosticsAsync
                // below instead, which is the supported way.
                var compilationWithAnalyzers = currentCompilation.WithAnalyzers(
                    analyzers,
                    options: null);

                // The analyzer driver is async; .Result is acceptable here
                // because we're already running synchronously in the
                // SmartAuditor compile pipeline (no SynchronizationContext
                // to deadlock against). cancellationToken propagates so the
                // run can be aborted from the orchestrator.
                ImmutableArray<Diagnostic> analyzerDiagnostics;
                try
                {
                    analyzerDiagnostics = compilationWithAnalyzers
                        .GetAnalyzerDiagnosticsAsync(cancellationToken)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // An analyzer threw during execution; surface as a
                    // synthetic diagnostic so the rest of the run continues.
                    diagnostics.Add(new RoslynDiagnosticInfo(
                        "<RoslynAnalyzer>",
                        RoslynDiagnosticSeverity.Warning,
                        "Analyzer execution failed: " + ex.Message,
                        filePath: null,
                        oneBasedLine: 0));
                    analyzerDiagnostics = ImmutableArray<Diagnostic>.Empty;
                }

                diagnostics.AddRange(RoslynApi.ConvertDiagnostics(analyzerDiagnostics));
            }

            return new AnalyzerRunResult
            {
                UpdatedCompilation = currentCompilation,
                Diagnostics = diagnostics,
            };
        }

        // AnalyzerFileReference can throw at .GetAnalyzers / .GetGenerators
        // time if the DLL has a bad metadata layout or fails to load (e.g.
        // Roslyn-version mismatch between the DLL and SmartAuditor's compile
        // host). Surface failures as a synthetic warning diagnostic rather
        // than swallowing them silently -- a missing generator means the
        // emit step compiles code that references members that were supposed
        // to be generated, producing cascading CS0103 / CS0117 errors with
        // no signal that the root cause was a generator load failure.
        static IEnumerable<DiagnosticAnalyzer> SafeGetAnalyzers(
            AnalyzerFileReference fileRef,
            List<RoslynDiagnosticInfo> diagnostics)
        {
            try { return fileRef.GetAnalyzers(LanguageNames.CSharp); }
            catch (Exception ex)
            {
                diagnostics.Add(new RoslynDiagnosticInfo(
                    "<RoslynAnalyzer>",
                    RoslynDiagnosticSeverity.Warning,
                    $"Failed to load analyzers from '{fileRef.FullPath}': {ex.Message}",
                    filePath: null,
                    oneBasedLine: 0));
                return Array.Empty<DiagnosticAnalyzer>();
            }
        }

        static IEnumerable<ISourceGenerator> SafeGetGenerators(
            AnalyzerFileReference fileRef,
            List<RoslynDiagnosticInfo> diagnostics)
        {
            try { return fileRef.GetGenerators(LanguageNames.CSharp); }
            catch (Exception ex)
            {
                diagnostics.Add(new RoslynDiagnosticInfo(
                    "<RoslynAnalyzer>",
                    RoslynDiagnosticSeverity.Warning,
                    $"Failed to load source generators from '{fileRef.FullPath}': {ex.Message}",
                    filePath: null,
                    oneBasedLine: 0));
                return Array.Empty<ISourceGenerator>();
            }
        }

        // Minimal IAnalyzerAssemblyLoader. Roslyn's default loader is
        // internal in netstandard2.0 builds; rolling our own keeps the
        // dependency surface small.
        sealed class SimpleAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
        {
            public void AddDependencyLocation(string fullPath) { /* no-op */ }
            public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
        }
    }
}
