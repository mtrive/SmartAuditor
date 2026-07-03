using System;
using System.Linq;
using Mono.Cecil;
using UnityEngine;

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// Classifies <c>MonoBehaviour</c> types and Unity message methods from Mono.Cecil metadata.
    /// </summary>
    /// <remarks>
    /// Two method name lists serve different consumers:
    /// <list type="bullet">
    /// <item><description><see cref="IsMonoBehaviourEvent"/> matches
    /// <see cref="k_ReflectionDispatchedMessageNames"/> — lifecycle messages Unity discovers via
    /// reflection and invokes even when empty (used by empty-message analysis).</description></item>
    /// <item><description><see cref="IsMonoBehaviourUpdateMethod"/> matches
    /// <see cref="k_PerFrameEntryPointMethodNames"/> — per-frame or per-render callbacks that
    /// anchor runtime hot-path call-graph analysis (see <see cref="PerfCriticalEntryPoints"/>).
    /// </description></item>
    /// </list>
    /// The lists overlap partially but are not identical; a method can be a hot-path entry point
    /// without being flagged as a reflection-dispatched empty message, and vice versa.
    /// </remarks>
    static class MonoBehaviourAnalysis
    {
        static readonly string k_CoreModuleName = "UnityEngine.CoreModule.dll";
        static readonly string k_MonoBehaviourFullName = "UnityEngine.MonoBehaviour";
        static readonly string k_ILPostProcessorFullName = "Unity.CompilationPipeline.Common.ILPostProcessing.ILPostProcessor";

        static readonly int k_CoreModuleHashCode = k_CoreModuleName.GetHashCode();
        static readonly int k_MonoBehaviourHashCode = k_MonoBehaviourFullName.GetHashCode();
        static readonly int k_ILPostProcessorHashCode = k_ILPostProcessorFullName.GetHashCode();

        /// <summary>
        /// Unity message names that are reflection-dispatched on <c>MonoBehaviour</c> instances.
        /// Used to detect empty handlers that still incur discovery and invocation cost.
        /// </summary>
        static readonly string[] k_ReflectionDispatchedMessageNames =
        {
            "Awake", "Start", "OnEnable", "OnDisable", "Update", "LateUpdate", "FixedUpdate",
            "OnBecameVisible", "OnBecameInvisible"
        };

        /// <summary>
        /// Per-frame or per-render <c>MonoBehaviour</c> callbacks used as runtime hot-path roots.
        /// </summary>
        static readonly string[] k_PerFrameEntryPointMethodNames =
        {
            "Update", "LateUpdate", "FixedUpdate", "OnAnimatorIK", "OnAnimatorMove",
            "OnWillRenderObject", "OnRenderObject"
        };

        /// <summary>
        /// Checks whether the type derives from <c>UnityEngine.MonoBehaviour</c>.
        /// </summary>
        /// <param name="typeReference">Type to test, or null.</param>
        /// <returns><c>true</c> when the type is or inherits from <c>MonoBehaviour</c>; otherwise
        /// <c>false</c>. Resolution failures are treated as not a <c>MonoBehaviour</c>.</returns>
        public static bool IsMonoBehaviour(TypeReference typeReference)
        {
            if (typeReference == null)
            {
                return false;
            }

            // IL post-processor types cannot be resolved during compilation pipeline analysis.
            if (typeReference.FullName.GetHashCode() == k_ILPostProcessorHashCode
                && typeReference.FullName == k_ILPostProcessorFullName)
            {
                return false;
            }

            try
            {
                var typeDefinition = typeReference.Resolve();

                if (typeDefinition == null)
                {
                    // temporary fix to handle case where the assembly is found but not the type
                    Debug.LogWarning(typeReference.FullName + " could not be resolved.");
                    return false;
                }

                if (typeDefinition.FullName.GetHashCode() == k_MonoBehaviourHashCode
                    && typeDefinition.FullName == k_MonoBehaviourFullName
                    && typeDefinition.Module.Name.GetHashCode() == k_CoreModuleHashCode
                    && typeDefinition.Module.Name == k_CoreModuleName)
                {
                    return true;
                }

                if (typeDefinition.BaseType != null)
                {
                    return IsMonoBehaviour(typeDefinition.BaseType);
                }
            }
            catch (AssemblyResolutionException e)
            {
                Debug.LogWarningFormat("Could not resolve {0}: {1}", typeReference.Name, e.Message);
            }

            return false;
        }

        /// <summary>
        /// Checks whether the method name matches a reflection-dispatched Unity message on
        /// <c>MonoBehaviour</c> (see <see cref="k_ReflectionDispatchedMessageNames"/>).
        /// </summary>
        /// <param name="methodDefinition">Resolved method definition to test.</param>
        public static bool IsMonoBehaviourEvent(MethodDefinition methodDefinition)
        {
            return k_ReflectionDispatchedMessageNames.Contains(methodDefinition.Name);
        }

        /// <summary>
        /// Checks whether the method name matches a per-frame or per-render hot-path entry point
        /// (see <see cref="k_PerFrameEntryPointMethodNames"/>).
        /// </summary>
        /// <param name="methodDefinition">Resolved method definition to test.</param>
        public static bool IsMonoBehaviourUpdateMethod(MethodDefinition methodDefinition)
        {
            return k_PerFrameEntryPointMethodNames.Contains(methodDefinition.Name);
        }
    }
}
