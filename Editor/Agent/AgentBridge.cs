using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor.Agent
{
    [InitializeOnLoad]
    internal static class AgentBridge
    {
        const string k_RequestSuffix = ".request.json";
        const string k_ResponseSuffix = ".response.json";
        const double k_PollIntervalSeconds = 0.5;
        internal const int ResponseSchema = 1;

        static double s_NextPollTime;
        static bool s_IsProcessing;
        static PendingAnalyze s_Pending;

        static AgentBridge()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        internal static string RootPath => Path.Combine(SmartAuditor.ProjectPath, "Temp", "SmartAuditorAgent");
        internal static string RequestsPath => Path.Combine(RootPath, "requests");
        internal static string ResponsesPath => Path.Combine(RootPath, "responses");
        internal static string ReportsPath => Path.Combine(RootPath, "reports");
        internal static string ProcessedArchivePath => Path.Combine(RootPath, "archive", "processed");
        internal static string FailedArchivePath => Path.Combine(RootPath, "archive", "failed");

        internal static void OpenFolder()
        {
            EnsureDirectories();
            EditorUtility.RevealInFinder(RootPath);
        }

        static void Update()
        {
            if (!UserPreferences.AgentBridgeEnabled)
            {
                return;
            }

            // Finish an analysis that was deferred across ticks (see ProcessRequestFile). The editor's
            // update loop keeps pumping while we wait here, so the main-thread async continuations the
            // asset modules scheduled get to run -- which is exactly what the old blocking wait starved.
            if (s_Pending != null)
            {
                if (s_Pending.Task.IsCompleted)
                {
                    FinishPendingAnalyze();
                }
                return;
            }

            if (s_IsProcessing ||
                EditorApplication.timeSinceStartup < s_NextPollTime)
            {
                return;
            }

            s_NextPollTime = EditorApplication.timeSinceStartup + k_PollIntervalSeconds;
            ProcessNextRequest();
        }

        internal static bool CanProcessRequests()
        {
            return !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating &&
                   !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        internal static bool ProcessNextRequest()
        {
            EnsureDirectories();

            var queued = Directory.GetFiles(RequestsPath, "*" + k_RequestSuffix)
                .OrderBy(File.GetLastWriteTimeUtc)
                .ToArray();

            if (queued.Length == 0)
                return false;

            // Pings jump the queue: they answer "is the bridge alive" and must
            // not wait behind a long-running analyze or a compiling editor.
            var pingPath = queued.FirstOrDefault(IsPingRequestFile);
            if (pingPath != null)
            {
                ProcessRequestFile(pingPath);
                return true;
            }

            if (!CanProcessRequests())
                return false;

            ProcessRequestFile(queued[0]);
            return true;
        }

        static bool IsPingRequestFile(string path)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<AgentBridgeRequest>(File.ReadAllText(path));
                return request != null && request.IsPingAction();
            }
            catch
            {
                return false;
            }
        }

        internal static AgentBridgeResponse ProcessRequestFile(string requestPath)
        {
            if (string.IsNullOrWhiteSpace(requestPath))
                throw new ArgumentNullException(nameof(requestPath));

            EnsureDirectories();

            s_IsProcessing = true;
            var stopwatch = Stopwatch.StartNew();
            var startedAt = DateTime.UtcNow;
            var responseFileId = GetRequestFileId(requestPath);
            var archivePath = FailedArchivePath;
            AgentBridgeResponse response = null;
            var shouldWriteResponse = true;
            var deferred = false;

            try
            {
                if (!IsSafeId(responseFileId))
                {
                    response = AgentBridgeResponse.Rejected(responseFileId, startedAt, stopwatch.ElapsedMilliseconds, GetUnityLogPath(),
                        "ArgumentException", $"Request file name '{Path.GetFileName(requestPath)}' does not contain a safe request id.");
                    return response;
                }

                var request = JsonConvert.DeserializeObject<AgentBridgeRequest>(File.ReadAllText(requestPath));
                if (request == null)
                    throw new InvalidDataException("Request JSON is empty.");

                if (!IsSafeId(request.Id))
                {
                    response = AgentBridgeResponse.Rejected(request.Id ?? responseFileId, startedAt, stopwatch.ElapsedMilliseconds, GetUnityLogPath(),
                        "ArgumentException", "Request id must contain only letters, numbers, '.', '_' or '-'.");
                    return response;
                }

                if (!string.Equals(responseFileId, request.Id, StringComparison.Ordinal))
                {
                    response = AgentBridgeResponse.Rejected(request.Id, startedAt, stopwatch.ElapsedMilliseconds, GetUnityLogPath(),
                        "ArgumentException", $"Request id '{request.Id}' must match file id '{responseFileId}'.");
                    return response;
                }

                var responsePath = GetResponsePath(request.Id);
                if (File.Exists(responsePath))
                {
                    shouldWriteResponse = false;
                    response = AgentBridgeResponse.Rejected(request.Id, startedAt, stopwatch.ElapsedMilliseconds, GetUnityLogPath(),
                        "InvalidOperationException", $"Response already exists for request id '{request.Id}'.");
                    return response;
                }

                if (request.IsPingAction())
                {
                    response = AgentBridgeResponse.Ponged(
                        request.Id,
                        startedAt,
                        stopwatch.ElapsedMilliseconds,
                        GetUnityLogPath(),
                        editorBusy: !CanProcessRequests());
                    archivePath = ProcessedArchivePath;
                    return response;
                }

                if (!request.IsAnalyzeAction())
                {
                    response = AgentBridgeResponse.Rejected(request.Id, startedAt, stopwatch.ElapsedMilliseconds, GetUnityLogPath(),
                        "ArgumentException", $"Unsupported agent bridge action '{request.Action}'.");
                    return response;
                }

                var reportPath = string.IsNullOrWhiteSpace(request.ReportPath)
                    ? Path.Combine(ReportsPath, request.Id + ".report.json")
                    : request.ReportPath;
                var options = request.ToCommandLineOptions(reportPath);

                // Drive the analysis without blocking the editor's main thread. Asset modules
                // (textures, meshes, ...) await Task.Yield() so they can call main-thread-only
                // AssetDatabase / AssetImporter APIs; blocking here would starve those continuations
                // and deadlock the editor. Scopes that never yield (e.g. ProjectSettings) leave the
                // task already completed, so they still finish synchronously within this call.
                var analyzeTask = CommandLine.RunAsync(options);
                if (!analyzeTask.IsCompleted)
                {
                    deferred = true;
                    s_Pending = new PendingAnalyze
                    {
                        Task = analyzeTask,
                        RequestPath = requestPath,
                        ResponseFileId = responseFileId,
                        RequestId = request.Id,
                        StartedAt = startedAt,
                        Stopwatch = stopwatch,
                    };
                    return null;
                }

                var result = analyzeTask.GetAwaiter().GetResult();
                if (result.IsValid)
                {
                    archivePath = ProcessedArchivePath;
                }
                response = result.IsValid
                    ? AgentBridgeResponse.Completed(
                        request.Id,
                        result,
                        startedAt,
                        stopwatch.ElapsedMilliseconds,
                        GetUnityLogPath())
                    : AgentBridgeResponse.Failed(
                        request.Id,
                        startedAt,
                        stopwatch.ElapsedMilliseconds,
                        GetUnityLogPath(),
                        "InvalidOperationException",
                        "Smart Auditor generated an invalid or incomplete report.");
                return response;
            }
            catch (Exception e)
            {
                var id = !string.IsNullOrWhiteSpace(response?.Id) ? response.Id : responseFileId;
                response = AgentBridgeResponse.Failed(id, startedAt, stopwatch.ElapsedMilliseconds, GetUnityLogPath(), e);
                return response;
            }
            finally
            {
                // A deferred analysis finishes on a later tick in FinishPendingAnalyze, which owns the
                // response write, the request archive, and clearing s_IsProcessing. Skip them here.
                if (!deferred)
                {
                    stopwatch.Stop();

                    if (response != null)
                    {
                        response.CompletedAt = AgentBridgeResponse.FormatDateTime(DateTime.UtcNow);
                        response.DurationMs = stopwatch.ElapsedMilliseconds;

                        if (shouldWriteResponse)
                        {
                            WriteResponse(responseFileId, response);
                        }
                    }

                    ArchiveRequest(requestPath, archivePath, responseFileId);
                    s_IsProcessing = false;
                }
            }
        }

        // Completes an analysis that was deferred across editor ticks (see ProcessRequestFile),
        // mirroring that method's finally block: stamp + write the response, archive the request,
        // and release the processing gate. Runs on the main thread from Update.
        static void FinishPendingAnalyze()
        {
            var pending = s_Pending;
            s_Pending = null;

            var archivePath = FailedArchivePath;
            AgentBridgeResponse response;
            try
            {
                var result = pending.Task.GetAwaiter().GetResult();
                if (result.IsValid)
                {
                    archivePath = ProcessedArchivePath;
                }
                response = result.IsValid
                    ? AgentBridgeResponse.Completed(
                        pending.RequestId,
                        result,
                        pending.StartedAt,
                        pending.Stopwatch.ElapsedMilliseconds,
                        GetUnityLogPath())
                    : AgentBridgeResponse.Failed(
                        pending.RequestId,
                        pending.StartedAt,
                        pending.Stopwatch.ElapsedMilliseconds,
                        GetUnityLogPath(),
                        "InvalidOperationException",
                        "Smart Auditor generated an invalid or incomplete report.");
            }
            catch (Exception e)
            {
                response = AgentBridgeResponse.Failed(
                    pending.RequestId,
                    pending.StartedAt,
                    pending.Stopwatch.ElapsedMilliseconds,
                    GetUnityLogPath(),
                    e);
            }

            pending.Stopwatch.Stop();
            response.CompletedAt = AgentBridgeResponse.FormatDateTime(DateTime.UtcNow);
            response.DurationMs = pending.Stopwatch.ElapsedMilliseconds;
            WriteResponse(pending.ResponseFileId, response);
            ArchiveRequest(pending.RequestPath, archivePath, pending.ResponseFileId);
            s_IsProcessing = false;
        }

        sealed class PendingAnalyze
        {
            public Task<CommandLineResult> Task;
            public string RequestPath;
            public string ResponseFileId;
            public string RequestId;
            public DateTime StartedAt;
            public Stopwatch Stopwatch;
        }

        internal static void EnsureDirectories()
        {
            Directory.CreateDirectory(RequestsPath);
            Directory.CreateDirectory(ResponsesPath);
            Directory.CreateDirectory(ReportsPath);
            Directory.CreateDirectory(ProcessedArchivePath);
            Directory.CreateDirectory(FailedArchivePath);
        }

        static void WriteResponse(string responseFileId, AgentBridgeResponse response)
        {
            if (!IsSafeId(responseFileId))
            {
                Debug.LogWarning($"[{SmartAuditor.DisplayName}] Agent bridge could not write response for unsafe id '{responseFileId}'.");
                return;
            }

            var responsePath = GetResponsePath(responseFileId);
            if (File.Exists(responsePath))
            {
                Debug.LogWarning($"[{SmartAuditor.DisplayName}] Agent bridge response already exists: {responsePath}");
                return;
            }

            var tempPath = responsePath + ".tmp";
            var json = JsonConvert.SerializeObject(response, Formatting.Indented, CreateJsonSettings());
            File.WriteAllText(tempPath, json);
            if (File.Exists(responsePath))
                File.Delete(tempPath);
            else
                File.Move(tempPath, responsePath);
        }

        static void ArchiveRequest(string requestPath, string archiveDirectory, string requestId)
        {
            if (!File.Exists(requestPath))
                return;

            Directory.CreateDirectory(archiveDirectory);
            var safeId = IsSafeId(requestId) ? requestId : "invalid-request";
            var destination = Path.Combine(archiveDirectory, safeId + k_RequestSuffix);
            if (File.Exists(destination))
            {
                var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                destination = Path.Combine(archiveDirectory, safeId + "." + suffix + k_RequestSuffix);
            }

            File.Move(requestPath, destination);
        }

        static string GetResponsePath(string requestId)
        {
            return Path.Combine(ResponsesPath, requestId + k_ResponseSuffix);
        }

        static string GetRequestFileId(string requestPath)
        {
            var fileName = Path.GetFileName(requestPath);
            return fileName != null && fileName.EndsWith(k_RequestSuffix, StringComparison.Ordinal)
                ? fileName.Substring(0, fileName.Length - k_RequestSuffix.Length)
                : fileName;
        }

        internal static bool IsSafeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
                return false;

            foreach (var c in id)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')
                    continue;

                return false;
            }

            return true;
        }

        static string GetUnityLogPath()
        {
            var property = typeof(Application).GetProperty("consoleLogPath");
            return property?.GetValue(null) as string;
        }

        internal static JsonSerializerSettings CreateJsonSettings()
        {
            return new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Converters = new List<JsonConverter>
                {
                    new Newtonsoft.Json.Converters.StringEnumConverter()
                }
            };
        }
    }

    [Serializable]
    internal sealed class AgentBridgeRequest
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("scope")]
        public string[] Scope { get; set; }

        [JsonProperty("categories")]
        public string[] Categories { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("codeContext")]
        public string CodeContext { get; set; }

        [JsonProperty("assemblies")]
        public string[] Assemblies { get; set; }

        [JsonProperty("assetPaths")]
        public string[] AssetPaths { get; set; }

        [JsonProperty("assetPathPrefixes")]
        public string[] AssetPathPrefixes { get; set; }

        [JsonProperty("analysisSource")]
        public string AnalysisSource { get; set; }

        [JsonProperty("scenePath")]
        public string ScenePath { get; set; }

        [JsonProperty("hierarchyPaths")]
        public string[] HierarchyPaths { get; set; }

        [JsonProperty("analyzeReadOnlyPackages")]
        public bool? AnalyzeReadOnlyPackages { get; set; }

        [JsonProperty("prettyPrint")]
        public bool? PrettyPrint { get; set; }

        [JsonProperty("debugReport")]
        public bool? DebugReport { get; set; }

        [JsonProperty("exportContentMode")]
        public string ExportContentMode { get; set; }

        [JsonProperty("failOnIssues")]
        public bool? FailOnIssues { get; set; }

        [JsonProperty("minSaveSeverity")]
        public string MinSaveSeverity { get; set; }

        [JsonProperty("reportPath")]
        public string ReportPath { get; set; }

        public bool IsAnalyzeAction()
        {
            return string.Equals(Action, "analyze", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsPingAction()
        {
            return string.Equals(Action, "ping", StringComparison.OrdinalIgnoreCase);
        }

        public CommandLineOptions ToCommandLineOptions(string reportPath)
        {
            var args = new List<string>();
            AddArg(args, CommandLine.ReportPathArgument, reportPath);
            AddArg(args, CommandLine.ScopeArgument, Join(Scope));
            AddArg(args, CommandLine.CategoriesArgument, Join(Categories));
            AddArg(args, CommandLine.PlatformArgument, Platform);
            AddArg(args, CommandLine.CodeContextArgument, CodeContext);
            AddArg(args, CommandLine.AssembliesArgument, Join(Assemblies));
            AddArg(args, CommandLine.AssetPathsArgument, Join(AssetPaths));
            AddArg(args, CommandLine.AssetPathPrefixesArgument, Join(AssetPathPrefixes));
            AddArg(args, CommandLine.AnalysisSourceArgument, AnalysisSource);
            AddArg(args, CommandLine.ScenePathArgument, ScenePath);
            AddArg(args, CommandLine.HierarchyPathsArgument, Join(HierarchyPaths, "|"));
            AddBoolArg(args, CommandLine.AnalyzeReadOnlyPackagesArgument, AnalyzeReadOnlyPackages);
            AddBoolArg(args, CommandLine.PrettyPrintArgument, PrettyPrint ?? true);
            AddBoolArg(args, CommandLine.DebugReportArgument, DebugReport);
            AddArg(args, CommandLine.ExportContentModeArgument, string.IsNullOrWhiteSpace(ExportContentMode) ? "IssuesOnly" : ExportContentMode);
            if (FailOnIssues == true)
                args.Add(CommandLine.FailOnIssuesArgument);
            AddArg(args, CommandLine.MinSaveSeverityArgument, MinSaveSeverity);

            return CommandLine.ParseArguments(args.ToArray());
        }

        static void AddArg(ICollection<string> args, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            args.Add(name);
            args.Add(value);
        }

        static void AddBoolArg(ICollection<string> args, string name, bool? value)
        {
            if (!value.HasValue)
                return;

            args.Add(name);
            args.Add(value.Value ? "true" : "false");
        }

        static string Join(string[] values, string separator = ",")
        {
            if (values == null || values.Length == 0)
                return null;

            return string.Join(separator, values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
        }
    }

    [Serializable]
    internal sealed class AgentBridgeResponse
    {
        // Schema version for the bridge response shape itself. Increment when fields
        // are removed or their semantics change; additive changes are non-breaking.
        [JsonProperty("schema")]
        public int Schema { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("reportPath")]
        public string ReportPath { get; set; }

        [JsonProperty("issueCount")]
        public int IssueCount { get; set; }

        [JsonProperty("issuesByCategory")]
        public Dictionary<string, int> IssuesByCategory { get; set; }

        [JsonProperty("issuesBySeverity")]
        public Dictionary<string, int> IssuesBySeverity { get; set; }

        // Per-module result (e.g. { "Code": "Failure", "Settings": "Success" }). Lets a consumer
        // tell a genuinely clean scope from one where a module aborted and produced no findings.
        [JsonProperty("moduleResults")]
        public Dictionary<string, string> ModuleResults { get; set; }

        [JsonProperty("startedAt")]
        public string StartedAt { get; set; }

        [JsonProperty("completedAt")]
        public string CompletedAt { get; set; }

        [JsonProperty("durationMs")]
        public long DurationMs { get; set; }

        [JsonProperty("unityLogPath")]
        public string UnityLogPath { get; set; }

        [JsonProperty("error")]
        public AgentBridgeError Error { get; set; }

        // Ping-only fields. NullValueHandling.Ignore keeps them out of analyze
        // responses; they describe the editor environment for liveness checks.

        [JsonProperty("smartAuditorVersion")]
        public string SmartAuditorVersion { get; set; }

        [JsonProperty("reportSchema")]
        public string ReportSchema { get; set; }

        [JsonProperty("unityVersion")]
        public string UnityVersion { get; set; }

        [JsonProperty("projectPath")]
        public string ProjectPath { get; set; }

        [JsonProperty("editorBusy")]
        public bool? EditorBusy { get; set; }

        public static AgentBridgeResponse Completed(
            string id,
            CommandLineResult result,
            DateTime startedAt,
            long durationMs,
            string unityLogPath)
        {
            var issues = result.Report?.Issues ?? Array.Empty<ReportItem>();
            return new AgentBridgeResponse
            {
                Schema = AgentBridge.ResponseSchema,
                Id = id,
                Status = "completed",
                ReportPath = result.ReportPath,
                IssueCount = result.IssueCount,
                IssuesByCategory = issues
                    .GroupBy(i => i.Category.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                IssuesBySeverity = issues
                    .GroupBy(i => i.Severity.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                ModuleResults = result.Report?.GetModuleResults()
                    .ToDictionary(m => m.Key, m => m.Value.ToString(), StringComparer.Ordinal),
                StartedAt = FormatDateTime(startedAt),
                DurationMs = durationMs,
                UnityLogPath = unityLogPath
            };
        }

        public static AgentBridgeResponse Ponged(
            string id,
            DateTime startedAt,
            long durationMs,
            string unityLogPath,
            bool editorBusy)
        {
            return new AgentBridgeResponse
            {
                Schema = AgentBridge.ResponseSchema,
                Id = id,
                Status = "ok",
                SmartAuditorVersion = PackageInfo.Version,
                ReportSchema = Report.CurrentVersion,
                UnityVersion = UnityEngine.Application.unityVersion,
                ProjectPath = SmartAuditor.ProjectPath,
                EditorBusy = editorBusy,
                StartedAt = FormatDateTime(startedAt),
                DurationMs = durationMs,
                UnityLogPath = unityLogPath
            };
        }

        public static AgentBridgeResponse Failed(string id, DateTime startedAt, long durationMs, string unityLogPath, Exception exception)
        {
            return CreateErrorResponse(id, "failed", startedAt, durationMs, unityLogPath, exception.GetType().Name, exception.Message);
        }

        public static AgentBridgeResponse Failed(
            string id,
            DateTime startedAt,
            long durationMs,
            string unityLogPath,
            string errorType,
            string message)
        {
            return CreateErrorResponse(id, "failed", startedAt, durationMs, unityLogPath, errorType, message);
        }

        public static AgentBridgeResponse Rejected(
            string id,
            DateTime startedAt,
            long durationMs,
            string unityLogPath,
            string errorType,
            string message)
        {
            return CreateErrorResponse(id, "rejected", startedAt, durationMs, unityLogPath, errorType, message);
        }

        static AgentBridgeResponse CreateErrorResponse(
            string id,
            string status,
            DateTime startedAt,
            long durationMs,
            string unityLogPath,
            string errorType,
            string message)
        {
            return new AgentBridgeResponse
            {
                Schema = AgentBridge.ResponseSchema,
                Id = id,
                Status = status,
                IssueCount = 0,
                StartedAt = FormatDateTime(startedAt),
                DurationMs = durationMs,
                UnityLogPath = unityLogPath,
                Error = new AgentBridgeError
                {
                    Type = errorType,
                    Message = message
                }
            };
        }

        public static string FormatDateTime(DateTime dateTime)
        {
            return dateTime.ToUniversalTime().ToString("o");
        }
    }

    [Serializable]
    internal sealed class AgentBridgeError
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
