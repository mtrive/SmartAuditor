// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation.Roslyn
{
    // Mirror of Microsoft.CodeAnalysis.DiagnosticSeverity. Kept as a POCO so
    // RoslynCompilationTask doesn't have to take a hard dependency on the
    // Roslyn types -- only this class does.
    internal enum RoslynDiagnosticSeverity
    {
        Hidden = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    // Mirror of Microsoft.CodeAnalysis.ReportDiagnostic.
    internal enum RoslynReportDiagnostic
    {
        Default = 0,
        Error = 1,
        Warn = 2,
        Info = 3,
        Hidden = 4,
        Suppress = 5,
    }

    internal readonly struct RoslynDiagnosticInfo
    {
        public RoslynDiagnosticInfo(string id, RoslynDiagnosticSeverity severity, string message, string filePath, int oneBasedLine)
        {
            Id = id;
            Severity = severity;
            Message = message;
            FilePath = filePath;
            Line = oneBasedLine;
        }

        public string Id { get; }
        public RoslynDiagnosticSeverity Severity { get; }
        public string Message { get; }
        public string FilePath { get; }
        public int Line { get; }
    }

    internal sealed class RoslynEmitResult
    {
        public bool Success;
        public IReadOnlyList<RoslynDiagnosticInfo> Diagnostics;
    }

    // Thin typed wrapper over Microsoft.CodeAnalysis used by
    // RoslynCompilationTask. Keeping the wrapper means the rest of
    // SmartAuditor doesn't have to reach into Roslyn types directly --
    // makes it easier to swap implementations or add caching later.
    //
    // Microsoft.CodeAnalysis.dll and Microsoft.CodeAnalysis.CSharp.dll are
    // vendored under Editor/Plugins/Roslyn/ (see that folder's README) and
    // referenced through SmartAuditor.Editor.asmdef's precompiledReferences,
    // so types resolve at compile time -- no reflection bootstrap needed.
    internal static class RoslynApi
    {
        public static CSharpParseOptions CreateParseOptions(string langVersion, IEnumerable<string> defines)
        {
            var lv = ResolveLanguageVersion(langVersion);
            return CSharpParseOptions.Default
                .WithLanguageVersion(lv)
                .WithPreprocessorSymbols(defines ?? Array.Empty<string>());
        }

        // Encoding MUST be non-null. Without it Roslyn cannot emit PDB
        // source-text references and Emit() returns CS8055 "Cannot emit debug
        // information for a source text without encoding."
        public static SyntaxTree ParseSyntaxTree(string sourceText, string filePath, CSharpParseOptions parseOptions)
        {
            return CSharpSyntaxTree.ParseText(
                sourceText,
                parseOptions,
                path: filePath ?? string.Empty,
                encoding: Encoding.UTF8);
        }

        public static MetadataReference CreateMetadataReferenceFromFile(string path)
        {
            // MetadataReference.CreateFromFile requires an absolute path and
            // throws "Absolute path expected" otherwise. Reference paths from
            // Unity's CompilationPipeline (Library/ScriptAssemblies/..., Packages/...)
            // are project-relative, so resolve here. Path.GetFullPath uses
            // Environment.CurrentDirectory, which is the project root inside
            // the Unity editor.
            return MetadataReference.CreateFromFile(Path.GetFullPath(path));
        }

        public static CSharpCompilationOptions CreateCompilationOptions(
            bool allowUnsafe,
            RoslynReportDiagnostic generalOption,
            IReadOnlyDictionary<string, RoslynReportDiagnostic> specificOptions)
        {
            var specific = specificOptions == null
                ? Enumerable.Empty<KeyValuePair<string, ReportDiagnostic>>()
                : specificOptions.Select(kv => new KeyValuePair<string, ReportDiagnostic>(kv.Key, (ReportDiagnostic)kv.Value));

            return new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: allowUnsafe,
                optimizationLevel: OptimizationLevel.Release,
                generalDiagnosticOption: (ReportDiagnostic)generalOption,
                specificDiagnosticOptions: specific);
        }

        public static CSharpCompilation CreateCompilation(
            string assemblyName,
            IEnumerable<SyntaxTree> syntaxTrees,
            IEnumerable<MetadataReference> references,
            CSharpCompilationOptions options)
        {
            return CSharpCompilation.Create(assemblyName, syntaxTrees, references, options);
        }

        // Emit DLL + portable PDB. Portable PDB is critical: without
        // DebugInformationFormat.PortablePdb, Roslyn falls back to legacy
        // Windows-PDB emission via Microsoft.DiaSymReader.Native, which
        // crashes Mono via broken COM interop.
        public static RoslynEmitResult Emit(CSharpCompilation compilation, string peOutputPath, string pdbOutputPath)
        {
            using var peStream = File.Create(peOutputPath);
            using var pdbStream = string.IsNullOrEmpty(pdbOutputPath) ? null : File.Create(pdbOutputPath);

            var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);
            var result = compilation.Emit(peStream, pdbStream, options: emitOptions);

            return new RoslynEmitResult
            {
                Success = result.Success,
                Diagnostics = ConvertDiagnostics(result.Diagnostics),
            };
        }

        public static IReadOnlyList<RoslynDiagnosticInfo> ConvertDiagnostics(IEnumerable<Diagnostic> diagnostics)
        {
            var list = new List<RoslynDiagnosticInfo>();
            if (diagnostics == null)
                return list;

            foreach (var d in diagnostics)
            {
                if (d == null)
                    continue;

                // Skip Hidden severity. csc-via-AssemblyBuilder filters
                // these out by default, so the legacy SmartAuditor pipeline
                // never surfaced them. Roslyn's compilation.Emit returns the
                // full set including hidden diagnostics; forwarding them
                // would inflate CodeCompilerMessage findings with noise that
                // doesn't appear in a normal Unity compile. Info-severity
                // diagnostics from analyzers / source generators are kept --
                // they're legitimate findings the user opted into.
                if (d.Severity == DiagnosticSeverity.Hidden)
                    continue;

                string filePath = null;
                int line = 0;
                if (d.Location != null)
                {
                    var span = d.Location.GetLineSpan();
                    filePath = span.Path;
                    // Roslyn line numbers are 0-based; CompilerMessage is 1-based.
                    line = span.StartLinePosition.Line + 1;
                }

                list.Add(new RoslynDiagnosticInfo(
                    d.Id,
                    (RoslynDiagnosticSeverity)d.Severity,
                    d.GetMessage(),
                    filePath,
                    line));
            }
            return list;
        }

        static LanguageVersion ResolveLanguageVersion(string langVersion)
        {
            // Mirrors what csc does: numeric, "latest", or "CSharpN" enum names.
            // Falls back to LanguageVersion.Latest if the requested value isn't
            // recognised. With Roslyn 4.8 vendored, all values up to C# 12 are
            // supported, so the project's csc.rsp -langversion:10 maps cleanly.
            if (string.IsNullOrEmpty(langVersion))
                return LanguageVersion.Default;

            if (LanguageVersionFacts.TryParse(langVersion.Trim(), out var lv))
                return lv;

            return LanguageVersion.Latest;
        }
    }
}
