// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class TextAssetBytesAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0173 = nameof(CDE0173);

        static readonly Descriptor BytesDescriptor = new Descriptor(
            CDE0173,
            "TextAsset.bytes Allocates",
            Impact.Memory,
            "<b>TextAsset.bytes</b> allocates a fresh managed <b>byte[]</b> copy of the asset data on every access.",
            "Use <b>TextAsset.GetData&lt;byte&gt;()</b>, which returns a <b>NativeArray&lt;byte&gt;</b> view of the underlying data without allocating."
        )
        {
            MessageFormat = "TextAsset.bytes allocates a managed byte array on each access"
        };

        static readonly Dictionary<string, Dictionary<string, Descriptor>> s_TypeToPropertyDescriptors = new Dictionary<string, Dictionary<string, Descriptor>>
        {
            {
                "UnityEngine.TextAsset", new Dictionary<string, Descriptor>
                {
                    { "bytes", BytesDescriptor }
                }
            }
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;

            // Check if it's one of our target types
            if (!s_TypeToPropertyDescriptors.TryGetValue(declaringType, out var propertyToDescriptor))
                return;

            // Property getters are compiled to methods starting with "get_"
            if (!methodReference.Name.StartsWith("get_"))
                return;

            // Extract the property name by removing "get_" prefix
            var propertyName = methodReference.Name.Substring("get_".Length);

            // Check if it's one of our target properties
            if (!propertyToDescriptor.TryGetValue(propertyName, out var descriptor))
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}
