using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Command-line entry points and helpers for running Smart Auditor from Unity batch mode.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Run"/> with Unity's <c>-executeMethod</c> argument to let CI systems,
    /// coding agents, or external tools trigger analysis and read the resulting JSON report.
    /// </remarks>
    public static class CommandLine
    {
        /// <summary>Command-line argument for the JSON report path.</summary>
        public const string ReportPathArgument = "-smartAuditorReportPath";

        /// <summary>Command-line argument for comma-separated analysis categories.</summary>
        public const string CategoriesArgument = "-smartAuditorCategories";

        /// <summary>Command-line argument for comma-separated broad analysis scopes.</summary>
        public const string ScopeArgument = "-smartAuditorScope";

        /// <summary>Command-line argument for the analysis build target.</summary>
        public const string PlatformArgument = "-smartAuditorPlatform";

        /// <summary>Command-line argument for code context: Runtime, Editor, or All.</summary>
        public const string CodeContextArgument = "-smartAuditorCodeContext";

        /// <summary>Command-line argument for comma-separated assembly names.</summary>
        public const string AssembliesArgument = "-smartAuditorAssemblies";

        /// <summary>Command-line argument for comma-separated exact asset paths.</summary>
        public const string AssetPathsArgument = "-smartAuditorAssetPaths";

        /// <summary>Command-line argument for comma-separated asset path prefixes.</summary>
        public const string AssetPathPrefixesArgument = "-smartAuditorAssetPathPrefixes";

        /// <summary>Command-line argument for analysis source: Assets or LoadedScene.</summary>
        public const string AnalysisSourceArgument = "-smartAuditorAnalysisSource";

        /// <summary>Command-line argument for the scene asset path to load before loaded-scene analysis.</summary>
        public const string ScenePathArgument = "-smartAuditorScenePath";

        /// <summary>Command-line argument for pipe-separated hierarchy paths in a loaded scene.</summary>
        public const string HierarchyPathsArgument = "-smartAuditorHierarchyPaths";

        /// <summary>Command-line argument for including read-only package assets and assemblies.</summary>
        public const string AnalyzeReadOnlyPackagesArgument = "-smartAuditorAnalyzeReadOnlyPackages";

        /// <summary>Command-line argument for enabling or disabling pretty-printed JSON.</summary>
        public const string PrettyPrintArgument = "-smartAuditorPrettyPrint";

        /// <summary>Command-line argument for including Smart Auditor debug metadata in the JSON report.</summary>
        public const string DebugReportArgument = "-smartAuditorDebugReport";

        /// <summary>
        /// Command-line argument for report export content mode.
        /// When omitted, Smart Auditor exports issues-only content by default.
        /// </summary>
        public const string ExportContentModeArgument = "-smartAuditorExportContentMode";

        /// <summary>Command-line flag that returns a non-zero exit code when any issues are reported.</summary>
        public const string FailOnIssuesArgument = "-smartAuditorFailOnIssues";

        /// <summary>
        /// Command-line argument for the minimum severity included in the saved JSON report.
        /// Accepted values: <c>Default</c>, <c>Error</c>, <c>Critical</c>, <c>Major</c>, <c>Moderate</c>, <c>Minor</c>
        /// (case-insensitive). When omitted, falls back to Project Settings &gt; Smart Auditor.
        /// </summary>
        public const string MinSaveSeverityArgument = "-smartAuditorMinSaveSeverity";

        /// <summary>
        /// Unity <c>-executeMethod</c> entry point. Runs analysis using command-line arguments and saves a JSON report.
        /// </summary>
        public static void Run()
        {
            var exitCode = 1;

            try
            {
                var result = Run(Environment.GetCommandLineArgs());
                exitCode = result.ExitCode;

                Debug.Log($"[{SmartAuditor.DisplayName}] Report written to: {result.ReportPath}");
                Debug.Log($"[{SmartAuditor.DisplayName}] Issues: {result.IssueCount}, exit code: {result.ExitCode}");

                if (result.HasModuleFailures)
                {
                    var failed = result.Report.GetModuleResults()
                        .Where(m => m.Value == AnalysisResult.Failure)
                        .Select(m => m.Key);
                    Debug.LogError($"[{SmartAuditor.DisplayName}] Analysis modules failed to complete: {string.Join(", ", failed)}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[{SmartAuditor.DisplayName}] Command-line analysis failed: {e.Message}\n{e.StackTrace}");
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Runs analysis using command-line arguments and saves a JSON report.
        /// </summary>
        /// <param name="args">The command-line arguments to parse.</param>
        /// <returns>Information about the saved report and suggested process exit code.</returns>
        public static CommandLineResult Run(string[] args)
        {
            return Run(ParseArguments(args));
        }

        /// <summary>
        /// Runs analysis using explicit command-line options and saves a JSON report.
        /// </summary>
        /// <param name="options">The command-line options controlling analysis and export.</param>
        /// <returns>Information about the saved report and suggested process exit code.</returns>
        public static CommandLineResult Run(CommandLineOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var reportPath = ResolveReportPath(options.ReportPath);
            var directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            options.PrepareProjectState();
            var analysisOptions = options.ToAnalysisOptions();
            analysisOptions.RunProvenance ??= new AnalysisRunProvenance();
            analysisOptions.RunProvenance.ReportOutputPath = reportPath;
            analysisOptions.RunProvenance.UnityLogPath = GetUnityLogPath();

            var report = SmartAuditor.Analyze(analysisOptions);
            report.Save(reportPath,
                options.PrettyPrint,
                options.DebugReport,
                options.ExportContentMode,
                options.MinSaveSeverity);

            return new CommandLineResult(
                reportPath,
                report.Issues.Length,
                report.IsValid(),
                options.FailOnIssues,
                report);
        }

        /// <summary>
        /// Runs analysis asynchronously using explicit command-line options and saves a JSON report.
        /// Unlike <see cref="Run(CommandLineOptions)"/>, this awaits <see cref="SmartAuditor.AnalyzeAsync"/>
        /// instead of blocking the calling thread, so a main-thread caller (e.g. the agent bridge) does
        /// not starve the main-thread continuations that asset modules schedule via <c>await Task.Yield()</c>.
        /// </summary>
        /// <param name="options">The command-line options controlling analysis and export.</param>
        /// <param name="ct">Token to observe for cancellation.</param>
        /// <returns>Information about the saved report and suggested process exit code.</returns>
        public static async Task<CommandLineResult> RunAsync(CommandLineOptions options, CancellationToken ct = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var reportPath = ResolveReportPath(options.ReportPath);
            var directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            options.PrepareProjectState();
            var analysisOptions = options.ToAnalysisOptions();
            analysisOptions.RunProvenance ??= new AnalysisRunProvenance();
            analysisOptions.RunProvenance.ReportOutputPath = reportPath;
            analysisOptions.RunProvenance.UnityLogPath = GetUnityLogPath();

            var report = await SmartAuditor.AnalyzeAsync(analysisOptions, null, ct);
            report.Save(reportPath,
                options.PrettyPrint,
                options.DebugReport,
                options.ExportContentMode,
                options.MinSaveSeverity);

            return new CommandLineResult(
                reportPath,
                report.Issues.Length,
                report.IsValid(),
                options.FailOnIssues,
                report);
        }

        /// <summary>
        /// Parses Smart Auditor command-line arguments into reusable options.
        /// </summary>
        /// <param name="args">The command-line arguments to parse.</param>
        /// <returns>The parsed command-line options.</returns>
        public static CommandLineOptions ParseArguments(string[] args)
        {
            if (args == null)
                args = Array.Empty<string>();

            return new CommandLineOptions
            {
                ReportPath = GetValue(args, ReportPathArgument),
                Categories = ParseCategories(GetValue(args, CategoriesArgument)),
                Scope = ParseScope(GetValue(args, ScopeArgument)),
                Platform = ParseNullableEnum<BuildTarget>(GetValue(args, PlatformArgument), PlatformArgument),
                CodeContext = ParseNullableEnum<CodeContext>(GetValue(args, CodeContextArgument), CodeContextArgument),
                IncludedAssemblies = SplitCommaSeparated(GetValue(args, AssembliesArgument)),
                AssetPaths = SplitCommaSeparated(GetValue(args, AssetPathsArgument)),
                AssetPathPrefixes = SplitCommaSeparated(GetValue(args, AssetPathPrefixesArgument)),
                AnalysisSource = ParseNullableEnum<AnalysisSource>(GetValue(args, AnalysisSourceArgument), AnalysisSourceArgument),
                ScenePath = GetValue(args, ScenePathArgument),
                HierarchyPaths = SplitPipeSeparated(GetValue(args, HierarchyPathsArgument)),
                AnalyzeReadOnlyPackages = GetBool(args, AnalyzeReadOnlyPackagesArgument, defaultValue: false),
                PrettyPrint = GetBool(args, PrettyPrintArgument, defaultValue: true),
                DebugReport = GetBool(args, DebugReportArgument, defaultValue: false),
                ExportContentMode = ParseReportExportContentMode(GetValue(args, ExportContentModeArgument)),
                FailOnIssues = HasFlag(args, FailOnIssuesArgument),
                MinSaveSeverity = ParseMinSaveSeverity(GetValue(args, MinSaveSeverityArgument))
            };
        }

        static Severity? ParseMinSaveSeverity(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (!Enum.TryParse<Severity>(value, ignoreCase: true, out var parsed))
                throw new ArgumentException($"Unknown value '{value}' for {MinSaveSeverityArgument}.");

            if (!ReportFilter.IsValidThreshold(parsed))
                throw new ArgumentException(
                    $"{MinSaveSeverityArgument} must be one of: Default, Error, Critical, Major, Moderate, Minor. " +
                    $"Got '{value}'.");

            return parsed;
        }

        /// <summary>
        /// Converts a broad analysis scope to the categories Smart Auditor should run.
        /// </summary>
        /// <param name="scope">The broad analysis scope.</param>
        /// <returns>The categories included by the scope.</returns>
        public static AnalysisCategory[] GetCategoriesForScope(AnalysisScope scope)
        {
            if (scope == AnalysisScope.None)
                return Array.Empty<AnalysisCategory>();

            var categories = new List<AnalysisCategory>();

            if (scope.HasFlag(AnalysisScope.Code))
            {
                categories.Add(AnalysisCategory.Code);
                categories.Add(AnalysisCategory.Assembly);
                categories.Add(AnalysisCategory.PrecompiledAssembly);
                categories.Add(AnalysisCategory.CodeCompilerMessage);
            }

            if (scope.HasFlag(AnalysisScope.ProjectSettings))
            {
                categories.Add(AnalysisCategory.ProjectSetting);
                categories.Add(AnalysisCategory.Package);
            }

            if (scope.HasFlag(AnalysisScope.Assets))
            {
                categories.Add(AnalysisCategory.AssetIssue);
                categories.Add(AnalysisCategory.Texture);
                categories.Add(AnalysisCategory.Sprite);
                categories.Add(AnalysisCategory.SpriteAtlas);
                categories.Add(AnalysisCategory.Mesh);
                categories.Add(AnalysisCategory.AudioClip);
                categories.Add(AnalysisCategory.AnimatorController);
                categories.Add(AnalysisCategory.AnimationClip);
                categories.Add(AnalysisCategory.Avatar);
                categories.Add(AnalysisCategory.AvatarMask);
                categories.Add(AnalysisCategory.AssetImportMessage);
            }

            if (scope.HasFlag(AnalysisScope.Prefabs))
            {
                categories.Add(AnalysisCategory.Prefab);
                categories.Add(AnalysisCategory.GameObjectIssue);
                categories.Add(AnalysisCategory.Scene);
                categories.Add(AnalysisCategory.SceneSettings);
            }

            if (scope.HasFlag(AnalysisScope.Shaders))
            {
                categories.Add(AnalysisCategory.Shader);
                categories.Add(AnalysisCategory.ShaderVariant);
                categories.Add(AnalysisCategory.ComputeShaderVariant);
                categories.Add(AnalysisCategory.ShaderCompilerMessage);
                categories.Add(AnalysisCategory.Material);
            }

            if (scope.HasFlag(AnalysisScope.Build))
                categories.Add(AnalysisCategory.BuildFile);

            return categories.ToArray();
        }

        static string ResolveReportPath(string reportPath)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
                reportPath = Path.Combine(SmartAuditor.ProjectPath, "smart-auditor-report.json");
            else if (!Path.IsPathRooted(reportPath))
                reportPath = Path.Combine(SmartAuditor.ProjectPath, reportPath);

            return Path.GetFullPath(reportPath);
        }

        static string GetUnityLogPath()
        {
            var property = typeof(Application).GetProperty("consoleLogPath");
            return property?.GetValue(null) as string;
        }

        static string GetValue(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            if (index < 0)
                return null;

            if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                throw new ArgumentException($"Missing value for {name}.");

            return args[index + 1];
        }

        static bool HasFlag(string[] args, string name)
        {
            return Array.IndexOf(args, name) >= 0;
        }

        static bool GetBool(string[] args, string name, bool defaultValue)
        {
            var value = GetValueOrNullForBoolFlag(args, name);
            if (value == null)
                return defaultValue;

            if (string.IsNullOrEmpty(value))
                return true;

            if (bool.TryParse(value, out var result))
                return result;

            throw new ArgumentException($"{name} must be true or false.");
        }

        static string GetValueOrNullForBoolFlag(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            if (index < 0)
                return null;

            if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                return string.Empty;

            return args[index + 1];
        }

        static AnalysisCategory[] ParseCategories(string value)
        {
            var categoryNames = SplitCommaSeparated(value);
            if (categoryNames == null)
                return null;

            return categoryNames.Select(ParseCategory).ToArray();
        }

        static AnalysisCategory ParseCategory(string value)
        {
            if (Enum.TryParse(value, ignoreCase: true, out AnalysisCategory category))
                return category;

            foreach (AnalysisCategory candidate in Enum.GetValues(typeof(AnalysisCategory)))
            {
                if (string.Equals(candidate.ToKey(), value, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            throw new ArgumentException($"Unknown Smart Auditor category '{value}'.");
        }

        static AnalysisScope? ParseScope(string value)
        {
            var values = SplitCommaSeparated(value);
            if (values == null)
                return null;

            var scope = AnalysisScope.None;
            foreach (var item in values)
            {
                if (!TryParseScope(item, out var parsed))
                    throw new ArgumentException($"Unknown Smart Auditor scope '{item}'.");

                scope |= parsed;
            }

            return scope;
        }

        static bool TryParseScope(string value, out AnalysisScope scope)
        {
            if (Enum.TryParse(value, ignoreCase: true, out scope))
                return true;

            if (string.Equals(value, "asset", StringComparison.OrdinalIgnoreCase))
            {
                scope = AnalysisScope.Assets;
                return true;
            }

            if (string.Equals(value, "settings", StringComparison.OrdinalIgnoreCase))
            {
                scope = AnalysisScope.ProjectSettings;
                return true;
            }

            return false;
        }

        static T? ParseNullableEnum<T>(string value, string argumentName) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (Enum.TryParse(value, ignoreCase: true, out T result))
                return result;

            throw new ArgumentException($"Unknown value '{value}' for {argumentName}.");
        }

        static ReportExportContentMode ParseReportExportContentMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ReportExportContentMode.IssuesOnly;

            if (Enum.TryParse(value, ignoreCase: true, out ReportExportContentMode mode))
                return mode;

            switch (value.Trim().ToLowerInvariant())
            {
                case "full":
                    return ReportExportContentMode.Full;
                case "issuesonly":
                case "issues-only":
                    return ReportExportContentMode.IssuesOnly;
                case "issuesplussummary":
                case "issues-plus-summary":
                case "issues+summary":
                    return ReportExportContentMode.IssuesPlusSummary;
                default:
                    throw new ArgumentException($"Unknown value '{value}' for {ExportContentModeArgument}.");
            }
        }

        static string[] SplitCommaSeparated(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToArray();
        }

        static string[] SplitPipeSeparated(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value
                .Split('|')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToArray();
        }

        internal static string ToProjectRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                return path;

            var projectPath = SmartAuditor.ProjectPath.Replace('\\', '/').TrimEnd('/');
            var normalizedPath = Path.GetFullPath(path).Replace('\\', '/');

            if (!normalizedPath.StartsWith(projectPath + "/", StringComparison.OrdinalIgnoreCase))
                return path;

            return normalizedPath.Substring(projectPath.Length + 1);
        }
    }

    /// <summary>
    /// Options for running Smart Auditor from command-line integrations.
    /// </summary>
    public sealed class CommandLineOptions
    {
        /// <summary>Path where the JSON report should be written.</summary>
        public string ReportPath { get; set; }

        /// <summary>Categories to analyze. A null value analyzes all categories.</summary>
        public AnalysisCategory[] Categories { get; set; }

        /// <summary>
        /// Broad project areas to analyze. Ignored when <see cref="Categories"/> is supplied.
        /// A null value analyzes all categories.
        /// </summary>
        public AnalysisScope? Scope { get; set; }

        /// <summary>Optional analysis build target. A null value uses the active build target.</summary>
        public BuildTarget? Platform { get; set; }

        /// <summary>Optional code context. A null value uses the default runtime code context.</summary>
        public CodeContext? CodeContext { get; set; }

        /// <summary>Optional assembly names to include in code analysis.</summary>
        public string[] IncludedAssemblies { get; set; }

        /// <summary>Optional exact asset paths to include in asset-backed analysis.</summary>
        public string[] AssetPaths { get; set; }

        /// <summary>Optional asset path prefixes to include in asset-backed analysis.</summary>
        public string[] AssetPathPrefixes { get; set; }

        /// <summary>Optional source filter that limits modules to asset or loaded-scene analysis.</summary>
        public AnalysisSource? AnalysisSource { get; set; }

        /// <summary>Optional scene asset path to load before using loaded-scene analysis.</summary>
        public string ScenePath { get; set; }

        /// <summary>Optional loaded scene name. Prefer <see cref="ScenePath"/> for command-line workflows.</summary>
        public string SceneName { get; set; }

        /// <summary>Optional hierarchy paths to include when analyzing a loaded scene.</summary>
        public string[] HierarchyPaths { get; set; }

        /// <summary>Whether read-only package assets and assemblies should be included in analysis.</summary>
        public bool AnalyzeReadOnlyPackages { get; set; }

        /// <summary>Whether the exported JSON report should be pretty printed.</summary>
        public bool PrettyPrint { get; set; } = true;

        /// <summary>Whether the exported JSON report should include Smart Auditor debug metadata.</summary>
        public bool DebugReport { get; set; }

        /// <summary>
        /// Controls which report sections are written to JSON.
        /// Defaults to <see cref="ReportExportContentMode.IssuesOnly"/> for compact CI and agent workflows.
        /// </summary>
        public ReportExportContentMode ExportContentMode { get; set; } = ReportExportContentMode.IssuesOnly;

        /// <summary>Whether the suggested exit code should fail when any issues are reported.</summary>
        public bool FailOnIssues { get; set; }

        /// <summary>
        /// Minimum severity included in the saved JSON report. Items below this threshold
        /// are dropped from the saved file (the in-memory Report is not modified).
        /// <c>null</c> falls back to Project Settings &gt; Smart Auditor &gt; Min Save Severity;
        /// <see cref="Severity.Default"/> explicitly disables filtering.
        /// </summary>
        public Severity? MinSaveSeverity { get; set; }

        /// <summary>
        /// Converts these command-line options to Smart Auditor analysis options.
        /// </summary>
        /// <returns>The analysis options used by <see cref="SmartAuditor"/>.</returns>
        public AnalysisOptions ToAnalysisOptions()
        {
            Validate();

            var options = new AnalysisOptions
            {
                Categories = Categories ?? (Scope.HasValue ? CommandLine.GetCategoriesForScope(Scope.Value) : null),
                IncludedAssemblies = IncludedAssemblies,
                AnalysisSource = AnalysisSource,
                SceneName = SceneName,
                HierarchyPaths = HierarchyPaths,
                AnalyzeReadOnlyPackages = AnalyzeReadOnlyPackages,
                RunProvenance = new AnalysisRunProvenance
                {
                    RequestedScope = Scope,
                    RequestedCategories = Categories,
                    RequestedPlatform = Platform,
                    RequestedCodeContext = CodeContext,
                    RequestedAssetPaths = AssetPaths,
                    RequestedAssetPathPrefixes = AssetPathPrefixes,
                    RequestedAnalysisSource = AnalysisSource,
                    RequestedScenePath = ScenePath,
                    RequestedAnalyzeReadOnlyPackages = AnalyzeReadOnlyPackages,
                    ReportPrettyPrint = PrettyPrint,
                    ReportDebugMode = DebugReport,
                    ReportExportContentMode = ExportContentMode,
                    ReportFailOnIssues = FailOnIssues,
                    ReportMinSaveSeverity = MinSaveSeverity
                }
            };

            options.AssetPathFilter = CreateAssetPathFilter();

            if (Platform.HasValue)
                options.Platform = Platform.Value;

            if (CodeContext.HasValue)
                options.CodeContext = CodeContext.Value;

            return options;
        }

        internal void PrepareProjectState()
        {
            Validate();

            if (AnalysisSource != global::SmartAuditor.Editor.AnalysisSource.LoadedScene)
                return;

            var scenePath = CommandLine.ToProjectRelativePath(ScenePath);
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null)
                throw new ArgumentException($"Scene path '{ScenePath}' does not identify a scene asset in this project.");

            var scene = EditorSceneManager.OpenScene(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException($"Failed to load scene '{scenePath}'.");

            ScenePath = scenePath;
            SceneName = scene.name;
        }

        internal void Validate()
        {
            if (!string.IsNullOrWhiteSpace(ScenePath) && !AnalysisSource.HasValue)
                AnalysisSource = global::SmartAuditor.Editor.AnalysisSource.LoadedScene;

            if (!string.IsNullOrWhiteSpace(ScenePath) && AnalysisSource != global::SmartAuditor.Editor.AnalysisSource.LoadedScene)
                throw new ArgumentException($"{CommandLine.ScenePathArgument} requires {CommandLine.AnalysisSourceArgument} LoadedScene.");

            if (AnalysisSource == global::SmartAuditor.Editor.AnalysisSource.LoadedScene && string.IsNullOrWhiteSpace(ScenePath))
                throw new ArgumentException($"{CommandLine.AnalysisSourceArgument} LoadedScene requires {CommandLine.ScenePathArgument}.");

            if (HierarchyPaths != null && HierarchyPaths.Length > 0 && AnalysisSource != global::SmartAuditor.Editor.AnalysisSource.LoadedScene)
                throw new ArgumentException($"{CommandLine.HierarchyPathsArgument} requires {CommandLine.AnalysisSourceArgument} LoadedScene and {CommandLine.ScenePathArgument}.");
        }

        Predicate<string> CreateAssetPathFilter()
        {
            if ((AssetPaths == null || AssetPaths.Length == 0) &&
                (AssetPathPrefixes == null || AssetPathPrefixes.Length == 0))
            {
                return null;
            }

            return path =>
            {
                if (string.IsNullOrEmpty(path))
                    return false;

                if (AssetPaths != null && AssetPaths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                    return true;

                return AssetPathPrefixes != null &&
                    AssetPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            };
        }
    }

    /// <summary>
    /// Result returned after command-line analysis exports a report.
    /// </summary>
    public sealed class CommandLineResult
    {
        internal CommandLineResult(
            string reportPath,
            int issueCount,
            bool isValid,
            bool failOnIssues,
            Report report)
        {
            ReportPath = reportPath;
            IssueCount = issueCount;
            IsValid = isValid;
            Report = report;
            HasModuleFailures = report?.HasModuleFailures ?? false;
            ExitCode = GetExitCode(isValid, HasModuleFailures, issueCount, failOnIssues);
        }

        /// <summary>Absolute path to the saved JSON report.</summary>
        public string ReportPath { get; }

        /// <summary>Number of issues in the saved report.</summary>
        public int IssueCount { get; }

        /// <summary>Whether the report is complete and valid.</summary>
        public bool IsValid { get; }

        /// <summary>Whether any analysis module aborted with a failure (e.g. a code module that failed to compile).</summary>
        public bool HasModuleFailures { get; }

        /// <summary>Suggested process exit code for batch-mode callers.</summary>
        public int ExitCode { get; }

        /// <summary>The generated report.</summary>
        public Report Report { get; }

        static int GetExitCode(bool isValid, bool hasModuleFailures, int issueCount, bool failOnIssues)
        {
            if (!isValid)
            {
                return 1;
            }

            // A module that aborted (e.g. a code assembly that failed to compile) produces an
            // incomplete report. Never let that masquerade as a clean exit 0.
            if (hasModuleFailures)
            {
                return 1;
            }

            return failOnIssues && issueCount > 0 ? 2 : 0;
        }
    }
}
