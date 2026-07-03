// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class RenderTargetBindingAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0165 = nameof(CDE0165);
        const string RenderTargetBindingType = "UnityEngine.Rendering.RenderTargetBinding";

        static readonly Descriptor RenderTargetBindingArrayDescriptor = new Descriptor(
            CDE0165,
            "Rendering: RenderTargetBinding Array Allocates",
            Impact.Memory,
            "The <b>RenderTargetBinding</b> array properties (<b>colorRenderTargets</b>, <b>colorLoadActions</b>, <b>colorStoreActions</b>) allocate a fresh array on every access.",
            "Cache the <b>RenderTargetBinding</b> array property during initialization and reuse the array."
        )
        {
            MessageFormat = "RenderTargetBinding.{0} allocates a managed array on each access"
        };

        static readonly HashSet<string> s_TargetProperties = new HashSet<string>
        {
            "colorRenderTargets",
            "colorLoadActions",
            "colorStoreActions"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;

            if (declaringType != RenderTargetBindingType)
                return;

            // Property getters are compiled to methods starting with "get_"
            if (!methodReference.Name.StartsWith("get_"))
                return;

            // Extract the property name by removing "get_" prefix
            var propertyName = methodReference.Name.Substring("get_".Length);

            // Check if it's one of our target properties
            if (!s_TargetProperties.Contains(propertyName))
                return;

            if (!context.IsDescriptorEnabled(RenderTargetBindingArrayDescriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, RenderTargetBindingArrayDescriptor.Id, propertyName);
            context.ReportIssue(diagnostic);
        }
    }
}
