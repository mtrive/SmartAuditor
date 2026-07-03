using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class GetComponentsAnalyzer : CodeAnalyzer
    {
        internal const string CDE0026 = nameof(CDE0026);
        internal const string CDE0027 = nameof(CDE0027);
        internal const string CDE0028 = nameof(CDE0028);
        internal const string CDE0029 = nameof(CDE0029);

        static readonly Descriptor ComponentGetChildrenDescriptor = new Descriptor(
            CDE0026,
            "Component: GetComponentsInChildren Allocates",
            Impact.Memory,
            "<b>Component.GetComponentsInChildren()</b> allocates a fresh component array on every call.",
            "Call the <b>Component.GetComponentsInChildren&lt;T&gt;(List&lt;T&gt;)</b> overload with a reusable list."
        )
        {
            MessageFormat = "Component.GetComponentsInChildren allocates a managed array"
        };

        static readonly Descriptor ComponentGetParentDescriptor = new Descriptor(
            CDE0027,
            "Component: GetComponentsInParent Allocates",
            Impact.Memory,
            "<b>Component.GetComponentsInParent()</b> allocates a fresh component array on every call.",
            "Call the <b>Component.GetComponentsInParent&lt;T&gt;(List&lt;T&gt;)</b> overload with a reusable list."
        )
        {
            MessageFormat = "Component.GetComponentsInParent allocates a managed array"
        };

        static readonly Descriptor GameObjectGetChildrenDescriptor = new Descriptor(
            CDE0028,
            "GameObject: GetComponentsInChildren Allocates",
            Impact.Memory,
            "<b>GameObject.GetComponentsInChildren()</b> allocates a fresh component array on every call.",
            "Call the <b>GameObject.GetComponentsInChildren&lt;T&gt;(List&lt;T&gt;)</b> overload with a reusable list."
        )
        {
            MessageFormat = "GameObject.GetComponentsInChildren allocates a managed array"
        };

        static readonly Descriptor GameObjectGetParentDescriptor = new Descriptor(
            CDE0029,
            "GameObject: GetComponentsInParent Allocates",
            Impact.Memory,
            "<b>GameObject.GetComponentsInParent()</b> allocates a fresh component array on every call.",
            "Call the <b>GameObject.GetComponentsInParent&lt;T&gt;(List&lt;T&gt;)</b> overload with a reusable list."
        )
        {
            MessageFormat = "GameObject.GetComponentsInParent allocates a managed array"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };
        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;
            var methodName = methodReference.Name;
            var parameters = methodReference.Parameters;

            // Check if this is an allocating signature
            bool isAllocating = false;

            if (parameters.Count == 0) // No parameters version
            {
                isAllocating = true;
            }
            else if (parameters.Count == 1)
            {
                var paramType = parameters[0].ParameterType.FullName;
                isAllocating = paramType == "System.Boolean" || // bool parameter
                             paramType == "System.Type";        // Type parameter
            }
            else if (parameters.Count == 2)
            {
                var param1Type = parameters[0].ParameterType.FullName;
                var param2Type = parameters[1].ParameterType.FullName;
                isAllocating = param1Type == "System.Type" && param2Type == "System.Boolean"; // Type, bool parameters
            }

            if (!isAllocating)
                return;

            // Check Component methods
            if (declaringType == "UnityEngine.Component")
            {
                if (methodName == "GetComponentsInChildren")
                {
                    if (!context.IsDescriptorEnabled(ComponentGetChildrenDescriptor))
                        return;
                    var diagnostic = Diagnostic.Create(AnalysisCategory.Code, ComponentGetChildrenDescriptor.Id);
                    context.ReportIssue(diagnostic);
                    return;
                }
                if (methodName == "GetComponentsInParent")
                {
                    if (!context.IsDescriptorEnabled(ComponentGetParentDescriptor))
                        return;
                    var diagnostic = Diagnostic.Create(AnalysisCategory.Code, ComponentGetParentDescriptor.Id);
                    context.ReportIssue(diagnostic);
                    return;
                }
            }
            // Check GameObject methods
            else if (declaringType == "UnityEngine.GameObject")
            {
                if (methodName == "GetComponentsInChildren")
                {
                    if (!context.IsDescriptorEnabled(GameObjectGetChildrenDescriptor))
                        return;
                    var diagnostic = Diagnostic.Create(AnalysisCategory.Code, GameObjectGetChildrenDescriptor.Id);
                    context.ReportIssue(diagnostic);
                    return;
                }
                if (methodName == "GetComponentsInParent")
                {
                    if (!context.IsDescriptorEnabled(GameObjectGetParentDescriptor))
                        return;
                    var diagnostic = Diagnostic.Create(AnalysisCategory.Code, GameObjectGetParentDescriptor.Id);
                    context.ReportIssue(diagnostic);
                    return;
                }
            }
        }
    }
}
