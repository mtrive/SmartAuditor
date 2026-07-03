// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes .prefab and .asset files for orphaned serialized fields that no longer match
    /// any serializable field on the corresponding MonoBehaviour or ScriptableObject script.
    /// This typically happens when a serialized field is renamed without adding [FormerlySerializedAs].
    /// </summary>
    sealed class OrphanedSerializedFieldAnalyzer : AssetAnalyzer
    {
        internal const string ASF0002 = nameof(ASF0002);

        static readonly Descriptor OrphanedFieldDescriptor = new Descriptor(
            ASF0002,
            "Asset: Orphaned Serialized Field",
            Impact.Correctness,
            "The asset's YAML carries a serialized field that does not match any serialized field on the script's current type. Unity silently drops the data on the next deserialization — the most common cause is a field rename without a corresponding <b>[FormerlySerializedAs]</b> attribute.",
            "Add <b>[FormerlySerializedAs(\"oldFieldName\")]</b> to the renamed field on the script to keep the existing serialized value, or remove the orphaned entry from the asset's YAML if the data is no longer needed."
        )
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "Asset has orphaned serialized field '{0}' on script '{1}'"
        };

        static readonly HashSet<string> s_ValidExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".prefab",
            ".asset"
        };

        // Built-in MonoBehaviour/ScriptableObject base class fields serialized by Unity.
        // These are always present in the YAML and should be ignored.
        static readonly HashSet<string> s_BuiltInFields = new HashSet<string>
        {
            "m_ObjectHideFlags",
            "m_CorrespondingSourceObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
            "m_GameObject",
            "m_Enabled",
            "m_EditorHideFlags",
            "m_Script",
            "m_Name",
            "m_EditorClassIdentifier"
        };

        // Regex to extract the script GUID from a m_Script line.
        // Matches: m_Script: {fileID: 11500000, guid: <hex>, type: 3}
        static readonly Regex s_ScriptGuidRegex = new Regex(
            @"m_Script:\s*\{.*guid:\s*([a-f0-9]+)",
            RegexOptions.Compiled);

        // Regex to match top-level field names in a MonoBehaviour YAML section.
        // Top-level fields have exactly 2 spaces of indentation followed by a word and colon.
        static readonly Regex s_TopLevelFieldRegex = new Regex(
            @"^  (\w+):",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Types to stop at when walking the type hierarchy for serializable fields.
        // System.Object is omitted because its BaseType is null, which already terminates the loop.
        static readonly HashSet<string> s_StopTypes = new HashSet<string>
        {
            "UnityEngine.MonoBehaviour",
            "UnityEngine.ScriptableObject",
            "UnityEngine.Behaviour",
            "UnityEngine.Component",
            "UnityEngine.Object"
        };

        // Cache: script GUID -> set of valid serialized field names (null if unresolvable)
        readonly Dictionary<string, HashSet<string>> m_FieldCache = new Dictionary<string, HashSet<string>>();

        public override void Analyze(AssetAnalysisContext context)
        {
            var ext = Path.GetExtension(context.AssetPath);
            if (!s_ValidExtensions.Contains(ext))
                return;

            if (!context.IsDescriptorEnabled(OrphanedFieldDescriptor, context.AssetPath))
                return;

            string fileText;
            try
            {
                fileText = File.ReadAllText(context.AssetPath);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[OrphanedSerializedFieldAnalyzer] Could not read '{context.AssetPath}': {e.Message}");
                return;
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogWarning($"[OrphanedSerializedFieldAnalyzer] Access denied reading '{context.AssetPath}': {e.Message}");
                return;
            }

            // ParseMonoBehaviourSections returns an empty list for files with no MonoBehaviour
            // marker, so a no-marker file falls through with no per-asset work.
            foreach (var section in ParseMonoBehaviourSections(fileText))
            {
                AnalyzeSection(context, section);
            }
        }

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
            // Analyzers persist across analysis runs (constructed once during module Initialize).
            // Drop the type-info cache so a re-run after a script rename doesn't keep reporting
            // against the old serialized field set.
            m_FieldCache.Clear();
        }

        void AnalyzeSection(AssetAnalysisContext context, string section)
        {
            var guid = ExtractScriptGuid(section);
            if (string.IsNullOrEmpty(guid))
                return;

            var validFields = GetValidFieldNames(guid);
            if (validFields == null)
                return;

            var yamlFields = ExtractTopLevelFieldNames(section);

            foreach (var fieldName in yamlFields)
            {
                if (s_BuiltInFields.Contains(fieldName))
                    continue;

                if (validFields.Contains(fieldName))
                    continue;

                var scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                var scriptName = Path.GetFileNameWithoutExtension(scriptPath);

                var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    OrphanedFieldDescriptor.Id,
                    fieldName,
                    scriptName)
                    .WithLocation(new Location(context.AssetPath));
                context.ReportIssue(diagnostic);
            }
        }

        /// <summary>
        /// Splits the file text into individual MonoBehaviour YAML sections.
        /// Each section starts with "--- !u!114" (Unity class ID 114 = MonoBehaviour).
        /// </summary>
        static List<string> ParseMonoBehaviourSections(string fileText)
        {
            var sections = new List<string>();
            var marker = "--- !u!114";
            var startIndex = 0;

            while (true)
            {
                var sectionStart = fileText.IndexOf(marker, startIndex, StringComparison.Ordinal);
                if (sectionStart < 0)
                    break;

                // Find the end of this section (next "---" marker or end of file)
                var sectionEnd = fileText.IndexOf("\n---", sectionStart + marker.Length, StringComparison.Ordinal);
                if (sectionEnd < 0)
                    sectionEnd = fileText.Length;
                else
                    sectionEnd++; // Include the newline

                sections.Add(fileText.Substring(sectionStart, sectionEnd - sectionStart));
                startIndex = sectionEnd;
            }

            return sections;
        }

        /// <summary>
        /// Extracts the script GUID from a MonoBehaviour YAML section.
        /// </summary>
        static string ExtractScriptGuid(string section)
        {
            var match = s_ScriptGuidRegex.Match(section);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Extracts all top-level field names from a MonoBehaviour YAML section.
        /// Top-level fields have 2 spaces of indentation (same level as m_Script, m_Name, etc.).
        /// </summary>
        static List<string> ExtractTopLevelFieldNames(string section)
        {
            var fields = new List<string>();
            var matches = s_TopLevelFieldRegex.Matches(section);

            foreach (Match match in matches)
            {
                fields.Add(match.Groups[1].Value);
            }

            return fields;
        }

        /// <summary>
        /// Gets the set of valid serialized field names for a script identified by its GUID.
        /// Returns null if the script cannot be resolved to a type.
        /// Results are cached for performance.
        /// </summary>
        HashSet<string> GetValidFieldNames(string guid)
        {
            if (m_FieldCache.TryGetValue(guid, out var cached))
                return cached;

            var result = BuildValidFieldNames(guid);
            m_FieldCache[guid] = result;
            return result;
        }

        /// <summary>
        /// Builds the set of valid serialized field names for a script GUID by resolving
        /// the type and enumerating all serializable fields including FormerlySerializedAs aliases.
        /// </summary>
        static HashSet<string> BuildValidFieldNames(string guid)
        {
            var scriptPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(scriptPath))
                return null;

            var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (monoScript == null)
                return null;

            var type = monoScript.GetClass();
            if (type == null)
                return null;

            var validNames = new HashSet<string>();
            var currentType = type;

            while (currentType != null && !s_StopTypes.Contains(currentType.FullName))
            {
                var fields = currentType.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.DeclaredOnly);

                foreach (var field in fields)
                {
                    if (!IsSerializedByUnity(field))
                        continue;

                    validNames.Add(field.Name);

                    // Include FormerlySerializedAs names as valid aliases
                    var formerNames = field.GetCustomAttributes<FormerlySerializedAsAttribute>();
                    foreach (var attr in formerNames)
                    {
                        if (!string.IsNullOrEmpty(attr.oldName))
                            validNames.Add(attr.oldName);
                    }
                }

                currentType = currentType.BaseType;
            }

            return validNames;
        }

        /// <summary>
        /// Determines whether a field would be serialized by Unity's serialization system.
        /// </summary>
        static bool IsSerializedByUnity(FieldInfo field)
        {
            // Static fields are never serialized
            if (field.IsStatic)
                return false;

            // Fields marked with [NonSerialized] are never serialized
            if (field.IsDefined(typeof(NonSerializedAttribute), inherit: false))
                return false;

            // Public fields are serialized by default
            if (field.IsPublic)
                return true;

            // Non-public fields are only serialized if they have [SerializeField]
            return field.IsDefined(typeof(SerializeField), inherit: false);
        }
    }
}
