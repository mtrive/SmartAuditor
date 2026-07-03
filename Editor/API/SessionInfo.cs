using System;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Contains information about the session in which a <seealso cref="Report"/> was created.
    /// This includes both session metadata and a snapshot of the analysis configuration used.
    /// </summary>
    [Serializable]
    public class SessionInfo
    {
        /// <summary>
        /// Constructor used by JSON deserialization.
        /// </summary>
        [JsonConstructor]
        public SessionInfo()
        {
            AnalysisConfiguration = new AnalysisOptionsSnapshot();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="analysisOptions">AnalysisOptions object which was passed to <see cref="SmartAuditor.Analyze(AnalysisOptions)"/> to create the Report</param>
        public SessionInfo(AnalysisOptions analysisOptions)
        {
            if (analysisOptions == null)
                throw new ArgumentNullException(nameof(analysisOptions));

            AnalysisConfiguration = new AnalysisOptionsSnapshot(analysisOptions);
        }

        /// <summary>
        /// The version number of the Smart Auditor package which was used.
        /// </summary>
        public string SmartAuditorVersion { get; set; }

        /// <summary>
        /// The version of Unity which was used.
        /// </summary>
        public string UnityVersion { get; set; }

        /// <summary>
        /// The Company Name string in the project's Project Settings.
        /// </summary>
        public string CompanyName { get; set; }

        /// <summary>
        /// The Product Name string in the project's Project Settings.
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// The `Application.cloudProjectId` identifier for the project.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// The Product Version string in the project's Project Settings.
        /// </summary>
        public string ProjectVersion { get; set; }

        /// <summary>
        /// The version control revision (commit hash, change-list, etc.) of the project at the time of analysis.
        /// </summary>
        public string VersionControlRevision { get; set; }

        /// <summary>
        /// The date and time at which the Report was created.
        /// </summary>
        public string DateTime { get; set; }

        /// <summary>
        /// The `SystemInfo.operatingSystem` identifier for the operating system on which the Unity Editor was running.
        /// </summary>
        public string HostPlatform { get; set; }

        /// <summary>
        /// A snapshot of the analysis configuration that was used for this session.
        /// </summary>
        public AnalysisOptionsSnapshot AnalysisConfiguration { get; set; }

        /// <summary>
        /// User-supplied C# preprocessor defines that were active during this analysis,
        /// after trimming, validation, and deduplication. Sourced from Project Settings &gt;
        /// Smart Auditor &gt; Compilation. Empty if the user did not configure any.
        /// </summary>
        public string[] AdditionalDefines { get; set; }

        /// <summary>
        /// User-supplied C# preprocessor defines that were stripped from the analyzer's
        /// resolved compilation set. Sourced from Project Settings &gt; Smart Auditor &gt;
        /// Compilation. Empty if the user did not configure any.
        /// </summary>
        public string[] RemovedDefines { get; set; }

        /// <summary>
        /// Project-wide C# preprocessor defines that were active during this analysis.
        /// Computed as the intersection of every player assembly's define set, with
        /// dev-only flags (<c>DEVELOPMENT_BUILD</c>, <c>ENABLE_PROFILER</c>, <c>DEBUG</c>,
        /// <c>TRACE</c>, <c>UNITY_ASSERTIONS</c>) excluded. Captures Unity built-ins
        /// (<c>UNITY_2026_1_OR_NEWER</c>, <c>UNITY_STANDALONE_*</c>) and Project Settings
        /// &gt; Player &gt; Scripting Define Symbols, while filtering out package
        /// versionDefines and asmdef-specific defines.
        /// </summary>
        public string[] GlobalDefines { get; set; }

        /// <summary>
        /// Paths of Roslyn analyzer / source-generator DLLs that Unity routes to compilation
        /// (asset label <c>RoslynAnalyzer</c>). Reported here because analyzers are build-time
        /// tooling, not assemblies linked into the player. <see cref="UseRoslynAnalyzers"/>
        /// reflects whether they were applied during this session; this list is the inventory
        /// regardless of the toggle.
        /// </summary>
        public string[] RoslynAnalyzerDllPaths { get; set; }

        /// <summary>
        /// Convenience property to access the analysis categories from the configuration.
        /// </summary>
        [JsonIgnore]
        public AnalysisCategory[] Categories
        {
            get => AnalysisConfiguration?.Categories;
            set
            {
                if (AnalysisConfiguration == null)
                    AnalysisConfiguration = new AnalysisOptionsSnapshot();
                AnalysisConfiguration.Categories = value;
            }
        }

        /// <summary>
        /// Convenience property to access the analysis platform from the configuration.
        /// </summary>
        [JsonIgnore]
        public BuildTarget Platform
        {
            get => AnalysisConfiguration?.Platform ?? BuildTarget.NoTarget;
            set
            {
                if (AnalysisConfiguration == null)
                    AnalysisConfiguration = new AnalysisOptionsSnapshot();
                AnalysisConfiguration.Platform = value;
            }
        }

        /// <summary>
        /// Convenience property to access the code context from the configuration.
        /// </summary>
        [JsonIgnore]
        public CodeContext CodeContext
        {
            get => AnalysisConfiguration?.CodeContext ?? CodeContext.Runtime;
            set
            {
                if (AnalysisConfiguration == null)
                    AnalysisConfiguration = new AnalysisOptionsSnapshot();
                AnalysisConfiguration.CodeContext = value;
            }
        }

        /// <summary>
        /// Convenience property to access the Roslyn analyzers setting from the configuration.
        /// </summary>
        [JsonIgnore]
        public bool UseRoslynAnalyzers
        {
            get => AnalysisConfiguration?.UseRoslynAnalyzers ?? false;
            set
            {
                if (AnalysisConfiguration == null)
                    AnalysisConfiguration = new AnalysisOptionsSnapshot();
                AnalysisConfiguration.UseRoslynAnalyzers = value;
            }
        }
    }
}
