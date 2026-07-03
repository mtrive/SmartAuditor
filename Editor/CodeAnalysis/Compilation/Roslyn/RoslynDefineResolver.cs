// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation.Roslyn
{
    // Computes the preprocessor-symbol set the Roslyn pipeline should hand to
    // CSharpParseOptions for one assembly.
    //
    // assembly.defines is Unity's editor-context define set for that asmdef
    // and includes development-only flags (DEVELOPMENT_BUILD, ENABLE_PROFILER,
    // DEBUG, TRACE, UNITY_ASSERTIONS) plus UNITY_EDITOR. SmartAuditor analyses
    // a non-development Release Player target, so those flags shouldn't apply.
    // The legacy AssemblyBuilder pipeline got away with leaking them because
    // AssemblyBuilder was opaque; the Roslyn pipeline owns the input set
    // explicitly, so the filter is enforced here.
    //
    // Final set: assembly.defines
    //   - minus dev-only flags
    //   - minus UNITY_EDITOR (when CompilationMode == Player)
    //   - plus extra defines from csc.rsp / AdditionalCompilerArguments
    //   - plus user-supplied defines from Project Settings > Smart Auditor
    //   - minus user-supplied removed defines from the same panel
    //   - finally, ENABLE_UNITY_COLLECTIONS_CHECKS forced to match the referenced CoreModule
    //
    // User-supplied removals are applied before that last step so the user can drop tokens injected
    // by any earlier layer (asmdef defines, csc.rsp, even their own additions). If a token appears in
    // both add and remove lists, removal wins -- the only sensible resolution for "I want this off".
    //
    // ENABLE_UNITY_COLLECTIONS_CHECKS is the exception, set last to match the referenced
    // UnityEngine.CoreModule (the `collectionsChecks` argument): the editor CoreModule is built with
    // checks and defines AtomicSafetyHandle + the safety-handle NativeArray.ReadOnly ctor, the
    // release-player CoreModule isn't. Unity.Collections and AtomicSafetyHandle-guarded code only
    // compile when the define matches the referenced variant, so it overrides the layers above.
    internal static class RoslynDefineResolver
    {
        static readonly HashSet<string> s_NonReleasePlayerDefines = new HashSet<string>(StringComparer.Ordinal)
        {
            "DEVELOPMENT_BUILD",
            "ENABLE_PROFILER",
            "DEBUG",
            "TRACE",
            "UNITY_ASSERTIONS",
        };

        const string k_UnityEditor = "UNITY_EDITOR";
        const string k_CollectionsChecks = "ENABLE_UNITY_COLLECTIONS_CHECKS";

        public static IReadOnlyList<string> Resolve(
            IReadOnlyList<string> assemblyDefines,
            CompilationMode mode,
            CscResponseFile responseFile,
            IReadOnlyList<string> userDefines = null,
            IReadOnlyList<string> removedDefines = null,
            bool? collectionsChecks = null)
        {
            var result = new List<string>(capacity: (assemblyDefines?.Count ?? 0) + 8);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (assemblyDefines != null)
            {
                foreach (var def in assemblyDefines)
                {
                    if (string.IsNullOrEmpty(def))
                        continue;
                    if (s_NonReleasePlayerDefines.Contains(def))
                        continue;
                    if (mode == CompilationMode.Player && def == k_UnityEditor)
                        continue;
                    if (seen.Add(def))
                        result.Add(def);
                }
            }

            if (responseFile != null)
            {
                foreach (var def in responseFile.Defines)
                {
                    if (string.IsNullOrEmpty(def))
                        continue;
                    if (seen.Add(def))
                        result.Add(def);
                }
            }

            if (userDefines != null)
            {
                foreach (var def in userDefines)
                {
                    if (string.IsNullOrEmpty(def))
                        continue;
                    if (seen.Add(def))
                        result.Add(def);
                }
            }

            if (removedDefines != null && removedDefines.Count > 0)
            {
                var toRemove = new HashSet<string>(StringComparer.Ordinal);
                foreach (var def in removedDefines)
                {
                    if (string.IsNullOrEmpty(def))
                        continue;
                    toRemove.Add(def);
                }
                if (toRemove.Count > 0)
                    result.RemoveAll(d => toRemove.Contains(d));
            }

            // Force ENABLE_UNITY_COLLECTIONS_CHECKS to match the referenced CoreModule variant (see the
            // class comment). Applied last so it overrides every layer above; null leaves it untouched.
            if (collectionsChecks.HasValue)
            {
                result.RemoveAll(d => d == k_CollectionsChecks);
                if (collectionsChecks.Value)
                    result.Add(k_CollectionsChecks);
            }

            return result;
        }
    }
}
