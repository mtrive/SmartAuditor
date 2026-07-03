// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class PInvokeAnalyzer : CodeAnalyzer
    {
        internal const string CDE2006 = nameof(CDE2006);

        static readonly Descriptor PInvokeIssueDescriptor = new Descriptor
        (
            CDE2006,
            "Interop: P/Invoke Call",
            Impact.Performance | Impact.Memory,
            "<b>P/Invoke</b> calls cross from managed to native code, incurring marshalling cost for each argument and pinning any reference-type buffers passed across the boundary.",
            "Move <b>P/Invoke</b> calls off frequently-updated code. Where the call is unavoidable, batch the work and prefer <b>blittable</b> parameter types to minimise marshalling."
        )
        {
            MessageFormat = "'{0}' is a P/Invoke call",
            DefaultSeverity = Severity.Minor
        };

        public override IReadOnlyCollection<OpCode> OpCodes => new[] { Mono.Cecil.Cil.OpCodes.Call };

        public override void Analyze(InstructionAnalysisContext context)
        {
            var callee = context.Instruction.Operand as MethodReference;
            if (!IsPInvoke(callee))
                return;
            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, PInvokeIssueDescriptor.Id, callee.Name);
            context.ReportIssue(diagnostic);
        }

        bool IsPInvoke(MethodReference methodReference)
        {
            try
            {
                var methodDefinition = methodReference.Resolve();

                if (methodDefinition != null && methodDefinition.HasPInvokeInfo)
                    return true;
            }
            catch (AssemblyResolutionException e)
            {
                Debug.LogWarningFormat("Could not resolve {0}: {1}", methodReference.Name, e.Message);
            }
            return false;
        }
    }
}
