using System;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Exception thrown when attempting to load a report with an incompatible version.
    /// </summary>
    public class ReportVersionException : Exception
    {
        /// <summary>
        /// The version of the report file that was attempted to be loaded.
        /// </summary>
        public string ReportVersion { get; }

        /// <summary>
        /// The current supported version of the report format.
        /// </summary>
        public string CurrentVersion { get; }

        /// <summary>
        /// Initializes a new instance of the ReportVersionException class.
        /// </summary>
        /// <param name="reportVersion">The version of the report file</param>
        /// <param name="currentVersion">The current supported version</param>
        public ReportVersionException(string reportVersion, string currentVersion)
            : base($"Report version '{reportVersion}' is not supported. Current supported version is '{currentVersion}'. Please regenerate the report with the current version of {SmartAuditor.DisplayName}.")
        {
            ReportVersion = reportVersion;
            CurrentVersion = currentVersion;
        }

        /// <summary>
        /// Initializes a new instance of the ReportVersionException class with a custom message.
        /// </summary>
        /// <param name="reportVersion">The version of the report file</param>
        /// <param name="currentVersion">The current supported version</param>
        /// <param name="message">Custom error message</param>
        public ReportVersionException(string reportVersion, string currentVersion, string message)
            : base(message)
        {
            ReportVersion = reportVersion;
            CurrentVersion = currentVersion;
        }

        /// <summary>
        /// Initializes a new instance of the ReportVersionException class with a custom message and inner exception.
        /// </summary>
        /// <param name="reportVersion">The version of the report file</param>
        /// <param name="currentVersion">The current supported version</param>
        /// <param name="message">Custom error message</param>
        /// <param name="innerException">The inner exception</param>
        public ReportVersionException(string reportVersion, string currentVersion, string message, Exception innerException)
            : base(message, innerException)
        {
            ReportVersion = reportVersion;
            CurrentVersion = currentVersion;
        }
    }
}
