// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class TextureLinearCtorAnalyzer : CodeAnalyzer
    {
        internal const string CDE0240 = nameof(CDE0240);

        static readonly Descriptor TextureCtorWithoutLinearDescriptor = new Descriptor(
            CDE0240,
            "Texture: Texture2D Constructor Omits Color Space",
            Impact.Performance | Impact.Memory,
            "<b>Texture2D</b> constructors without an explicit color-space argument fall back to a project-wide default, which can silently produce sRGB or linear data that disagrees with how shaders sample the texture.",
            "Call the <b>Texture2D(int, int, TextureFormat, bool mipChain, bool linear)</b> overload with an explicit <b>linear</b> flag, or the <b>GraphicsFormat</b> overload with an explicit sRGB or Linear <b>GraphicsFormat</b>."
        )
        {
            MessageFormat = "Texture2D constructor does not explicitly define color space",
            DefaultSeverity = Severity.Minor,
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Newobj };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            if (context.Instruction.OpCode != Mono.Cecil.Cil.OpCodes.Newobj)
                return;

            var ctor = (MethodReference)context.Instruction.Operand;

            // Target Texture2D constructors
            if (ctor.DeclaringType.FullName != "UnityEngine.Texture2D")
                return;

            if (ctor.Name != ".ctor")
                return;

            // Heuristics:
            // - Legacy explicit-linear overload typically has >= 5 parameters with last param bool 'linear'.
            // - Shorter overloads (e.g., (int,int) or (int,int,TextureFormat,bool)) lack explicit 'linear'.
            // - Prefer parameter name match if available.
            var parameters = ctor.Parameters;

            var hasNamedLinear = parameters.Any(p => p.Name == "linear" && p.ParameterType.FullName == "System.Boolean");

            var hasTrailingBoolLinear = parameters.Count >= 5 &&
                                        parameters[parameters.Count - 1].ParameterType.FullName == "System.Boolean";

            var hasLinear = hasNamedLinear || hasTrailingBoolLinear;

            if (hasLinear)
                return;

            if (!context.IsDescriptorEnabled(TextureCtorWithoutLinearDescriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, TextureCtorWithoutLinearDescriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}


