using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class DebugLogAnalyzer : CodeAnalyzer
    {
        static readonly int k_ModuleHashCode = "UnityEngine.CoreModule.dll".GetHashCode();
        static readonly int k_TypeHashCode = "UnityEngine.Debug".GetHashCode();
        static readonly int k_ConditionalAttributeHashCode = "System.Diagnostics.ConditionalAttribute".GetHashCode();

        internal const string CDE0192 = nameof(CDE0192);
        internal const string CDE0193 = nameof(CDE0193);

        static readonly Descriptor DebugLogIssueDescriptor = new Descriptor
            (
            CDE0192,
            "Logging: Debug.Log / Debug.LogFormat Call",
            Impact.Performance,
            "<b>Debug.Log</b> and <b>Debug.LogFormat</b> format the message, capture a managed stack trace, and dispatch through the native log pipeline on every call. Costs add up in release builds where logging serves no purpose.",
            "Strip release logging with <b>#if</b> guards or apply <b>[System.Diagnostics.Conditional(\"UNITY_EDITOR\")]</b> to a wrapper that calls <b>Debug.Log</b>. Where release logging is required, disable stack traces via <b>Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None)</b>."
            )
        {
            DocumentationUrl = "https://docs.unity3d.com/Manual/UnderstandingPerformanceGeneralOptimizations.html",
            MessageFormat = "Use of Debug.{0} in '{1}'",
            DefaultSeverity = Severity.Minor
        };

        static readonly Descriptor DebugLogWarningIssueDescriptor = new Descriptor
            (
            CDE0193,
            "Logging: Debug.LogWarning / Debug.LogWarningFormat Call",
            Impact.Performance,
            "<b>Debug.LogWarning</b> and <b>Debug.LogWarningFormat</b> format the message, capture a managed stack trace, and dispatch through the native log pipeline on every call. Costs add up in release builds where logging serves no purpose.",
            "Strip release logging with <b>#if</b> guards or apply <b>[System.Diagnostics.Conditional(\"UNITY_EDITOR\")]</b> to a wrapper that calls <b>Debug.LogWarning</b>. Where release logging is required, disable stack traces via <b>Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None)</b>."
            )
        {
            DocumentationUrl = "https://docs.unity3d.com/Manual/UnderstandingPerformanceGeneralOptimizations.html",
            MessageFormat = "Use of Debug.{0} in '{1}'",
            DefaultSeverity = Severity.Minor
        };

        readonly OpCode[] m_OpCodes =
        {
            Mono.Cecil.Cil.OpCodes.Call,
            Mono.Cecil.Cil.OpCodes.Callvirt
        };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var callee = (MethodReference)context.Instruction.Operand;
            var methodName = callee.Name;
            var declaringType = callee.DeclaringType;

            if (k_TypeHashCode != declaringType.FastFullName().GetHashCode())
                return;

            // second check on module name which requires resolving the type
            try
            {
                var typeDefinition = declaringType.Resolve();
                if (typeDefinition == null)
                {
                    Debug.LogWarning(declaringType.FullName + " could not be resolved.");
                    return;
                }

                if (k_ModuleHashCode != typeDefinition.Module.Name.GetHashCode())
                    return;
            }
            catch (AssemblyResolutionException e)
            {
                Debug.LogWarningFormat("Could not resolve {0}: {1}", declaringType.Name, e.Message);
            }

            // If we find the ConditionalAttribute, we assume this is intended to be compiled out on release
            if (context.MethodDefinition.HasCustomAttributes && context.MethodDefinition.CustomAttributes.Any(a =>
                a.AttributeType.FullName.GetHashCode() == k_ConditionalAttributeHashCode))
            {
                return;
            }

            switch (methodName)
            {
                case nameof(Debug.Log):
                case nameof(Debug.LogFormat):
                    if (!context.IsDescriptorEnabled(DebugLogIssueDescriptor))
                        return;
                    var logDiagnostic = Diagnostic.Create(AnalysisCategory.Code, DebugLogIssueDescriptor.Id, methodName, context.MethodDefinition.Name);
                    context.ReportIssue(logDiagnostic);
                    return;
                case nameof(Debug.LogWarning):
                case nameof(Debug.LogWarningFormat):
                    if (!context.IsDescriptorEnabled(DebugLogWarningIssueDescriptor))
                        return;
                    var warningDiagnostic = Diagnostic.Create(AnalysisCategory.Code, DebugLogWarningIssueDescriptor.Id, methodName, context.MethodDefinition.Name);
                    context.ReportIssue(warningDiagnostic);
                    return;
            }
        }
    }
}
