using System;
using Mono.Cecil;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// Applies shared metadata (location, assembly name, evidence) to code diagnostics before
    /// they are converted to <see cref="ReportItem"/> instances.
    /// </summary>
    internal static class CodeDiagnosticEnricher
    {
        public static Func<Diagnostic, Diagnostic> CreateTypeLevelReporter(
            string assemblyName,
            Location fallbackLocation,
            Action<ReportItem> issueReporter)
        {
            return diagnostic =>
            {
                diagnostic = ApplyLocationFallback(diagnostic, fallbackLocation);
                diagnostic = diagnostic.WithProperty(CodeColumns.Assembly, assemblyName);
                issueReporter(diagnostic.ToReportItem());
                return diagnostic;
            };
        }

        public static Func<Diagnostic, Diagnostic> CreateMethodLevelReporter(
            string assemblyName,
            Location fallbackLocation,
            string containingMemberFullName,
            Action<ReportItem> issueReporter)
        {
            return diagnostic =>
            {
                diagnostic = ApplyLocationFallback(diagnostic, fallbackLocation);
                diagnostic = diagnostic.WithProperty(CodeColumns.Assembly, assemblyName);
                diagnostic = AttachContainingMemberEvidence(diagnostic, containingMemberFullName);
                issueReporter(diagnostic.ToReportItem());
                return diagnostic;
            };
        }

        public static Func<Diagnostic, Diagnostic> CreateInstructionLevelReporter(
            string assemblyName,
            Location resolvedLocation,
            CallTreeNode callerNode,
            string containingMemberFullName,
            MethodReference callee,
            Action<ReportItem> issueReporter)
        {
            return diagnostic =>
            {
                diagnostic = diagnostic
                    .WithDependencies(callerNode)
                    .WithLocation(resolvedLocation)
                    .WithProperty(CodeColumns.Assembly, assemblyName);
                diagnostic = AttachContainingMemberEvidence(diagnostic, containingMemberFullName);
                if (callee != null)
                    diagnostic = AttachApiEvidence(diagnostic, callee);
                issueReporter(diagnostic.ToReportItem());
                return diagnostic;
            };
        }

        public static Diagnostic EnrichCallGraphDiagnostic(Diagnostic diagnostic)
        {
            if (diagnostic.Dependencies is CallTreeNode node)
            {
                if (diagnostic.Properties == null)
                {
                    var assemblyName = StripDllExtension(node.AssemblyName);
                    if (!string.IsNullOrEmpty(assemblyName))
                        diagnostic = diagnostic.WithProperty(CodeColumns.Assembly, assemblyName);
                }
                diagnostic = AttachContainingMemberEvidence(diagnostic, node.MethodFullName);
            }
            return diagnostic;
        }

        static Diagnostic ApplyLocationFallback(Diagnostic diagnostic, Location fallbackLocation)
        {
            if (diagnostic.Location == null && fallbackLocation != null)
                diagnostic = diagnostic.WithLocation(fallbackLocation);
            return diagnostic;
        }

        static Diagnostic AttachContainingMemberEvidence(Diagnostic diagnostic, string containingMember)
        {
            return WithEvidenceIfAbsent(diagnostic, CodeEvidenceKeys.ContainingMember, containingMember);
        }

        static Diagnostic AttachApiEvidence(Diagnostic diagnostic, MethodReference callee)
        {
            if (callee?.DeclaringType == null)
                return diagnostic;

            diagnostic = WithEvidenceIfAbsent(diagnostic, CodeEvidenceKeys.ApiType, callee.DeclaringType.FullName);
            diagnostic = WithEvidenceIfAbsent(diagnostic, CodeEvidenceKeys.ApiMethod, callee.Name);
            return diagnostic;
        }

        static Diagnostic WithEvidenceIfAbsent(Diagnostic diagnostic, string key, object value)
        {
            if (diagnostic == null || string.IsNullOrEmpty(key) || value == null)
                return diagnostic;

            if (diagnostic.Evidence != null && diagnostic.Evidence.ContainsKey(key))
                return diagnostic;

            return diagnostic.WithEvidence(key, value);
        }

        static string StripDllExtension(string assemblyName)
        {
            const string dll = ".dll";
            if (string.IsNullOrEmpty(assemblyName))
                return assemblyName;
            if (assemblyName.EndsWith(dll, StringComparison.OrdinalIgnoreCase))
                return assemblyName.Substring(0, assemblyName.Length - dll.Length);
            return assemblyName;
        }
    }
}
