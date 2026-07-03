// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MonoBehaviourOnGuiAnalyzer : CodeAnalyzer
    {
        internal const string CDE0058 = nameof(CDE0058);

        static readonly Descriptor Descriptor = new Descriptor(
            CDE0058,
            "MonoBehaviour: OnGUI Initializes Legacy IMGUI",
            Impact.Performance,
            "<b>MonoBehaviour.OnGUI</b> is the entry point for legacy Immediate Mode GUI (IMGUI). A single <b>OnGUI</b> method anywhere in the project causes IMGUI to initialize and consume CPU time every frame, even on instances whose body is empty.",
            "Delete the <b>OnGUI</b> method from the MonoBehaviour. Migrate runtime UI to UI Toolkit or uGUI."
        )
        {
            MessageFormat = "MonoBehaviour.OnGUI initializes legacy IMGUI and consumes CPU time",
            DefaultSeverity = Severity.Minor
        };

        public override IReadOnlyCollection<OpCode> OpCodes => Array.Empty<OpCode>();

        public override void Analyze(MethodAnalysisContext context)
        {
            var methodDefinition = context.MethodDefinition;

            if (methodDefinition.Name != "OnGUI")
                return;

            if (methodDefinition.IsStatic || methodDefinition.Parameters.Count != 0)
                return;

            if (!MonoBehaviourAnalysis.IsMonoBehaviour(methodDefinition.DeclaringType))
                return;

            if (!context.IsDescriptorEnabled(Descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, Descriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}
