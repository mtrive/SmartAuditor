using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Settings
{
    /// <summary>
    /// User-specific preferences.
    /// </summary>
    /// <remarks>
    /// This class is responsible for serializing a list of <seealso cref="SmartAuditor.Editor.Diagnostic.Rule"/>s.
    /// These can be viewed and edited in the Preferences > Smart Auditor window in the Editor, but
    /// they are not directly exposed to scripts in the package API.
    /// </remarks>
    [FilePath("SmartAuditorPrefs.asset", FilePathAttribute.Location.PreferencesFolder)]
    class SmartAuditorPrefs : ScriptableSingleton<SmartAuditorPrefs>
    {
        // The DiagnosticRules object which defines which issues should be ignored or given increased severity when viewing reports.
        [SerializeField]
        internal DiagnosticRules Rules;

        // Default constructor.
        internal SmartAuditorPrefs()
        {
            Rules = new DiagnosticRules();
        }

        void OnEnable()
        {
            hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
        }

        void OnDisable()
        {
            Save();
        }

        /// <summary>
        /// Save the Smart Auditor preferences file.
        /// </summary>
        public void Save()
        {
            Save(saveAsText: true);
        }

        internal SerializedObject GetSerializedObject()
        {
            return new SerializedObject(this);
        }
    }
}
