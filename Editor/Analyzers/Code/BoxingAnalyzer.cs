using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class BoxingAnalyzer : CodeAnalyzer
    {
        internal const string CDE2000 = nameof(CDE2000);

        static readonly Descriptor Descriptor = new Descriptor
            (
            CDE2000,
            "Allocation: Value Type Boxed to Object",
            Impact.Memory,
            "Boxing converts a value type (for example an <b>int</b> or a <b>struct</b>) into an <b>object</b> reference, allocating the wrapper on the GC heap. Every boxing site produces a fresh allocation.",
            "Use a generic API or overload that takes the value type directly. For string formatting, prefer <b>string.Create</b> or <b>Span&lt;char&gt;</b>-based formatting that does not box the arguments."
            )
        {
            DefaultSeverity = Severity.Hidden,
            DocumentationUrl = "https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/types/boxing-and-unboxing",
            MessageFormat = "Conversion from value type '{0}' to ref type"
        };

        readonly OpCode[] m_OpCodes =
        {
            Mono.Cecil.Cil.OpCodes.Box
        };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var type = (TypeReference)context.Instruction.Operand;
            if (type.IsGenericParameter)
            {
                var isValueType = true; // assume it's value type
                var genericType = (GenericParameter)type;
                if (genericType.HasReferenceTypeConstraint)
                    isValueType = false;
                else
                    foreach (var constraint in genericType.Constraints)
                        if (!constraint.ConstraintType.IsValueType)
                            isValueType = false;
                if (!isValueType)
                    // boxing on ref types are no-ops, so not a problem
                    return;
            }

            var typeName = type.Name;
            if (type.FullName.Equals("System.Single"))
                typeName = "float";
            else if (type.FullName.Equals("System.Double"))
                typeName = "double";

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, Descriptor.Id, typeName);
            context.ReportIssue(diagnostic);
        }
    }
}
