using System;
using System.Collections.Generic;
using System.Linq;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.UI.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor.UI
{
    internal sealed class SmartAuditorWindow : EditorWindow, IHasCustomMenu, IReportItemFilter
    {
        #region Types and constants

        enum AnalysisState
        {
            Initializing,
            Initialized,
            InProgress,
            Completed,
            Valid
        }

        const string k_Discard = "Start New Analysis";
        const string k_DiscardQuestion = "Starting a new analysis will discard the current report and any unsaved changes.";

        static readonly string k_NavigatorSearchPrefKey = SmartAuditor.CompactName + ".Navigator.SearchString";
        static readonly string k_MinViewSeverityPrefKey = SmartAuditor.CompactName + ".Navigator.MinViewSeverity";

        static readonly CodeContext[] s_CodeContextChoices =
        {
            CodeContext.Editor,
            CodeContext.Runtime,
            CodeContext.All
        };

        static readonly GUIContent[] s_CodeContextLabels =
        {
            new GUIContent("Editor"),
            new GUIContent("Runtime"),
            new GUIContent("All")
        };

        const AnalysisScope k_MainWindowSupportedScope =
            AnalysisScope.Code |
            AnalysisScope.ProjectSettings |
            AnalysisScope.Assets |
            AnalysisScope.Prefabs |
            AnalysisScope.Shaders;

        const AnalysisScope k_AssetDetailScope =
            AnalysisScope.Assets |
            AnalysisScope.Prefabs |
            AnalysisScope.Shaders;

        #endregion

        #region Entry points

        static SmartAuditorWindow s_Instance;

        public static SmartAuditorWindow Instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = ShowWindow();
                return s_Instance;
            }
        }

        [MenuItem("Window/Analysis/" + SmartAuditor.DisplayName)]
        public static SmartAuditorWindow ShowWindow()
        {
            var wnd = GetWindow(typeof(SmartAuditorWindow)) as SmartAuditorWindow;
            if (wnd != null)
            {
                s_Instance = wnd;
                wnd.minSize = new Vector2(LayoutSize.MinWindowWidth, LayoutSize.MinWindowHeight);
                wnd.titleContent = Contents.WindowTitle;
            }

            return wnd;
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(Contents.PreferencesMenuItem, on: false, OpenPreferences);
        }

        static void OpenPreferences()
        {
            var preferencesWindow = SettingsService.OpenUserPreferences(UserPreferences.Path);
            if (preferencesWindow == null)
            {
                Debug.LogError($"Could not find Preferences for 'Analysis/{SmartAuditor.DisplayName}'");
            }
        }

        #endregion

        #region Serialized state

        [SerializeField]
        AnalysisState m_AnalysisState = AnalysisState.Initializing;
        [SerializeField]
        bool m_HasPendingUnsavedChanges;
        [SerializeField]
        bool m_IsLoadedByUser;
        [SerializeField]
        AnalysisScope m_AnalysisScope = AnalysisScope.All;
        [SerializeField]
        BuildTarget m_Platform = BuildTarget.NoTarget;
        [SerializeField]
        CodeContext m_CodeContext = CodeContext.Runtime;
        [SerializeField]
        bool m_OptionsFoldout = true;
        [SerializeField]
        float m_NavigatorPaneWidth = 200f;
        [SerializeField]
        string m_NavigatorSearchText = "";
        [SerializeField]
        Severity m_MinViewSeverity = Severity.Default;
        [SerializeField]
        ReportViewWorkspaceState m_ReportViewWorkspaceState = new ReportViewWorkspaceState();

        #endregion

        #region Instance state

        // Not [SerializeField]: after a domain reload the window restores its report by reading
        // the canonical JSON autosave from disk (see TryLoadAutosavedReport). Persisting Report
        // through Unity's binary serializer would be a second parallel survival mechanism.
        Report m_Report;
        Report m_PreviousReport;
        AnalysisReportView m_ReportView;

        bool m_ShouldRefresh;
        ScrollingAnalysisProgress m_ScrollingAnalysisProgress;
        readonly List<string> m_AnalysisLogBuffer = new List<string>(1024);

        Impact m_ImpactSelection = Impact.All;

        BuildTarget[] m_SupportedBuildTargets;
        GUIContent[] m_PlatformContents;

        NavigatorPanel m_NavigatorPanel;

        #endregion

        #region Properties

        internal bool IsReportReadOnly => m_IsLoadedByUser;

        internal Report Report => m_Report;

        internal ReportViewWorkspaceState ReportViewWorkspaceState => m_ReportViewWorkspaceState;

        internal void ClearReportViewWorkspaceState()
        {
            m_ReportViewWorkspaceState = new ReportViewWorkspaceState();
        }

        #endregion

        #region Unity lifecycle

        void OnEnable()
        {
            s_Instance = this;

            var currentState = m_AnalysisState;
            m_AnalysisState = AnalysisState.Initializing;

            var buildTargets = Enum.GetValues(typeof(BuildTarget)).Cast<BuildTarget>();
            var supportedBuildTargets = buildTargets.Where(bt => bt != BuildTarget.NoTarget &&
                BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(bt), bt)).ToList();
            supportedBuildTargets.Sort((t1, t2) => String.Compare(t1.ToString(), t2.ToString(), StringComparison.Ordinal));
            supportedBuildTargets.Insert(0, BuildTarget.NoTarget);

            m_SupportedBuildTargets = supportedBuildTargets.ToArray();

            m_PlatformContents = m_SupportedBuildTargets
                .Select(t => new GUIContent(t == BuildTarget.NoTarget
                    ? $"Use Build Settings ({EditorUserBuildSettings.activeBuildTarget})"
                    : t.ToString())).ToArray();

            m_AnalysisScope = NormalizeMainWindowScope(UserPreferences.AnalysisScope);
            m_CodeContext = UserPreferences.CodeContext;
            m_Platform = UserPreferences.AnalysisTargetPlatform;
            if (m_Platform != BuildTarget.NoTarget && !BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(m_Platform), m_Platform))
                m_Platform = BuildTarget.NoTarget;

            if (m_ImpactSelection == Impact.None)
                m_ImpactSelection = Impact.All;

            InitializeReportView(SmartAuditorSettings.instance.Rules);

            // After a domain reload m_Report is null (it isn't Unity-serialized), so restoration
            // goes through the canonical JSON autosave on disk via TryLoadAutosavedReport.
            // The in-memory branch covers a same-session OnEnable where m_Report is already set.
            var hasInMemoryReport = m_Report != null && m_Report.IsValid() &&
                (currentState == AnalysisState.Valid || currentState == AnalysisState.Completed);
            if (hasInMemoryReport)
            {
                Profiler.BeginSample("Views Update");
                m_ReportView.MarkDirty();
                m_AnalysisState = AnalysisState.Valid;
                Profiler.EndSample();
            }
            else
            {
                m_AnalysisState = AnalysisState.Initialized;
                TryLoadAutosavedReport();
            }

            m_NavigatorPanel ??= new NavigatorPanel();
            m_NavigatorPaneWidth = Mathf.Max(m_NavigatorPaneWidth, 240f);
            m_NavigatorSearchText = EditorPrefs.GetString(k_NavigatorSearchPrefKey, m_NavigatorSearchText ?? "");
            m_MinViewSeverity = LoadMinViewSeverityFromPrefs();

            Profiler.BeginSample("Refresh");
            RefreshWindow();
            Profiler.EndSample();

            wantsMouseMove = true;
        }

        void OnDisable()
        {
            AssetDatabase.SaveAssets();
            m_ReportView?.SaveSettings();
            EditorPrefs.SetString(k_NavigatorSearchPrefKey, m_NavigatorSearchText ?? "");
            EditorPrefs.SetInt(k_MinViewSeverityPrefKey, (int)m_MinViewSeverity);
            // Guarantee the autosave file is written before domain reload.
            // Unity calls OnDisable synchronously in the pre-reload phase; the
            // EditorApplication.delayCall scheduled after analysis may not have
            // fired yet, so without this call the file would not exist on restart.
            AutosaveReport();
        }

        static Severity LoadMinViewSeverityFromPrefs()
        {
            var value = (Severity)EditorPrefs.GetInt(k_MinViewSeverityPrefKey, (int)Severity.Default);
            return ReportFilter.IsValidThreshold(value) ? value : Severity.Default;
        }

        void OnGUI()
        {
            if (m_AnalysisState == AnalysisState.InProgress && m_ScrollingAnalysisProgress != null)
            {
                ScrollingAnalysisProgressUI.DrawOverlay(this, m_ScrollingAnalysisProgress, m_AnalysisLogBuffer);
                return;
            }

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
            {
                if (IsAnalysisValid())
                    DrawToolbar();

                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
                {
                    if (IsAnalysisValid())
                    {
                        DrawNavigator();

                        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
                        {
                            DrawPanels();
                        }
                    }
                    else
                    {
                        DrawHome();
                    }
                }
            }
        }

        void Update()
        {
            if (m_ShouldRefresh)
                Repaint();
            if (m_AnalysisState == AnalysisState.InProgress)
                Repaint();
        }

        #endregion

        #region IReportItemFilter

        public bool IsMatch(ReportItem issue)
        {
            if (!ReportItemTextSearch.MatchesSubstringFilter(issue, m_NavigatorSearchText))
                return false;

            Profiler.BeginSample("MatchArea");
            var matchArea = issue.Descriptor.IsValid() && issue.Descriptor.MatchesAnyImpact(m_ImpactSelection);
            Profiler.EndSample();
            if (!matchArea)
                return false;

            if (!ReportFilter.Includes(issue.Severity, m_MinViewSeverity))
                return false;

            return true;
        }

        public bool IsMatch(Message message)
        {
            if (!ReportItemTextSearch.MatchesSubstringFilter(message, m_NavigatorSearchText))
                return false;

            // Messages carry no Descriptor (no impact, no severity rules) — the diagnostic
            // arms of the ReportItem path don't apply.
            return true;
        }

        #endregion

        #region View setup

        void InitializeReportView(DiagnosticRules rules)
        {
            m_ReportView ??= new AnalysisReportView();
            m_ReportView.SetWindow(this);
            m_ReportView.SetRules(rules);
            m_ReportView.OnEnable();
        }

        void ResetReportViewState()
        {
            if (m_ReportView == null)
                return;

            m_ReportView.Clear();
            m_ReportView.ShowOverview();
        }

        void RefreshWindow()
        {
            if (!IsAnalysisValid())
                return;

            m_ReportView?.MarkDirty();

            if (m_AnalysisState == AnalysisState.Completed)
            {
                m_AnalysisState = AnalysisState.Valid;
                Repaint();
            }

            UpdateWindowTitle();
        }

        void UpdateWindowTitle()
        {
            titleContent = Contents.WindowTitle;
        }

        #endregion

        #region Analysis

        bool IsAnalysisValid()
        {
            return m_AnalysisState != AnalysisState.Initializing && m_AnalysisState != AnalysisState.Initialized && m_AnalysisState != AnalysisState.InProgress;
        }

        void Analyze()
        {
            m_ShouldRefresh = true;
            m_AnalysisState = AnalysisState.InProgress;
            m_IsLoadedByUser = false;
            m_Report = null;
            ResetReportViewState();

            var reportDisplayName = Application.productName + "_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

            var analysisParams = new AnalysisOptions
            {
                Categories = GetSelectedCategories(),
                Platform = GetSelectedAnalysisPlatform(),
                CodeContext = GetSelectedCodeContext(),

                OnIncomingIssues = _ =>
                {
                    m_ReportView?.MarkDirty();
                },
                OnCompleted = report =>
                {
                    try
                    {
                        if (!report.IsValid())
                        {
                            m_AnalysisState = AnalysisState.Initialized;
                            return;
                        }

                        m_ShouldRefresh = true;
                        m_AnalysisState = AnalysisState.Completed;

                        m_PreviousReport = m_Report;
                        m_Report = report;
                        m_Report.DisplayName = reportDisplayName;
                        m_HasPendingUnsavedChanges = true;

                        m_ReportView?.MarkDirty();

                        EditorApplication.delayCall += AutosaveReport;
                    }
                    finally
                    {
                        m_ScrollingAnalysisProgress?.Dispose();
                        m_ScrollingAnalysisProgress = null;
                    }
                }
            };

            InitializeReportView(analysisParams.Rules);

            m_ScrollingAnalysisProgress = new ScrollingAnalysisProgress(this);
            _ = SmartAuditor.AnalyzeAsync(analysisParams, m_ScrollingAnalysisProgress, m_ScrollingAnalysisProgress.CancellationToken);
        }

        internal void AnalyzeCategories(AnalysisCategory[] categories)
        {
            if (m_IsLoadedByUser)
                return;

            m_ShouldRefresh = true;
            m_AnalysisState = AnalysisState.InProgress;

            var modules = SmartAuditor.GetModules(categories);
            var actualCategories = modules.SelectMany(m => m.Categories).Distinct().ToArray();

            m_ReportView?.Clear();

            var analysisParams = new AnalysisOptions
            {
                Categories = actualCategories,
                Platform = m_Report.SessionInfo.Platform,
                CodeContext = GetSelectedCodeContext(),
                ExistingReport = m_Report,
                OnIncomingIssues = _ =>
                {
                    m_ReportView?.MarkDirty();
                },
                OnCompleted = report =>
                {
                    try
                    {
                        if (!report.IsValid())
                        {
                            m_AnalysisState = AnalysisState.Initialized;
                            return;
                        }

                        m_ShouldRefresh = true;
                        m_AnalysisState = AnalysisState.Completed;
                        m_HasPendingUnsavedChanges = true;

                        m_ReportView?.MarkDirty();

                        EditorApplication.delayCall += AutosaveReport;
                    }
                    finally
                    {
                        m_ScrollingAnalysisProgress?.Dispose();
                        m_ScrollingAnalysisProgress = null;
                    }
                }
            };

            m_ScrollingAnalysisProgress?.Dispose();
            m_ScrollingAnalysisProgress = new ScrollingAnalysisProgress(this);
            _ = SmartAuditor.AnalyzeAsync(analysisParams, m_ScrollingAnalysisProgress, m_ScrollingAnalysisProgress.CancellationToken);
        }

        AnalysisCategory[] GetSelectedCategories() => CommandLine.GetCategoriesForScope(NormalizeMainWindowScope(m_AnalysisScope));

        BuildTarget GetSelectedAnalysisPlatform() => m_Platform;

        BuildTarget GetDisplayPlatform()
        {
            if (m_Report?.SessionInfo != null && m_Report.SessionInfo.Platform != BuildTarget.NoTarget)
                return m_Report.SessionInfo.Platform;
            return m_Platform;
        }

        CodeContext GetSelectedCodeContext() => m_CodeContext;

        internal string GetSelectedImpactSummary() => m_ImpactSelection.ToString();

        static AnalysisScope NormalizeMainWindowScope(AnalysisScope scope)
        {
            return scope & k_MainWindowSupportedScope;
        }

        static bool HasAssetDetailScope(AnalysisScope scope)
        {
            return (scope & k_AssetDetailScope) != 0;
        }

        static int CountAssetDetailScopes(AnalysisScope scope)
        {
            var count = 0;
            if (scope.HasFlag(AnalysisScope.Assets))
                count++;
            if (scope.HasFlag(AnalysisScope.Shaders))
                count++;
            if (scope.HasFlag(AnalysisScope.Prefabs))
                count++;
            return count;
        }

        static AnalysisScope SetScopeEnabled(AnalysisScope scope, AnalysisScope flag, bool enabled)
        {
            return enabled ? scope | flag : scope & ~flag;
        }

        AnalysisScope DrawScopeSelection(AnalysisScope scope, float optionsFieldWidth)
        {
            scope = NormalizeMainWindowScope(scope);
            var fieldWidth = Mathf.Max(220f, optionsFieldWidth);
            const float formLabelWidth = 96f;
            const float formGap = 12f;
            var controlWidth = Mathf.Max(120f, fieldWidth - formLabelWidth - formGap);
            var previousLabelWidth = EditorGUIUtility.labelWidth;

            EditorGUIUtility.labelWidth = formLabelWidth;
            try
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(fieldWidth)))
                {
                    var controlHeight = EditorGUIUtility.singleLineHeight + 4f;
                    const float buttonGap = 2f;
                    var buttonControlWidth = Mathf.Max(120f, controlWidth - 16f);
                    var topButtonWidth = Mathf.Floor((buttonControlWidth - buttonGap * 2f) / 3f);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(fieldWidth)))
                    {
                        GUILayout.Label(Contents.ProjectAreaSelection, EditorStyles.miniBoldLabel,
                            GUILayout.Width(formLabelWidth), GUILayout.Height(controlHeight));
                        GUILayout.Space(formGap);

                        var code = GUILayout.Toggle(scope.HasFlag(AnalysisScope.Code), Contents.CodeScope,
                            EditorStyles.miniButtonLeft, GUILayout.Width(topButtonWidth), GUILayout.Height(controlHeight));
                        scope = SetScopeEnabled(scope, AnalysisScope.Code, code);

                        var assetScope = HasAssetDetailScope(scope);
                        var newAssetScope = GUILayout.Toggle(assetScope, GetAssetScopeContent(scope),
                            EditorStyles.miniButtonMid, GUILayout.Width(topButtonWidth), GUILayout.Height(controlHeight));
                        if (newAssetScope != assetScope)
                            scope = SetScopeEnabled(scope, k_AssetDetailScope, newAssetScope);

                        var settings = GUILayout.Toggle(scope.HasFlag(AnalysisScope.ProjectSettings), Contents.SettingsScope,
                            EditorStyles.miniButtonRight, GUILayout.Width(topButtonWidth), GUILayout.Height(controlHeight));
                        scope = SetScopeEnabled(scope, AnalysisScope.ProjectSettings, settings);
                    }

                    if (CommandLine.GetCategoriesForScope(scope).Length == 0)
                    {
                        EditorGUILayout.Space(6f);
                        EditorGUILayout.HelpBox(Contents.NoScopeSelectedMessage, MessageType.Warning);
                    }

                    EditorGUILayout.Space(10f);
                    scope = DrawSelectedScopeOptions(scope, fieldWidth, formLabelWidth, formGap, controlWidth, buttonControlWidth, controlHeight);

                    EditorGUILayout.Space(10f);
                    DrawRunOptions(fieldWidth, formLabelWidth, formGap, buttonControlWidth);
                }
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }

            return NormalizeMainWindowScope(scope);
        }

        AnalysisScope DrawSelectedScopeOptions(AnalysisScope scope, float fieldWidth, float formLabelWidth, float formGap, float controlWidth, float buttonControlWidth, float controlHeight)
        {
            EditorGUILayout.LabelField(Contents.SelectedScopeOptionsTitle, EditorStyles.miniBoldLabel);

            using (new EditorGUI.DisabledScope(!scope.HasFlag(AnalysisScope.Code)))
            {
                DrawCodeOptions(fieldWidth, formLabelWidth, formGap, buttonControlWidth);
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(!HasAssetDetailScope(scope)))
            {
                scope = DrawAssetOptions(scope, fieldWidth, formLabelWidth, formGap, buttonControlWidth, controlHeight);
            }

            return scope;
        }

        void DrawCodeOptions(float fieldWidth, float formLabelWidth, float formGap, float controlWidth)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(fieldWidth)))
            {
                EditorGUILayout.LabelField(Contents.CodeOptionsTitle, EditorStyles.miniBoldLabel);

                EditorGUI.BeginChangeCheck();
                var codeContextIndex = Array.IndexOf(s_CodeContextChoices, m_CodeContext);
                if (codeContextIndex < 0)
                    codeContextIndex = Array.IndexOf(s_CodeContextChoices, CodeContext.Runtime);
                using (new EditorGUILayout.HorizontalScope(GUILayout.Width(fieldWidth)))
                {
                    GUILayout.Label(Contents.CodeContextSelection, GUILayout.Width(formLabelWidth));
                    GUILayout.Space(formGap);
                    codeContextIndex = EditorGUILayout.Popup(codeContextIndex, s_CodeContextLabels, GUILayout.Width(controlWidth));
                }
                m_CodeContext = s_CodeContextChoices[codeContextIndex];
                if (EditorGUI.EndChangeCheck())
                    UserPreferences.CodeContext = m_CodeContext;
            }
        }

        void DrawRunOptions(float fieldWidth, float formLabelWidth, float formGap, float controlWidth)
        {
            EditorGUILayout.LabelField(Contents.RunOptionsTitle, EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(fieldWidth)))
            {
                var selectedTarget = Array.IndexOf(m_SupportedBuildTargets, m_Platform);
                if (selectedTarget < 0)
                    selectedTarget = 0;

                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope(GUILayout.Width(fieldWidth)))
                {
                    GUILayout.Label(Contents.PlatformSelection, GUILayout.Width(formLabelWidth));
                    GUILayout.Space(formGap);
                    selectedTarget = EditorGUILayout.Popup(selectedTarget, m_PlatformContents, GUILayout.Width(controlWidth));
                }
                m_Platform = m_SupportedBuildTargets[selectedTarget];
                if (EditorGUI.EndChangeCheck())
                    UserPreferences.AnalysisTargetPlatform = m_Platform;
            }
        }

        static AnalysisScope DrawAssetOptions(AnalysisScope scope, float fieldWidth, float formLabelWidth, float formGap, float buttonControlWidth, float controlHeight)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(fieldWidth)))
            {
                EditorGUILayout.LabelField(Contents.AssetGroupsTitle, EditorStyles.miniBoldLabel);

                var selectedGroupCount = CountAssetDetailScopes(scope);
                using (new EditorGUILayout.HorizontalScope(GUILayout.Width(fieldWidth)))
                {
                    GUILayout.Space(formLabelWidth + formGap);

                    var assetsSelected = scope.HasFlag(AnalysisScope.Assets);
                    var shadersSelected = scope.HasFlag(AnalysisScope.Shaders);
                    var prefabsSelected = scope.HasFlag(AnalysisScope.Prefabs);
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(buttonControlWidth)))
                    {
                        using (new EditorGUI.DisabledScope(selectedGroupCount == 1 && assetsSelected))
                        {
                            assetsSelected = GUILayout.Toggle(assetsSelected, Contents.ImportedAssetsScope,
                                EditorStyles.miniButton, GUILayout.Width(buttonControlWidth), GUILayout.Height(controlHeight));
                        }

                        using (new EditorGUI.DisabledScope(selectedGroupCount == 1 && shadersSelected))
                        {
                            shadersSelected = GUILayout.Toggle(shadersSelected, Contents.ShadersMaterialsScope,
                                EditorStyles.miniButton, GUILayout.Width(buttonControlWidth), GUILayout.Height(controlHeight));
                        }

                        using (new EditorGUI.DisabledScope(selectedGroupCount == 1 && prefabsSelected))
                        {
                            prefabsSelected = GUILayout.Toggle(prefabsSelected, Contents.ScenesPrefabsScope,
                                EditorStyles.miniButton, GUILayout.Width(buttonControlWidth), GUILayout.Height(controlHeight));
                        }
                    }

                    scope = SetScopeEnabled(scope, AnalysisScope.Assets, assetsSelected);
                    scope = SetScopeEnabled(scope, AnalysisScope.Shaders, shadersSelected);
                    scope = SetScopeEnabled(scope, AnalysisScope.Prefabs, prefabsSelected);
                }
            }

            return scope;
        }

        static GUIContent GetAssetScopeContent(AnalysisScope scope)
        {
            var selected = 0;
            if (scope.HasFlag(AnalysisScope.Assets))
                selected++;
            if (scope.HasFlag(AnalysisScope.Shaders))
                selected++;
            if (scope.HasFlag(AnalysisScope.Prefabs))
                selected++;

            return selected == 0 || selected == 3 ? Contents.AssetScope : Contents.AssetScopePartial[selected - 1];
        }

        #endregion

        #region Impact filtering

        bool IsAllImpactScope() => m_ImpactSelection == Impact.All;

        void ApplyImpactAreaFilter(Impact impact)
        {
            if (impact == Impact.None)
                return;

            m_ImpactSelection = impact;
            RefreshWindow();
        }

        void ClearImpactAreaFilter()
        {
            m_ImpactSelection = Impact.All;
            RefreshWindow();
        }

        #endregion

        #region Report persistence

        void SaveReport()
        {
            ReportFileManager.SaveReportToFile(m_Report, _ =>
            {
                m_HasPendingUnsavedChanges = false;
                ReportFileManager.SkipAutosaveRestoreAfterManualSave();
            });

            GUIUtility.ExitGUI();
        }

        void LoadReport(bool exitGUI = true)
        {
            ReportFileManager.LoadReportFromFile(report =>
            {
                if (report != null)
                    LoadReportFromFile(report, isManualLoad: true);
            });

            if (exitGUI)
                GUIUtility.ExitGUI();
        }

        void LoadReportFromFile(Report report, bool isManualLoad = false)
        {
            m_PreviousReport = m_Report;
            m_Report = report;

            m_HasPendingUnsavedChanges = false;
            m_IsLoadedByUser = isManualLoad;
            m_AnalysisState = AnalysisState.Valid;

            InitializeReportView(SmartAuditorSettings.instance.Rules);
            ResetReportViewState();
            m_ReportView.MarkDirty();

            UpdateWindowTitle();
            Repaint();
        }

        void AutosaveReport()
        {
            if (m_AnalysisState != AnalysisState.Valid && m_AnalysisState != AnalysisState.Completed)
                return;

            ReportFileManager.AutosaveReport(m_Report);
        }

        void TryLoadAutosavedReport()
        {
            ReportFileManager.TryLoadAutosavedReport(report =>
            {
                if (report == null)
                    return;

                if (m_ReportView == null)
                {
                    LoadReportFromFile(report);
                    return;
                }

                m_PreviousReport = m_Report;
                m_Report = report;
                m_HasPendingUnsavedChanges = false;
                m_IsLoadedByUser = false;
                m_AnalysisState = AnalysisState.Valid;

                m_ReportView.MarkDirty();

                UpdateWindowTitle();
                Repaint();
            });
        }

        void DeleteAutosave()
        {
            ReportFileManager.DeleteAutosave();
        }

        #endregion

        #region Drawing — welcome

        void DrawHome()
        {
            const float kSpaceSm = 8f;
            const float kSpaceMd = 12f;
            const float kSpaceLg = 16f;
            const float k_WelcomeContentWidth = 680f;
            const float k_IntroMaxWidth = 620f;
            const float k_OptionsFieldMaxWidth = 400f;
            const float k_ActionButtonHeight = 34f;
            const float k_ActionCardHeight = 52f;
            const float k_PrimaryButtonWidth = 170f;
            const float k_LoadButtonWidth = 96f;
            const float k_SettingsButtonWidth = 30f;
            var optionsFieldWidth = Mathf.Clamp(position.width - 96f, 320f, k_OptionsFieldMaxWidth);
            var optionsCardWidth = optionsFieldWidth + SharedStyles.WelcomeCard.padding.horizontal;
            var actionClusterWidth = k_PrimaryButtonWidth + kSpaceSm + k_LoadButtonWidth + kSpaceSm + k_SettingsButtonWidth;
            var actionRowIndent = Mathf.Clamp(EditorGUIUtility.labelWidth, 0f, Mathf.Max(0f, optionsFieldWidth - actionClusterWidth));

            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            EditorGUILayout.Space(kSpaceLg);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(k_WelcomeContentWidth), GUILayout.ExpandWidth(true)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        var titleStyle = SharedStyles.TitleLabel;
                        var titleHeight = Mathf.Max(40f, titleStyle.lineHeight + 10f);
                        GUILayout.Label(Contents.TextTitle, titleStyle, GUILayout.Height(titleHeight));
                        GUILayout.FlexibleSpace();
                    }

                    EditorGUILayout.Space(kSpaceLg);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField(Contents.WelcomeText, SharedStyles.WelcomeTextArea, GUILayout.MaxWidth(k_IntroMaxWidth));
                        GUILayout.FlexibleSpace();
                    }

                    EditorGUILayout.Space(kSpaceLg);
                    DrawStartAnalysisExpectations(kSpaceSm);

                    EditorGUILayout.Space(kSpaceLg);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        using (new EditorGUILayout.VerticalScope(SharedStyles.WelcomeCard, GUILayout.Width(optionsCardWidth)))
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                m_OptionsFoldout = EditorGUILayout.Foldout(m_OptionsFoldout, Contents.OptionsTitle, toggleOnLabelClick: true, SharedStyles.Foldout);
                                GUILayout.FlexibleSpace();
                            }

                            if (m_OptionsFoldout)
                            {
                                EditorGUILayout.Space(kSpaceMd);
                                EditorGUI.indentLevel++;

                                EditorGUI.BeginChangeCheck();
                                m_AnalysisScope = DrawScopeSelection(m_AnalysisScope, optionsFieldWidth);
                                if (EditorGUI.EndChangeCheck())
                                    UserPreferences.AnalysisScope = m_AnalysisScope;

                                EditorGUILayout.Space(kSpaceSm);
                                EditorGUI.indentLevel--;
                            }
                        }
                        GUILayout.FlexibleSpace();
                    }

                    EditorGUILayout.Space(kSpaceLg);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        using (new EditorGUILayout.VerticalScope(SharedStyles.WelcomeCard, GUILayout.Width(optionsCardWidth), GUILayout.Height(k_ActionCardHeight)))
                        {
                            GUILayout.FlexibleSpace();
                            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(optionsFieldWidth)))
                            {
                                GUILayout.Space(actionRowIndent);
                                using (new EditorGUI.DisabledScope(GetSelectedCategories().Length == 0 || m_AnalysisState == AnalysisState.InProgress))
                                {
                                    if (GUILayout.Button(Contents.AnalyzeButton, EditorStyles.miniButton, GUILayout.Width(k_PrimaryButtonWidth), GUILayout.Height(k_ActionButtonHeight)))
                                    {
                                        Analyze();
                                        GUIUtility.ExitGUI();
                                    }
                                }

                                GUILayout.Space(kSpaceSm);
                                using (new EditorGUI.DisabledScope(m_AnalysisState == AnalysisState.InProgress))
                                {
                                    if (GUILayout.Button(Contents.WelcomeLoadButton, EditorStyles.miniButton, GUILayout.Width(k_LoadButtonWidth), GUILayout.Height(k_ActionButtonHeight)))
                                        LoadReport(exitGUI: false);
                                }

                                GUILayout.Space(kSpaceSm);
                                if (GUILayout.Button(Contents.OptionsSettingsButton, EditorStyles.miniButton, GUILayout.Width(k_SettingsButtonWidth), GUILayout.Height(k_ActionButtonHeight)))
                                    EditorInterop.OpenSettings();
                            }
                            GUILayout.FlexibleSpace();
                        }
                        GUILayout.FlexibleSpace();
                    }
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        void DrawStartAnalysisExpectations(float sectionSpacing)
        {
            using (new EditorGUILayout.VerticalScope(SharedStyles.WelcomeCard))
            {
                EditorGUILayout.LabelField(Contents.StartAnalysisExpectationsTitle, SharedStyles.BoldLabel);
                EditorGUILayout.Space(sectionSpacing);
                EditorGUILayout.LabelField(Contents.StartAnalysisExpectationsBody, SharedStyles.TextArea);
                EditorGUILayout.Space(sectionSpacing);
                EditorGUILayout.LabelField(Contents.StartAnalysisExpectationsStepOne, SharedStyles.TextArea);
                EditorGUILayout.LabelField(Contents.StartAnalysisExpectationsStepTwo, SharedStyles.TextArea);
                EditorGUILayout.LabelField(Contents.StartAnalysisExpectationsStepThree, SharedStyles.TextArea);
                EditorGUILayout.LabelField(Contents.StartAnalysisExpectationsStepFour, SharedStyles.TextArea);
            }
        }

        #endregion

        #region Drawing — report

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.FlexibleSpace();

                const int discardButtonWidth = 130;
                const int loadSaveButtonWidth = 40;

                using (new EditorGUI.DisabledScope(m_AnalysisState != AnalysisState.Valid))
                {
                    if (GUILayout.Button(Contents.DiscardButton, EditorStyles.toolbarButton, GUILayout.Width(discardButtonWidth)))
                    {
                        if (!m_HasPendingUnsavedChanges ||
                            m_IsLoadedByUser ||
                            EditorUtility.DisplayDialog(k_Discard, k_DiscardQuestion, "Discard", "Cancel"))
                        {
                            m_AnalysisState = AnalysisState.Initialized;
                            m_HasPendingUnsavedChanges = false;
                            m_IsLoadedByUser = false;
                            m_Report = null;
                            ResetReportViewState();

                            DeleteAutosave();
                            UpdateWindowTitle();
                        }
                    }

                    if (GUILayout.Button(Contents.SaveButton, EditorStyles.toolbarButton,
                        GUILayout.Width(loadSaveButtonWidth)))
                    {
                        SaveReport();
                    }
                }

                if (GUILayout.Button(Contents.LoadButton, EditorStyles.toolbarButton, GUILayout.Width(loadSaveButtonWidth)))
                {
                    LoadReport();
                }

            }
        }

        void DrawNavigator()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(m_NavigatorPaneWidth), GUILayout.ExpandHeight(true)))
            {
                m_NavigatorPanel.Draw(MakeNavigatorContext());
            }

            SolidDraw.ThemeVerticalSeparator();
        }

        NavigatorPanelContext MakeNavigatorContext()
        {
            return new NavigatorPanelContext
            {
                Report = m_Report,
                PreviousReport = m_PreviousReport,
                ActiveImpactSummary = GetSelectedImpactSummary(),
                IsAllImpactScope = IsAllImpactScope(),
                IsMessageFocusActive = m_ReportView?.IsMessageInvestigationMode == true,
                MinViewSeverity = m_MinViewSeverity,
                NavigatorSearchText = m_NavigatorSearchText ?? "",
                OnMinViewSeverityChanged = severity =>
                {
                    if (m_MinViewSeverity == severity)
                        return;
                    m_MinViewSeverity = severity;
                    EditorPrefs.SetInt(k_MinViewSeverityPrefKey, (int)m_MinViewSeverity);
                    RefreshWindow();
                },
                OnNavigatorSearchTextChanged = s =>
                {
                    var next = s ?? string.Empty;
                    if (m_NavigatorSearchText == next)
                        return;
                    m_NavigatorSearchText = next;
                    EditorPrefs.SetString(k_NavigatorSearchPrefKey, m_NavigatorSearchText);
                    RefreshWindow();
                },
                OnFocusAreaSelected = focusImpact =>
                {
                    ApplyImpactAreaFilter(focusImpact);
                    m_ReportView?.ShowFocusOverview(focusImpact);
                    Repaint();
                },
                OnOverviewRequested = () =>
                {
                    if (!IsAllImpactScope())
                        ClearImpactAreaFilter();
                    m_ReportView?.ShowOverview();
                    Repaint();
                }
            };
        }

        void DrawPanels()
        {
            if (m_ReportView == null)
                return;

            AnalysisViewHost.Draw(
                m_ReportView,
                new AnalysisViewHost.ReportTitleOptions
                {
                    TitleLabel = Contents.ReportTitle,
                    Report = m_Report,
                    DisplayPlatform = GetDisplayPlatform(),
                    HasPendingUnsavedChanges = m_HasPendingUnsavedChanges,
                    ViewDescription = m_ReportView.Description
                },
                DrawReportBody);
        }

        void DrawReportBody()
        {
            if (m_ReportView == null || !m_ReportView.IsValid())
                return;

            if (m_ShouldRefresh || m_AnalysisState == AnalysisState.Completed)
            {
                RefreshWindow();
                m_ShouldRefresh = false;
            }

            m_ReportView.DrawContent();
        }

        #endregion

        #region Nested types

        static class LayoutSize
        {
            public static readonly int MinWindowWidth = 410;
            public static readonly int MinWindowHeight = 540;
        }

        static class Contents
        {
            public static readonly GUIContent WindowTitle = new GUIContent(SmartAuditor.DisplayName);
            public static readonly GUIContent ReportTitle = new GUIContent("Analysis Report");

            public static readonly GUIContent AnalyzeButton =
                new GUIContent("Start Analysis", "Run static analysis and generate an interactive report of actionable findings.");
            public static readonly GUIContent OptionsSettingsButton =
                Utility.GetIcon(Utility.IconType.Settings, "Open Smart Auditor project settings.");
            public static readonly GUIContent WelcomeLoadButton =
                new GUIContent("Load Report", "Load a saved Smart Auditor JSON report.");

            public static readonly GUIContent SaveButton = Utility.GetIcon(Utility.IconType.Save, "Save the current report as JSON.");
            public static readonly GUIContent LoadButton = Utility.GetIcon(Utility.IconType.Load, "Load a saved JSON report.");
            public static readonly GUIContent DiscardButton = EditorGUIUtility.TrTextContent("Start New Analysis", "Discard the current report and return to the Welcome view.");

            public static readonly GUIContent PreferencesMenuItem = EditorGUIUtility.TrTextContent("Preferences", $"Open user preferences for {SmartAuditor.DisplayName}");

            public static readonly GUIContent TextTitle = new GUIContent($"{SmartAuditor.DisplayName}");

            public static readonly GUIContent WelcomeText = new GUIContent(
                $@"{SmartAuditor.DisplayName} is an analysis tool for Unity projects.
It inspects code, assets, scenes, prefabs, and settings without running your game, then produces an interactive report of actionable findings."
            );

            public static readonly GUIContent OptionsTitle = new GUIContent("Analysis Options");
            public static readonly GUIContent StartAnalysisExpectationsTitle = new GUIContent("What to expect");
            public static readonly GUIContent StartAnalysisExpectationsBody = new GUIContent(
                "The default run analyzes code, all assets, scenes, prefabs, settings, and packages without entering Play Mode:"
            );
            public static readonly GUIContent StartAnalysisExpectationsStepOne = new GUIContent(
                "- Review the Summary first, then open category Views from the navigator."
            );
            public static readonly GUIContent StartAnalysisExpectationsStepTwo = new GUIContent(
                "- Prioritize Critical and Major findings before lower-severity cleanup."
            );
            public static readonly GUIContent StartAnalysisExpectationsStepThree = new GUIContent(
                "- Use Focus Areas, search, and severity filters to narrow the report."
            );
            public static readonly GUIContent StartAnalysisExpectationsStepFour = new GUIContent(
                "- Larger Code and Shader runs can take longer. You can cancel anytime."
            );

            public static readonly GUIContent ProjectAreaSelection = new GUIContent(
                "Scope",
                "Choose the broad project areas to include in this run.\n\n" +
                "Code: C# scripts, assemblies, and compiler messages.\n\n" +
                "Assets: imported assets, shaders and materials, scenes, and prefabs. Use the nested choices to narrow this run.\n\n" +
                "Settings: Player, Quality, Physics, Time, Editor, Graphics, render pipeline settings, and packages.");
            public const string NoScopeSelectedMessage = "Select at least one scope to start analysis.";
            public static readonly GUIContent CodeScope = new GUIContent(
                "Code",
                "Analyze C# scripts, assemblies, and compiler messages.");
            public static readonly GUIContent SelectedScopeOptionsTitle = new GUIContent(
                "Options for selected scopes",
                "Configure the selected analysis scopes.");
            public static readonly GUIContent CodeOptionsTitle = new GUIContent(
                "Code analysis",
                "Choose how code analysis should run.");
            public static readonly GUIContent AssetScope = new GUIContent(
                "Assets",
                "Analyze all asset-related project content. Use the nested choices to narrow this run.");
            public static readonly GUIContent[] AssetScopePartial =
            {
                new GUIContent("Assets 1/3", "One asset analysis group is selected."),
                new GUIContent("Assets 2/3", "Two asset analysis groups are selected.")
            };
            public static readonly GUIContent AssetGroupsTitle = new GUIContent(
                "Asset groups",
                "Choose which asset analysis groups to include.");
            public static readonly GUIContent RunOptionsTitle = new GUIContent(
                "Run options",
                "Configure options that apply to the whole analysis run.");
            public static readonly GUIContent ImportedAssetsScope = new GUIContent(
                "Imported Assets",
                "Analyze textures, sprites, meshes, audio, animation, generic asset issues, and asset import messages.");
            public static readonly GUIContent ShadersMaterialsScope = new GUIContent(
                "Shaders & Materials",
                "Analyze shader source, variants, compute variants, materials, and shader compiler messages. This scope can be slower.");
            public static readonly GUIContent ScenesPrefabsScope = new GUIContent(
                "Scenes & Prefabs",
                "Analyze prefabs, scene objects, GameObject/component issues, and scene-level settings.");
            public static readonly GUIContent SettingsScope = new GUIContent(
                "Settings",
                "Analyze Player, Quality, Physics, Time, Editor, Graphics, render pipeline settings, and packages.");
            public static readonly GUIContent PlatformSelection =
                new GUIContent("Platform", "Select the build target used for platform-specific diagnostics.");
            public static readonly GUIContent CodeContextSelection =
                new GUIContent("Code Context", "Choose whether code analysis covers Editor assemblies, Runtime assemblies, or both.");
        }

        #endregion
    }
}
