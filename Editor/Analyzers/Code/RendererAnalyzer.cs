// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class RendererAnalyzer : CodeAnalyzer
    {
        internal const string CDE0084 = nameof(CDE0084);
        internal const string CDE0085 = nameof(CDE0085);
        internal const string CDE0168 = nameof(CDE0168);

        static readonly Descriptor RendererMaterialsDescriptor = new Descriptor(
            CDE0084,
            "Renderer.materials Allocates",
            Impact.Memory,
            "<b>Renderer.materials</b> allocates a fresh <b>Material[]</b> on every access; reading it also forces a per-renderer material instantiation if the slots are still shared.",
            "Call <b>Renderer.GetMaterials(List&lt;Material&gt;)</b> with a reusable list."
        )
        {
            MessageFormat = "'{0}' in {1}.{2}() allocates a new Material[] array on each access"
        };

        static readonly Descriptor RendererSharedMaterialsDescriptor = new Descriptor(
            CDE0085,
            "Renderer.sharedMaterials Allocates",
            Impact.Memory,
            "<b>Renderer.sharedMaterials</b> allocates a fresh <b>Material[]</b> on every access.",
            "Call <b>Renderer.GetSharedMaterials(List&lt;Material&gt;)</b> with a reusable list."
        )
        {
            MessageFormat = "'{0}' in {1}.{2}() allocates a new Material[] array on each access"
        };

        static readonly Descriptor SkinnedMeshRendererBonesDescriptor = new Descriptor(
            CDE0168,
            "SkinnedMeshRenderer.bones Allocates",
            Impact.Memory,
            "<b>SkinnedMeshRenderer.bones</b> allocates a fresh <b>Transform[]</b> on every access.",
            "Cache <b>SkinnedMeshRenderer.bones</b> during initialization and reuse the array."
        )
        {
            MessageFormat = "'{0}' in {1}.{2}() allocates a new Transform[] array on each access"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };
        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;
            var methodName = methodReference.Name;

            // Check for Renderer properties
            if (declaringType == "UnityEngine.Renderer")
            {
                // Check for Renderer.materials property (getter)
                if (methodName == "get_materials")
                {
                    if (!context.IsDescriptorEnabled(RendererMaterialsDescriptor))
                        return;
                    var diagnostic = Diagnostic.Create(AnalysisCategory.Code, RendererMaterialsDescriptor.Id, "Renderer.materials", context.CallingType, context.CallingMethod);
                    context.ReportIssue(diagnostic);
                    return;
                }

                // Check for Renderer.sharedMaterials property (getter)
                if (methodName == "get_sharedMaterials")
                {
                    if (!context.IsDescriptorEnabled(RendererSharedMaterialsDescriptor))
                        return;
                    var diagnostic = Diagnostic.Create(AnalysisCategory.Code, RendererSharedMaterialsDescriptor.Id, "Renderer.sharedMaterials", context.CallingType, context.CallingMethod);
                    context.ReportIssue(diagnostic);
                    return;
                }

            }

            // Check for SkinnedMeshRenderer.bones property (getter)
            if (declaringType == "UnityEngine.SkinnedMeshRenderer" && methodName == "get_bones")
            {
                if (!context.IsDescriptorEnabled(SkinnedMeshRendererBonesDescriptor))
                    return;
                var diagnostic = Diagnostic.Create(AnalysisCategory.Code, SkinnedMeshRendererBonesDescriptor.Id, "SkinnedMeshRenderer.bones", context.CallingType, context.CallingMethod);
                context.ReportIssue(diagnostic);
            }
        }
    }
}
