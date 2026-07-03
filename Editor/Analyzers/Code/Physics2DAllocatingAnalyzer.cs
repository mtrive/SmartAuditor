// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class Physics2DAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0015 = nameof(CDE0015);
        internal const string CDE0021 = nameof(CDE0021);

        static readonly Descriptor CastAllDescriptor = new Descriptor(
            CDE0015,
            "Physics 2D: Cast-All API Allocates",
            Impact.Memory,
            "The <b>Physics2D</b> cast-all APIs (<b>RaycastAll</b>, <b>LinecastAll</b>, <b>CircleCastAll</b>, <b>BoxCastAll</b>, <b>CapsuleCastAll</b>, <b>GetRayIntersectionAll</b>) allocate a fresh <b>RaycastHit2D[]</b> on every call.",
            "Call the matching <b>Physics2D</b> <b>*NonAlloc</b> overload (for example <b>Physics2D.RaycastNonAlloc</b>) with a reusable <b>RaycastHit2D[]</b>."
        )
        {
            MessageFormat = "Physics2D.{0} allocates a managed RaycastHit2D array"
        };

        static readonly Descriptor OverlapAllDescriptor = new Descriptor(
            CDE0021,
            "Physics 2D: Overlap-All API Allocates",
            Impact.Memory,
            "The <b>Physics2D</b> overlap-all APIs (<b>OverlapPointAll</b>, <b>OverlapCircleAll</b>, <b>OverlapBoxAll</b>, <b>OverlapAreaAll</b>, <b>OverlapCapsuleAll</b>) allocate a fresh <b>Collider2D[]</b> on every call.",
            "Call the matching <b>Physics2D</b> <b>*NonAlloc</b> overload (for example <b>Physics2D.OverlapCircleNonAlloc</b>) with a reusable <b>Collider2D[]</b>."
        )
        {
            MessageFormat = "Physics2D.{0} allocates a managed Collider2D array"
        };

        static readonly HashSet<string> s_CastAllMethods = new HashSet<string>
        {
            "LinecastAll",
            "RaycastAll",
            "CircleCastAll",
            "BoxCastAll",
            "CapsuleCastAll",
            "GetRayIntersectionAll"
        };

        static readonly HashSet<string> s_OverlapAllMethods = new HashSet<string>
        {
            "OverlapPointAll",
            "OverlapCircleAll",
            "OverlapBoxAll",
            "OverlapAreaAll",
            "OverlapCapsuleAll"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's UnityEngine.Physics2D type
            if (methodReference.DeclaringType.FullName != "UnityEngine.Physics2D")
                return;

            var methodName = methodReference.Name;
            Descriptor descriptor;
            if (s_CastAllMethods.Contains(methodName))
                descriptor = CastAllDescriptor;
            else if (s_OverlapAllMethods.Contains(methodName))
                descriptor = OverlapAllDescriptor;
            else
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id, methodName);
            context.ReportIssue(diagnostic);
        }
    }
}

