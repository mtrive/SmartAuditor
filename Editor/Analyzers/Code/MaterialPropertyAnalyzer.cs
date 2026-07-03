// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MaterialPropertyAnalyzer : CodeAnalyzer
    {
        internal const string CDE0236 = nameof(CDE0236);

        static readonly Descriptor MaterialPropertyStringDescriptor = new Descriptor(
            CDE0236,
            "Material: Shader Property Rehashed Per Call",
            Impact.Performance,
            "String-based <b>Material</b> property accessors hash the property name on every call before performing the lookup. Repeated calls to the same property rehash the same string.",
            "Cache the property ID once with <b>Shader.PropertyToID(\"propertyName\")</b> and call the integer overloads (for example <b>Material.SetFloat(int, float)</b>) thereafter."
        )
        {
            MessageFormat = "Material.{0} hashes the property name string on each call",
            DefaultSeverity = Severity.Minor
        };

        static readonly HashSet<string> k_MaterialPropertyMethods = new HashSet<string>
        {
            // Set methods
            "SetColor", "SetColorArray",
            "SetConstantBuffer",
            "SetFloat", "SetFloatArray",
            "SetInt", "SetInteger",
            "SetMatrix", "SetMatrixArray",
            "SetTexture",
            "SetTextureOffset", "SetTextureScale",
            "SetVector", "SetVectorArray",
            // Get methods
            "GetColor", "GetColorArray",
            "GetFloat", "GetFloatArray",
            "GetInt",
            "GetMatrix", "GetMatrixArray",
            "GetTexture",
            "GetTextureOffset", "GetTextureScale",
            "GetVector", "GetVectorArray",
            // Has methods
            "HasColor",
            "HasConstantBuffer",
            "HasFloat",
            "HasInt",
            "HasMatrix",
            "HasTexture",
            "HasVector"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's a Material method
            if (methodReference.DeclaringType.FullName != "UnityEngine.Material")
                return;

            // Check if it's one of our target methods
            if (!k_MaterialPropertyMethods.Contains(methodReference.Name))
                return;

            // Check if the first parameter is a string
            if (!methodReference.Parameters.Any() || methodReference.Parameters[0].ParameterType.FullName != "System.String")
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, MaterialPropertyStringDescriptor.Id, methodReference.Name);
            context.ReportIssue(diagnostic);
        }
    }
}
