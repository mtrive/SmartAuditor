// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// Optional capability for code analyzers that need access to the assembled call graph
    /// after per-method analysis is complete.
    /// </summary>
    /// <remarks>
    /// The pipeline runs an <see cref="Analyze"/> pass once after every assembly has been
    /// scanned and the per-callee buckets have been built. Implementations can use the
    /// supplied <see cref="CallGraphAnalysisContext.CallCrawler"/> to ask reachability
    /// questions across method boundaries (e.g. "is this call site reachable from a method
    /// that satisfies a given predicate?") and use
    /// <see cref="CallGraphAnalysisContext.RemoveIssue"/> together with
    /// <see cref="Core.AnalysisContext.ReportIssue"/> to re-tag diagnostics that depend on
    /// such cross-method context.
    /// </remarks>
    interface ICallGraphCodeAnalyzer
    {
        void Analyze(CallGraphAnalysisContext context);
    }
}
