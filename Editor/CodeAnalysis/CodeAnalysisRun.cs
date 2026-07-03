using System;
using System.Collections.Generic;
using System.Threading;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// Per-analysis-run context threaded through the IL pipeline. Bundles the analyzer plan,
    /// options, output sinks, and cross-cutting runtime state.
    /// </summary>
    internal sealed class CodeAnalysisRun
    {
        public CodeAnalysisPlan Plan { get; }
        public AnalysisOptions Options { get; }
        public IFindingSink Sink { get; }
        public IProgress Progress { get; }
        public CancellationToken CancellationToken { get; }
        public CallCrawler CallCrawler { get; } = new CallCrawler();
        public List<ReportItem> FoundIssues { get; } = new List<ReportItem>();

        /// <summary>
        /// Per-batch diagnostic sink. Set before each assembly walk; <c>null</c> disables
        /// issue reporting while call-graph crawling continues.
        /// </summary>
        public Action<ReportItem> IssueReporter { get; set; }

        public CodeAnalysisRun(
            CodeAnalysisPlan plan,
            AnalysisOptions options,
            IFindingSink sink,
            IProgress progress,
            CancellationToken cancellationToken)
        {
            Plan = plan;
            Options = options;
            Sink = sink;
            Progress = progress;
            CancellationToken = cancellationToken;
        }

        public void RecordCall(CallInfo callInfo) => CallCrawler.Add(callInfo);
    }
}
