// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MaterialShaderKeywordsAnalyzer : CodeAnalyzer
    {
        internal const string CDE0163 = nameof(CDE0163);

        static readonly Descriptor ShaderKeywordsDescriptor = new Descriptor(
            CDE0163,
            "Material.shaderKeywords Allocates",
            Impact.Memory,
            "<b>Material.shaderKeywords</b> allocates a fresh <b>string[]</b> on every access.",
            "Check or toggle individual keywords with <b>Material.IsKeywordEnabled</b>, <b>Material.EnableKeyword</b>, and <b>Material.DisableKeyword</b>. If the full list is required, cache <b>Material.shaderKeywords</b> once during initialization."
        )
        {
            MessageFormat = "'Material.shaderKeywords' allocates a managed array on each access",
            DefaultSeverity = Severity.Minor
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            if (methodReference.DeclaringType.FullName != "UnityEngine.Material")
                return;

            if (methodReference.Name != "get_shaderKeywords")
                return;

            if (!context.IsDescriptorEnabled(ShaderKeywordsDescriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, ShaderKeywordsDescriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}
