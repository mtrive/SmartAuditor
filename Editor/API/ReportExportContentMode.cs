namespace SmartAuditor.Editor
{
    /// <summary>
    /// Controls which sections are exported when saving a report to JSON.
    /// </summary>
    public enum ReportExportContentMode
    {
        /// <summary>
        /// Exports the full report payload.
        /// </summary>
        Full = 0,

        /// <summary>
        /// Exports only the report version and issues array.
        /// </summary>
        IssuesOnly,

        /// <summary>
        /// Exports issues and a compact summary; insights can be enabled separately.
        /// </summary>
        IssuesPlusSummary
    }
}
