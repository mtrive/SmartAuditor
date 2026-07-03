// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes code for usage of System.String.Concat that allocates managed memory.
    /// </summary>
    sealed class SystemStringConcatAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE1002 = nameof(CDE1002);

        static readonly Descriptor ConcatDescriptor = new Descriptor(
            CDE1002,
            "Allocation: String.Concat Allocates",
            Impact.Memory,
            "<b>String.Concat</b> (and the <b>+</b> operator on strings) allocates a fresh managed string on every call. Repeated concatenation in a loop produces O(n^2) garbage.",
            "Build the result with <b>System.Text.StringBuilder</b> when concatenation runs in a loop. For known small parts, use <b>string.Concat(ReadOnlySpan&lt;char&gt;...)</b> or interpolation with a cached <b>StringBuilder</b>."
        )
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "String.Concat allocates a new managed string"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's System.String type
            if (methodReference.DeclaringType.FullName != "System.String")
                return;

            // Check if it's Concat method
            if (methodReference.Name != "Concat")
                return;

            if (!context.IsDescriptorEnabled(ConcatDescriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, ConcatDescriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}
