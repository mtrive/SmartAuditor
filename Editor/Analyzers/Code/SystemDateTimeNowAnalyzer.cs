// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes code for usage of System.DateTime.Now property that can impact CPU performance.
    /// </summary>
    sealed class SystemDateTimeNowAnalyzer : CodeAnalyzer
    {
        internal const string CDE1003 = nameof(CDE1003);

        static readonly Descriptor DateTimeNowDescriptor = new Descriptor(
            CDE1003,
            "Time: DateTime.Now Performs Timezone Conversion",
            Impact.Performance,
            "<b>DateTime.Now</b> reads UTC, then converts to local time on every call. The conversion performs a timezone and daylight-savings lookup that dominates the operation.",
            "For game-relative time use <b>UnityEngine.Time.time</b> or <b>UnityEngine.Time.unscaledTime</b>. For wall-clock measurements use <b>DateTime.UtcNow</b>, which skips the timezone conversion."
        )
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "DateTime.Now performs a timezone lookup"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's System.DateTime type
            if (methodReference.DeclaringType.FullName != "System.DateTime")
                return;

            // Property getters are compiled to methods starting with "get_"
            if (!methodReference.Name.StartsWith("get_"))
                return;

            // Extract the property name by removing "get_" prefix
            var propertyName = methodReference.Name.Substring("get_".Length);

            // Check if it's the Now property
            if (propertyName != "Now")
                return;

            if (!context.IsDescriptorEnabled(DateTimeNowDescriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, DateTimeNowDescriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}
