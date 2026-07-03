// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ClothPropertyAnalyzer : CodeAnalyzer
    {
        internal const string CDE0148 = nameof(CDE0148);

        static readonly Descriptor ClothArrayPropertyDescriptor = new Descriptor(
            CDE0148,
            "Cloth: Array Properties Allocate",
            Impact.Memory,
            "<b>Cloth</b> array properties (<b>vertices</b>, <b>normals</b>, <b>coefficients</b>, <b>capsuleColliders</b>, <b>sphereColliders</b>) allocate a fresh managed array on every access.",
            "Cache the Cloth array property during initialization and reuse the array between frames."
        )
        {
            MessageFormat = "Cloth.{0} allocates a managed array on each access"
        };

        static readonly HashSet<string> s_Properties = new HashSet<string>
        {
            "vertices",
            "normals",
            "coefficients",
            "capsuleColliders",
            "sphereColliders"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's UnityEngine.Cloth type
            if (methodReference.DeclaringType.FullName != "UnityEngine.Cloth")
                return;

            // Property getters are compiled to methods starting with "get_"
            if (!methodReference.Name.StartsWith("get_"))
                return;

            // Extract the property name by removing "get_" prefix
            var propertyName = methodReference.Name.Substring("get_".Length);

            // Check if it's one of our target properties
            if (!s_Properties.Contains(propertyName))
                return;

            if (!context.IsDescriptorEnabled(ClothArrayPropertyDescriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, ClothArrayPropertyDescriptor.Id, propertyName);
            context.ReportIssue(diagnostic);
        }
    }
}

