using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Settings
{
    internal static class UserPreferences
    {
        public static string Path => k_PreferencesKey;
        static readonly string k_PreferencesKey = "Preferences/Analysis/Smart Auditor";

        static readonly string k_EditorPrefsPrefix = "SmartAuditor";

        static readonly bool k_UseRoslynAnalyzersDefault = true;

        static readonly bool k_LogTimingsInfoDefault = false;

        static readonly bool k_AnalyzeAfterBuildDefault = false;

        static readonly bool k_CollectShaderVariantsOnBuildDefault = false;

        static readonly bool k_AnalyzeReadOnlyPackagesDefault = false;

        static readonly bool k_FailBuildOnIssuesDefault = false;

        static readonly bool k_PrettifyJSONOutputDefault = false;

        static readonly bool k_DebugReportDefault = false;

        static readonly ReportExportContentMode k_ReportExportContentModeDefault = ReportExportContentMode.Full;

        static readonly bool k_AgentBridgeEnabledDefault = false;

        static readonly bool k_SuppressPackageCompilerMessagesDefault = true;

        internal static string LoadSavePath
        {
            get => EditorPrefs.GetString(MakeKey(nameof(LoadSavePath)), string.Empty);
            set => EditorPrefs.SetString(MakeKey(nameof(LoadSavePath)), value);
        }

        /// <summary>
        /// If enabled, SmartAuditor will re-run the BuildReport analysis every time the project is built.
        /// </summary>
        public static bool AnalyzeAfterBuild
        {
            get => EditorPrefs.GetBool(MakeKey(nameof(AnalyzeAfterBuild)), k_AnalyzeAfterBuildDefault);
            set => EditorPrefs.SetBool(MakeKey(nameof(AnalyzeAfterBuild)), value);
        }

        /// <summary>
        /// If enabled, Smart Auditor captures shader and compute shader variants during Unity builds
        /// for variant insight tables and built fragment variant counts. Adds build-time overhead.
        /// </summary>
        public static bool CollectShaderVariantsOnBuild
        {
            get => EditorPrefs.GetBool(MakeKey(nameof(CollectShaderVariantsOnBuild)), k_CollectShaderVariantsOnBuildDefault);
            set => EditorPrefs.SetBool(MakeKey(nameof(CollectShaderVariantsOnBuild)), value);
        }

        /// <summary>
        /// If enabled, SmartAuditor will analyze package code and assets from read-only packages by default.
        /// </summary>
        public static bool AnalyzeReadOnlyPackages
        {
            get => EditorPrefs.GetBool(MakeKey(nameof(AnalyzeReadOnlyPackages)), k_AnalyzeReadOnlyPackagesDefault);
            set => EditorPrefs.SetBool(MakeKey(nameof(AnalyzeReadOnlyPackages)), value);
        }

        /// <summary>
        /// If enabled, SmartAuditor will use Roslyn Analyzer DLLs that are present in the project
        /// </summary>
        public static bool UseRoslynAnalyzers
        {
            get => EditorPrefs.GetBool(MakeKey(nameof(UseRoslynAnalyzers)), k_UseRoslynAnalyzersDefault);
            set => EditorPrefs.SetBool(MakeKey(nameof(UseRoslynAnalyzers)), value);
        }

        /// <summary>
        /// If enabled, any issue reported by SmartAuditor will cause the build to fail.
        /// </summary>
        public static bool FailBuildOnIssues
        {
            get => EditorPrefs.GetBool(MakeKey(nameof(FailBuildOnIssues)), k_FailBuildOnIssuesDefault);
            set => EditorPrefs.SetBool(MakeKey(nameof(FailBuildOnIssues)), value);
        }

        public static bool PrettifyJsonOutput
        {
            get => EditorPrefs.GetBool(MakeKey(nameof(PrettifyJsonOutput)), k_PrettifyJSONOutputDefault);
            set => EditorPrefs.SetBool(MakeKey(nameof(PrettifyJsonOutput)), value);
        }

        /// <summary>
        /// If enabled, saved JSON reports include metadata used to debug Smart Auditor itself.
        /// </summary>
        public static bool DebugReport
        {
            get => EditorPrefs.GetBool(MakeKey(nameof(DebugReport)), k_DebugReportDefault);
            set => EditorPrefs.SetBool(MakeKey(nameof(DebugReport)), value);
        }

        /// <summary>
        /// Controls which report sections are written when manually saving a report JSON file.
        /// </summary>
        public static ReportExportContentMode ReportExportContentMode
        {
            get => (ReportExportContentMode)EditorPrefs.GetInt(MakeKey(nameof(ReportExportContentMode)), (int)k_ReportExportContentModeDefault);
            set => EditorPrefs.SetInt(MakeKey(nameof(ReportExportContentMode)), (int)value);
        }

        /// <summary>
        /// If enabled, Smart Auditor watches the project-local agent bridge request folder.
        /// </summary>
        public static bool AgentBridgeEnabled
        {
            get => EditorPrefs.GetBool(MakeKey(nameof(AgentBridgeEnabled)), k_AgentBridgeEnabledDefault);
            set => EditorPrefs.SetBool(MakeKey(nameof(AgentBridgeEnabled)), value);
        }

        public static bool LogTimingsInfo
        {
            get => EditorPrefs.GetBool(MakeKey(nameof(LogTimingsInfo)), k_LogTimingsInfoDefault);
            set => EditorPrefs.SetBool(MakeKey(nameof(LogTimingsInfo)), value);
        }

        /// <summary>
        /// If enabled, non-error compiler messages (warnings, info) coming from source files
        /// outside Assets/ are dropped from the report. Errors are always kept. Targets the
        /// noise from Unity-package and third-party-package code that the project doesn't own
        /// (e.g. CS0109 / CS8019 in com.unity.ugui or shared packages).
        /// </summary>
        public static bool SuppressPackageCompilerMessages
        {
            get => EditorPrefs.GetBool(MakeKey(nameof(SuppressPackageCompilerMessages)), k_SuppressPackageCompilerMessagesDefault);
            set => EditorPrefs.SetBool(MakeKey(nameof(SuppressPackageCompilerMessages)), value);
        }

        internal static readonly AnalysisScope k_AnalysisScopeDefault =
            AnalysisScope.Code |
            AnalysisScope.ProjectSettings |
            AnalysisScope.Assets |
            AnalysisScope.Prefabs |
            AnalysisScope.Shaders;
        internal static readonly BuildTarget k_AnalysisTargetPlatformDefault = BuildTarget.NoTarget;
        internal static readonly CodeContext k_CodeContextDefault = CodeContext.Runtime;

        public static AnalysisScope AnalysisScope
        {
            get => (AnalysisScope)EditorPrefs.GetInt(
                MakeKey(nameof(AnalysisScope)), (int)k_AnalysisScopeDefault);
            set => EditorPrefs.SetInt(MakeKey(nameof(AnalysisScope)), (int)value);
        }

        public static BuildTarget AnalysisTargetPlatform
        {
            get => (BuildTarget)EditorPrefs.GetInt(
                MakeKey(nameof(AnalysisTargetPlatform)), (int)k_AnalysisTargetPlatformDefault);
            set => EditorPrefs.SetInt(MakeKey(nameof(AnalysisTargetPlatform)), (int)value);
        }

        public static CodeContext CodeContext
        {
            get => (CodeContext)EditorPrefs.GetInt(
                MakeKey(nameof(CodeContext)), (int)k_CodeContextDefault);
            set => EditorPrefs.SetInt(MakeKey(nameof(CodeContext)), (int)value);
        }

        static UserPreferences()
        {
        }

        static string MakeKey(string key)
        {
            return $"{k_EditorPrefsPrefix}.{key}";
        }
    }
}
