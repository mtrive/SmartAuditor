// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Encapsulates the context for a single module's execution during an analysis run.
    /// Created by the orchestrator and passed to each <see cref="AnalysisModule"/>; modules use it
    /// to access analysis options, report progress, and emit findings.
    /// </summary>
    internal sealed class AnalysisSession : IFindingSink
    {
        readonly System.Action<IEnumerable<ReportItem>> m_OnItemsReported;
        readonly System.Action<InsightTable> m_OnInsightTableReported;
        readonly System.Action<Message> m_OnMessageReported;

        /// <summary>The options that govern this analysis run.</summary>
        public AnalysisOptions Options { get; }

        /// <summary>The progress reporter, or <c>null</c> if no progress UI is active.</summary>
        public IProgress Progress { get; }

        internal AnalysisSession(
            AnalysisOptions options,
            IProgress progress,
            System.Action<IEnumerable<ReportItem>> onItemsReported,
            System.Action<InsightTable> onInsightTableReported = null,
            System.Action<Message> onMessageReported = null)
        {
            Options = options;
            Progress = progress;
            m_OnItemsReported = onItemsReported;
            m_OnInsightTableReported = onInsightTableReported;
            m_OnMessageReported = onMessageReported;
        }

        /// <summary>
        /// Forwards a batch of report items through the orchestrator's filtering and reporting pipeline.
        /// </summary>
        public void ReportItems(IEnumerable<ReportItem> items) => m_OnItemsReported?.Invoke(items);

        /// <summary>Forwards a single insight table to the report.</summary>
        public void ReportInsightTable(InsightTable table) => m_OnInsightTableReported?.Invoke(table);

        /// <summary>Forwards a single toolchain message to the report.</summary>
        public void ReportMessage(Message message) => m_OnMessageReported?.Invoke(message);
    }
}
