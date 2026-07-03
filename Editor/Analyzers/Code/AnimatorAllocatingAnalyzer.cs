// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes code for usage of Unity Animator properties and methods that allocate managed memory.
    /// </summary>
    sealed class AnimatorAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0060 = nameof(CDE0060);
        internal const string CDE0115 = nameof(CDE0115);
        internal const string CDE0116 = nameof(CDE0116);

        static readonly Descriptor ParametersDescriptor = new Descriptor(
            CDE0060,
            "Animator.parameters Allocates",
            Impact.Memory,
            "<b>Animator.parameters</b> allocates a fresh <b>AnimatorControllerParameter[]</b> on every access.",
            "Read individual entries with <b>Animator.GetParameter(int index)</b>, or cache <b>Animator.parameters</b> once at initialization and reuse the array."
        )
        {
            MessageFormat = "Animator.parameters allocates a managed array on each access"
        };

        static readonly Descriptor GetCurrentAnimatorClipInfoDescriptor = new Descriptor(
            CDE0115,
            "Animator.GetCurrentAnimatorClipInfo Allocates",
            Impact.Memory,
            "<b>Animator.GetCurrentAnimatorClipInfo()</b> allocates a fresh <b>AnimatorClipInfo[]</b> on every call.",
            "Use the <b>Animator.GetCurrentAnimatorClipInfo(int layerIndex, List&lt;AnimatorClipInfo&gt;)</b> overload that fills a reusable list, or cache the result during initialization."
        )
        {
            MessageFormat = "Animator.GetCurrentAnimatorClipInfo allocates a managed array on each call"
        };

        static readonly Descriptor GetBehavioursDescriptor = new Descriptor(
            CDE0116,
            "Animator.GetBehaviours Allocates",
            Impact.Memory,
            "<b>Animator.GetBehaviours()</b> allocates a fresh <b>StateMachineBehaviour[]</b> on every call.",
            "Cache the result of <b>Animator.GetBehaviours()</b> during initialization and reuse the array."
        )
        {
            MessageFormat = "Animator.GetBehaviours allocates a managed array on each call"
        };

        static readonly Dictionary<string, Descriptor> s_PropertyDescriptors = new Dictionary<string, Descriptor>
        {
            { "parameters", ParametersDescriptor }
        };

        static readonly Dictionary<string, Descriptor> s_MethodDescriptors = new Dictionary<string, Descriptor>
        {
            { "GetCurrentAnimatorClipInfo", GetCurrentAnimatorClipInfoDescriptor },
            { "GetBehaviours", GetBehavioursDescriptor }
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;

            // Only analyze UnityEngine.Animator calls
            if (declaringType != "UnityEngine.Animator")
                return;

            Descriptor descriptor = null;

            // Check if it's a property getter
            if (methodReference.Name.StartsWith("get_"))
            {
                var propertyName = methodReference.Name.Substring("get_".Length);
                s_PropertyDescriptors.TryGetValue(propertyName, out descriptor);
            }
            else
            {
                // Check if it's one of our target methods
                s_MethodDescriptors.TryGetValue(methodReference.Name, out descriptor);
            }

            if (descriptor == null)
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}
