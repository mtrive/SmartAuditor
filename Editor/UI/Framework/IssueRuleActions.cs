using System;
using System.Collections.Generic;
using System.IO;
using SmartAuditor.Editor;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Settings;
using SmartAuditor.Editor.Utils;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Determines which settings store a rule mutation should be written to.
    /// </summary>
    internal enum RuleStore
    {
        /// <summary>Project-level rules saved to <c>ProjectSettings/SmartAuditorSettings.asset</c>.</summary>
        ProjectSettings,
        /// <summary>Per-machine rules saved to <c>SmartAuditorPrefs.asset</c> in Unity's preferences folder.</summary>
        UserPreferences
    }

    /// <summary>
    /// Determines the scope of a rule mutation applied to a <see cref="ReportItem"/>.
    /// </summary>
    internal enum RuleAction
    {
        /// <summary>
        /// Upsert a rule keyed by descriptor Id + item context (asset path or dependency root).
        /// Covers both "suppress this occurrence" and "change severity of this occurrence".
        /// </summary>
        SetOccurrence,
        /// <summary>
        /// Add a descriptor-wide rule (empty pattern), replacing all existing per-context
        /// rules for the same Id. Covers "suppress all" and "set severity for all".
        /// </summary>
        SetDescriptor,
        /// <summary>
        /// Add a folder-glob rule matching all asset paths under a selected folder scope.
        /// Defaults to the item's containing directory when no explicit scope is provided.
        /// </summary>
        SetFolder,
    }

    /// <summary>
    /// Static helper that writes a <see cref="Rule"/> mutation to an in-memory
    /// <see cref="DiagnosticRules"/> instance and the selected persistent settings store,
    /// then saves the store. Separate from IMGUI code so it can be unit-tested without the
    /// Unity editor GUI infrastructure.
    /// </summary>
    internal static class IssueRuleActions
    {
        internal readonly struct FolderScopeOption
        {
            public readonly string Label;
            public readonly string Pattern;

            public FolderScopeOption(string label, string pattern)
            {
                Label = label;
                Pattern = pattern;
            }
        }

        /// <summary>
        /// Apply a rule mutation for <paramref name="issue"/> to both <paramref name="inMemoryRules"/>
        /// (for immediate UI feedback) and the chosen persistent settings store.
        /// </summary>
        /// <param name="inMemoryRules">The in-memory rules instance owned by the view (mutated for immediate effect).</param>
        /// <param name="issue">The report item the action targets.</param>
        /// <param name="action">The scope of the rule: per-occurrence, descriptor-wide, or folder.</param>
        /// <param name="severity">The severity to store (use <see cref="Severity.Suppressed"/> for suppress actions).</param>
        /// <param name="store">Which persistent settings backend to write to and save.</param>
        /// <param name="folderPattern">Optional explicit folder glob used for <see cref="RuleAction.SetFolder"/>.</param>
        internal static void Apply(
            DiagnosticRules inMemoryRules,
            ReportItem issue,
            RuleAction action,
            Severity severity,
            RuleStore store,
            string folderPattern = null)
        {
            Apply(inMemoryRules, GetPersistentRules(store), () => Save(store), issue, action, severity, folderPattern);
        }

        /// <summary>
        /// Testable overload: accepts explicit <paramref name="persistentRules"/> and a
        /// <paramref name="saveAction"/> delegate so unit tests can pass isolated
        /// <see cref="DiagnosticRules"/> instances without touching the settings singletons.
        /// </summary>
        internal static void Apply(
            DiagnosticRules inMemoryRules,
            DiagnosticRules persistentRules,
            Action saveAction,
            ReportItem issue,
            RuleAction action,
            Severity severity,
            string folderPattern = null)
        {
            switch (action)
            {
                case RuleAction.SetOccurrence:
                    inMemoryRules.SetRule(issue, severity);
                    persistentRules.SetRule(issue, severity);
                    break;

                case RuleAction.SetDescriptor:
                    inMemoryRules.AddRule(new Rule { Id = issue.Id, Severity = severity });
                    persistentRules.AddRule(new Rule { Id = issue.Id, Severity = severity });
                    break;

                case RuleAction.SetFolder:
                    var resolvedFolderPattern = string.IsNullOrEmpty(folderPattern) ? GetFolderPattern(issue) : folderPattern;
                    inMemoryRules.AddRule(new Rule { Id = issue.Id, Pattern = resolvedFolderPattern, Severity = severity });
                    persistentRules.AddRule(new Rule { Id = issue.Id, Pattern = resolvedFolderPattern, Severity = severity });
                    break;
            }

            saveAction?.Invoke();
        }

        /// <summary>
        /// Returns the glob pattern that covers all asset paths under the directory containing
        /// <paramref name="issue"/>'s asset, e.g. <c>Assets/Textures/**</c>.
        /// Returns an empty string when the item has no valid asset path.
        /// </summary>
        internal static string GetFolderPattern(ReportItem issue)
        {
            var path = issue.Location?.Path;
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
            return string.IsNullOrEmpty(dir) ? "*" : $"{dir}/**";
        }

        /// <summary>
        /// Returns a short, user-facing list of ancestor folder scopes that can be used for
        /// folder suppression. The list starts at the item's containing directory and walks
        /// upward to the asset-space root (Assets) or package root (Packages/&lt;name&gt;).
        /// </summary>
        internal static List<FolderScopeOption> GetFolderScopeOptions(ReportItem issue)
        {
            var path = issue.Location?.Path;
            if (string.IsNullOrEmpty(path))
                return new List<FolderScopeOption>();

            var dir = PathUtils.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir))
                return new List<FolderScopeOption>();

            var parts = PathUtils.Split(dir);
            if (parts.Length == 0)
                return new List<FolderScopeOption>();

            var minIndex = 0;
            if (parts[0].Equals("Packages", StringComparison.OrdinalIgnoreCase) && parts.Length > 1)
                minIndex = 1;

            var options = new List<FolderScopeOption>();
            var deepestIndex = parts.Length - 1;

            for (var i = deepestIndex; i >= minIndex; --i)
            {
                var scopePath = string.Join(PathUtils.Separator.ToString(), parts, 0, i + 1);
                if (string.IsNullOrEmpty(scopePath))
                    continue;

                options.Add(new FolderScopeOption(GetFolderScopeLabel(parts, i, minIndex, deepestIndex), $"{scopePath}/**"));
            }

            return options;
        }

        /// <summary>Returns true when <paramref name="issue"/> has a non-empty asset path suitable for folder suppression.</summary>
        internal static bool HasAssetPath(ReportItem issue) =>
            !string.IsNullOrEmpty(issue.Location?.Path);

        static DiagnosticRules GetPersistentRules(RuleStore store) =>
            store == RuleStore.ProjectSettings
                ? SmartAuditorSettings.instance.Rules
                : SmartAuditorPrefs.instance.Rules;

        static void Save(RuleStore store)
        {
            if (store == RuleStore.ProjectSettings)
                SmartAuditorSettings.instance.Save();
            else
                SmartAuditorPrefs.instance.Save();
        }

        static string GetFolderScopeLabel(string[] parts, int index, int minIndex, int deepestIndex)
        {
            if (index == deepestIndex)
            {
                if (parts[0].Equals("Assets", StringComparison.OrdinalIgnoreCase) && deepestIndex == 0)
                    return "Assets root";

                if (parts[0].Equals("Packages", StringComparison.OrdinalIgnoreCase) && minIndex == 1 && deepestIndex == 1)
                    return $"Package root ({parts[1]})";

                return $"This folder ({parts[index]})";
            }

            if (parts[0].Equals("Assets", StringComparison.OrdinalIgnoreCase) && index == 0)
                return "Assets root";

            if (parts[0].Equals("Packages", StringComparison.OrdinalIgnoreCase) && minIndex == 1 && index == 1)
                return $"Package root ({parts[1]})";

            var levelsUp = deepestIndex - index;
            return levelsUp == 1
                ? $"Parent folder ({parts[index]})"
                : $"{levelsUp} levels up ({parts[index]})";
        }
    }
}
