// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class SystemAppDomainGetAssembliesAnalyzer : CodeAnalyzer
    {
        internal const string CDE1004 = nameof(CDE1004);

        static readonly Descriptor AppDomainGetAssembliesDescriptor = new Descriptor(
            CDE1004,
            "Reflection: AppDomain.GetAssemblies Is Expensive",
            Impact.EditorWorkflow,
            "<b>AppDomain.CurrentDomain.GetAssemblies()</b> enumerates every assembly loaded in the editor domain and allocates a fresh array. Calls compound across domain reloads, inspector redraws, and asset post-processors.",
            "Cache the assembly list once per domain reload. For type, method, or field lookups in Editor code, use <b>UnityEditor.TypeCache</b> instead."
        )
        {
            MessageFormat = "AppDomain.GetAssemblies() can be slow"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's System.AppDomain type
            if (methodReference.DeclaringType.FullName != "System.AppDomain")
                return;

            // Check if it's GetAssemblies method
            if (methodReference.Name != "GetAssemblies")
                return;

            if (!context.IsDescriptorEnabled(AppDomainGetAssembliesDescriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, AppDomainGetAssembliesDescriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}

