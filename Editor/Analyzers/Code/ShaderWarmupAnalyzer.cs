// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ShaderWarmupAnalyzer : CodeAnalyzer
    {
        internal const string CDE0227 = nameof(CDE0227);
        internal const string CDE0228 = nameof(CDE0228);

        static readonly Descriptor WarmupAllShadersDescriptor = new Descriptor(
            CDE0227,
            "Shader: WarmupAllShaders Is Ineffective on Modern APIs",
            Impact.Performance,
            "<b>Shader.WarmupAllShaders()</b> does not actually pre-warm shaders on Metal, Vulkan, or DX12. Those backends defer pipeline-state compilation until first draw, so the method returns without producing the GPU work it claims to.",
            "Pre-warm via <b>UnityEngine.Experimental.Rendering.ShaderWarmup.WarmupShader</b>, issuing a small offscreen draw for every shader / vertex-layout combination used at runtime."
        )
        {
            MessageFormat = "Shader.WarmupAllShaders does not work on Metal, Vulkan, or DX12",
            DefaultSeverity = Severity.Major
        };

        static readonly Descriptor ShaderVariantCollectionWarmUpDescriptor = new Descriptor(
            CDE0228,
            "Shader: ShaderVariantCollection.WarmUp Is Ineffective on Modern APIs",
            Impact.Performance,
            "<b>ShaderVariantCollection.WarmUp()</b> does not actually pre-warm shader variants on Metal, Vulkan, or DX12. Those backends defer pipeline-state compilation until first draw, so the method returns without producing the GPU work it claims to.",
            "Pre-warm via <b>UnityEngine.Experimental.Rendering.ShaderWarmup.WarmupShader</b>, issuing a small offscreen draw for every shader / variant / vertex-layout combination used at runtime."
        )
        {
            MessageFormat = "ShaderVariantCollection.WarmUp does not work on Metal, Vulkan, or DX12",
            DefaultSeverity = Severity.Major
        };

        static readonly Dictionary<string, Descriptor> s_MethodToDescriptor = new Dictionary<string, Descriptor>
        {
            { "UnityEngine.Shader.WarmupAllShaders", WarmupAllShadersDescriptor },
            { "UnityEngine.ShaderVariantCollection.WarmUp", ShaderVariantCollectionWarmUpDescriptor }
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var key = methodReference.DeclaringType.FullName + "." + methodReference.Name;
            if (!s_MethodToDescriptor.TryGetValue(key, out var descriptor))
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}
