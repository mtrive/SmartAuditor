using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SmartAuditor.Editor;
using SmartAuditor.Editor.Utils;
using Formatting = Newtonsoft.Json.Formatting;

namespace SmartAuditor.Editor.Core.Serialization
{
    /// <summary>
    /// Handles serialization and deserialization of Report objects to/from JSON files.
    /// </summary>
    internal static class ReportSerializer
    {
        /// <summary>
        /// Returns the <see cref="JsonSerializerSettings"/> used when saving a report. Exposed so tests can mirror production behaviour without duplication.
        /// </summary>
        internal static JsonSerializerSettings CreateSaveSettings(
            bool debugReport = false,
            ReportExportContentMode exportContentMode = ReportExportContentMode.Full) => new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new DescriptorJsonConverter() },
            ContractResolver = new ReportContractResolver(debugReport, exportContentMode),
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// Returns the <see cref="JsonSerializerSettings"/> used when loading a report. Exposed so tests can mirror production behaviour without duplication.
        /// </summary>
        internal static JsonSerializerSettings CreateLoadSettings() => new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        static void ValidateAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path), "Path cannot be null or empty");

            if (!Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
                throw new ArgumentException("Path must be absolute or fully qualified", nameof(path));
        }

        /// <summary>
        /// Compares two version strings to determine if the first is older than the second.
        /// </summary>
        /// <param name="version1">First version to compare</param>
        /// <param name="version2">Second version to compare</param>
        /// <returns>True if version1 is older than version2, false otherwise</returns>
        static bool IsVersionOlder(string version1, string version2)
        {
            if (string.IsNullOrEmpty(version1) || string.IsNullOrEmpty(version2))
                return false;

            try
            {
                var v1 = new Version(version1);
                var v2 = new Version(version2);
                return v1 < v2;
            }
            catch (ArgumentException)
            {
                // If version parsing fails, assume versions are incompatible
                return true;
            }
        }

        /// <summary>
        /// Validates that the report version is compatible with the current version.
        /// </summary>
        /// <param name="reportVersion">The version of the report to validate</param>
        /// <param name="currentVersion">The current supported version</param>
        /// <exception cref="ReportVersionException">Thrown when the report version is incompatible</exception>
        static void ValidateReportVersion(string reportVersion, string currentVersion)
        {
            // Handle null or empty version gracefully
            if (string.IsNullOrEmpty(reportVersion))
            {
                throw new ReportVersionException("Unknown", currentVersion,
                    $"Report version is missing or invalid. Please regenerate the report with the current version of {SmartAuditor.DisplayName}.");
            }

            if (IsVersionOlder(reportVersion, currentVersion))
            {
                throw new ReportVersionException(reportVersion, currentVersion);
            }
        }

        /// <summary>
        /// Load a Report from a JSON file at the specified path.
        /// </summary>
        /// <param name="path">File path of the report to load</param>
        /// <param name="currentVersion">The current supported version for validation</param>
        /// <returns>A loaded Report object</returns>
        /// <exception cref="ArgumentNullException">Thrown when path is null or empty</exception>
        /// <exception cref="ArgumentException">Thrown when path is invalid</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied</exception>
        /// <exception cref="JsonException">Thrown when the file contains invalid JSON</exception>
        /// <exception cref="ReportVersionException">Thrown when the report version is older than the current supported version</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs</exception>
        public static Report Load(string path, string currentVersion)
        {
            ValidateAbsolutePath(path);

            if (!File.Exists(path))
                throw new FileNotFoundException($"Report file not found: {path}", path);

            try
            {
                var jsonContent = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(jsonContent))
                    throw new InvalidDataException("Report file is empty or contains only whitespace");

                // First, deserialize to get the version for validation
                var report = JsonConvert.DeserializeObject<Report>(jsonContent, CreateLoadSettings());

                // Validate the report version before returning
                ValidateReportVersion(report.Version, currentVersion);

                return report;
            }
            catch (JsonException)
            {
                throw; // Re-throw JSON exceptions as-is
            }
            catch (UnauthorizedAccessException)
            {
                throw; // Re-throw access exceptions as-is
            }
            catch (IOException)
            {
                throw; // Re-throw I/O exceptions as-is
            }
            catch (InvalidOperationException ex)
            {
                throw new JsonException($"Failed to deserialize report due to data incompatibility: {ex.Message}. This may be due to an older report format. Please regenerate the report.", ex);
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is FileNotFoundException || ex is InvalidDataException || ex is JsonException || ex is UnauthorizedAccessException || ex is IOException || ex is ReportVersionException))
            {
                throw new IOException($"Failed to read report file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Load a Report from a JSON file at the specified path asynchronously.
        /// </summary>
        /// <param name="path">File path of the report to load</param>
        /// <param name="currentVersion">The current supported version for validation</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A Task containing the loaded Report object</returns>
        /// <exception cref="ArgumentNullException">Thrown when path is null or empty</exception>
        /// <exception cref="ArgumentException">Thrown when path is invalid</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied</exception>
        /// <exception cref="JsonException">Thrown when the file contains invalid JSON</exception>
        /// <exception cref="ReportVersionException">Thrown when the report version is older than the current supported version</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled</exception>
        public static async Task<Report> LoadAsync(string path, string currentVersion, CancellationToken cancellationToken = default)
        {
            ValidateAbsolutePath(path);

            if (!File.Exists(path))
                throw new FileNotFoundException($"Report file not found: {path}", path);

            try
            {
                var jsonContent = await File.ReadAllTextAsync(path, cancellationToken);
                if (string.IsNullOrWhiteSpace(jsonContent))
                    throw new InvalidDataException("Report file is empty or contains only whitespace");

                // First, deserialize to get the version for validation
                var report = JsonConvert.DeserializeObject<Report>(jsonContent, CreateLoadSettings());

                // Validate the report version before returning
                ValidateReportVersion(report.Version, currentVersion);

                return report;
            }
            catch (JsonException)
            {
                throw; // Re-throw JSON exceptions as-is
            }
            catch (UnauthorizedAccessException)
            {
                throw; // Re-throw access exceptions as-is
            }
            catch (IOException)
            {
                throw; // Re-throw I/O exceptions as-is
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation exceptions as-is
            }
            catch (InvalidOperationException ex)
            {
                throw new JsonException($"Failed to deserialize report due to data incompatibility: {ex.Message}. This may be due to an older report format. Please regenerate the report.", ex);
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is FileNotFoundException || ex is InvalidDataException || ex is JsonException || ex is UnauthorizedAccessException || ex is IOException || ex is ReportVersionException))
            {
                throw new IOException($"Failed to read report file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Save the Report as a JSON file.
        /// </summary>
        /// <param name="report">The Report to save</param>
        /// <param name="path">The file path at which to save the file</param>
        /// <param name="prettyPrint">Whether the JSON report should be formatted for readability.</param>
        /// <param name="debugReport">Whether the JSON report should include Smart Auditor debug metadata.</param>
        /// <exception cref="ArgumentNullException">Thrown when path is null or empty</exception>
        /// <exception cref="ArgumentException">Thrown when path is invalid</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the directory is denied</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the directory does not exist and cannot be created</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs</exception>
        public static void Save(Report report,
            string path,
            bool prettyPrint = false,
            bool debugReport = false,
            ReportExportContentMode exportContentMode = ReportExportContentMode.Full,
            Severity minSaveSeverity = Severity.Default)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            ValidateAbsolutePath(path);

            report.DebugReportSerialization = debugReport;
            report.SerializationMinSeverity = minSaveSeverity == Severity.Default ? (Severity?)null : minSaveSeverity;
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // Validate we can write to the location
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    throw new UnauthorizedAccessException($"Cannot write to read-only file: {path}");

                var jsonContent = JsonConvert.SerializeObject(report,
                    prettyPrint ? Formatting.Indented : Formatting.None,
                    CreateSaveSettings(debugReport, exportContentMode));

                if (string.IsNullOrWhiteSpace(jsonContent))
                    throw new InvalidOperationException("Serialized report content is empty");

                File.WriteAllText(path, jsonContent);
            }
            catch (JsonException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (DirectoryNotFoundException)
            {
                throw;
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is InvalidOperationException))
            {
                throw new IOException($"Failed to save report file: {ex.Message}", ex);
            }
            finally
            {
                report.DebugReportSerialization = false;
                report.SerializationMinSeverity = null;
            }
        }

        /// <summary>
        /// Save the Report as a JSON file asynchronously.
        /// </summary>
        /// <param name="report">The Report to save</param>
        /// <param name="path">The file path at which to save the file</param>
        /// <param name="prettyPrint">Whether the JSON report should be formatted for readability.</param>
        /// <param name="debugReport">Whether the JSON report should include Smart Auditor debug metadata.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A Task representing the asynchronous save operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when path is null or empty</exception>
        /// <exception cref="ArgumentException">Thrown when path is invalid</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the directory is denied</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the directory does not exist and cannot be created</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled</exception>
        public static async Task SaveAsync(Report report,
            string path,
            bool prettyPrint = false,
            bool debugReport = false,
            ReportExportContentMode exportContentMode = ReportExportContentMode.Full,
            Severity minSaveSeverity = Severity.Default,
            CancellationToken cancellationToken = default)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            ValidateAbsolutePath(path);

            report.DebugReportSerialization = debugReport;
            report.SerializationMinSeverity = minSaveSeverity == Severity.Default ? (Severity?)null : minSaveSeverity;
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // Validate we can write to the location
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    throw new UnauthorizedAccessException($"Cannot write to read-only file: {path}");

                var jsonContent = JsonConvert.SerializeObject(report,
                    prettyPrint ? Formatting.Indented : Formatting.None,
                    CreateSaveSettings(debugReport, exportContentMode));

                if (string.IsNullOrWhiteSpace(jsonContent))
                    throw new InvalidOperationException("Serialized report content is empty");

                await File.WriteAllTextAsync(path, jsonContent, cancellationToken);
            }
            catch (JsonException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (DirectoryNotFoundException)
            {
                throw;
            }
            catch (IOException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is InvalidOperationException))
            {
                throw new IOException($"Failed to save report file: {ex.Message}", ex);
            }
            finally
            {
                report.DebugReportSerialization = false;
                report.SerializationMinSeverity = null;
            }
        }

        public static async Task SaveAsync(Report report, string path, bool prettyPrint, CancellationToken cancellationToken)
        {
            await SaveAsync(report,
                path,
                prettyPrint,
                debugReport: false,
                exportContentMode: ReportExportContentMode.Full,
                cancellationToken: cancellationToken);
        }

        sealed class ReportContractResolver : DefaultContractResolver
        {
            const string k_ModuleInfoTypeName = "SmartAuditor.Editor.Report+ModuleInfo";

            const string k_ReportTypeName = "SmartAuditor.Editor.Report";

            readonly bool m_DebugReport;
            readonly ReportExportContentMode m_ExportContentMode;

            public ReportContractResolver(bool debugReport, ReportExportContentMode exportContentMode)
            {
                m_DebugReport = debugReport;
                m_ExportContentMode = exportContentMode;
            }

            protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                if (member.DeclaringType?.FullName == k_ReportTypeName)
                {
                    ApplyReportModeSerializationRules(property);
                }

                if (!m_DebugReport &&
                    member.DeclaringType?.FullName == k_ModuleInfoTypeName &&
                    property.UnderlyingName == "Layouts")
                {
                    property.ShouldSerialize = _ => false;
                }
                else if (!m_DebugReport &&
                         member.DeclaringType == typeof(ReportItem) &&
                         property.UnderlyingName == nameof(ReportItem.FingerprintParts))
                {
                    property.ShouldSerialize = _ => false;
                }

                return property;
            }

            void ApplyReportModeSerializationRules(JsonProperty property)
            {
                switch (m_ExportContentMode)
                {
                    case ReportExportContentMode.IssuesOnly:
                        property.ShouldSerialize = _ =>
                            property.PropertyName == "version" ||
                            property.PropertyName == "issues" ||
                            property.PropertyName == "messages" ||
                            property.PropertyName == "descriptors" ||
                            property.PropertyName == "sessionInfo";
                        break;
                    case ReportExportContentMode.IssuesPlusSummary:
                        property.ShouldSerialize = _ =>
                            property.PropertyName == "version" ||
                            property.PropertyName == "issues" ||
                            property.PropertyName == "messages" ||
                            property.PropertyName == "descriptors" ||
                            property.PropertyName == "sessionInfo" ||
                            property.PropertyName == "summary";
                        break;
                    case ReportExportContentMode.Full:
                    default:
                        if (property.PropertyName == "summary")
                        {
                            property.ShouldSerialize = _ => false;
                        }
                        break;
                }
            }
        }
    }
}
