// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes code for usage of Unity's message-passing methods (SendMessage, SendMessageUpwards, BroadcastMessage)
    /// on both <c>GameObject</c> and <c>Component</c>, all of which use Reflection and are CPU-intensive.
    /// </summary>
    sealed class SendMessageAnalyzer : CodeAnalyzer
    {
        internal const string CDE0056 = nameof(CDE0056);
        internal const string CDE0057 = nameof(CDE0057);
        internal const string CDE0266 = nameof(CDE0266);
        internal const string CDE0267 = nameof(CDE0267);
        internal const string CDE0268 = nameof(CDE0268);
        internal const string CDE0269 = nameof(CDE0269);

        static readonly Descriptor GameObjectSendMessageDescriptor = new Descriptor(
            CDE0056,
            "Reflection: GameObject.SendMessage Uses Reflection",
            Impact.Performance,
            "<b>GameObject.SendMessage</b> uses reflection on every call to look up a method by name on every component of the GameObject, then invokes it through the reflection path. The dispatch is orders of magnitude slower than a direct call.",
            "Hold a direct reference to the target component and invoke the method directly. Use a C# event or an interface where multiple receivers are required."
        )
        {
            MessageFormat = "GameObject.SendMessage() uses Reflection and is very slow"
        };

        static readonly Descriptor ComponentSendMessageDescriptor = new Descriptor(
            CDE0057,
            "Reflection: Component.SendMessage Uses Reflection",
            Impact.Performance,
            "<b>Component.SendMessage</b> uses reflection on every call to look up a method by name on every component of the owning GameObject, then invokes it through the reflection path. The dispatch is orders of magnitude slower than a direct call.",
            "Hold a direct reference to the target component and invoke the method directly. Use a C# event or an interface where multiple receivers are required."
        )
        {
            MessageFormat = "Component.SendMessage() uses Reflection and is very slow"
        };

        static readonly Descriptor GameObjectSendMessageUpwardsDescriptor = new Descriptor(
            CDE0266,
            "Reflection: GameObject.SendMessageUpwards Uses Reflection",
            Impact.Performance,
            "<b>GameObject.SendMessageUpwards</b> walks the GameObject and every ancestor, reflecting on each component to find a method matching the supplied name string.",
            "Hold direct references to the receivers and invoke the method directly, or use an interface lookup with a cached list."
        )
        {
            MessageFormat = "GameObject.SendMessageUpwards() uses Reflection and is very slow"
        };

        static readonly Descriptor ComponentSendMessageUpwardsDescriptor = new Descriptor(
            CDE0267,
            "Reflection: Component.SendMessageUpwards Uses Reflection",
            Impact.Performance,
            "<b>Component.SendMessageUpwards</b> walks the owning GameObject and every ancestor, reflecting on each component to find a method matching the supplied name string.",
            "Hold direct references to the receivers and invoke the method directly, or use an interface lookup with a cached list."
        )
        {
            MessageFormat = "Component.SendMessageUpwards() uses Reflection and is very slow"
        };

        static readonly Descriptor GameObjectBroadcastMessageDescriptor = new Descriptor(
            CDE0268,
            "Reflection: GameObject.BroadcastMessage Uses Reflection",
            Impact.Performance,
            "<b>GameObject.BroadcastMessage</b> walks the GameObject and every descendant, reflecting on each component to find a method matching the supplied name string.",
            "Hold direct references to the receivers and invoke the method directly, or use an interface lookup with a cached list."
        )
        {
            MessageFormat = "GameObject.BroadcastMessage() uses Reflection and is very slow"
        };

        static readonly Descriptor ComponentBroadcastMessageDescriptor = new Descriptor(
            CDE0269,
            "Reflection: Component.BroadcastMessage Uses Reflection",
            Impact.Performance,
            "<b>Component.BroadcastMessage</b> walks the owning GameObject and every descendant, reflecting on each component to find a method matching the supplied name string.",
            "Hold direct references to the receivers and invoke the method directly, or use an interface lookup with a cached list."
        )
        {
            MessageFormat = "Component.BroadcastMessage() uses Reflection and is very slow"
        };

        static readonly Dictionary<string, Descriptor> s_GameObjectDescriptors = new Dictionary<string, Descriptor>
        {
            { "SendMessage", GameObjectSendMessageDescriptor },
            { "SendMessageUpwards", GameObjectSendMessageUpwardsDescriptor },
            { "BroadcastMessage", GameObjectBroadcastMessageDescriptor }
        };

        static readonly Dictionary<string, Descriptor> s_ComponentDescriptors = new Dictionary<string, Descriptor>
        {
            { "SendMessage", ComponentSendMessageDescriptor },
            { "SendMessageUpwards", ComponentSendMessageUpwardsDescriptor },
            { "BroadcastMessage", ComponentBroadcastMessageDescriptor }
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;

            Dictionary<string, Descriptor> descriptors;
            if (declaringType == "UnityEngine.GameObject")
                descriptors = s_GameObjectDescriptors;
            else if (declaringType == "UnityEngine.Component")
                descriptors = s_ComponentDescriptors;
            else
                return;

            if (!descriptors.TryGetValue(methodReference.Name, out var descriptor))
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}
