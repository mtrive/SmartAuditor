// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes code for usage of Unity LightProbes and LightProbeGroup properties that allocate managed memory.
    /// </summary>
    sealed class LightProbesAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0159 = nameof(CDE0159);

        static readonly Descriptor LightProbesPropertyDescriptor = new Descriptor(
            CDE0159,
            "Light Probes: Array Properties Allocate",
            Impact.Memory,
            "Light-probe array properties (<b>LightProbes.positions</b>, <b>LightProbes.bakedProbes</b>, <b>LightProbeGroup.probePositions</b>) return a fresh copy of the underlying data on every access.",
            "Cache the property result during initialization and reuse the array."
        )
        {
            MessageFormat = "{0} allocates a managed array on each access"
        };

        static readonly HashSet<(string DeclaringType, string PropertyName)> s_TargetProperties = new HashSet<(string DeclaringType, string PropertyName)>
        {
            ("UnityEngine.LightProbes", "positions"),
            ("UnityEngine.LightProbes", "bakedProbes"),
            ("UnityEngine.LightProbeGroup", "probePositions")
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;

            // Property getters are compiled to methods starting with "get_"
            if (!methodReference.Name.StartsWith("get_"))
                return;

            // Extract the property name by removing "get_" prefix
            var propertyName = methodReference.Name.Substring("get_".Length);

            if (!s_TargetProperties.Contains((declaringType, propertyName)))
                return;

            if (!context.IsDescriptorEnabled(LightProbesPropertyDescriptor))
                return;

            var typeName = methodReference.DeclaringType.Name;
            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, LightProbesPropertyDescriptor.Id, $"{typeName}.{propertyName}");
            context.ReportIssue(diagnostic);
        }
    }
}
