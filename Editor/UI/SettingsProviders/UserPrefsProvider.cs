using System;
using System.Collections.Generic;
using SmartAuditor.Editor.CodeAnalysis.Compilation;
using UnityEditor;
using UnityEngine;
using SmartAuditor.Editor.Agent;
using SmartAuditor.Editor.Settings;
using SmartAuditor.Editor.UI.Framework;

namespace SmartAuditor.Editor.UI.SettingsProviders
{
    // Register a SettingsProvider using IMGUI for the drawing framework:
    static class UserPrefsProvider
    {
        const float k_ActionButtonWidth = 240f;

        static readonly GUIContent k_UseRoslynAnalyzersLabel = new GUIContent(
            "Use Roslyn Analyzers",
            "Run compatible Roslyn analyzer DLLs found in the project during Smart Auditor code analysis.");
        static readonly GUIContent k_PurgeAssemblyCompilationCacheLabel = new GUIContent(
            "Purge Assembly Compilation Cache",
            "Delete Smart Auditor's cached compiled package assemblies. The next code analysis may take longer while the cache is rebuilt.");
        static readonly GUIContent k_SuppressPackageCompilerMessagesLabel = new GUIContent(
            "Suppress non-error compiler messages from packages",
            "Hide package compiler warnings and informational messages from reports. Compiler errors are always kept.");
        static readonly GUIContent k_LogTimingsInfoLabel = new GUIContent(
            "Log timing information",
            "Write Smart Auditor module timing information to the Unity Console after analysis.");
        static readonly GUIContent k_AnalyzeReadOnlyPackagesLabel = new GUIContent(
            "Analyze read-only packages",
            "Include code and assets from read-only packages in the default analysis scope.");
        static readonly GUIContent k_AnalyzeAfterBuildLabel = new GUIContent(
            "Auto Analyze after Build",
            "Run Smart Auditor automatically after Unity builds the project.");
        static readonly GUIContent k_CollectShaderVariantsOnBuildLabel = new GUIContent(
            "Collect Shader Variants on Build",
            "Capture shader and compute shader variants during Unity builds for variant analysis. This can add build overhead.");
        static readonly string k_CollectShaderVariantsOnBuildHelp =
            "Captures shader and compute shader variants during Unity builds for Smart Auditor variant analysis. Off by default to avoid build overhead.";
        static readonly GUIContent k_FailBuildOnIssuesLabel = new GUIContent(
            "Fail Build on Issues",
            "Fail the build when the post-build Smart Auditor analysis reports one or more issues. Requires Auto Analyze after Build.");
        static readonly string k_FailBuildOnIssuesHelp = "Requires Auto Analyze after Build.";
        static readonly GUIContent k_PrettifyJSONOutputLabel = new GUIContent(
            "Prettify saved JSON files",
            "Write manually saved report JSON with indentation for easier reading.");
        static readonly GUIContent k_ExportContentModeLabel = new GUIContent(
            "JSON export content mode",
            "Choose which report sections are included when manually saving report JSON.");
        static readonly GUIContent k_DebugReport = new GUIContent(
            "Debug JSON reports",
            "Include Smart Auditor debug metadata in saved JSON reports, including descriptor catalog and UI layout metadata.");
        static readonly GUIContent k_AgentBridgeEnabledLabel = new GUIContent(
            "Enable Agent Bridge",
            "Watch the project-local agent bridge request folder so local tools can ask the open Unity Editor to run Smart Auditor.");
        static readonly GUIContent k_OpenAgentBridgeFolderLabel = new GUIContent(
            "Open Agent Bridge Folder",
            "Reveal the request, response, report, and archive folders used by the agent bridge.");
        static readonly GUIContent k_OpenProjectRuleSettingsLabel = new GUIContent(
            "Open Project Rule Settings",
            "Open shared Smart Auditor project settings for team-wide rules.");
        static readonly GUIContent k_PersonalRulesLabel = new GUIContent(
            "Rules",
            "Personal suppressions and severity overrides stored in your Unity Preferences.");

