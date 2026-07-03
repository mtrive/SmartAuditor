// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class PhysicsCallbackAnalyzer : CodeAnalyzer
    {
        internal const string CDE0030 = nameof(CDE0030);
        internal const string CDE0032 = nameof(CDE0032);
        internal const string CDE0034 = nameof(CDE0034);

        static readonly Descriptor OnTriggerStayDescriptor = new Descriptor(
            CDE0030,
            "Physics: OnTriggerStay Fires Every Physics Step",
            Impact.Performance,
            "<b>OnTriggerStay</b> fires every physics step for every overlapping trigger pair. With many overlapping objects or non-trivial per-callback work the cost dominates the physics update.",
            "Move logic to <b>OnTriggerEnter</b> / <b>OnTriggerExit</b> so it runs once per state change. If continuous handling is genuinely required, gate the body so the heavy work runs at a lower frequency."
        )
        {
            MessageFormat = "OnTriggerStay fires every physics frame"
        };

        static readonly Descriptor OnTriggerStay2DDescriptor = new Descriptor(
            CDE0032,
            "Physics 2D: OnTriggerStay2D Fires Every Physics Step",
            Impact.Performance,
            "<b>OnTriggerStay2D</b> fires every physics step for every overlapping 2D trigger pair. With many overlapping colliders or non-trivial per-callback work the cost dominates the physics update.",
            "Move logic to <b>OnTriggerEnter2D</b> / <b>OnTriggerExit2D</b> so it runs once per state change. If continuous handling is genuinely required, gate the body so the heavy work runs at a lower frequency."
        )
        {
            MessageFormat = "OnTriggerStay2D fires every physics frame"
        };

        static readonly Descriptor OnCollisionStayDescriptor = new Descriptor(
            CDE0034,
            "Physics: OnCollisionStay Fires Every Physics Step",
            Impact.Performance,
            "<b>OnCollisionStay</b> fires every physics step for every active contact. With many simultaneous contacts or non-trivial per-callback work the cost dominates the physics update.",
            "Move logic to <b>OnCollisionEnter</b> / <b>OnCollisionExit</b> so it runs once per state change. If continuous handling is genuinely required, gate the body so the heavy work runs at a lower frequency."
        )
        {
            MessageFormat = "OnCollisionStay fires every physics frame"
        };

        static readonly Dictionary<string, Descriptor> s_MethodToDescriptor = new Dictionary<string, Descriptor>
        {
            { "OnTriggerStay", OnTriggerStayDescriptor },
            { "OnTriggerStay2D", OnTriggerStay2DDescriptor },
            { "OnCollisionStay", OnCollisionStayDescriptor }
        };

        static readonly Dictionary<string, string[]> s_MethodToBaseTypes = new Dictionary<string, string[]>
        {
            { "OnTriggerStay", new[] { "UnityEngine.Collider", "UnityEngine.MonoBehaviour" } },
            { "OnTriggerStay2D", new[] { "UnityEngine.Collider2D", "UnityEngine.MonoBehaviour" } },
            { "OnCollisionStay", new[] { "UnityEngine.Collider", "UnityEngine.MonoBehaviour", "UnityEngine.Rigidbody" } }
        };

        public override IReadOnlyCollection<OpCode> OpCodes => Array.Empty<OpCode>();

        public override void Analyze(MethodAnalysisContext context)
        {
            var methodDefinition = context.MethodDefinition;
            var methodName = methodDefinition.Name;
            var declaringType = methodDefinition.DeclaringType;

            if (!s_MethodToDescriptor.TryGetValue(methodName, out var descriptor))
                return;

            if (!s_MethodToBaseTypes.TryGetValue(methodName, out var baseTypes))
                return;

            var isApplicable = false;
            foreach (var baseType in baseTypes)
            {
                if (MonoCecilHelper.IsOrInheritedFrom(declaringType, baseType))
                {
                    isApplicable = true;
                    break;
                }
            }

            if (!isApplicable || !context.IsDescriptorEnabled(descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}

