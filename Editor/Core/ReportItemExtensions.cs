using SmartAuditor.Editor.Utils;

namespace SmartAuditor.Editor.Core
{
    // Extension methods for ReportItems and IReportEntries which don't form part of the API:
    // used in UI and tests.
    internal static class ReportItemExtensions
    {
        internal const string k_NotAvailable = "N/A";

        /// <summary>
        /// Returns the value for <paramref name="propertyType"/> from any <see cref="IReportEntry"/>.
        /// Falls back to <see cref="GetProperty(ReportItem,PropertyType)"/> for <see cref="ReportItem"/>
        /// entries so the rich detail (Descriptor, Severity, etc.) is preserved for diagnostics.
        /// </summary>
        public static string GetProperty(IReportEntry entry, PropertyType propertyType)
        {
            if (entry == null)
                return string.Empty;

            if (entry is ReportItem issue)
                return GetProperty(issue, propertyType);

            return GetNonDiagnosticProperty(entry, propertyType);
        }

        public static string GetContext(this ReportItem issue)
        {
            if (issue.Dependencies == null)
                return issue.RelativePath;

            return issue.Dependencies.Name;
        }

        public static string GetProperty(this ReportItem issue, PropertyType propertyType)
        {
            switch (propertyType)
            {
                case PropertyType.Id:
                    return issue.Id;
                case PropertyType.LogLevel:
                    return issue.LogLevel.ToString();
                case PropertyType.Severity:
                    return issue.Severity.ToString();
                case PropertyType.Impact:
                    return issue.Descriptor.GetImpactSummary();
                case PropertyType.Description:
                    return issue.Description;
                case PropertyType.Descriptor:
                    return issue.Descriptor.Title;
                case PropertyType.Platform:
                    return issue.Descriptor.GetPlatformsSummary();
                case PropertyType.Path:
                    return FormatPathProperty(issue.Location);
                case PropertyType.Filename:
                    return FormatFilenameProperty(issue.Location);
                case PropertyType.Directory:
                    return FormatDirectoryProperty(issue.Location);
                case PropertyType.FileType:
                    return FormatFileTypeProperty(issue.Location);
                default:
                    return string.Empty;
            }
        }

        static string GetNonDiagnosticProperty(IReportEntry entry, PropertyType propertyType)
        {
            switch (propertyType)
            {
                case PropertyType.Description:
                    return entry.Description;
                case PropertyType.LogLevel:
                    return entry.LogLevel.ToString();
                case PropertyType.Path:
                    return FormatPathProperty(entry.Location);
                case PropertyType.Filename:
                    return FormatFilenameProperty(entry.Location);
                case PropertyType.Directory:
                    return FormatDirectoryProperty(entry.Location);
                case PropertyType.FileType:
                    return FormatFileTypeProperty(entry.Location);
                default:
                    return string.Empty;
            }
        }

        static string FormatPathProperty(Location location)
        {
            if (location == null || string.IsNullOrEmpty(location.Path))
                return k_NotAvailable;
            return Formatting.FormatPath(location.Path, location.Line, location.EndLine);
        }

        static string FormatFilenameProperty(Location location)
        {
            if (location == null || string.IsNullOrEmpty(location.Filename))
                return k_NotAvailable;
            return Formatting.FormatPath(location.Filename, location.Line, location.EndLine);
        }

        static string FormatDirectoryProperty(Location location)
        {
            if (location == null || string.IsNullOrEmpty(location.Path))
                return k_NotAvailable;
            return PathUtils.GetDirectoryName(location.Path);
        }

        static string FormatFileTypeProperty(Location location)
        {
            if (location == null || string.IsNullOrEmpty(location.Extension))
                return k_NotAvailable;
            return location.Extension;
        }
    }
}
