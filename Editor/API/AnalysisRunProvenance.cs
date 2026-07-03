using System;
using System.Linq;
using Newtonsoft.Json;
using SmartAuditor.Editor.Core.Serialization;
using UnityEditor;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Captures run provenance details used for report session snapshots.
    /// This includes caller-requested values and command-line report export settings.
    /// </summary>
    [Serializable]
    internal sealed class AnalysisRunProvenance
    {
        [JsonConverter(typeof(BuildTargetJsonConverter))]
        public BuildTarget? RequestedPlatform { get; set; }

        public AnalysisScope? RequestedScope { get; set; }
        public AnalysisCategory[] RequestedCategories { get; set; }
        public CodeContext? RequestedCodeContext { get; set; }
        public string[] RequestedAssetPaths { get; set; }
        public string[] RequestedAssetPathPrefixes { get; set; }
        public AnalysisSource? RequestedAnalysisSource { get; set; }
        public string RequestedScenePath { get; set; }
        public bool? RequestedAnalyzeReadOnlyPackages { get; set; }
        public bool? ReportPrettyPrint { get; set; }
        public bool? ReportDebugMode { get; set; }
        public ReportExportContentMode? ReportExportContentMode { get; set; }
        public bool? ReportFailOnIssues { get; set; }

        // Minimum severity threshold applied when saving the report. null means
        // "fall back to Project Settings"; Severity.Default means "no filter".
        public Severity? ReportMinSaveSeverity { get; set; }
        public string ReportOutputPath { get; set; }
        public string UnityLogPath { get; set; }

        public AnalysisRunProvenance()
        {
        }

        public AnalysisRunProvenance(AnalysisRunProvenance original)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));

            RequestedPlatform = original.RequestedPlatform;
            RequestedScope = original.RequestedScope;
            RequestedCategories = original.RequestedCategories?.ToArray();
            RequestedCodeContext = original.RequestedCodeContext;
            RequestedAssetPaths = original.RequestedAssetPaths?.ToArray();
            RequestedAssetPathPrefixes = original.RequestedAssetPathPrefixes?.ToArray();
            RequestedAnalysisSource = original.RequestedAnalysisSource;
            RequestedScenePath = original.RequestedScenePath;
            RequestedAnalyzeReadOnlyPackages = original.RequestedAnalyzeReadOnlyPackages;
            ReportPrettyPrint = original.ReportPrettyPrint;
            ReportDebugMode = original.ReportDebugMode;
            ReportExportContentMode = original.ReportExportContentMode;
            ReportFailOnIssues = original.ReportFailOnIssues;
            ReportMinSaveSeverity = original.ReportMinSaveSeverity;
            ReportOutputPath = original.ReportOutputPath;
            UnityLogPath = original.UnityLogPath;
        }
    }
}
