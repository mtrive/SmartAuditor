// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MaterialMainTextureAnalyzer : CodeAnalyzer
    {
        internal const string CDE0237 = nameof(CDE0237);
        internal const string CDE0238 = nameof(CDE0238);

        static readonly Descriptor MaterialMainTextureGetDescriptor = new Descriptor(
            CDE0237,
            "Material.mainTexture Rehashes _MainTex on Read",
            Impact.Performance | Impact.Memory,
            "<b>Material.mainTexture</b> is a legacy convenience property that hashes the string <b>\"_MainTex\"</b> on every read to look up the underlying shader property.",
            "Read the texture via <b>Material.GetTexture(int nameID)</b> with a cached <b>Shader.PropertyToID(\"_MainTex\")</b>, or hold a direct <b>Texture</b> reference."
        )
        {
            MessageFormat = "Material.mainTexture hashes \"_MainTex\" on each access"
        };

        static readonly Descriptor MaterialMainTextureSetDescriptor = new Descriptor(
            CDE0238,
            "Material.mainTexture Rehashes _MainTex on Write",
            Impact.Performance | Impact.Memory,
            "<b>Material.mainTexture</b> is a legacy convenience property that hashes the string <b>\"_MainTex\"</b> on every write to look up the underlying shader property.",
            "Write the texture via <b>Material.SetTexture(int nameID, Texture)</b> with a cached <b>Shader.PropertyToID(\"_MainTex\")</b>."
        )
        {
            MessageFormat = "Material.mainTexture hashes \"_MainTex\" on each assignment"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's a Material method
            if (methodReference.DeclaringType.FullName != "UnityEngine.Material")
                return;

            switch (methodReference.Name)
            {
                // Check if it's get_mainTexture or set_mainTexture
                case "get_mainTexture":
                {
                    if (!context.IsDescriptorEnabled(MaterialMainTextureGetDescriptor))
                        return;
                    var diagnostic = Diagnostic.Create(AnalysisCategory.Code, MaterialMainTextureGetDescriptor.Id);
                    context.ReportIssue(diagnostic);
                    return;
                }
                case "set_mainTexture":
                {
                    if (!context.IsDescriptorEnabled(MaterialMainTextureSetDescriptor))
                        return;
                    var diagnostic = Diagnostic.Create(AnalysisCategory.Code, MaterialMainTextureSetDescriptor.Id);
                    context.ReportIssue(diagnostic);
                    break;
                }
            }

        }
    }
}

