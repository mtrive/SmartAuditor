// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using Mono.Cecil;
using UnityEngine;

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// Identifies methods that act as Unity Editor entry points: code paths the editor invokes
    /// frequently or on critical iteration moments such as domain reload, inspector repaint,
    /// scene drawing, asset import, or script reload.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="CallCrawler.TryFindRootAncestor"/> to drive the reachability check for
    /// CDE1007 ("Use of LINQ on editor hot paths"). Detection is based purely on attribute
    /// presence and the resolved declaring type's base chain, so it works across both editor
    /// and runtime assemblies as long as the metadata is intact.
    /// </remarks>
    static class EditorEntryPointRoots
    {
        const string k_InitializeOnLoadAttribute = "UnityEditor.InitializeOnLoadAttribute";
        const string k_InitializeOnLoadAttributeShort = "UnityEditor.InitializeOnLoad";
        const string k_InitializeOnLoadMethodAttribute = "UnityEditor.InitializeOnLoadMethodAttribute";
        const string k_InitializeOnLoadMethodAttributeShort = "UnityEditor.InitializeOnLoadMethod";
        const string k_MenuItemAttribute = "UnityEditor.MenuItem";
        const string k_MenuItemAttributeLong = "UnityEditor.MenuItemAttribute";
        const string k_DidReloadScriptsAttribute = "UnityEditor.Callbacks.DidReloadScripts";
        const string k_DidReloadScriptsAttributeLong = "UnityEditor.Callbacks.DidReloadScriptsAttribute";

        const string k_EditorWindowFullName = "UnityEditor.EditorWindow";
        const string k_EditorFullName = "UnityEditor.Editor";
        const string k_AssetPostprocessorFullName = "UnityEditor.AssetPostprocessor";

        const string k_StaticConstructorName = ".cctor";
        const string k_OnPostprocessAllAssetsMethod = "OnPostprocessAllAssets";

        static readonly string[] s_EditorGuiMethodNames =
        {
            "OnGUI",
            "OnInspectorGUI",
            "OnSceneGUI"
        };

        static readonly string[] s_AssetPostprocessorInstanceMethods =
        {
            "OnPreprocessAsset",
            "OnPreprocessAnimation",
            "OnPostprocessAnimation",
            "OnPreprocessAudio",
            "OnPostprocessAudio",
            "OnPreprocessCubemap",
            "OnPreprocessMaterialDescription",
            "OnPostprocessMaterial",
            "OnPreprocessModel",
            "OnPostprocessModel",
            "OnPostprocessGameObjectWithUserProperties",
            "OnPostprocessGameObjectWithAnimatedUserProperties",
            "OnPreprocessSpeedTree",
            "OnPostprocessSpeedTree",
            "OnPreprocessTexture",
            "OnPostprocessTexture",
            "OnAssignMaterialModel"
        };

        /// <summary>
        /// Checks whether the given method (resolved from a <see cref="MethodReference"/>) is an
        /// editor entry point. Resolution failures and unresolvable assemblies are treated as
        /// "not an entry point" to avoid false positives on incomplete metadata.
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
                var methodDefinition = methodReference.Resolve();
                return IsEntryPoint(methodDefinition);
            }
            catch (AssemblyResolutionException e)
            {
                Debug.LogWarningFormat("Could not resolve {0}: {1}", methodReference.FullName, e.Message);
                return false;
            }
        }

        /// <summary>
        /// Checks whether the given method definition is an editor entry point by inspecting
        /// attributes on the method (and its declaring type) and walking the declaring type's
        /// base chain for editor framework types.
        /// </summary>
        /// <param name="methodDefinition">Resolved method definition to test, or null.</param>
        public static bool IsEntryPoint(MethodDefinition methodDefinition)
        {
            if (methodDefinition == null)
            {
                return false;
            }

            if (HasInitializeOnLoadMethodAttribute(methodDefinition))
            {
                return true;
            }

            if (HasMenuItemAttribute(methodDefinition))
            {
                return true;
            }

            if (HasDidReloadScriptsAttribute(methodDefinition))
            {
                return true;
            }

            if (IsInitializeOnLoadStaticConstructor(methodDefinition))
            {
                return true;
            }

            if (IsEditorGuiMethod(methodDefinition))
            {
                return true;
            }

            if (IsAssetPostprocessorOverride(methodDefinition))
            {
                return true;
            }

            return false;
        }

        static bool HasInitializeOnLoadMethodAttribute(MethodDefinition methodDefinition)
        {
            if (!methodDefinition.IsStatic)
            {
                return false;
            }
            return HasAttribute(methodDefinition, k_InitializeOnLoadMethodAttribute, k_InitializeOnLoadMethodAttributeShort);
        }

        static bool HasMenuItemAttribute(MethodDefinition methodDefinition)
        {
            if (!methodDefinition.IsStatic)
            {
                return false;
            }
            return HasAttribute(methodDefinition, k_MenuItemAttribute, k_MenuItemAttributeLong);
        }

        static bool HasDidReloadScriptsAttribute(MethodDefinition methodDefinition)
        {
            if (!methodDefinition.IsStatic)
            {
                return false;
            }
            return HasAttribute(methodDefinition, k_DidReloadScriptsAttribute, k_DidReloadScriptsAttributeLong);
        }

        static bool IsInitializeOnLoadStaticConstructor(MethodDefinition methodDefinition)
        {
            if (!methodDefinition.IsStatic || !methodDefinition.IsConstructor)
            {
                return false;
            }
            if (methodDefinition.Name != k_StaticConstructorName)
            {
                return false;
            }

            var declaringType = methodDefinition.DeclaringType;
            if (declaringType == null)
            {
                return false;
            }

            return HasAttribute(declaringType, k_InitializeOnLoadAttribute, k_InitializeOnLoadAttributeShort);
        }

        static bool IsEditorGuiMethod(MethodDefinition methodDefinition)
        {
            if (methodDefinition.IsStatic || methodDefinition.Parameters.Count != 0)
            {
                return false;
            }
            if (!IsKnownName(methodDefinition.Name, s_EditorGuiMethodNames))
            {
                return false;
            }
            var declaringType = methodDefinition.DeclaringType;
            if (declaringType == null)
            {
                return false;
            }
            return MonoCecilHelper.IsOrInheritedFrom(declaringType, k_EditorWindowFullName) ||
                MonoCecilHelper.IsOrInheritedFrom(declaringType, k_EditorFullName);
        }

        static bool IsAssetPostprocessorOverride(MethodDefinition methodDefinition)
        {
            var declaringType = methodDefinition.DeclaringType;
            if (declaringType == null)
            {
                return false;
            }
            if (!MonoCecilHelper.IsOrInheritedFrom(declaringType, k_AssetPostprocessorFullName))
            {
                return false;
            }

            if (methodDefinition.IsStatic)
            {
                return methodDefinition.Name == k_OnPostprocessAllAssetsMethod;
            }
            return IsKnownName(methodDefinition.Name, s_AssetPostprocessorInstanceMethods);
        }

        static bool IsKnownName(string name, string[] candidates)
        {
            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == name)
                {
                    return true;
                }
            }
            return false;
        }

        static bool HasAttribute(ICustomAttributeProvider provider, params string[] candidateNames)
        {
            if (provider == null || !provider.HasCustomAttributes)
            {
                return false;
            }
            foreach (var attr in provider.CustomAttributes)
            {
                var fullName = attr.AttributeType.FullName;
                for (var i = 0; i < candidateNames.Length; i++)
                {
                    if (fullName == candidateNames[i])
                    {
                        return true;
                    }
                }
            }
            return false;
        }

    }
}
