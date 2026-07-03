// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Destination for findings produced by an <see cref="AnalysisContext"/> when it flushes.
    /// One sink per context, set at construction. The three channels — report items, insight
    /// tables, and toolchain messages — always travel together; representing them as a single
    /// interface makes it structurally impossible to wire one and forget another.
    /// </summary>
    public interface IFindingSink
    {
        void ReportItems(IEnumerable<ReportItem> items);
        void ReportInsightTable(InsightTable table);
        void ReportMessage(Message message);
    }
}
