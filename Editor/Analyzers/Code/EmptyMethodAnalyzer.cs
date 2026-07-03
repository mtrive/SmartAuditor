using System;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class EmptyMethodAnalyzer : CodeAnalyzer
    {
        internal const string CDE2001 = nameof(CDE2001);

        static readonly Descriptor Descriptor = new Descriptor
            (
            CDE2001,
            "MonoBehaviour: Empty Unity Message",
            Impact.Performance,
            "Empty Unity message handlers (<b>Awake</b>, <b>Start</b>, <b>Update</b>, etc.) are still discovered by reflection at scene load and called every frame on every instance. Each empty call costs a small amount of CPU and a managed-to-native transition.",
            "Remove the empty message handler from the MonoBehaviour."
            )
        {
            MessageFormat = "MonoBehaviour method '{0}' is empty",
            DefaultSeverity = Severity.Minor
        };

        readonly OpCode[] m_OpCodes =
        {
            Mono.Cecil.Cil.OpCodes.Ret
        };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            // skip any no-op
            var previousIL = context.Instruction.Previous;
            while (previousIL != null && previousIL.OpCode == Mono.Cecil.Cil.OpCodes.Nop)
                previousIL = previousIL.Previous;

            // if there is no instruction before OpCodes.Ret, then we know this method is empty
            if (previousIL != null)
                return;

            if (!MonoBehaviourAnalysis.IsMonoBehaviour(context.MethodDefinition.DeclaringType))
                return;

            if (!MonoBehaviourAnalysis.IsMonoBehaviourEvent(context.MethodDefinition))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, Descriptor.Id, context.MethodDefinition.Name);
            context.ReportIssue(diagnostic);
        }
    }
}
