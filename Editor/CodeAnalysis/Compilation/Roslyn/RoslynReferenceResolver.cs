// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Compilation;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation.Roslyn
{
    // Builds the explicit reference-path set for one assembly's Roslyn
    // compilation.
    //
    // The Roslyn pipeline owns refs explicitly, so each path here MUST be one
    // the asmdef itself opted into. There is no AssemblyBuilder.defaultReferences
    // leak surface to filter against, which is the whole reason for moving to
    // this pipeline.
    //
    // Sources, in order:
    //   1. Asmdef-built deps (assembly.assemblyReferences) -- redirected to
    //      the rebuilt-temp DLL path emitted by an earlier task in this run.
    //   2. Precompiled deps (assembly.compiledAssemblyReferences) -- per-asmdef
    //      precompiled refs (Plugins, asmdef precompiledReferences, the
    //      UnityEngine / system modules Unity computed for this asmdef).
    //
    // Deduplication is by filename (case-insensitive). Earlier sources win.
    //
    // We deliberately do NOT enumerate every UnityEngine.*Module.dll from
    // CompilationPipeline.GetPrecompiledAssemblyPaths(UnityEngine). Adding
    // them all gets the legacy UnityEngine.dll umbrella in scope, but it
    // also makes every UnityEngine type visible to every asmdef -- and that
    // collides with user types of the same name (e.g. CS0104 ambiguity
    // between FM.Match.Stadium.Cloth and UnityEngine.Cloth). Unity's normal
    // compile relies on the per-asmdef compiledAssemblyReferences set, which
    // is more conservative; mirroring it is the only reliable parity move.
    internal static class RoslynReferenceResolver
    {
        public static IReadOnlyList<string> Resolve(
            Assembly assembly,
            string outputFolder)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (string.IsNullOrEmpty(outputFolder))
                throw new ArgumentException("outputFolder must not be empty.", nameof(outputFolder));

            var seenFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new List<string>();

            // Pass 1: rebuilt-temp paths for asmdef refs. These take precedence.
            if (assembly.assemblyReferences != null)
            {
                foreach (var dep in assembly.assemblyReferences)
                {
                    if (dep == null)
                        continue;
                    var filename = Path.GetFileName(dep.outputPath);
                    if (string.IsNullOrEmpty(filename))
                        continue;
                    var rebuilt = Path.Combine(outputFolder, filename);
                    if (seenFilenames.Add(filename))
                        paths.Add(rebuilt);
                }
            }

            // Pass 2: per-asmdef precompiled refs (UnityEngine modules,
            // system refs, asmdef precompiledReferences, plugin DLLs).
            if (assembly.compiledAssemblyReferences != null)
            {
                foreach (var path in assembly.compiledAssemblyReferences)
                {
                    if (string.IsNullOrEmpty(path))
                        continue;
                    var filename = Path.GetFileName(path);
                    if (seenFilenames.Add(filename))
                        paths.Add(path);
                }
            }

            return paths;
        }
    }
}
