// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// Context passed to <see cref="ICallGraphCodeAnalyzer.Analyze"/>. Exposes the populated
    /// call graph and a curated view of the issues already collected this run, plus a
    /// removal hook for analyzers that need to drop earlier diagnostics they are about to
    /// supersede.
    /// </summary>
    /// <remarks>
    /// Inherits from <see cref="AnalysisContext"/> so analyzers report new diagnostics with
    /// the same <c>Diagnostic.Create(...)</c> + <c>ReportIssue</c> pattern they already use
    /// for per-instruction analysis. The pipeline wires
    /// <see cref="AnalysisContext.OnIssueCreated"/> to enrich and route reported items into
    /// the run's accumulated issue list.
    /// </remarks>
    public sealed class CallGraphAnalysisContext : AnalysisContext
    {
        public CallGraphAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        Action<ReportItem> m_RemoveIssue;

        /// <summary>
        /// Call graph populated from every analysed assembly (editor and runtime).
        /// </summary>
        public CallCrawler CallCrawler { get; private set; }

        /// <summary>
        /// Read-only view of the issues collected so far during this analysis run. Use this
        /// to find earlier diagnostics that should be removed in favour of a newly emitted
        /// one (call <see cref="RemoveIssue"/> with the matched item).
        /// </summary>
        public IReadOnlyList<ReportItem> Issues { get; private set; }

        /// <summary>
        /// Removes <paramref name="issue"/> from the accumulated issue list. Used when an
        /// analyzer's call-graph pass supersedes an earlier diagnostic at the same call site.
        /// </summary>
        /// <param name="issue">The issue to remove. Must be a reference held in
        /// <see cref="Issues"/>.</param>
        public void RemoveIssue(ReportItem issue)
        {
            if (issue == null)
            {
                throw new ArgumentNullException(nameof(issue));
            }
            m_RemoveIssue?.Invoke(issue);
        }

        internal void Configure(CallCrawler callCrawler, IReadOnlyList<ReportItem> issues, Action<ReportItem> removeIssue)
        {
            CallCrawler = callCrawler;
            Issues = issues;
            m_RemoveIssue = removeIssue;
        }
    }
}