        [SettingsProvider]
        public static UnityEditor.SettingsProvider CreateSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new UnityEditor.SettingsProvider(UserPreferences.Path, SettingsScope.User)
            {
                // By default the last token of the path is used as display name if no label is provided.
                label = SmartAuditor.DisplayName,
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = SettingsGUI,

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[]
                {
                    "auditor",
                    "performance",
                    "static",
                    "analysis",
                    "agent",
                    "bridge",
                    "roslyn",
                    "json",
                    "build",
                    "shader",
                    "cache",
                    "compiler",
                    "packages",
                    "timings",
                    "rules",
                    "suppress",
                    "export"
                })
            };

            return provider;
        }

        static void SettingsGUI(string searchContext)
        {
            const float labelWidth = 300f;

            EditorGUIUtility.labelWidth = labelWidth;

            EditorGUILayout.LabelField("General", SharedStyles.BoldLabel);
            EditorGUI.indentLevel++;
            UserPreferences.LogTimingsInfo = EditorGUILayout.Toggle(k_LogTimingsInfoLabel, UserPreferences.LogTimingsInfo);
            UserPreferences.AnalyzeReadOnlyPackages = EditorGUILayout.Toggle(k_AnalyzeReadOnlyPackagesLabel, UserPreferences.AnalyzeReadOnlyPackages);
            EditorGUI.indentLevel--;

            GUILayout.Space(10f);

            EditorGUILayout.LabelField("Agent Bridge", SharedStyles.BoldLabel);
            EditorGUI.indentLevel++;
            var agentBridgeEnabled = EditorGUILayout.Toggle(k_AgentBridgeEnabledLabel, UserPreferences.AgentBridgeEnabled);
            if (agentBridgeEnabled != UserPreferences.AgentBridgeEnabled)
            {
                UserPreferences.AgentBridgeEnabled = agentBridgeEnabled;
                if (agentBridgeEnabled)
                    AgentBridge.EnsureDirectories();
            }

            EditorGUILayout.HelpBox(GetAgentBridgeStatusMessage(), UserPreferences.AgentBridgeEnabled ? MessageType.Info : MessageType.None);
            using (new EditorGUI.DisabledScope(!UserPreferences.AgentBridgeEnabled))
            {
                if (DrawFixedWidthButton(k_OpenAgentBridgeFolderLabel))
                    AgentBridge.OpenFolder();
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(10f);

            EditorGUILayout.LabelField("Code Analysis", SharedStyles.BoldLabel);
            EditorGUI.indentLevel++;
            UserPreferences.UseRoslynAnalyzers = EditorGUILayout.Toggle(k_UseRoslynAnalyzersLabel, UserPreferences.UseRoslynAnalyzers);
            UserPreferences.SuppressPackageCompilerMessages = EditorGUILayout.Toggle(
                k_SuppressPackageCompilerMessagesLabel,
                UserPreferences.SuppressPackageCompilerMessages);
            EditorGUI.indentLevel--;

            GUILayout.Space(10f);

            EditorGUILayout.LabelField("Maintenance", SharedStyles.BoldLabel);
            EditorGUI.indentLevel++;
            if (DrawFixedWidthButton(k_PurgeAssemblyCompilationCacheLabel))
                PurgeAssemblyCompilationCache();
            EditorGUI.indentLevel--;

            GUILayout.Space(10f);

            EditorGUILayout.LabelField("Build", SharedStyles.BoldLabel);
            EditorGUI.indentLevel++;

            UserPreferences.AnalyzeAfterBuild = EditorGUILayout.Toggle(k_AnalyzeAfterBuildLabel, UserPreferences.AnalyzeAfterBuild);
            using (new EditorGUI.DisabledScope(!UserPreferences.AnalyzeAfterBuild))
            {
                EditorGUI.indentLevel++;
                UserPreferences.FailBuildOnIssues = EditorGUILayout.Toggle(k_FailBuildOnIssuesLabel, UserPreferences.FailBuildOnIssues);
                EditorGUI.indentLevel--;
            }
            if (!UserPreferences.AnalyzeAfterBuild)
                EditorGUILayout.HelpBox(k_FailBuildOnIssuesHelp, MessageType.None);

            UserPreferences.CollectShaderVariantsOnBuild = EditorGUILayout.Toggle(
                k_CollectShaderVariantsOnBuildLabel,
                UserPreferences.CollectShaderVariantsOnBuild);
            EditorGUILayout.HelpBox(k_CollectShaderVariantsOnBuildHelp, MessageType.Info);

            EditorGUI.indentLevel--;
            GUILayout.Space(10f);

            EditorGUILayout.LabelField("Report", SharedStyles.BoldLabel);
            EditorGUI.indentLevel++;

            UserPreferences.PrettifyJsonOutput = EditorGUILayout.Toggle(k_PrettifyJSONOutputLabel, UserPreferences.PrettifyJsonOutput);
            UserPreferences.DebugReport = EditorGUILayout.Toggle(k_DebugReport, UserPreferences.DebugReport);
            UserPreferences.ReportExportContentMode = (ReportExportContentMode)EditorGUILayout.EnumPopup(
                k_ExportContentModeLabel,
                UserPreferences.ReportExportContentMode);

            EditorGUI.indentLevel--;
            GUILayout.Space(10f);

            EditorGUILayout.LabelField("Personal Rule Overrides", SharedStyles.BoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "These suppressions and severity overrides are stored in your Unity Preferences and only apply to you. Use Project Settings for shared team rules.",
                MessageType.Info);
            if (DrawFixedWidthButton(k_OpenProjectRuleSettingsLabel))
                OpenProjectSettings();

            var settings = SmartAuditorPrefs.instance.GetSerializedObject();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(settings.FindProperty("Rules"), k_PersonalRulesLabel);

            if (EditorGUI.EndChangeCheck())
            {
                settings.ApplyModifiedPropertiesWithoutUndo();
                SmartAuditorPrefs.instance.Save();
            }
            EditorGUI.indentLevel--;
        }

        static bool DrawFixedWidthButton(GUIContent label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                return GUILayout.Button(label, GUILayout.Width(k_ActionButtonWidth));
            }
        }

        static string GetAgentBridgeStatusMessage()
        {
            if (UserPreferences.AgentBridgeEnabled)
                return "Enabled. Smart Auditor is watching for request files in:\n" + AgentBridge.RequestsPath;

            return "Disabled. When enabled, local tools can ask the open Unity Editor to run Smart Auditor by writing request files to:\n" +
                AgentBridge.RequestsPath;
        }

        static void OpenProjectSettings()
        {
            var window = SettingsService.OpenProjectSettings($"Project/{SmartAuditor.CompactName}");
            window?.Repaint();
        }

        static void PurgeAssemblyCompilationCache()
        {
            const string purgeDialogTitle = "Purge Assembly Compilation Cache";
            const string purgeDialogMessage =
                "This will delete cached package assemblies used by Smart Auditor and force recompilation on next analysis. Continue?";
            if (!EditorUtility.DisplayDialog(purgeDialogTitle, purgeDialogMessage, "Purge", "Cancel"))
                return;

            if (AssemblyCompilationCache.PurgeAll(out var error))
            {
                Debug.Log(
                    $"[{SmartAuditor.DisplayName}] Purged assembly compilation cache at '{AssemblyCompilationCache.GetCacheRootDirectory()}'.");
                return;
            }

            Debug.LogWarning(
                $"[{SmartAuditor.DisplayName}] Failed to purge assembly compilation cache: {error}");
        }
    }
}
