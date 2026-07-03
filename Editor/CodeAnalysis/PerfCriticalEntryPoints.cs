// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using Mono.Cecil;
using UnityEngine;

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// Identifies methods that run on a runtime performance-critical path: the per-frame update
    /// callbacks of a <c>MonoBehaviour</c>. These are the runtime counterpart to the editor entry
    /// points in <see cref="EditorEntryPointRoots"/>.
    /// </summary>
    /// <remarks>
    /// Used both by the code module to flag the perf-critical context of a call site and by
    /// call-graph analyzers (via <see cref="CallCrawler.TryFindRootAncestor"/>) to ask whether a
    /// suspect call is reachable from a hot path. Resolution failures are treated as "not a hot
    /// path" to avoid false positives on incomplete metadata.
    /// </remarks>
    static class PerfCriticalEntryPoints
    {
        /// <summary>
        /// Checks whether the type owns per-frame update callbacks: a <c>MonoBehaviour</c>
        /// subclass.
        /// </summary>
        /// <param name="typeReference">Type to test, or null.</param>
        public static bool IsPerfCriticalType(TypeReference typeReference)
        {
            if (typeReference == null)
            {
                return false;
            }

            return MonoBehaviourAnalysis.IsMonoBehaviour(typeReference);
        }

        /// <summary>
        /// Checks whether the method is a per-frame update callback (<c>Update</c>,
        /// <c>LateUpdate</c>, <c>FixedUpdate</c>, ...). The name match
        /// alone is not sufficient to call a method an entry point — pair it with
        /// <see cref="IsPerfCriticalType"/> on the declaring type, as <see cref="IsEntryPoint(MethodDefinition)"/> does.
        /// </summary>
        /// <param name="methodDefinition">Resolved method definition to test, or null.</param>
        public static bool IsPerfCriticalMethod(MethodDefinition methodDefinition)
        {
            if (methodDefinition == null)
            {
                return false;
            }

            return MonoBehaviourAnalysis.IsMonoBehaviourUpdateMethod(methodDefinition);
        }

        /// <summary>
        /// Checks whether the given method (resolved from a <see cref="MethodReference"/>) is a
        /// runtime hot-path entry point. Resolution failures are treated as "not an entry point".
        /// </summary>
        /// <param name="methodReference">Method reference to test, typically the caller of a
        /// suspect call site.</param>
        public static bool IsEntryPoint(MethodReference methodReference)
        {
            if (methodReference == null)
            {
                return false;
            }

            try
            {
                return IsEntryPoint(methodReference.Resolve());
            }
            catch (AssemblyResolutionException e)
            {
                Debug.LogWarningFormat("Could not resolve {0}: {1}", methodReference.FullName, e.Message);
                return false;
            }
        }

        /// <summary>
        /// Checks whether the method definition is a per-frame update callback declared on a type
        /// whose update callbacks the engine drives every frame.
        /// </summary>
        /// <param name="methodDefinition">Resolved method definition to test, or null.</param>
        public static bool IsEntryPoint(MethodDefinition methodDefinition)
        {
            if (methodDefinition == null)
            {
                return false;
            }

            return IsPerfCriticalMethod(methodDefinition)
                && IsPerfCriticalType(methodDefinition.DeclaringType);
        }
    }
}
