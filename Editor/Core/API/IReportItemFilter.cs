namespace SmartAuditor.Editor.Core
{
    internal interface IReportItemFilter
    {
        bool IsMatch(ReportItem issue);

        /// <summary>
        /// Native-<see cref="Message"/> counterpart to <see cref="IsMatch(ReportItem)"/>.
        /// Implementations route navigator search and (where relevant) per-view filtering
        /// against the message's Description, Location, and Properties without first
        /// adapting it back to a <see cref="ReportItem"/>.
        /// </summary>
        bool IsMatch(Message message);
    }
}
