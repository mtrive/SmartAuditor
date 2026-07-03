using System;
using System.Collections.Generic;
using SmartAuditor.Editor.UI.Framework;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.SettingsProviders
{
    [CustomPropertyDrawer(typeof(Rule))]
    internal sealed class RuleDrawer : PropertyDrawer
    {
        static readonly GUIContent k_IdLabel = new GUIContent("Id");
        static readonly GUIContent k_SeverityLabel = new GUIContent("Severity");
        static readonly GUIContent k_PatternLabel = new GUIContent("Pattern");

        internal static IReadOnlyList<string> FilteredSeverityNames => BuildFilteredSeverityNames();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var idProperty = property.FindPropertyRelative("Id");
            var severityProperty = property.FindPropertyRelative("Severity");
            var patternProperty = property.FindPropertyRelative("m_Pattern");

            if (idProperty == null || severityProperty == null || patternProperty == null)
            {
                EditorGUI.LabelField(position, label.text, "(missing Rule properties)");
                EditorGUI.EndProperty();
                return;
            }

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(headerRect, label, SharedStyles.BoldLabel);

            var idRect = new Rect(
                position.x,
                headerRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(idRect, idProperty, k_IdLabel);

            var severityRect = new Rect(
                position.x,
                idRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);
            DrawSeverityPopup(severityRect, idProperty, patternProperty, severityProperty);

            var patternRect = new Rect(
                position.x,
                severityRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(patternRect, patternProperty, k_PatternLabel);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight * 4) + (EditorGUIUtility.standardVerticalSpacing * 3);
        }

        static void DrawSeverityPopup(Rect position, SerializedProperty idProperty, SerializedProperty patternProperty, SerializedProperty severityProperty)
        {
            EnsureNewRuleDefaultsToSuppress(idProperty, patternProperty, severityProperty);
            BuildSeverityChoices(severityProperty, out var filteredIndexes, out var filteredLabels);
            var selectedChoiceIndex = GetSelectedSeverityChoiceIndex(severityProperty, filteredIndexes);
            EditorGUI.BeginChangeCheck();
            var newChoiceIndex = EditorGUI.Popup(position, k_SeverityLabel, selectedChoiceIndex, filteredLabels);
            if (!EditorGUI.EndChangeCheck())
                return;

            if (newChoiceIndex < 0 || newChoiceIndex >= filteredIndexes.Length)
                return;

            severityProperty.enumValueIndex = filteredIndexes[newChoiceIndex];
        }

        static void EnsureNewRuleDefaultsToSuppress(SerializedProperty idProperty, SerializedProperty patternProperty, SerializedProperty severityProperty)
        {
            if (severityProperty.intValue != (int)Severity.Default)
                return;

            var hasId = !string.IsNullOrEmpty(idProperty.stringValue);
            var hasPattern = !string.IsNullOrEmpty(patternProperty.stringValue);
            if (hasId || hasPattern)
                return;

            severityProperty.intValue = (int)Severity.Suppressed;
        }

        static int GetSelectedSeverityChoiceIndex(SerializedProperty severityProperty, IReadOnlyList<int> filteredIndexes)
        {
            var currentEnumIndex = severityProperty.enumValueIndex;
            for (var i = 0; i < filteredIndexes.Count; i++)
                if (filteredIndexes[i] == currentEnumIndex)
                    return i;

            for (var i = 0; i < filteredIndexes.Count; i++)
            {
                var candidateEnumIndex = filteredIndexes[i];
                if (GetSeverityValueAtEnumIndex(severityProperty, candidateEnumIndex) == severityProperty.intValue)
                    return i;
            }

            return 0;
        }

        static void BuildSeverityChoices(SerializedProperty severityProperty, out int[] filteredIndexes, out GUIContent[] filteredLabels)
        {
            var indexBuffer = new List<int>(FilteredSeverityNames.Count);
            var labelBuffer = new List<GUIContent>(FilteredSeverityNames.Count);
            foreach (var severityName in FilteredSeverityNames)
            {
                var enumIndex = Array.IndexOf(severityProperty.enumNames, severityName);
                if (enumIndex < 0)
                    continue;

                indexBuffer.Add(enumIndex);
                var displayName = enumIndex < severityProperty.enumDisplayNames.Length
                    ? severityProperty.enumDisplayNames[enumIndex]
                    : ObjectNames.NicifyVariableName(severityName);
                labelBuffer.Add(new GUIContent(displayName));
            }

            filteredIndexes = indexBuffer.ToArray();
            filteredLabels = labelBuffer.ToArray();
        }

        static int GetSeverityValueAtEnumIndex(SerializedProperty severityProperty, int enumIndex)
        {
            if (enumIndex < 0 || enumIndex >= severityProperty.enumNames.Length)
                return (int)Severity.Default;

            return Enum.TryParse(severityProperty.enumNames[enumIndex], out Severity severityValue)
                ? (int)severityValue
                : (int)Severity.Default;
        }

        static string[] BuildFilteredSeverityNames()
        {
            var names = Enum.GetNames(typeof(Severity));
            var filtered = new List<string>(names.Length);
            for (var i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], nameof(Severity.Warning), StringComparison.Ordinal))
                    continue;

                if (string.Equals(names[i], nameof(Severity.Default), StringComparison.Ordinal))
                    continue;

                filtered.Add(names[i]);
            }

            return filtered.ToArray();
        }
    }
}
