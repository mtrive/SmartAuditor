using System;
using Newtonsoft.Json;
using SmartAuditor.Editor.Core.Serialization;
using UnityEditor;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// A snapshot of analysis configuration settings used during a specific analysis session.
    /// This captures the essential analysis parameters without the runtime delegates and non-serializable data.
    /// </summary>
    [Serializable]
    public class AnalysisOptionsSnapshot
    {
        /// <summary>
        /// Issue Categories that were included in the analysis.
        /// </summary>
        public AnalysisCategory[] Categories { get; set; }

        /// <summary>
        /// The analysis platform that was used.
        /// </summary>
        [JsonConverter(typeof(BuildTargetJsonConverter))]
        public BuildTarget Platform { get; set; }

        /// <summary>
        /// Assemblies that were analyzed. If null, all compiled assemblies were analyzed.
        /// </summary>
        public string[] IncludedAssemblies { get; set; }

        /// <summary>
        /// The code context that was analyzed (Editor, Runtime, or both).
        /// </summary>
        public CodeContext CodeContext { get; set; }

        /// <summary>
        /// Whether Roslyn analyzers were used during the analysis.
        /// </summary>
        public bool UseRoslynAnalyzers { get; set; }

        /// <summary>
        /// Whether assets and assemblies from read-only packages were included in the analysis.
        /// </summary>
        public bool AnalyzeReadOnlyPackages { get; set; }

        /// <summary>
        /// Requested high-level scope from the caller, when provided.
        /// </summary>
        public AnalysisScope? RequestedScope { get; set; }

        /// <summary>
        /// Requested categories from the caller before any scope expansion.
        /// </summary>
        public AnalysisCategory[] RequestedCategories { get; set; }

        /// <summary>
        /// Requested platform from the caller before default resolution.
        /// </summary>
        [JsonConverter(typeof(BuildTargetJsonConverter))]
        public BuildTarget? RequestedPlatform { get; set; }

        /// <summary>
        /// Requested code context from the caller before default resolution.
        /// </summary>
        public CodeContext? RequestedCodeContext { get; set; }

        /// <summary>
        /// Requested exact asset paths used to build the asset filter, when provided.
        /// </summary>
        public string[] RequestedAssetPaths { get; set; }

        /// <summary>
        /// Requested asset path prefixes used to build the asset filter, when provided.
        /// </summary>
        public string[] RequestedAssetPathPrefixes { get; set; }

        /// <summary>
        /// Effective analysis source used to include compatible modules.
        /// </summary>
        public AnalysisSource? AnalysisSource { get; set; }

        /// <summary>
        /// Requested analysis source from the caller before defaults.
        /// </summary>
        public AnalysisSource? RequestedAnalysisSource { get; set; }

        /// <summary>
        /// Effective scene name used for loaded-scene analysis.
        /// </summary>
        public string SceneName { get; set; }

        /// <summary>
        /// Requested scene path, typically provided via command-line integrations.
        /// </summary>
        public string RequestedScenePath { get; set; }

        /// <summary>
        /// Effective hierarchy paths used for partial loaded-scene analysis.
        /// </summary>
        public string[] HierarchyPaths { get; set; }

        /// <summary>
        /// Requested read-only package flag from the caller.
        /// </summary>
        public bool? RequestedAnalyzeReadOnlyPackages { get; set; }

        /// <summary>
        /// Whether pretty-printing was requested for the exported report.
        /// </summary>
        public bool? ReportPrettyPrint { get; set; }

        /// <summary>
        /// Whether debug report mode was requested for the exported report.
        /// </summary>
        public bool? ReportDebugMode { get; set; }

        /// <summary>
        /// Requested export content mode for the exported report.
        /// </summary>
        public ReportExportContentMode? ReportExportContentMode { get; set; }

        /// <summary>
        /// Whether the run requested non-zero exit on issues.
        /// </summary>
        public bool? ReportFailOnIssues { get; set; }

        /// <summary>
        /// Minimum severity threshold applied when saving the report. <c>null</c> means
        /// the run inherited the Project Settings default; <see cref="Severity.Default"/>
        /// means filtering was explicitly disabled. Items below the threshold were dropped
        /// from the saved JSON only -- the in-memory Report still contained them.
        /// </summary>
        public Severity? ReportMinSaveSeverity { get; set; }

        /// <summary>
        /// Output path used when the report was written.
        /// </summary>
        public string ReportOutputPath { get; set; }

        /// <summary>
        /// Unity log path associated with the run, when available.
        /// </summary>
        public string UnityLogPath { get; set; }

        /// <summary>
        /// Default constructor for serialization.
        /// </summary>
        public AnalysisOptionsSnapshot()
        {
        }

        /// <summary>
        /// Creates a snapshot from an AnalysisOptions object.
        /// </summary>
        /// <param name="options">The AnalysisOptions to snapshot</param>
        public AnalysisOptionsSnapshot(AnalysisOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var provenance = options.RunProvenance;

            Categories = options.Categories;
            Platform = options.Platform;
            IncludedAssemblies = options.IncludedAssemblies;
            CodeContext = options.CodeContext;
            UseRoslynAnalyzers = UserPreferences.UseRoslynAnalyzers;
            AnalyzeReadOnlyPackages = options.AnalyzeReadOnlyPackages;
            AnalysisSource = options.AnalysisSource;
            SceneName = options.SceneName;
            HierarchyPaths = options.HierarchyPaths;

            RequestedScope = provenance?.RequestedScope;
            RequestedCategories = provenance?.RequestedCategories;
            RequestedPlatform = provenance?.RequestedPlatform;
            RequestedCodeContext = provenance?.RequestedCodeContext;
            RequestedAssetPaths = provenance?.RequestedAssetPaths;
            RequestedAssetPathPrefixes = provenance?.RequestedAssetPathPrefixes;
            RequestedAnalysisSource = provenance?.RequestedAnalysisSource;
            RequestedScenePath = provenance?.RequestedScenePath;
            RequestedAnalyzeReadOnlyPackages = provenance?.RequestedAnalyzeReadOnlyPackages;
            ReportPrettyPrint = provenance?.ReportPrettyPrint;
            ReportDebugMode = provenance?.ReportDebugMode;
            ReportExportContentMode = provenance?.ReportExportContentMode;
            ReportFailOnIssues = provenance?.ReportFailOnIssues;
            ReportMinSaveSeverity = provenance?.ReportMinSaveSeverity;
            ReportOutputPath = provenance?.ReportOutputPath;
            UnityLogPath = provenance?.UnityLogPath;
        }

        /// <summary>
        /// Creates a new AnalysisOptions object from this snapshot.
        /// Note: This will not include delegates or other runtime-only data.
        /// </summary>
        /// <returns>A new AnalysisOptions object with the settings from this snapshot</returns>
        public AnalysisOptions ToAnalysisOptions()
        {
            return new AnalysisOptions(copyParamsFromGlobal: false)
            {
                Categories = Categories,
                Platform = Platform,
                IncludedAssemblies = IncludedAssemblies,
                CodeContext = CodeContext,
                AnalyzeReadOnlyPackages = AnalyzeReadOnlyPackages,
                AnalysisSource = AnalysisSource,
                SceneName = SceneName,
                HierarchyPaths = HierarchyPaths
            };
        }
    }
}
