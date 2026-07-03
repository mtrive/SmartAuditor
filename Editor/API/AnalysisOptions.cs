using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Represents an object which can be passed to <see cref="SmartAuditor"/> static methods to specify how analysis should be performed and to provide delegates to be called when analysis steps have completed.
    /// AnalysisOptions defaults to values which instruct Smart Auditor to analyze everything in the project for the current build target, but instances can be populated with custom data in an object initializer to provide additional constraints.
    /// </summary>
    public class AnalysisOptions
    {
        BuildTarget m_Platform;
        DiagnosticParams m_DiagnosticParams;

        /// <summary>
        /// Issue categories to include in the analysis. If null, the analysis includes all categories.
        /// </summary>
        public AnalysisCategory[] Categories { get; set; }

        /// <summary>
        /// Optional asset filter. Return true to include an asset path in analysis.
        /// If null, all assets are eligible.
        /// </summary>
        public Predicate<string> AssetPathFilter { get; set; }

        /// <summary>
        /// Optional hierarchy root paths for partial scene analysis.
        /// If provided, only GameObjects whose hierarchy paths start with one of these prefixes
        /// (for example, "Root/Child") will be analyzed. If null or empty, the full scene is analyzed.
        /// </summary>
        public string[] HierarchyPaths { get; set; }

        /// <summary>
        /// Optional scene name to analyze.
        /// Loaded-scene command-line flows resolve this from the loaded scene path before analysis.
        /// </summary>
        public string SceneName { get; set; }

        /// <summary>
        /// Specifies the source or trigger point for analysis, which determines which modules are included.
        /// If null, all modules matching the categories are included.
        /// </summary>
        public AnalysisSource? AnalysisSource { get; set; }

        /// <summary>
        /// Specifies which code context should be analyzed (Editor, Runtime, or both).
        /// The default is <see cref="CodeContext.Runtime"/>.
        /// </summary>
        public CodeContext CodeContext { get; set; }

        /// <summary>
        /// Assemblies to analyze. If null, all compiled assemblies will be analyzed.
        /// </summary>
        public string[] IncludedAssemblies { get; set; }

        /// <summary>
        /// Whether assets and assemblies from read-only packages should be analyzed.
        /// </summary>
        public bool AnalyzeReadOnlyPackages { get; set; }

        /// <summary>
        /// Reports a batch of new issues. Note that this can be called multiple times per analysis.
        /// </summary>
        public Action<IEnumerable<ReportItem>> OnIncomingIssues { get; set; }

        /// <summary>
        /// Notifies that all Modules completed their analysis.
        /// </summary>
        public Action<Report> OnCompleted { get; set; }

        /// <summary>
        /// The DiagnosticParams object which defines the customizable thresholds for reporting certain diagnostics.
        /// By default (when constructed with <c>copyParamsFromGlobal=true</c>), this makes a copy of
        /// <see cref="SmartAuditorSettings.DiagnosticParams"/>.
        /// </summary>
        public DiagnosticParams DiagnosticParams
        {
            get => m_DiagnosticParams;
            set
            {
                m_DiagnosticParams = value;
                m_DiagnosticParams?.SetAnalysisPlatform(Platform);
            }
        }

        // AnalysisOptions copy of the global rules. Can be added to with WithAdditionalDiagnosticRules but doesn't need
        // to be exposed to the API.
        internal DiagnosticRules Rules { get; set; }

        internal Report ExistingReport { get; set; }
        internal AnalysisRunProvenance RunProvenance { get; set; }

        /// <summary>
        /// Analysis platform.
        /// The default value is <see cref="BuildTarget.NoTarget"/> and resolves to the active build target
        /// during analysis preparation.
        /// </summary>
        public BuildTarget Platform
        {
            get => m_Platform;

            set
            {
                m_Platform = value;
                DiagnosticParams?.SetAnalysisPlatform(Platform);
            }
        }

        internal string PlatformAsString => Platform.ToString();

        /// <summary>
        /// AnalysisOptions constructor.
        /// </summary>
        /// <param name="copyParamsFromGlobal">If true, the global <see cref="SmartAuditorSettings"/> will register DiagnosticParams defaults, save any changes and copy the data into this object. This is usually the desired behaviour, but is not allowed during serialization. </param>
        public AnalysisOptions(bool copyParamsFromGlobal = true)
        {
            Rules = new DiagnosticRules();

            if (copyParamsFromGlobal)
            {
                // Check for any new defaults (newly-installed package, new user modules, or an updated version of the package since last analysis)
                SmartAuditorSettings.instance.DiagnosticParams.RegisterParameters();
                SmartAuditorSettings.instance.Save();

                Rules = new DiagnosticRules(SmartAuditorSettings.instance.Rules);
                Rules.AddRules(SmartAuditorPrefs.instance.Rules);

                DiagnosticParams = new DiagnosticParams(SmartAuditorSettings.instance.DiagnosticParams);
            }

            Platform = BuildTarget.NoTarget;
            CodeContext = CodeContext.Runtime;
            AnalyzeReadOnlyPackages = copyParamsFromGlobal && UserPreferences.AnalyzeReadOnlyPackages;
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        /// <param name="original">The AnalysisOptions object to copy from.</param>
        public AnalysisOptions(AnalysisOptions original)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));

            Rules = original.Rules != null ? new DiagnosticRules(original.Rules) : new DiagnosticRules();
            DiagnosticParams = original.DiagnosticParams != null ? new DiagnosticParams(original.DiagnosticParams) : null;

            Categories = original.Categories?.ToArray();
            IncludedAssemblies = original.IncludedAssemblies?.ToArray();
            AnalyzeReadOnlyPackages = original.AnalyzeReadOnlyPackages;
            CodeContext = original.CodeContext;
            Platform = original.Platform;

            OnIncomingIssues = original.OnIncomingIssues;
            OnCompleted = original.OnCompleted;

            ExistingReport = original.ExistingReport;

            AssetPathFilter = original.AssetPathFilter;
            HierarchyPaths = original.HierarchyPaths?.ToArray();
            AnalysisSource = original.AnalysisSource;
            SceneName = original.SceneName;
            RunProvenance = original.RunProvenance != null ? new AnalysisRunProvenance(original.RunProvenance) : null;
        }

        /// <summary>
        /// Adds a list of additional Rules which will be applied during analysis.
        /// </summary>
        /// <param name="rules">Additional Rules to impose.</param>
        /// <returns>This AnalysisOptions object, after adding the additional Rules.</returns>
        public AnalysisOptions WithAdditionalDiagnosticRules(List<Rule> rules)
        {
            if (rules == null || rules.Count == 0)
                return this;

            Rules ??= new DiagnosticRules();

            foreach (var rule in rules)
            {
                Rules.AddRule(rule);
            }

            return this;
        }
    }
}
