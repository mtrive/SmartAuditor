// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class SpritePropertyAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0177 = nameof(CDE0177);
        const string SpriteType = "UnityEngine.Sprite";

        static readonly Descriptor SpriteArrayPropertyDescriptor = new Descriptor(
            CDE0177,
            "Sprite: Geometry Properties Allocate",
            Impact.Memory,
            "<b>Sprite</b> geometry properties (<b>vertices</b>, <b>triangles</b>, <b>uv</b>) allocate a fresh managed array on every access.",
            "Cache the Sprite geometry array during initialization and reuse it. Use <b>Sprite.GetVertexAttribute</b> / <b>Sprite.GetIndices</b> overloads where a per-frame read is required."
        )
        {
            MessageFormat = "Sprite.{0} allocates a managed array on each access"
        };

        static readonly HashSet<string> s_TargetProperties = new HashSet<string>
        {
            "vertices",
            "triangles",
            "uv"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;

            if (declaringType != SpriteType)
                return;

            // Property getters are compiled to methods starting with "get_"
            if (!methodReference.Name.StartsWith("get_"))
                return;

            // Extract the property name by removing "get_" prefix
            var propertyName = methodReference.Name.Substring("get_".Length);

            // Check if it's one of our target properties
            if (!s_TargetProperties.Contains(propertyName))
                return;

            if (!context.IsDescriptorEnabled(SpriteArrayPropertyDescriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, SpriteArrayPropertyDescriptor.Id, propertyName);
            context.ReportIssue(diagnostic);
        }
    }
}
