using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Core.Serialization;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Report contains all items produced by an analysis run: issues, insights, and toolchain messages.
    /// </summary>
    public sealed class Report
    {
        const string k_CurrentVersion = "0.1.0";

        internal static string CurrentVersion => k_CurrentVersion;

        [JsonProperty("version")]
        [SerializeField]
        string m_Version = k_CurrentVersion;

        /// <summary>
        /// File format version of the Report (read-only).
        /// </summary>
        [JsonIgnore]
        public string Version
        {
            get => m_Version;
            internal set => m_Version = value;
        }

        [Serializable]
        class ModuleInfo
        {
            public string Name;

            // this is used by HasCategory
            public AnalysisCategory[] Categories;

            public string StartTime;
            public string EndTime;

            public AnalysisResult Result;
        }

        /// <summary>
        /// Contains information about the session in which this Report was created.
        /// </summary>
        [JsonProperty("sessionInfo")]
        public SessionInfo SessionInfo;

        /// <summary>
        /// A name to display along with the Report, configurable by the user.
        /// </summary>
        public string DisplayName;

        [JsonProperty("moduleMetadata")]
        [SerializeField]
        List<ModuleInfo> m_ModuleInfos = new List<ModuleInfo>();

        [JsonIgnore]
        [SerializeField]
        DescriptorLibrary m_DescriptorLibrary = new DescriptorLibrary();

        [JsonIgnore]
        [SerializeField]
        List<ReportItem> m_Issues = new List<ReportItem>();

        // Per-category insight tables: the canonical store for tabular insight data. Populated
        // via AddInsightTable and serialised as the "insights" JSON section.
        [JsonIgnore]
        Dictionary<AnalysisCategory, InsightTable> m_InsightTables = new Dictionary<AnalysisCategory, InsightTable>();

        // Toolchain messages (compiler diagnostics, asset-importer warnings). Populated via
        // AddMessage and serialised as the "messages" JSON section.
        [JsonIgnore]
        List<Message> m_Messages = new List<Message>();

        // Set by ReportSerializer before serialization to control which descriptors are written.
        [JsonIgnore]
        internal bool DebugReportSerialization { get; set; }

        // When non-null, the Issues / Insights / Messages getters drop items
        // below this severity during serialization. Set by ReportSerializer.Save
        // before writing and cleared in finally; the live in-memory Report stays
        // intact so the open SmartAuditor window keeps showing every item.
        [JsonIgnore]
        internal Severity? SerializationMinSeverity { get; set; }

        static Mutex s_Mutex = new Mutex();

        [JsonProperty("issues")]
        public ReportItem[] Issues
        {
            get
            {
                if (m_Issues == null)
                    return null;

                return m_Issues
                    .Where(PassesSaveFilter)
                    .ToArray();
            }
            set => m_Issues.AddRange(value);
        }

        bool PassesSaveFilter(ReportItem item)
        {
            return ReportFilter.Includes(item.Severity, SerializationMinSeverity ?? Severity.Default);
        }

        [JsonProperty("descriptors")]
        internal List<Descriptor> Descriptors
        {
            get
            {
                var issueIds = DebugReportSerialization
                    ? null
                    : m_Issues.Select(i => i.Id);
                return DescriptorLibrary.GetDescriptors(issueIds);
            }
            set
            {
                m_DescriptorLibrary.m_SerializedDescriptors = value;
                m_DescriptorLibrary.OnAfterDeserialize();

                // "issues" is serialised before "descriptors" in the JSON, so each
                // ReportItem's SetDescriptorId ran while s_Descriptors was still empty
                // and fell back to an empty placeholder Descriptor. Now that
                // s_Descriptors is populated, back-fill the reference on every loaded
                // issue so Descriptor.IsValid() returns true for display filtering.
                if (m_Issues != null)
                {
                    foreach (var issue in m_Issues)
                    {
                        if (issue != null &&
                            !string.IsNullOrEmpty(issue.Id) &&
                            DescriptorLibrary.TryGetDescriptor(issue.Id, out var descriptor))
                        {
                            issue.Descriptor = descriptor;
                        }
                    }
                }
            }
        }

        [JsonProperty("summary")]
        internal ReportExportSummary Summary => BuildSummary();

        /// <summary>
        /// The total number of diagnostic issues in this report.
        /// </summary>
        [JsonIgnore]
        public int IssueCount => m_Issues.Count;

        // for serialization purposes only
        internal Report() { }

        // for internal use only
        internal Report(AnalysisOptions options)
        {
            var compilation = SmartAuditorSettings.instance.Compilation;
            var validDefines = compilation?.GetValidDefines();
            var validRemovedDefines = compilation?.GetValidRemovedDefines();

            SessionInfo = new SessionInfo(options)
            {
                SmartAuditorVersion = PackageInfo.Version,

                ProjectId = Application.cloudProjectId,
                ProductName = Application.productName,
                ProjectVersion = Application.version,
                VersionControlRevision = VersionControlUtil.GetCurrentRevision(),
                CompanyName = Application.companyName,
                UnityVersion = Application.unityVersion,

                DateTime = Utils.Json.SerializeDateTime(DateTime.Now),
                HostPlatform = SystemInfo.operatingSystem,

                AdditionalDefines = validDefines != null ? validDefines.ToArray() : Array.Empty<string>(),
                RemovedDefines = validRemovedDefines != null ? validRemovedDefines.ToArray() : Array.Empty<string>(),
                GlobalDefines = ReadGlobalDefines(),
                RoslynAnalyzerDllPaths = ReadRoslynAnalyzerDllPaths()
            };
        }

        // Roslyn analyzer / source-generator DLLs Unity discovers via the "RoslynAnalyzer"
        // asset label. They're build-time tooling (routed via per-assembly
        // compilerOptions.RoslynAnalyzerDllPaths), not assemblies linked into the player —
        // so they belong with the rest of the toolchain context on SessionInfo, not in the
        // PrecompiledAssembly insight category.
        static string[] ReadRoslynAnalyzerDllPaths()
        {
            return AssetDatabase.FindAssets("l:RoslynAnalyzer")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        // Defines considered "global" are those present in EVERY player assembly.
        // The intersection across CompilationPipeline.GetAssemblies(...) gives us
        // Unity built-ins (UNITY_2026_1_OR_NEWER, UNITY_STANDALONE_WIN, ...) and
        // Project Settings > Player > Scripting Define Symbols, while excluding
        // package versionDefines (BURST_PRESENT, UNITY_RENDER_PIPELINES_*, ...)
        // which only land in asmdefs that depend on the package, and excluding
        // asmdef-specific defines.
        //
        // Dev-only flags are dropped to match RoslynDefineResolver's release-player
        // semantics -- the report represents a non-development build by default,
        // even if the active build target has Development Build ticked.
        static readonly HashSet<string> s_DevOnlyDefines = new HashSet<string>(StringComparer.Ordinal)
        {
            "DEVELOPMENT_BUILD",
            "ENABLE_PROFILER",
            "DEBUG",
            "TRACE",
            "UNITY_ASSERTIONS",
        };

        static string[] ReadGlobalDefines()
        {
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            if (assemblies == null || assemblies.Length == 0)
                return Array.Empty<string>();

            HashSet<string> intersection = null;
            foreach (var asm in assemblies)
            {
                var defs = asm.defines;
                if (defs == null || defs.Length == 0)
                    return Array.Empty<string>(); // any empty asmdef breaks the intersection

                var current = new HashSet<string>(defs, StringComparer.Ordinal);
                if (intersection == null)
                    intersection = current;
                else
                    intersection.IntersectWith(current);

                if (intersection.Count == 0)
                    break;
            }

            if (intersection == null || intersection.Count == 0)
                return Array.Empty<string>();

            return intersection
                .Where(d => !string.IsNullOrEmpty(d) && !s_DevOnlyDefines.Contains(d))
                .OrderBy(d => d, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Checks whether the Report includes analysis for a given <see cref="AnalysisCategory"/>.
        /// </summary>
        /// <param name="category">The <see cref="AnalysisCategory"/> to check</param>
        /// <returns>True if analysis ran one or more modules that report issues of the specified <see cref="AnalysisCategory"/>. Otherwise, returns false.</returns>
        public bool HasCategory(AnalysisCategory category)
        {
            return m_ModuleInfos.Any(m => m.Categories.Contains(category));
        }

        /// <summary>
        /// Gets the analysis result for the module that handles the given category.
        /// </summary>
        /// <param name="category">The <see cref="AnalysisCategory"/> to look up</param>
        /// <returns>The AnalysisResult if the category was analyzed, or null if not.</returns>
        internal AnalysisResult? GetCategoryResult(AnalysisCategory category)
        {
            var moduleInfo = m_ModuleInfos.FirstOrDefault(m => m.Categories.Contains(category));
            return moduleInfo?.Result;
        }

        /// <summary>
        /// Module/result pairs for every module that ran, in execution order. Lets bridge / CLI
        /// consumers distinguish a clean scope from one where a module aborted (for example a code
        /// module that failed to compile an assembly and so produced no findings).
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, AnalysisResult>> GetModuleResults()
        {
            return m_ModuleInfos
                .Select(m => new KeyValuePair<string, AnalysisResult>(m.Name, m.Result))
                .ToList();
        }

        /// <summary>True when any module finished with <see cref="AnalysisResult.Failure"/>.</summary>
        [JsonIgnore]
        public bool HasModuleFailures => m_ModuleInfos.Any(m => m.Result == AnalysisResult.Failure);

        /// <summary>
        /// Gets a read-only collection of all diagnostic issues in the report.
        /// </summary>
        /// <returns>All diagnostic issues in the report.</returns>
        public IReadOnlyCollection<ReportItem> GetIssues()
        {
            s_Mutex.WaitOne();
            var result = m_Issues.ToArray();
            s_Mutex.ReleaseMutex();
            return result;
        }

        /// <summary>
        /// Returns the number of diagnostic issues in the specified category.
        /// </summary>
        /// <remarks>
        /// Only queries <c>m_Issues</c>. Passing a message category is a programming error
        /// (messages live in a separate store); use <see cref="FindMessagesByCategory"/> instead.
        /// Insight categories return 0 — insight rows are in <see cref="InsightTables"/>.
        /// </remarks>
        /// <param name="category">The issue category to count.</param>
        /// <returns>Number of diagnostic issues in that category.</returns>
        internal int GetIssueCount(AnalysisCategory category)
        {
            Debug.Assert(!category.IsMessageCategory(),
                $"GetIssueCount called with message category '{category}'. Use FindMessagesByCategory instead.");
            s_Mutex.WaitOne();
            var result = m_Issues.Count(i => i.Category == category);
            s_Mutex.ReleaseMutex();
            return result;
        }

        /// <summary>
        /// Find all diagnostic issues for a specific category.
        /// </summary>
        /// <remarks>
        /// Returns diagnostic issues only — toolchain messages live in a separate store.
        /// For messages use <see cref="FindMessagesByCategory"/> (native <see cref="Message"/>).
        /// Passing a message category here returns empty.
        /// </remarks>
        /// <param name="category">The issue category to query.</param>
        /// <returns>Array of issues in that category.</returns>
        public IReadOnlyCollection<ReportItem> FindByCategory(AnalysisCategory category)
        {
            s_Mutex.WaitOne();
            try
            {
                return m_Issues.Where(i => i.Category == category).ToArray();
            }
            finally
            {
                s_Mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Find all toolchain <see cref="Message"/>s for a specific message category.
        /// Use this from new code that wants the native message shape (LogLevel, properties
        /// dictionary, location). Pairs with <see cref="FindByCategory"/>, which exposes the
        /// same items as <see cref="ReportItem"/> for legacy consumers.
        /// </summary>
        /// <param name="category">A category for which <c>IsMessageCategory()</c> is true.</param>
        /// <returns>All messages in that category. Empty when called with a non-message category.</returns>
        public IReadOnlyList<Message> FindMessagesByCategory(AnalysisCategory category)
        {
            s_Mutex.WaitOne();
            try
            {
                var result = new List<Message>();
                foreach (var message in m_Messages)
                {
                    if (message.Category == category)
                        result.Add(message);
                }
                return result;
            }
            finally
            {
                s_Mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Find all Issues that match a specific ID.
        /// </summary>
        /// <param name="id"> Desired Descriptor ID</param>
        /// <returns> Array of project issues</returns>
        public IReadOnlyCollection<ReportItem> FindByDescriptorId(string id)
        {
            s_Mutex.WaitOne();
            var result = m_Issues.Where(i => i.Id.Equals(id)).ToArray();
            s_Mutex.ReleaseMutex();
            return result;
        }

        /// <summary>
        /// Clears all issues that match the specified <see cref="AnalysisCategory"/> from the report.
        /// </summary>
        /// <param name="category">The <see cref="AnalysisCategory"/> of the issues to remove.</param>
        public void ClearIssues(AnalysisCategory category)
        {
            s_Mutex.WaitOne();
            m_Issues.RemoveAll(issue => issue.Category == category);
            foreach (var info in m_ModuleInfos)
            {
                var categories = info.Categories.ToList();
                categories.RemoveAll(c => c == category);
                info.Categories = categories.ToArray();
            }
            m_ModuleInfos.RemoveAll(info => info.Categories.Length == 0);
            s_Mutex.ReleaseMutex();
        }

        /// <summary>
        /// Check whether all issues in the report are valid.
        /// </summary>
        /// <returns>True is none of the issues in the report have a null description string. Otherwise returns false.</returns>
        public bool IsValid()
        {
            // for the time being we can only support reports of the same version
            if (!m_Version.Equals(k_CurrentVersion))
                return false;
            if (m_ModuleInfos.Count == 0)
                return false;
            return m_Issues.All(i => i.IsValid()) && m_ModuleInfos.All(m => m.Result != AnalysisResult.Cancelled);
        }

        /// <summary>
        /// Load a Report from a JSON file at the specified path.
        /// </summary>
        /// <param name="path">File path of the report to load</param>
        /// <returns>A loaded Report object</returns>
        /// <exception cref="ArgumentNullException">Thrown when path is null or empty</exception>
        /// <exception cref="ArgumentException">Thrown when path is invalid</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied</exception>
        /// <exception cref="JsonException">Thrown when the file contains invalid JSON</exception>
        /// <exception cref="ReportVersionException">Thrown when the report version is older than the current supported version</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs</exception>
        public static Report Load(string path)
        {
            return ReportSerializer.Load(path, k_CurrentVersion);
        }

        /// <summary>
        /// Load a Report from a JSON file at the specified path asynchronously.
        /// </summary>
        /// <param name="path">File path of the report to load</param>
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
        public static async Task<Report> LoadAsync(string path, CancellationToken cancellationToken = default)
        {
            return await ReportSerializer.LoadAsync(path, k_CurrentVersion, cancellationToken);
        }

        /// <summary>
        /// Save the Report as a JSON file.
        /// </summary>
        /// <param name="path">The file path at which to save the file</param>
        /// <param name="prettyPrint">Whether the JSON report should be formatted for readability.</param>
        /// <param name="debugReport">Whether the JSON report should include Smart Auditor debug metadata.</param>
        /// <param name="exportContentMode">Controls which report sections are written to the JSON file.</param>
        /// <param name="minSaveSeverity">Optional override for the minimum severity included in the saved file. Pass <c>null</c> to inherit Project Settings &gt; Smart Auditor; pass <see cref="Severity.Default"/> to disable filtering for this run.</param>
        /// <exception cref="ArgumentNullException">Thrown when path is null or empty</exception>
        /// <exception cref="ArgumentException">Thrown when path is invalid</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the directory is denied</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the directory does not exist and cannot be created</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs</exception>
        public void Save(string path,
            bool prettyPrint = false,
            bool debugReport = false,
            ReportExportContentMode exportContentMode = ReportExportContentMode.Full,
            Severity? minSaveSeverity = null)
        {
            ReportSerializer.Save(this, path, prettyPrint, debugReport, exportContentMode,
                ResolveMinSaveSeverity(minSaveSeverity));
        }

        /// <summary>
        /// Saves a lossless snapshot of the report for domain-reload survival (autosave).
        /// Unlike <see cref="Save"/>, this path always uses <see cref="Severity.Default"/> so
        /// every issue is written regardless of the project-level min-save-severity setting,
        /// ensuring nothing visible in the Smart Auditor window is silently dropped across a reload.
        /// </summary>
        internal void SaveForReloadSurvival(string path)
        {
            ReportSerializer.Save(this, path, prettyPrint: false, minSaveSeverity: Severity.Default);
        }

        /// <summary>
        /// Save the Report as a JSON file asynchronously.
        /// </summary>
        /// <param name="path">The file path at which to save the file</param>
        /// <param name="prettyPrint">Whether the JSON report should be formatted for readability.</param>
        /// <param name="debugReport">Whether the JSON report should include Smart Auditor debug metadata.</param>
        /// <param name="exportContentMode">Controls which report sections are written to the JSON file.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <param name="minSaveSeverity">Optional override for the minimum severity included in the saved file. Pass <c>null</c> to inherit Project Settings &gt; Smart Auditor; pass <see cref="Severity.Default"/> to disable filtering for this run.</param>
        /// <returns>A Task representing the asynchronous save operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when path is null or empty</exception>
        /// <exception cref="ArgumentException">Thrown when path is invalid</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the directory is denied</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the directory does not exist and cannot be created</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled</exception>
        public async Task SaveAsync(string path,
            bool prettyPrint = false,
            bool debugReport = false,
            ReportExportContentMode exportContentMode = ReportExportContentMode.Full,
            CancellationToken cancellationToken = default,
            Severity? minSaveSeverity = null)
        {
            await ReportSerializer.SaveAsync(this, path, prettyPrint, debugReport, exportContentMode,
                ResolveMinSaveSeverity(minSaveSeverity), cancellationToken);
        }

        // Falls back to the project-level SmartAuditorSettings.ReportFilter when the
        // caller doesn't supply an explicit value. Severity.Default means "no filter".
        static Severity ResolveMinSaveSeverity(Severity? perCall)
        {
            if (perCall.HasValue)
                return perCall.Value;
            return SmartAuditorSettings.instance.ReportFilter?.MinSaveSeverity ?? Severity.Default;
        }

        /// <summary>
        /// Save the Report as a JSON file asynchronously.
        /// </summary>
        /// <param name="path">The file path at which to save the file</param>
        /// <param name="prettyPrint">Whether the JSON report should be formatted for readability.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A Task representing the asynchronous save operation</returns>
        public async Task SaveAsync(string path, bool prettyPrint, CancellationToken cancellationToken)
        {
            await SaveAsync(path,
                prettyPrint,
                debugReport: false,
                exportContentMode: ReportExportContentMode.Full,
                cancellationToken: cancellationToken);
        }

        ReportExportSummary BuildSummary()
        {
            var issues = m_Issues.ToArray();

            var issuesBySeverity = issues
                .GroupBy(i => i.Severity.ToString())
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            var issuesByCategory = issues
                .GroupBy(i => i.Category.ToKey())
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            var messagesBySeverity = m_Messages
                .GroupBy(m => m.LogLevel.ToString())
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            var messagesBySource = m_Messages
                .GroupBy(m => m.Category.ToKey())
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            var insightCount = 0;
            foreach (var table in m_InsightTables.Values)
                insightCount += table.Rows.Count;

            return new ReportExportSummary
            {
                IssueCount = issues.Length,
                InsightCount = insightCount,
                MessageCount = m_Messages.Count,
                IssuesBySeverity = issuesBySeverity,
                IssuesByCategory = issuesByCategory,
                MessagesBySeverity = messagesBySeverity,
                MessagesBySource = messagesBySource
            };
        }

        // Internal only: Data written during analysis
        internal void RecordModuleInfo(AnalysisModule module, DateTime startTime, DateTime endTime, AnalysisResult analysisResult)
        {
            var name = module.Name;
            var info = m_ModuleInfos.FirstOrDefault(m => m.Name.Equals(name));
            if (info == null)
            {
                info = new ModuleInfo
                {
                    Name = module.Name,
                    Categories = module.Categories,
                };
                m_ModuleInfos.Add(info);
            }

            info.StartTime = Utils.Json.SerializeDateTime(startTime);
            info.EndTime = Utils.Json.SerializeDateTime(endTime);
            info.Result = analysisResult;
        }

        // Internal only: Data written during analysis.
        // Lazy-initialised on the first AddIssues call. Seeded from existing m_Issues so
        // incremental analyses (AnalysisOptions.ExistingReport) and JSON-loaded reports
        // dedup against what's already in the report. Issues with a null Fingerprint pass
        // through unchanged.
        [NonSerialized]
        HashSet<string> m_SeenFingerprints;

        /// <summary>
        /// Stores an insight table emitted by an analyzer. If a table for the same category was already
        /// added during this analysis run, the existing rows are merged with the new ones (schemas must
        /// match). Per-category replacement is the analyzer's responsibility — they keep their own
        /// builder and add to it across calls.
        /// </summary>
        internal void AddInsightTable(InsightTable table)
        {
            if (table == null)
                return;

            s_Mutex.WaitOne();
            try
            {
                if (m_InsightTables.TryGetValue(table.Category, out var existing))
                {
                    if (!ReferenceEquals(existing.Schema, table.Schema))
                        throw new InvalidOperationException(
                            $"InsightTable schema mismatch for {table.Category}: a different schema was registered earlier in this run.");

                    var merged = new List<InsightRow>(existing.Rows.Count + table.Rows.Count);
                    merged.AddRange(existing.Rows);
                    merged.AddRange(table.Rows);
                    m_InsightTables[table.Category] = new InsightTable(table.Category, table.Schema, merged);
                }
                else
                {
                    m_InsightTables[table.Category] = table;
                }
            }
            finally
            {
                s_Mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Stores a toolchain message (compiler warning, shader compile error, asset-importer message).
        /// </summary>
        internal void AddMessage(Message message)
        {
            if (message == null)
                return;

            s_Mutex.WaitOne();
            try
            {
                m_Messages.Add(message);
            }
            finally
            {
                s_Mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Read-only view of all insight tables collected by this report, keyed by category.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<AnalysisCategory, InsightTable> InsightTables => m_InsightTables;

        /// <summary>
        /// Read-only view of all toolchain messages collected by this report.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<Message> MessagesList => m_Messages;

        // Per-category insight tables with embedded schema, computed summary, and full row data.
        [JsonProperty("insights", NullValueHandling = NullValueHandling.Ignore)]
        Dictionary<string, InsightTable> SerializedInsightTables
        {
            get
            {
                if (m_InsightTables == null || m_InsightTables.Count == 0)
                    return null;
                var output = new Dictionary<string, InsightTable>(m_InsightTables.Count, StringComparer.Ordinal);
                foreach (var kvp in m_InsightTables)
                    output[kvp.Key.ToKey()] = kvp.Value;
                return output;
            }
        }

        // Typed Message list with first-class LogLevel and key/value Properties.
        // Always serialized as an array (empty when there are no messages) so consumers
        // -- CLI, agent bridge, LLMs -- can rely on the "messages" section existing, matching
        // the always-present "issues" and "descriptors" sections.
        // Settable for round-trip: domain-reload survival depends on Newtonsoft restoring
        // m_Messages from the "messages" JSON section. Without a setter the section is
        // silently dropped on load and every message disappears.
        [JsonProperty("messages")]
        List<Message> SerializedMessages
        {
            get => m_Messages ?? new List<Message>();
            set => m_Messages = value ?? new List<Message>();
        }

        internal void AddIssues(IEnumerable<ReportItem> issues)
        {
            s_Mutex.WaitOne();
            try
            {
                if (m_SeenFingerprints == null)
                {
                    m_SeenFingerprints = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var existing in m_Issues)
                    {
                        var existingFp = existing?.Fingerprint;
                        if (!string.IsNullOrEmpty(existingFp))
                            m_SeenFingerprints.Add(existingFp);
                    }
                }

                foreach (var issue in issues)
                {
                    if (issue == null)
                        continue;
                    var fp = issue.Fingerprint;
                    // First fingerprint wins. Issues with a null Fingerprint bypass the dedup gate.
                    if (!string.IsNullOrEmpty(fp) && !m_SeenFingerprints.Add(fp))
                        continue;
                    m_Issues.Add(issue);
                }
            }
            finally
            {
                s_Mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Replaces all issues for a specific asset path and category with a fresh set.
        /// Removes existing issues for that (assetPath, category) pair, evicts their fingerprints
        /// from the dedup set, then adds the fresh issues honouring the existing fingerprint dedup.
        /// Issues in <paramref name="freshIssues"/> whose location does not match
        /// <paramref name="assetPath"/> are silently skipped as a safety guard.
        /// </summary>
        internal void ReplaceAssetIssues(string assetPath, AnalysisCategory category, IReadOnlyList<ReportItem> freshIssues)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            s_Mutex.WaitOne();
            try
            {
                // Collect fingerprints of removed issues so the dedup set stays in sync.
                var removedFingerprints = new List<string>();
                m_Issues.RemoveAll(issue =>
                {
                    if (issue == null || issue.Category != category || issue.Location?.Path != assetPath)
                        return false;
                    var fp = issue.Fingerprint;
                    if (!string.IsNullOrEmpty(fp))
                        removedFingerprints.Add(fp);
                    return true;
                });

                if (m_SeenFingerprints != null)
                {
                    foreach (var fp in removedFingerprints)
                        m_SeenFingerprints.Remove(fp);
                }

                if (freshIssues == null || freshIssues.Count == 0)
                    return;

                if (m_SeenFingerprints == null)
                {
                    m_SeenFingerprints = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var existing in m_Issues)
                    {
                        var existingFp = existing?.Fingerprint;
                        if (!string.IsNullOrEmpty(existingFp))
                            m_SeenFingerprints.Add(existingFp);
                    }
                }

                foreach (var issue in freshIssues)
                {
                    if (issue == null)
                        continue;
                    if (issue.Location?.Path != assetPath)
                        continue;
                    var fp = issue.Fingerprint;
                    if (!string.IsNullOrEmpty(fp) && !m_SeenFingerprints.Add(fp))
                        continue;
                    m_Issues.Add(issue);
                }
            }
            finally
            {
                s_Mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Compares this report with another and returns the differences
        /// </summary>
        /// <param name="other">The report to compare against</param>
        /// <returns>A ReportDiff containing added and removed issues</returns>
        public ReportDiff CompareWith(Report other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            s_Mutex.WaitOne();
            try
            {
                var currentIssues = GetIssues();
                var otherIssues = other.GetIssues();

                var added = currentIssues.Except(otherIssues, new ReportItemComparer()).ToList();
                var removed = otherIssues.Except(currentIssues, new ReportItemComparer()).ToList();

                return new ReportDiff(added, removed);
            }
            finally
            {
                s_Mutex.ReleaseMutex();
            }
        }

        class ReportItemComparer : IEqualityComparer<ReportItem>
        {
            public bool Equals(ReportItem x, ReportItem y)
            {
                if (ReferenceEquals(x, y))
                    return true;
                if (ReferenceEquals(x, null))
                    return false;
                if (ReferenceEquals(y, null))
                    return false;

                if (!string.IsNullOrEmpty(x.Fingerprint) && !string.IsNullOrEmpty(y.Fingerprint))
                    return x.Fingerprint == y.Fingerprint;

                // Compare essential properties and filename, but ignore line number
                // since the same issue might move within a file
                return x.Id.Equals(y.Id) &&
                       x.Category == y.Category &&
                       x.Description == y.Description &&
                       x.RelativePath == y.RelativePath;
            }

            public int GetHashCode(ReportItem obj)
            {
                unchecked
                {
                    var hashCode = obj.Id?.GetHashCode() ?? 0;
                    hashCode = (hashCode * 397) ^ obj.Category.GetHashCode();
                    return hashCode;
                }
            }
        }
    }
}
