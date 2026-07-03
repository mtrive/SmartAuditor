using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class NonAllocAlternativeApiAnalyzer : CodeAnalyzer
    {
        internal const string CDE0223 = nameof(CDE0223);
        internal const string CDE0229 = nameof(CDE0229);

        static readonly Descriptor GameObjectTagDescriptor = new Descriptor(
            CDE0223,
            "GameObject.tag Allocates",
            Impact.Memory,
            "<b>GameObject.tag</b> marshals the tag back from native code and returns a fresh managed string on every access.",
            "Compare tags with <b>GameObject.CompareTag(string)</b>, which performs the comparison natively without allocating a string.")
        {
            MessageFormat = "GameObject.tag allocates a managed string on each access"
        };

        static readonly Descriptor ComponentTagDescriptor = new Descriptor(
            CDE0229,
            "Component.tag Allocates",
            Impact.Memory,
            "<b>Component.tag</b> marshals the tag back from native code and returns a fresh managed string on every access.",
            "Compare tags with <b>Component.CompareTag(string)</b>, which performs the comparison natively without allocating a string.")
        {
            MessageFormat = "Component.tag allocates a managed string on each access"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            if (methodReference.Name != "get_tag")
                return;

            var descriptor = ResolveDescriptor(methodReference.DeclaringType);
            if (descriptor == null || !context.IsDescriptorEnabled(descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id);
            context.ReportIssue(diagnostic);
        }

        static Descriptor ResolveDescriptor(TypeReference declaringType)
        {
            if (MonoCecilHelper.IsOrInheritedFrom(declaringType, "UnityEngine.GameObject"))
                return GameObjectTagDescriptor;
            if (MonoCecilHelper.IsOrInheritedFrom(declaringType, "UnityEngine.Component"))
                return ComponentTagDescriptor;

            return null;
        }
    }
}
