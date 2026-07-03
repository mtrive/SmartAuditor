using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Core.Services;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using Debug = UnityEngine.Debug;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Class is responsible for analyzing the Unity project.
    /// </summary>
    public static class SmartAuditor
    {
        internal const string DisplayName = "Smart Auditor";
        internal const string CompactName = "SmartAuditor";

        internal static string ProjectPath
        {
            get
            {
                if (string.IsNullOrEmpty(s_CachedProjectPath))
                    s_CachedProjectPath = PathUtils.GetDirectoryName(UnityEngine.Application.dataPath);
                return s_CachedProjectPath;
            }
        }

        static string s_CachedProjectPath;
        static readonly IAnalysisModuleRegistry s_ModuleRegistry = new AnalysisModuleRegistry();

        /// <summary>
        /// Performs synchronous static analysis of the project using default parameters.
        /// Blocks the calling thread until all modules complete.
        /// </summary>
        /// <param name="progress">Optional progress reporter.</param>
        /// <returns>The generated report.</returns>
        internal static Report Analyze(IProgress progress = null, CancellationToken ct = default) => Analyze(new AnalysisOptions(), progress, ct);

        /// <summary>
        /// Performs synchronous static analysis of the project.
        /// Blocks the calling thread until all modules complete.
        /// </summary>
        /// <param name="options">Parameters to control the analysis process.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="ct">Token to observe for cancellation.</param>
        /// <returns>The generated report.</returns>
        public static Report Analyze(AnalysisOptions options, IProgress progress = null, CancellationToken ct = default)
        {
            var (modules, report) = PrepareAnalysis(options);
            var stopwatch = Stopwatch.StartNew();

            foreach (var module in modules)
            {
                var session = CreateSession(options, report, progress);
                var start = DateTime.Now;
                try
                {
                    var task = module.AnalyzeAsync(session, ct);

                    // Poll until the task completes. ConfigureAwait(false) on background tasks means
                    // their continuations run on thread-pool threads, so we will not deadlock here.
                    while (!task.IsCompleted)
                        Thread.Sleep(10);

                    var result = task.GetAwaiter().GetResult();
                    report.RecordModuleInfo(module, start, DateTime.Now, result);

                    if (result == AnalysisResult.Cancelled)
                    {
                        progress?.Clear();
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    progress?.Clear();
                    report.RecordModuleInfo(module, start, DateTime.Now, AnalysisResult.Cancelled);
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[{DisplayName}] Module {module.Name} failed: {e.Message}\n{e.StackTrace}");
                    report.RecordModuleInfo(module, start, DateTime.Now, AnalysisResult.Failure);
                }
            }

            stopwatch.Stop();
            if (UserPreferences.LogTimingsInfo)
                Debug.Log($"[{DisplayName}] Analysis took: {stopwatch.ElapsedMilliseconds / 1000.0f} seconds.");

            options.OnCompleted?.Invoke(report);
            return report;
        }

        /// <summary>
        /// Performs asynchronous static analysis of the project.
        /// Returns a <see cref="Task{Report}"/> that completes when all modules finish.
        /// </summary>
        /// <param name="options">Parameters to control the analysis process.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="ct">Token to observe for cancellation requests.</param>
        /// <returns>A task that resolves to the generated report.</returns>
        public static async Task<Report> AnalyzeAsync(AnalysisOptions options, IProgress progress = null, CancellationToken ct = default)
        {
            var (modules, report) = PrepareAnalysis(options);
            var stopwatch = Stopwatch.StartNew();

            foreach (var module in modules)
            {
                var session = CreateSession(options, report, progress);
                var start = DateTime.Now;
                try
                {
                    var result = await module.AnalyzeAsync(session, ct);
                    report.RecordModuleInfo(module, start, DateTime.Now, result);

                    if (result == AnalysisResult.Cancelled)
                    {
                        progress?.Clear();
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    progress?.Clear();
                    report.RecordModuleInfo(module, start, DateTime.Now, AnalysisResult.Cancelled);
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[{DisplayName}] Module {module.Name} failed: {e.Message}\n{e.StackTrace}");
                    report.RecordModuleInfo(module, start, DateTime.Now, AnalysisResult.Failure);
                }
            }

            stopwatch.Stop();
            if (UserPreferences.LogTimingsInfo)
                Debug.Log($"[{DisplayName}] Analysis took: {stopwatch.ElapsedMilliseconds / 1000.0f} seconds.");

            options.OnCompleted?.Invoke(report);
            return report;
        }

        static (AnalysisModule[] modules, Report report) PrepareAnalysis(AnalysisOptions options)
        {
            s_ModuleRegistry.Initialize();

            ConfigureAnalysisOptions(options);

            var report = InitializeReport(options);

            if (!IsTargetPlatformSupported(options.Platform))
                return (Array.Empty<AnalysisModule>(), report);

            var modules = GetModulesToAnalyze(options);
            return (modules, report);
        }

        static AnalysisSession CreateSession(AnalysisOptions options, Report report, IProgress progress)
        {
            return new AnalysisSession(
                options,
                progress,
                items =>
                {
                    var filtered = items.Where(i => !i.Descriptor.IsValid() || i.Descriptor.IsApplicable(options));
                    report.AddIssues(filtered);
                    options.OnIncomingIssues?.Invoke(filtered);
                },
                onInsightTableReported: report.AddInsightTable,
                onMessageReported: report.AddMessage);
        }

        static void ConfigureAnalysisOptions(AnalysisOptions options)
        {
            if (options.Platform == BuildTarget.NoTarget)
                options.Platform = EditorUserBuildSettings.activeBuildTarget;

            if (options.Categories == null)
                options.Categories = s_ModuleRegistry.GetModules().SelectMany(m => m.Categories).ToArray();
        }

        static Report InitializeReport(AnalysisOptions options)
        {
            var report = options.ExistingReport ?? new Report(options);

            if (options.ExistingReport != null)
            {
                if (report.SessionInfo == null)
                    report.SessionInfo = new SessionInfo(options);

                var reportCategories = report.SessionInfo.Categories?.ToList() ?? new System.Collections.Generic.List<AnalysisCategory>();
                reportCategories.AddRange(options.Categories);

                var snapshot = new AnalysisOptionsSnapshot(options)
                {
                    Categories = reportCategories.Distinct().ToArray()
                };
                report.SessionInfo.AnalysisConfiguration = snapshot;

                foreach (var category in options.Categories)
                    report.ClearIssues(category);
            }

            return report;
        }

        static bool IsTargetPlatformSupported(BuildTarget platform)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(platform), platform))
            {
                Debug.LogError($"[{DisplayName}] Build target {platform} is not supported in this Unity Editor");
                return false;
            }
            return true;
        }

        static AnalysisModule[] GetModulesToAnalyze(AnalysisOptions options)
        {
            var requestedModules = s_ModuleRegistry.GetModules(options.Categories).Distinct().ToArray();

            var filteredModules = requestedModules
                .Where(m => m != null && CoreUtils.SupportsPlatform(m.GetType(), options.Platform))
                .ToArray();

            if (options.AnalysisSource.HasValue)
            {
                var requiredSource = options.AnalysisSource.Value;
                filteredModules = filteredModules
                    .Where(m =>
                    {
                        var supportedSource = m.SupportedAnalysisSource;
                        return supportedSource == null || supportedSource == requiredSource;
                    })
                    .ToArray();
            }

            return filteredModules;
        }

        internal static IReadOnlyList<AnalysisModule> GetModules(AnalysisCategory[] categories) => s_ModuleRegistry.GetModules(categories);

        /// <summary>Gets the display name of the specified issue category.</summary>
        internal static string GetCategoryName(AnalysisCategory category) => category.ToString();

        // Only used for testing
        internal static string[] GetDescriptorIDs() => s_ModuleRegistry.GetAllDescriptorIds();

        // Only used for testing
        internal static bool IsModuleSupported(AnalysisCategory category) => s_ModuleRegistry.IsModuleSupported(category);
    }
}
