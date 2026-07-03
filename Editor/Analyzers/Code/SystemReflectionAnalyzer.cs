// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes code for usage of System.Reflection which is slow and not generally
    /// considered performant enough for runtime code.
    /// </summary>
    sealed class SystemReflectionAnalyzer : CodeAnalyzer
    {
        internal const string CDE1001 = nameof(CDE1001);

        static readonly Descriptor ReflectionDescriptor = new Descriptor(
            CDE1001,
            "Reflection: System.Reflection Call",
            Impact.Performance,
            "<b>System.Reflection</b> calls perform a metadata lookup and invoke through a dispatch path that the JIT cannot inline. Repeated reflection in runtime code is orders of magnitude slower than a direct call.",
            "Resolve <b>MemberInfo</b> / <b>MethodInfo</b> handles once during initialization and convert to cached delegates via <b>Delegate.CreateDelegate</b> or <b>System.Linq.Expressions</b>. Replace runtime reflection with direct calls where possible."
        )
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "'{0}' uses Reflection"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType;

            // Check if it's in the System.Reflection namespace or sub-namespaces
            if (declaringType.Namespace == null || !declaringType.Namespace.StartsWith("System.Reflection"))
                return;

            if (!context.IsDescriptorEnabled(ReflectionDescriptor))
                return;

            var description = $"{declaringType.FullName}.{methodReference.Name}";
            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, ReflectionDescriptor.Id, description);
            context.ReportIssue(diagnostic);
        }
    }
}
