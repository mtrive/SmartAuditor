// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.
// SHD0008

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;
using UnityEditor.Compilation;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Flags shaders that declare a keyword via <c>#pragma shader_feature*</c> when the same keyword
    /// is toggled at runtime by C# code (<c>Material.EnableKeyword</c>, <c>Shader.EnableKeyword</c>, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>shader_feature</c> compiles only the variants whose keyword state is baked into a material
    /// in the build. When the keyword is set at runtime via <c>EnableKeyword</c>, the runtime ON state
    /// might not match any compiled variant, and the renderer falls back to the OFF variant (or
    /// rendering breaks). The correct directive for runtime-toggled keywords is <c>multi_compile</c>,
    /// which compiles every combination regardless of material state.
    /// </para>
    /// <para>
    /// The analyzer scans every project Player assembly via Mono.Cecil once per run, identifies
    /// <c>Ldstr</c> + keyword-toggle call patterns, then cross-references the resulting keyword set
    /// against each shader's <c>shader_feature</c> declarations.
    /// </para>
    /// </remarks>
    sealed class ShaderFeatureRuntimeToggleAnalyzer : ShaderAnalyzer
    {
        internal const string SHD0008 = nameof(SHD0008);

        internal static readonly Descriptor RuntimeToggleDescriptor = new Descriptor(
            SHD0008,
            "Shader: shader_feature Keyword Toggled at Runtime",
            Impact.Correctness | Impact.BuildSize,
            "The shader declares a keyword via <b>#pragma shader_feature</b> but the same keyword is set from C# code via <b>Material.EnableKeyword</b>, <b>Shader.EnableKeyword</b>, or a related runtime API. <b>shader_feature</b> only compiles variants whose keyword state is enabled on a material at build time; the runtime-only variant is never compiled, and the renderer either falls back to the wrong variant or skips it.",
            "Replace <b>#pragma shader_feature</b> with <b>#pragma multi_compile</b> for keywords that are toggled at runtime (<b>multi_compile_local</b> if the keyword is per-material rather than global). Use <b>shader_feature</b> only for keywords baked into materials in the Editor."
        )
        {
            MessageFormat = "Shader '{0}' declares '{1}' as shader_feature but C# toggles it at runtime ({2})"
        };

        // Regex matches `#pragma shader_feature[_local][_fragment|_vertex|_compute|_keyword]` directives,
        // capturing the keyword list. The trailing `\s+` ensures we don't pick up multi_compile lines.
        static readonly Regex s_ShaderFeatureRegex = new Regex(
            @"^\s*#\s*pragma\s+shader_feature(?:_local)?(?:_fragment|_vertex|_compute|_keyword)?\s+(.+)$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        static readonly char[] s_KeywordSeparators = { ' ', '\t' };

        // Methods whose first string argument is a shader keyword. Format: "TypeFullName::MethodName".
        // Mono.Cecil's MethodReference uses :: as the separator in DeclaringType/Name combinations.
        static readonly HashSet<string> s_KeywordSetterMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "UnityEngine.Material::EnableKeyword",
            "UnityEngine.Material::DisableKeyword",
            "UnityEngine.Material::SetKeyword",
            "UnityEngine.Shader::EnableKeyword",
            "UnityEngine.Shader::DisableKeyword",
            "UnityEngine.Shader::SetKeyword",
            "UnityEngine.MaterialPropertyBlock::EnableKeyword",
            "UnityEngine.MaterialPropertyBlock::DisableKeyword",
            "UnityEngine.MaterialPropertyBlock::SetKeyword",
            "UnityEngine.Rendering.CommandBuffer::EnableShaderKeyword",
            "UnityEngine.Rendering.CommandBuffer::DisableShaderKeyword",
            "UnityEngine.Rendering.CommandBuffer::EnableKeyword",
            "UnityEngine.Rendering.CommandBuffer::DisableKeyword",
        };

        // Instance-level cache populated on first AnalyzeShader call. Reset in Initialize so each
        // analysis run sees a fresh scan.
        Dictionary<string, List<string>> m_RuntimeToggledKeywords;

        public override void Initialize()
        {
            m_RuntimeToggledKeywords = null;
        }

        public override void AnalyzeShader(ShaderAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(RuntimeToggleDescriptor, context.AssetPath))
            {
                return;
            }

            var shader = context.Shader;
            if (!shader)
            {
                return;
            }

            var source = context.StrippedSourceCode;
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            EnsureRuntimeKeywordIndex();

            if (m_RuntimeToggledKeywords.Count == 0)
            {
                return;
            }

            foreach (Match match in s_ShaderFeatureRegex.Matches(source))
            {
                var keywords = match.Groups[1].Value.Trim()
                    .Split(s_KeywordSeparators, StringSplitOptions.RemoveEmptyEntries);

                foreach (var keyword in keywords)
                {
                    if (keyword == "_")
                    {
                        // Sentinel for the OFF arm; never a runtime keyword.
                        continue;
                    }

                    if (!m_RuntimeToggledKeywords.TryGetValue(keyword, out var callSites))
                    {
                        continue;
                    }

                    var sampleCallSite = callSites[0];
                    var diagnostic = Diagnostic.Create(
                            AnalysisCategory.AssetIssue,
                            RuntimeToggleDescriptor.Id,
                            shader.name,
                            keyword,
                            sampleCallSite)
                        .WithLocation(new Location(context.AssetPath))
                        .WithEvidence("Keyword", keyword)
                        .WithEvidence("CallSites", callSites.ToArray());
                    context.ReportIssue(diagnostic);
                }
            }
        }

        void EnsureRuntimeKeywordIndex()
        {
            if (m_RuntimeToggledKeywords != null)
            {
                return;
            }

            m_RuntimeToggledKeywords = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            foreach (var assembly in assemblies)
            {
                if (string.IsNullOrEmpty(assembly.outputPath) || !File.Exists(assembly.outputPath))
                {
                    continue;
                }

                try
                {
                    using (var def = AssemblyDefinition.ReadAssembly(assembly.outputPath))
                    {
                        ScanAssembly(def);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SmartAuditor] SHD0008: failed to scan assembly '{assembly.name}' for runtime shader keywords: {e.Message}");
                }
            }
        }

        void ScanAssembly(AssemblyDefinition assemblyDef)
        {
            foreach (var module in assemblyDef.Modules)
            {
                foreach (var type in module.Types)
                {
                    ScanType(type, assemblyDef.Name.Name);
                }
            }
        }

        void ScanType(TypeDefinition type, string assemblyName)
        {
            foreach (var nested in type.NestedTypes)
            {
                ScanType(nested, assemblyName);
            }

            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }
                ScanMethod(method, type, assemblyName);
            }
        }

        void ScanMethod(MethodDefinition method, TypeDefinition type, string assemblyName)
        {
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)
                {
                    continue;
                }

                if (!(instruction.Operand is MethodReference methodRef))
                {
                    continue;
                }

                var key = $"{methodRef.DeclaringType.FullName}::{methodRef.Name}";
                if (!s_KeywordSetterMethods.Contains(key))
                {
                    continue;
                }

                var keyword = ResolveFirstStringArgument(instruction);
                if (string.IsNullOrEmpty(keyword))
                {
                    continue;
                }

                var callSite = $"{type.FullName}.{method.Name} ({assemblyName}.dll)";
                if (!m_RuntimeToggledKeywords.TryGetValue(keyword, out var sites))
                {
                    sites = new List<string>();
                    m_RuntimeToggledKeywords[keyword] = sites;
                }
                // De-duplicate identical sites — the same method can have the same EnableKeyword call inside a loop.
                if (sites.Count == 0 || sites[sites.Count - 1] != callSite)
                {
                    sites.Add(callSite);
                }
            }
        }

        // Walks back from a call instruction looking for the Ldstr that supplied the keyword
        // string. The keyword is always the first argument of every method in s_KeywordSetterMethods,
        // but for overloads like Material::SetKeyword(string, bool) intermediate args (the bool) are
        // pushed between the Ldstr and the call. Bound the walk so we don't accidentally pick up an
        // unrelated Ldstr from earlier in the method body.
        internal static string ResolveFirstStringArgument(Instruction callInstruction)
        {
            var cursor = callInstruction.Previous;
            int budget = 8;
            while (cursor != null && budget-- > 0)
            {
                if (cursor.OpCode == OpCodes.Ldstr)
                {
                    return cursor.Operand as string;
                }
                cursor = cursor.Previous;
            }
            return null;
        }
    }
}
