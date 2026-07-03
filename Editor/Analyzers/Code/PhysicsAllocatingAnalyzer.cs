// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class PhysicsAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0007 = nameof(CDE0007);
        internal const string CDE0008 = nameof(CDE0008);
        internal const string CDE0012 = nameof(CDE0012);

        static readonly Descriptor SweepTestAllDescriptor = new Descriptor(
            CDE0007,
            "Physics: Rigidbody.SweepTestAll Allocates",
            Impact.Memory,
            "<b>Rigidbody.SweepTestAll()</b> allocates a fresh <b>RaycastHit[]</b> on every call.",
            "Call <b>Rigidbody.SweepTestAll(Vector3, RaycastHit[], float, QueryTriggerInteraction)</b> with a reusable <b>RaycastHit[]</b>."
        )
        {
            MessageFormat = "Rigidbody.SweepTestAll allocates a managed RaycastHit array"
        };

        static readonly Descriptor PhysicsCastAllDescriptor = new Descriptor(
            CDE0008,
            "Physics: Cast-All API Allocates",
            Impact.Memory,
            "The <b>Physics</b> cast-all APIs (<b>RaycastAll</b>, <b>CapsuleCastAll</b>, <b>SphereCastAll</b>, <b>BoxCastAll</b>) allocate a fresh <b>RaycastHit[]</b> on every call.",
            "Call the matching <b>Physics</b> <b>*NonAlloc</b> overload (for example <b>Physics.RaycastNonAlloc</b>) with a reusable <b>RaycastHit[]</b>."
        )
        {
            MessageFormat = "Physics.{0} allocates a managed RaycastHit array"
        };

        static readonly Descriptor PhysicsOverlapDescriptor = new Descriptor(
            CDE0012,
            "Physics: Overlap API Allocates",
            Impact.Memory,
            "The <b>Physics</b> overlap APIs (<b>OverlapCapsule</b>, <b>OverlapSphere</b>, <b>OverlapBox</b>) allocate a fresh <b>Collider[]</b> on every call.",
            "Call the matching <b>Physics</b> <b>*NonAlloc</b> overload (for example <b>Physics.OverlapSphereNonAlloc</b>) with a reusable <b>Collider[]</b>."
        )
        {
            MessageFormat = "Physics.{0} allocates a managed Collider array"
        };

        static readonly HashSet<string> s_PhysicsCastAllMethods = new HashSet<string>
        {
            "RaycastAll",
            "CapsuleCastAll",
            "SphereCastAll",
            "BoxCastAll"
        };

        static readonly HashSet<string> s_PhysicsOverlapMethods = new HashSet<string>
        {
            "OverlapCapsule",
            "OverlapSphere",
            "OverlapBox"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;
            var methodName = methodReference.Name;
            Descriptor descriptor;
            if (declaringType == "UnityEngine.Physics")
            {
                if (s_PhysicsCastAllMethods.Contains(methodName))
                    descriptor = PhysicsCastAllDescriptor;
                else if (s_PhysicsOverlapMethods.Contains(methodName))
                    descriptor = PhysicsOverlapDescriptor;
                else
                    return;
            }
            else if (declaringType == "UnityEngine.Rigidbody" && methodName == "SweepTestAll")
            {
                descriptor = SweepTestAllDescriptor;
            }
            else
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var diagnostic = descriptor == SweepTestAllDescriptor
                ? Diagnostic.Create(AnalysisCategory.Code, descriptor.Id)
                : Diagnostic.Create(AnalysisCategory.Code, descriptor.Id, methodName);
            context.ReportIssue(diagnostic);
        }
    }
}

