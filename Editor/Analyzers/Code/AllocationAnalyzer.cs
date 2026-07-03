using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AllocationAnalyzer : CodeAnalyzer
    {
        internal const string CDE2002 = nameof(CDE2002);
        internal const string CDE2003 = nameof(CDE2003);
        internal const string CDE2004 = nameof(CDE2004);
        internal const string CDE2005 = nameof(CDE2005);

        static readonly Descriptor ObjectAllocationDescriptor = new Descriptor
            (
            CDE2002,
            "Allocation: Reference-Type Allocation",
            Impact.Memory,
            "A managed reference-type instance is allocated on the GC heap. In frequently-updated code each allocation feeds GC pressure and risks visible collection spikes.",
            "Hoist the allocation out of the hot path, or pool the instance and reuse it."
            )
        {
            DefaultSeverity = Severity.Hidden, // This will be promoted to Minor if in a hot path
            MessageFormat = "'{0}' allocation",
        };

        static readonly Descriptor ClosureAllocationDescriptor = new Descriptor
            (
            CDE2003,
            "Allocation: Closure Captures Local State",
            Impact.Memory,
            "An inline delegate or lambda captures an enclosing variable, forcing the compiler to allocate a closure object on the GC heap to hold the captured state.",
            "Capture nothing, or refactor to a static lambda. Where state must be passed, use the <b>Action&lt;T, ...&gt;</b> / <b>Func&lt;T, ...&gt;</b> overloads that take a state argument, or move the work into an explicit class with the state as fields."
            )
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "Closure allocation in '{0}.{1}'",
        };

        static readonly Descriptor ArrayAllocationDescriptor = new Descriptor
            (
            CDE2004,
            "Allocation: Managed Array Allocation",
            Impact.Memory,
            "A managed array is allocated on the GC heap. In frequently-updated code each allocation feeds GC pressure and risks visible collection spikes.",
            "Allocate the array once and reuse it, or use a pooled <b>List&lt;T&gt;</b> / <b>ArrayPool&lt;T&gt;</b>."
            )
        {
            DefaultSeverity = Severity.Hidden, // This will be promoted to Minor if in a hot path
            MessageFormat = "'{0}' array allocation",
        };

        static readonly Descriptor ParamArrayAllocationDescriptor = new Descriptor
            (
            CDE2005,
            "Allocation: params Array Allocation",
            Impact.Memory,
            "The compiler allocates a fresh array at every call site that uses the variadic <b>params</b> argument, even when the array contents are constant.",
            "Call the explicit-array overload with a cached array, or use overloads that accept a fixed number of typed parameters."
            )
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "Parameters array '{0} {1}' allocation"
        };

        static readonly int k_ParamArrayAtributeHashCode = "System.ParamArrayAttribute".GetHashCode();

        readonly OpCode[] m_OpCodes =
        {
            Mono.Cecil.Cil.OpCodes.Call,
            Mono.Cecil.Cil.OpCodes.Callvirt,
            Mono.Cecil.Cil.OpCodes.Newobj,
            Mono.Cecil.Cil.OpCodes.Newarr
        };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            if (context.Instruction.OpCode == Mono.Cecil.Cil.OpCodes.Call || context.Instruction.OpCode == Mono.Cecil.Cil.OpCodes.Callvirt)
            {
                if (!context.IsDescriptorEnabled(ParamArrayAllocationDescriptor))
                    return;

                var callee = (MethodReference)context.Instruction.Operand;
                if (callee.HasParameters)
                {
                    var lastParam = callee.Parameters.Last();
                    if (lastParam.HasCustomAttributes && lastParam.CustomAttributes.Any(a => a.AttributeType.FastFullName().GetHashCode() == k_ParamArrayAtributeHashCode))
                    {
                        var diagnostic = Diagnostic.Create(AnalysisCategory.Code, ParamArrayAllocationDescriptor.Id, lastParam.ParameterType.Name, lastParam.Name);
                        context.ReportIssue(diagnostic);
                    }
                }
                return;
            }

            if (context.Instruction.OpCode == Mono.Cecil.Cil.OpCodes.Newobj)
            {
                var methodReference = (MethodReference)context.Instruction.Operand;
                var typeReference = methodReference.DeclaringType;
                if (typeReference.IsValueType)
                    return;

                if (IsExceptionType(typeReference))
                    return;

                var isClosure = typeReference.Name.StartsWith("<>c__DisplayClass", StringComparison.Ordinal);
                if (isClosure)
                {
                    if (!context.IsDescriptorEnabled(ClosureAllocationDescriptor))
                        return;
                    var diagnostic = Diagnostic.Create(AnalysisCategory.Code, ClosureAllocationDescriptor.Id, context.MethodDefinition.DeclaringType.Name, context.MethodDefinition.Name);
                    context.ReportIssue(diagnostic);
                    return;
                }
                if (!context.IsDescriptorEnabled(ObjectAllocationDescriptor))
                    return;
                var objectDiagnostic = Diagnostic.Create(AnalysisCategory.Code, ObjectAllocationDescriptor.Id, typeReference.FastFullName());
                context.ReportIssue(objectDiagnostic);
            }
            else // OpCodes.Newarr
            {
                if (!context.IsDescriptorEnabled(ArrayAllocationDescriptor))
                    return;

                var typeReference = (TypeReference)context.Instruction.Operand;

                var diagnostic = Diagnostic.Create(AnalysisCategory.Code, ArrayAllocationDescriptor.Id, typeReference.Name);
                context.ReportIssue(diagnostic);
            }
        }

        bool IsExceptionType(TypeReference type)
        {
            return MonoCecilHelper.IsOrInheritedFrom(type, "System.Exception");
        }
    }
}
