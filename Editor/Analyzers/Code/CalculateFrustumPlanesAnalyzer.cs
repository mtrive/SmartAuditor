// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class CalculateFrustumPlanesAnalyzer : CodeAnalyzer
    {
        internal const string CDE0003 = nameof(CDE0003);

        static readonly Descriptor GeometryUtilityAllocatingDescriptor = new Descriptor(
            CDE0003,
            "Allocation: GeometryUtility.CalculateFrustumPlanes Allocates",
            Impact.Memory,
            "<b>GeometryUtility.CalculateFrustumPlanes(Camera)</b> and <b>GeometryUtility.CalculateFrustumPlanes(Matrix4x4)</b> allocate a fresh <b>Plane[]</b> of length 6 on every call.",
            "Call the non-allocating overload <b>GeometryUtility.CalculateFrustumPlanes(Matrix4x4, Plane[])</b> with a reusable six-element <b>Plane[]</b>."
        )
        {
            MessageFormat = "GeometryUtility.CalculateFrustumPlanes allocates a Plane[] on each call"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };
        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(GeometryUtilityAllocatingDescriptor))
                return;

            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;
            var methodName = methodReference.Name;
            var parameters = methodReference.Parameters;

            // Only check GeometryUtility.CalculateFrustumPlanes
            if (declaringType != "UnityEngine.GeometryUtility" || methodName != "CalculateFrustumPlanes")
                return;

            // Check if this is an allocating signature (no Plane[] parameter)
            if (parameters.Count == 2)  // The non-allocating version has 2 parameters
            {
                var param2Type = parameters[1].ParameterType.FullName;
                if (param2Type == "UnityEngine.Plane[]")  // Check if the second parameter is Plane[]
                    return;
            }

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, GeometryUtilityAllocatingDescriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}
