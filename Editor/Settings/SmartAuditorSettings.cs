using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Settings
{
    /// <summary>
    /// Project-specific settings.
    /// </summary>
    /// <remarks>
    /// The settings in this class include the global <seealso cref="SmartAuditor.Editor.DiagnosticParams"/> and a structure containing a list of <seealso cref="SmartAuditor.Editor.Diagnostic.Rule"/>s.
    /// These can be viewed and edited in the Settings > Smart Auditor window in the Editor and are saved in ProjectSettings/SmartAuditorSettings.asset, but
    /// they are not directly exposed to scripts in the package API.
    /// </remarks>
    [FilePath("ProjectSettings/SmartAuditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    class SmartAuditorSettings : ScriptableSingleton<SmartAuditorSettings>
    {
        // The DiagnosticRules object which defines which issues should be ignored or given increased severity when viewing reports.
        [SerializeField]
        internal DiagnosticRules Rules;

        // The DiagnosticParams object which defines the customizable thresholds for reporting certain diagnostics.
        [SerializeField]
        internal DiagnosticParams DiagnosticParams;

        // Project-level compilation overrides (e.g. extra preprocessor defines
        // applied on top of Unity's per-asmdef set).
        [SerializeField]
        internal CompilationOverrides Compilation;

        // Project-level filter applied when a Report is saved (e.g. drop
        // anything below Major severity from the saved JSON).
        [SerializeField]
        internal ReportFilter ReportFilter;

        // Default constructor.
        internal SmartAuditorSettings()
        {
            Rules = new DiagnosticRules();
            DiagnosticParams = new DiagnosticParams();
            Compilation = new CompilationOverrides();
            ReportFilter = new ReportFilter();
        }

        void OnEnable()
        {
            hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;

            // Existing assets serialised before Compilation was added come back
            // null on load (the field isn't in their YAML and the ctor only
            // runs for fresh instances). The inspector needs a live object to
            // bind the +/- list against.
            if (Compilation == null)
                Compilation = new CompilationOverrides();

            if (ReportFilter == null)
                ReportFilter = new ReportFilter();
        }

        void OnDisable()
        {
            Save();
        }

        /// <summary>
        /// Save the Smart Auditor project settings file.
        /// </summary>
        public void Save()
        {
            DiagnosticParams.OnBeforeSerialize();

            if (Compilation == null)
                Compilation = new CompilationOverrides();

            if (ReportFilter == null)
                ReportFilter = new ReportFilter();

            Save(saveAsText: true);
        }

        internal SerializedObject GetSerializedObject()
        {
            return new SerializedObject(this);
        }
    }
}


