// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.IO;
using System.Linq;
using System.Text;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation
{
    // Shared assembly-compilation cache for the Roslyn pipeline.
    // Per-assembly cache lookup / save routes through here so cache keys and
    // on-disk layout stay consistent across runs.
    //
    // Cache files live at:
    //   <projectRoot>/Library/<SmartAuditor.CompactName>/AssemblyCache/<key>/<dll>
    //   <projectRoot>/Library/<SmartAuditor.CompactName>/AssemblyCache/<key>/<pdb>
    //
    // <key> is a 128-bit hash of every input that affects the compiled output.
    // Pipelines pass a non-empty `pipelinePrefix` (e.g. "roslyn") to namespace
    // their cache slots.
    internal static class AssemblyCompilationCache
    {
        public static string GetCacheRootDirectory()
        {
            return PathUtils.Combine(
                SmartAuditor.ProjectPath, "Library", SmartAuditor.CompactName, "AssemblyCache");
        }

        public static bool PurgeAll()
        {
            return PurgeAll(out _);
        }

        public static bool PurgeAll(out string error)
        {
            var cacheRootDirectory = GetCacheRootDirectory();

            try
            {
                if (Directory.Exists(cacheRootDirectory))
                    Directory.Delete(cacheRootDirectory, recursive: true);

                Directory.CreateDirectory(cacheRootDirectory);
                error = string.Empty;
                return true;
            }
            catch (IOException e)
            {
                error = e.Message;
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                error = e.Message;
                return false;
            }
        }

        public static bool IsReadOnlyPackageAssembly(Assembly assembly)
        {
            return AssemblyResolver.IsReadOnlyAssembly(assembly.name);
        }

        public static string GetCacheKey(
            Assembly assembly,
            BuildTarget platform,
            CompilationMode compilationMode,
            string pipelinePrefix = "")
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(pipelinePrefix))
                builder.Append(pipelinePrefix).Append("|");
            builder.Append(assembly.name);
            builder.Append("|").Append(Application.unityVersion);
            builder.Append("|").Append(platform);
            builder.Append("|").Append(compilationMode);
            builder.Append("|").Append(assembly.compilerOptions.AllowUnsafeCode);
            builder.Append("|").Append(assembly.compilerOptions.ApiCompatibilityLevel);
            builder.Append("|").Append(assembly.compilerOptions.CodeOptimization);
            if (assembly.compilerOptions.AdditionalCompilerArguments != null)
                builder.Append("|").Append(string.Join(",", assembly.compilerOptions.AdditionalCompilerArguments));

            builder.Append("|defines:");
            foreach (var define in assembly.defines.OrderBy(define => define))
                builder.Append(define).Append(",");

            // SmartAuditor's own define overrides (Project Settings > Smart Auditor) are layered on
            // top of assembly.defines by the pipeline's define resolver, so a change to them must
            // invalidate the cache -- assembly.defines alone won't reflect them.
            var compilationOverrides = SmartAuditorSettings.instance.Compilation;
            builder.Append("|user-defines:");
            foreach (var define in (compilationOverrides?.GetValidDefines() ?? Enumerable.Empty<string>()).OrderBy(d => d))
                builder.Append(define).Append(",");
            builder.Append("|removed-defines:");
            foreach (var define in (compilationOverrides?.GetValidRemovedDefines() ?? Enumerable.Empty<string>()).OrderBy(d => d))
                builder.Append(define).Append(",");

            builder.Append("|references:");
            foreach (var reference in assembly.assemblyReferences.Select(r => r.name).OrderBy(name => name))
                builder.Append(reference).Append(",");

            builder.Append("|analyzers:");
            var analyzerPaths = assembly.compilerOptions.RoslynAnalyzerDllPaths ?? Array.Empty<string>();
            foreach (var analyzer in analyzerPaths.OrderBy(path => path))
                builder.Append(analyzer).Append(",");
            // Fold the analyzer toggle in: if analyzers are skipped on the
            // Roslyn path, we want a fresh cache slot, otherwise toggling the
            // pref off would surface stale-cached DLLs that were emitted with
            // the analyzer step active.
            builder.Append("|run-diag-analyzers:").Append(UserPreferences.UseRoslynAnalyzers);

            builder.Append("|sources:");
            foreach (var sourceFile in assembly.sourceFiles.Select(PathUtils.ReplaceSeparators).OrderBy(path => path))
                builder.Append(sourceFile).Append(",");

            return Hash128.Compute(builder.ToString()).ToString();
        }

        public static string GetCachedAssemblyPath(
            Assembly assembly,
            string outputPath,
            BuildTarget platform,
            CompilationMode compilationMode,
            string pipelinePrefix = "")
        {
            var cacheKey = GetCacheKey(assembly, platform, compilationMode, pipelinePrefix);
            var cacheDirectory = PathUtils.Combine(GetCacheRootDirectory(), cacheKey);
            return PathUtils.Combine(cacheDirectory, Path.GetFileName(outputPath));
        }

        // Copies a cached DLL+PDB into outputPath when one exists for this
        // assembly's cache key. Returns true if cache was used. Read-only
        // packages only -- editable code is rebuilt every time.
        public static bool TryPopulate(
            Assembly assembly,
            string outputPath,
            BuildTarget platform,
            CompilationMode compilationMode,
            string pipelinePrefix = "")
        {
            if (!IsReadOnlyPackageAssembly(assembly))
                return false;

            var cachedAssemblyPath = GetCachedAssemblyPath(assembly, outputPath, platform, compilationMode, pipelinePrefix);
            if (!File.Exists(cachedAssemblyPath))
                return false;

            File.Copy(cachedAssemblyPath, outputPath, overwrite: true);

            var cachedPdbPath = Path.ChangeExtension(cachedAssemblyPath, ".pdb");
            var outputPdbPath = Path.ChangeExtension(outputPath, ".pdb");
            if (File.Exists(cachedPdbPath))
                File.Copy(cachedPdbPath, outputPdbPath, overwrite: true);

            return true;
        }

        // Saves a freshly-compiled DLL+PDB into the cache. No-op for editable
        // assemblies (caching them would never hit, just waste disk).
        public static void Save(
            Assembly assembly,
            string assemblyPath,
            BuildTarget platform,
            CompilationMode compilationMode,
            string pipelinePrefix = "")
        {
            if (!IsReadOnlyPackageAssembly(assembly))
                return;

            var cachedAssemblyPath = GetCachedAssemblyPath(assembly, assemblyPath, platform, compilationMode, pipelinePrefix);
            var cacheDirectory = Path.GetDirectoryName(cachedAssemblyPath);
            if (!string.IsNullOrEmpty(cacheDirectory) && !Directory.Exists(cacheDirectory))
                Directory.CreateDirectory(cacheDirectory);

            File.Copy(assemblyPath, cachedAssemblyPath, overwrite: true);

            var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            if (File.Exists(pdbPath))
                File.Copy(pdbPath, Path.ChangeExtension(cachedAssemblyPath, ".pdb"), overwrite: true);
        }
    }
}
