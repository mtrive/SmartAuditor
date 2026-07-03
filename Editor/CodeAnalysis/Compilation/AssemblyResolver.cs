using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartAuditor.Editor.Utils;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEditor.PackageManager;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation
{
    // PrecompiledAssemblyType is a 1:1 match to PrecompiledAssemblySources (https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Compilation.CompilationPipeline.PrecompiledAssemblySources.html)
    [Flags]
    enum PrecompiledAssemblyTypes
    {
        /// <summary>
        ///   <para>Matches precompiled assemblies present in the project and packages.</para>
        /// </summary>
        UserAssembly = 1,
        /// <summary>
        ///   <para>Matches UnityEngine and runtime module assemblies.</para>
        /// </summary>
        UnityEngine = 2,
        /// <summary>
        ///   <para>Matches UnityEditor and editor module assemblies.</para>
        /// </summary>
        UnityEditor = 4,
        /// <summary>
        ///   <para>Matches assemblies supplied by the target framework.</para>
        /// </summary>
        SystemAssembly = 8,
        /// <summary>
        ///   <para>Matches all assembly sources.</para>
        /// </summary>
        All = -1 // 0xFFFFFFFF
    }

    static class AssemblyResolver
    {
        const string k_VirtualPackagesRoot = "Packages";
        const string k_UnknownAsmDefSentinel = "Unknown";
        const string k_BuiltInAsmDefSentinel = "Built-in";

        internal static IEnumerable<string> GetPrecompiledAssemblyPaths(PrecompiledAssemblyTypes flags)
        {
            var assemblyPaths = new List<string>();
            var precompiledAssemblySources = (CompilationPipeline.PrecompiledAssemblySources)flags;
            assemblyPaths.AddRange(CompilationPipeline.GetPrecompiledAssemblyPaths(precompiledAssemblySources));

            return assemblyPaths.Select(PathUtils.ReplaceSeparators);
        }

        internal static IEnumerable<string> GetPrecompiledAssemblyDirectories(PrecompiledAssemblyTypes flags)
        {
            foreach (var dir in GetPrecompiledAssemblyPaths(flags).Select(Path.GetDirectoryName).Distinct())
            {
                if (!string.IsNullOrEmpty(dir))
                    yield return dir;
            }
        }

        // Per-asmdef cache: parsing the same JSON for every report refresh would be wasteful.
        // The cache is per-domain (Unity reloads after every recompile, so it self-invalidates).
        static readonly Dictionary<string, bool> s_AsmdefIsTestByContent = new Dictionary<string, bool>();

        /// <summary>
        /// Returns true if <paramref name="assembly"/>'s asmdef explicitly opts into a test
        /// framework — either lists <c>nunit.framework.dll</c> in <c>precompiledReferences</c>,
        /// or names <c>UnityEngine.TestRunner</c> / <c>UnityEditor.TestRunner</c> in
        /// <c>references</c>. Reads the asmdef JSON directly, so it's not fooled by the
        /// auto-injected NUnit reference Unity adds to non-test asmdefs (which is what made
        /// the runtime <c>Assembly.compiledAssemblyReferences</c>-based check unreliable on
        /// projects with the test framework package installed).
        /// </summary>
        /// <remarks>
        /// Only catches asmdefs whose JSON refers to the test runner by name. References
        /// stored as <c>"GUID:xxxxxxxx"</c> are not resolved here — in practice the test-
        /// runner asmdefs are referenced by name in the cases we care about, and adding GUID
        /// resolution would require a round-trip through <c>AssetDatabase.GUIDToAssetPath</c>
        /// plus another asmdef read.
        /// </remarks>
        internal static bool IsTestAssembly(UnityEditor.Compilation.Assembly assembly)
        {
            if (assembly == null)
                return false;

            var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
            return IsTestAsmdef(asmdefPath);
        }

        /// <summary>
        /// As <see cref="IsTestAssembly"/>, but takes the asmdef path directly so callers
        /// that already resolved it (for example to use as a <c>Location</c> on an issue)
        /// can avoid the second name-to-path lookup.
        /// </summary>
        internal static bool IsTestAsmdef(string asmdefPath)
        {
            if (string.IsNullOrEmpty(asmdefPath))
                return false;

            if (s_AsmdefIsTestByContent.TryGetValue(asmdefPath, out var cached))
                return cached;

            var result = ComputeIsTestAsmdef(asmdefPath);
            s_AsmdefIsTestByContent[asmdefPath] = result;
            return result;
        }

        static bool ComputeIsTestAsmdef(string asmdefPath)
        {
            if (!File.Exists(asmdefPath))
                return false;

            string json;
            try
            {
                json = File.ReadAllText(asmdefPath);
            }
            catch
            {
                return false;
            }

            AsmdefManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<AsmdefManifest>(json);
            }
            catch
            {
                return false;
            }

            if (manifest == null)
                return false;

            if (manifest.precompiledReferences != null)
            {
                foreach (var pref in manifest.precompiledReferences)
                {
                    if (string.Equals(pref, "nunit.framework.dll", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (manifest.references != null)
            {
                foreach (var refName in manifest.references)
                {
                    if (string.IsNullOrEmpty(refName))
                        continue;
                    // GUID-form references would require an AssetDatabase round-trip to resolve;
                    // the test-runner asmdefs are typically referenced by name in misconfigured
                    // test asmdefs, so a name-only check is sufficient here.
                    if (refName.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (string.Equals(refName, "UnityEngine.TestRunner", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(refName, "UnityEditor.TestRunner", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [Serializable]
        sealed class AsmdefManifest
        {
            public string[] references;
            public string[] precompiledReferences;
        }

        internal static bool IsReadOnlyAssembly(string assemblyName)
        {
            var info = GetAssemblyInfoFromAssemblyName(assemblyName);
            return info.IsPackageReadOnly;
        }

        internal static CompiledAssemblyInfo GetAssemblyInfoFromAssemblyPath(string assemblyPath)
        {
            var info = GetAssemblyInfoFromAssemblyName(Path.GetFileNameWithoutExtension(assemblyPath));
            info.Path = assemblyPath;
            return info;
        }

        internal static CompiledAssemblyInfo GetAssemblyInfoFromAssemblyName(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                throw new ArgumentException("assemblyName must be non-empty", nameof(assemblyName));
            }

            var assemblyInfo = new CompiledAssemblyInfo
            {
                Name = assemblyName,
                RelativePath = "Assets",
                AsmDefPath = k_UnknownAsmDefSentinel,
                IsPackageReadOnly = false
            };

            if (assemblyName == CompiledAssemblyInfo.DefaultAssemblyName)
            {
                assemblyInfo.AsmDefPath = k_BuiltInAsmDefSentinel;
                return assemblyInfo;
            }

            var asmDefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
            if (asmDefPath == null)
            {
                // Can happen when loading a report from a different project.
                Debug.LogWarning($"Assembly Definition cannot be found for {assemblyName}");
                return assemblyInfo;
            }

            assemblyInfo.AsmDefPath = asmDefPath;
            var folders = PathUtils.Split(asmDefPath);
            if (folders.Length <= 2 || !folders[0].Equals(k_VirtualPackagesRoot))
            {
                // Non-package user-defined assembly.
                return assemblyInfo;
            }

            assemblyInfo.RelativePath = PathUtils.Combine(folders[0], folders[1]);
            // Fail-closed: anything under Packages/ is read-only unless PackageInfo
            // confirms it's an embedded or local package the user can edit.
            assemblyInfo.IsPackageReadOnly = true;
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(asmDefPath);
            if (packageInfo != null)
            {
                assemblyInfo.IsPackageReadOnly = packageInfo.source != PackageSource.Embedded
                                                 && packageInfo.source != PackageSource.Local;
                assemblyInfo.PackageResolvedPath = PathUtils.ReplaceSeparators(packageInfo.resolvedPath);
            }
            return assemblyInfo;
        }

        internal static string ResolveAssetPath(CompiledAssemblyInfo assemblyInfo, string path)
        {
            var fullPath = PathUtils.GetFullPath(path);
            if (!string.IsNullOrEmpty(assemblyInfo.PackageResolvedPath)
                && fullPath.StartsWith(assemblyInfo.PackageResolvedPath, StringComparison.Ordinal))
            {
                return assemblyInfo.RelativePath + fullPath.Substring(assemblyInfo.PackageResolvedPath.Length);
            }

            var projectPrefix = SmartAuditor.ProjectPath + PathUtils.Separator;
            if (fullPath.StartsWith(projectPrefix, StringComparison.Ordinal))
                return fullPath.Substring(projectPrefix.Length);

            return fullPath;
        }
    }
}
