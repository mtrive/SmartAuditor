// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AssetDatabaseFindAssetsAnalyzer : CodeAnalyzer
    {
        internal const string CDE0232 = nameof(CDE0232);

        static readonly Descriptor AssetDatabaseFindAssetsDescriptor = new Descriptor(
            CDE0232,
            "Editor: AssetDatabase.FindAssets Is Expensive",
            Impact.EditorWorkflow,
            "<b>AssetDatabase.FindAssets()</b> walks the entire asset database on every call. Repeated invocations slow down inspectors, editor windows, and asset post-processors on large projects.",
            "Cache the result of <b>AssetDatabase.FindAssets()</b> and refresh only when the relevant assets change. Pass the narrowest filter and search-folder list possible to limit the scan."
        )
        {
            MessageFormat = "AssetDatabase.FindAssets() can be slow"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's UnityEditor.AssetDatabase type
            if (methodReference.DeclaringType.FullName != "UnityEditor.AssetDatabase")
                return;

            // Check if it's FindAssets method
            if (methodReference.Name != "FindAssets")
                return;

            if (!context.IsDescriptorEnabled(AssetDatabaseFindAssetsDescriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, AssetDatabaseFindAssetsDescriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}

