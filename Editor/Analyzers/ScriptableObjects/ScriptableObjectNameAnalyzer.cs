// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ScriptableObjectNameAnalyzer : ScriptableObjectAnalyzer
    {
        internal const string ASF0001 = nameof(ASF0001);

        static readonly Descriptor ScriptableObjectNameMismatchDescriptor = new Descriptor(
            ASF0001,
            "ScriptableObject: Name Mismatch With Filename",
            Impact.Correctness,
            "The ScriptableObject's <b>name</b> property and its asset filename do not agree. Code that looks up the asset by either route — <b>AssetDatabase.LoadAssetAtPath</b> by filename, or <b>Resources.Load</b> by object name — observes inconsistent results depending on which side it queries.",
            "Rename either the ScriptableObject (via the Inspector) or the asset file so the two names agree."
        )
        {
            IsEnabledByDefault = true,
            MessageFormat = "ScriptableObject name '{0}' does not match filename'",
            DefaultSeverity = Severity.Minor
        };

        public override void Analyze(ScriptableObjectAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(ScriptableObjectNameMismatchDescriptor, context.AssetPath))
                return;

            var objectName = context.ScriptableObject.name;
            if (string.IsNullOrEmpty(objectName))
                return;

            if (objectName.Equals(context.AssetName, System.StringComparison.Ordinal))
                return;

            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    ScriptableObjectNameMismatchDescriptor.Id,
                    objectName)
                .WithLocation(new Location(context.AssetPath, TryFindNameFieldLine(context.AssetPath, objectName)));
            context.ReportIssue(diagnostic);
        }

        // Finds the 1-based line of the serialized m_Name field so the report anchors to it and can
        // render the YAML source. Prefers the field carrying the mismatching name (an asset may hold
        // sub-objects with their own m_Name), falling back to the first. Returns null when the file
        // can't be read or holds no m_Name, leaving a file-level location rather than dropping the finding.
        static int? TryFindNameFieldLine(string assetPath, string objectName)
        {
            const string nameField = "m_Name:";
            try
            {
                var lineNumber = 0;
                int? firstNameLine = null;
                foreach (var rawLine in System.IO.File.ReadLines(assetPath))
                {
                    lineNumber++;
                    var trimmed = rawLine.Trim();
                    if (!trimmed.StartsWith(nameField, System.StringComparison.Ordinal))
                        continue;

                    firstNameLine ??= lineNumber;
                    var value = trimmed.Substring(nameField.Length).Trim();
                    if (value.Equals(objectName, System.StringComparison.Ordinal))
                        return lineNumber;
                }

                return firstNameLine;
            }
            catch (System.IO.IOException e)
            {
                Debug.LogWarning($"[ScriptableObjectNameAnalyzer] Could not read '{assetPath}': {e.Message}");
            }
            catch (System.UnauthorizedAccessException e)
            {
                Debug.LogWarning($"[ScriptableObjectNameAnalyzer] Access denied reading '{assetPath}': {e.Message}");
            }

            return null;
        }
    }
}
