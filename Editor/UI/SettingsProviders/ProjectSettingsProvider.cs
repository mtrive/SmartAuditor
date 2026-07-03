using System.Collections.Generic;
using UnityEditor;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor.UI.SettingsProviders
{
    // Register a SettingsProvider using IMGUI for the drawing framework:
    static class ProjectSettingsProvider
    {
        [SettingsProvider]
        public static UnityEditor.SettingsProvider CreateSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new UnityEditor.SettingsProvider($"Project/{SmartAuditor.CompactName}", SettingsScope.Project)
            {
                // By default the last token of the path is used as display name if no label is provided.
                label = SmartAuditor.DisplayName,
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = SettingsGUI,

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] { "Rules", "Compilation", "Defines", "Reporting", "Severity" })
            };

            return provider;
        }

        static void SettingsGUI(string searchContext)
        {
            // Existing assets predate the Compilation field and load it null;
            // a SerializedProperty bound to a null reference can't host the
            // +/- list. OnEnable handles the typical case, but mirror the init
            // here so the inspector is robust regardless of load order.
            if (SmartAuditorSettings.instance.Compilation == null)
                SmartAuditorSettings.instance.Compilation = new CompilationOverrides();
            if (SmartAuditorSettings.instance.ReportFilter == null)
                SmartAuditorSettings.instance.ReportFilter = new ReportFilter();

            var settings = SmartAuditorSettings.instance.GetSerializedObject();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(settings.FindProperty("Rules"));
            // Temporarily hidden while the diagnostic parameters feature is refined before exposure.
            // EditorGUILayout.PropertyField(settings.FindProperty("DiagnosticParams"));
            EditorGUILayout.PropertyField(settings.FindProperty("Compilation"));
            EditorGUILayout.PropertyField(settings.FindProperty("ReportFilter"));

            if (EditorGUI.EndChangeCheck())
            {
                settings.ApplyModifiedPropertiesWithoutUndo();
                SmartAuditorSettings.instance.Save();
            }
        }
    }
}
