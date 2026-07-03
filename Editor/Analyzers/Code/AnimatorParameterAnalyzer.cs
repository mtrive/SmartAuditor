// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AnimatorParameterAnalyzer : CodeAnalyzer
    {
        internal const string CDE0235 = nameof(CDE0235);

        static readonly Descriptor AnimatorStringDescriptor = new Descriptor(
            CDE0235,
            "Animator: Parameter Name Rehashed Per Call",
            Impact.Performance,
            "String-based <b>Animator</b> parameter accessors hash the parameter name on every call before performing the lookup. Repeated calls to the same parameter rehash the same string.",
            "Cache the parameter hash once with <b>Animator.StringToHash(\"parameterName\")</b> and call the integer overloads (for example <b>Animator.SetFloat(int, float)</b>) thereafter."
        )
        {
            MessageFormat = "Animator.{0} hashes the parameter name string on each call",
            DefaultSeverity = Severity.Minor
        };

        static readonly HashSet<string> k_AnimatorParameterMethods = new HashSet<string>
        {
            "SetFloat",
            "SetBool",
            "SetInteger",
            "SetTrigger",
            "ResetTrigger",
            "GetFloat",
            "GetBool",
            "GetInteger",
            "IsParameterControlledByCurve",
            "GetParameter"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's an Animator method
            if (methodReference.DeclaringType.FullName != "UnityEngine.Animator")
                return;

            // Check if it's one of our target methods
            if (!k_AnimatorParameterMethods.Contains(methodReference.Name))
                return;

            // Check if the first parameter is a string
            if (!methodReference.Parameters.Any() ||
                methodReference.Parameters[0].ParameterType.FullName != "System.String")
                return;

            var diagnostic = Diagnostic.Create(
                AnalysisCategory.Code,
                AnimatorStringDescriptor.Id,
                methodReference.Name);
            context.ReportIssue(diagnostic);
        }
    }
}
