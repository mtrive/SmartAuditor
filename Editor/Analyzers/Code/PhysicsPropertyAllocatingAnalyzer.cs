// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class PhysicsPropertyAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0103 = nameof(CDE0103);
        internal const string CDE0104 = nameof(CDE0104);
        internal const string CDE0183 = nameof(CDE0183);

        static readonly Descriptor CollisionContactsDescriptor = new Descriptor(
            CDE0103,
            "Physics: Collision.contacts Allocates",
            Impact.Memory,
            "<b>Collision.contacts</b> allocates a fresh <b>ContactPoint[]</b> on every access.",
            "Call <b>Collision.GetContacts(List&lt;ContactPoint&gt;)</b> with a reusable list."
        )
        {
            MessageFormat = "{0}.contacts allocates a managed ContactPoint array on each access"
        };

        static readonly Descriptor Collision2DContactsDescriptor = new Descriptor(
            CDE0104,
            "Physics 2D: Collision2D.contacts Allocates",
            Impact.Memory,
            "<b>Collision2D.contacts</b> allocates a fresh <b>ContactPoint2D[]</b> on every access.",
            "Call <b>Collision2D.GetContacts(List&lt;ContactPoint2D&gt;)</b> with a reusable list."
        )
        {
            MessageFormat = "{0}.contacts allocates a managed ContactPoint2D array on each access"
        };

        static readonly Descriptor Collider2DPointsDescriptor = new Descriptor(
            CDE0183,
            "Physics 2D: Collider2D.points Allocates",
            Impact.Memory,
            "The <b>points</b> property on <b>EdgeCollider2D</b> and <b>PolygonCollider2D</b> allocates a fresh <b>Vector2[]</b> on every access.",
            "Cache <b>EdgeCollider2D.points</b> / <b>PolygonCollider2D.points</b> during initialization and reuse the array."
        )
        {
            MessageFormat = "{0}.points allocates a managed Vector2 array on each access"
        };

        static readonly Dictionary<string, Descriptor> s_PropertyToDescriptor = new Dictionary<string, Descriptor>
        {
            { "UnityEngine.Collision.contacts", CollisionContactsDescriptor },
            { "UnityEngine.Collision2D.contacts", Collision2DContactsDescriptor },
            { "UnityEngine.EdgeCollider2D.points", Collider2DPointsDescriptor },
            { "UnityEngine.PolygonCollider2D.points", Collider2DPointsDescriptor }
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Property getters are compiled to methods starting with "get_". Filter early to avoid
            // the FullName access and string concat below for the vast majority of opcodes.
            if (!methodReference.Name.StartsWith("get_"))
                return;

            var propertyName = methodReference.Name.Substring("get_".Length);
            var key = methodReference.DeclaringType.FullName + "." + propertyName;
            if (!s_PropertyToDescriptor.TryGetValue(key, out var descriptor))
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var typeName = methodReference.DeclaringType.Name;
            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id, typeName);
            context.ReportIssue(diagnostic);
        }
    }
}
