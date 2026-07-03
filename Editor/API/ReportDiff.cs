// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Contains the differences between two reports
    /// </summary>
    public class ReportDiff
    {
        /// <summary>
        /// Issues that are present in the new report but not in the old one
        /// </summary>
        public IReadOnlyList<ReportItem> AddedIssues { get; }

        /// <summary>
        /// Issues that were present in the old report but not in the new one
        /// </summary>
        public IReadOnlyList<ReportItem> RemovedIssues { get; }

        /// <summary>
        /// Total number of differences (added + removed issues)
        /// </summary>
        public int TotalDifferences => AddedIssues.Count + RemovedIssues.Count;

        /// <summary>
        /// Whether there are any differences between the reports
        /// </summary>
        public bool HasDifferences => TotalDifferences > 0;

        internal ReportDiff(IReadOnlyList<ReportItem> added, IReadOnlyList<ReportItem> removed)
        {
            AddedIssues = added ?? throw new ArgumentNullException(nameof(added));
            RemovedIssues = removed ?? throw new ArgumentNullException(nameof(removed));
        }

        /// <summary>
        /// Gets a summary of the differences between reports
        /// </summary>
        public string GetSummary()
        {
            if (!HasDifferences)
                return "No differences found between reports.";

            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"Found {TotalDifferences} differences:");

            if (AddedIssues.Count > 0)
            {
                summary.AppendLine($"- {AddedIssues.Count} new issues:");
                var addedByCategory = AddedIssues.GroupBy(i => i.Category)
                    .Select(g => $"  • {g.Count()} in {g.Key}");
                summary.AppendLine(string.Join("\n", addedByCategory));
            }

            if (RemovedIssues.Count > 0)
            {
                summary.AppendLine($"- {RemovedIssues.Count} resolved issues:");
                var removedByCategory = RemovedIssues.GroupBy(i => i.Category)
                    .Select(g => $"  • {g.Count()} in {g.Key}");
                summary.AppendLine(string.Join("\n", removedByCategory));
            }

            return summary.ToString();
        }
    }
}
