using SmartAuditor.Editor.UI.Framework;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.SettingsProviders
{
    // Custom drawer that limits the Severity dropdown for ReportFilter.MinSaveSeverity
    // to values that make sense as a threshold. None / Hidden / Warning / Info / Error
    // alias values are intentionally excluded; Default is the "no filter" sentinel.
    [CustomPropertyDrawer(typeof(ReportFilter))]
    internal sealed class ReportFilterDrawer : PropertyDrawer
    {
        static readonly Severity[] s_Choices = ReportFilter.ValidThresholds;

        static readonly GUIContent[] s_ChoiceLabels = BuildChoiceLabels();

        static readonly GUIContent k_FieldLabel = new GUIContent(
            "Min Save Severity",
            "Drop issues below this severity from saved reports. Default = no filter.");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var minSeverity = property.FindPropertyRelative("m_MinSaveSeverity");
            if (minSeverity == null)
            {
                EditorGUI.LabelField(position, label.text, "(missing m_MinSaveSeverity)");
                EditorGUI.EndProperty();
                return;
            }

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(headerRect, label, SharedStyles.BoldLabel);

            var fieldRect = new Rect(
                position.x,
                headerRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);

            var current = (Severity)minSeverity.intValue;
            var currentIndex = IndexOf(current);
            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUI.Popup(fieldRect, k_FieldLabel, currentIndex, s_ChoiceLabels);
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < s_Choices.Length)
                minSeverity.intValue = (int)s_Choices[newIndex];

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight * 2) + EditorGUIUtility.standardVerticalSpacing;
        }

        static int IndexOf(Severity value)
        {
            for (var i = 0; i < s_Choices.Length; i++)
                if (s_Choices[i] == value)
                    return i;
            return 0;
        }

        static GUIContent[] BuildChoiceLabels()
        {
            var labels = new GUIContent[s_Choices.Length];
            for (var i = 0; i < s_Choices.Length; i++)
            {
                var label = s_Choices[i] == Severity.Default
                    ? "Default (no filter)"
                    : s_Choices[i].ToString();
                labels[i] = new GUIContent(label);
            }
            return labels;
        }
    }
}
