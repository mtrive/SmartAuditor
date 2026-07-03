using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor
{
    // Rules to specify the Severity of individual Issues.
    // Smart Auditor interacts with this to Ignore/Display issues, and it's exposed in the Settings window, but we don't need it in the API.
    // Users can simply construct a List<Rule> and pass it to AnalysisOptions.WithAdditionalDiagnosticRules()
    [Serializable]
    internal sealed class DiagnosticRules
    {
        [NonReorderable]
        [JsonProperty("rules")]
        [SerializeField]
        List<Rule> m_Rules = new List<Rule>();

        internal int NumRules => m_Rules.Count;

        public DiagnosticRules()
        { }

        public DiagnosticRules(List<Rule> rules)
        {
            m_Rules = rules;
        }

        // Copy constructor
        public DiagnosticRules(DiagnosticRules copyFrom)
        {
            foreach (var rule in copyFrom.m_Rules)
            {
                m_Rules.Add(new Rule
                {
                    Severity = rule.Severity,
                    Pattern = rule.Pattern,
                    Id = rule.Id
                });
            }
        }

        internal void AddRule(Rule ruleToAdd)
        {
            if (string.IsNullOrEmpty(ruleToAdd.Pattern))
            {
                ruleToAdd.Pattern = string.Empty; // make sure it's empty, as opposed to null

                var rules = m_Rules.Where(r => r.Id.Equals(ruleToAdd.Id)).ToArray();
                foreach (var ruleToDelete in rules)
                    m_Rules.Remove(ruleToDelete);
            }

            m_Rules.Add(ruleToAdd);
        }

        public Rule AddRule(string id)
        {
            var rule = new Rule { Id = id };
            AddRule(rule);
            return rule;
        }

        internal void AddRules(DiagnosticRules rules)
        {
            foreach (var rule in rules.m_Rules)
            {
                m_Rules.Add(rule);
            }
        }

        internal Rule GetRule(string id, string context = "")
        {
            foreach (var r in m_Rules)
            {
                if (r.Id.Equals(id) && PatternMatches(r.Pattern, context))
                    return r;
            }
            return null;
        }

        /// <summary>
        /// Returns true if any rule with no descriptor Id (i.e. one that applies to every
        /// diagnostic) is set to <see cref="Severity.Suppressed"/> and its glob pattern matches
        /// <paramref name="path"/>. Modules can use this to skip per-file analysis entirely
        /// for paths the user has globally suppressed, rather than running every analyzer
        /// and then dropping the findings on the floor at report time.
        /// </summary>
        internal bool IsPathGloballySuppressed(string path)
        {
            if (string.IsNullOrEmpty(path) || m_Rules.Count == 0)
                return false;

            foreach (var r in m_Rules)
            {
                if (!string.IsNullOrEmpty(r.Id))
                    continue;
                if (r.Severity != Severity.Suppressed)
                    continue;
                if (PatternMatches(r.Pattern, path))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Glob-match <paramref name="path"/> against <paramref name="globPattern"/>. An empty
        /// pattern (or <c>"*"</c>) matches anything; otherwise <c>*</c> matches non-slash
        /// segments at the root and full paths under <c>**</c>, and <c>?</c> matches a single
        /// character. Case-insensitive — Unity asset paths normalise to lowercase on Windows.
        /// </summary>
        static bool PatternMatches(string globPattern, string path)
        {
            // Match-all?
            if (string.IsNullOrEmpty(globPattern) || globPattern.Equals("*"))
                return true;

            // Pattern for root level (replace ** and following slash with empty string)
            var rootPattern = "^" + Regex.Escape(globPattern)
                .Replace(@"\*\*/", "")      // Remove **/ for root-level match
                .Replace(@"\*", "[^/]*")    // Replace * with non-slash chars
                .Replace(@"\?", ".")        // Replace ? with single char
                + "$";

            if (Regex.IsMatch(path ?? string.Empty, rootPattern, RegexOptions.IgnoreCase))
                return true;

            // Try subfolders
            if (globPattern.Contains("**"))
            {
                var subfoldersPattern = "^" + Regex.Escape(globPattern)
                    .Replace(@"\*", ".*")       // Replace * with .*
                    .Replace(@"\?", ".")        // Replace ? with .
                    + "$";

                if (Regex.IsMatch(path ?? string.Empty, subfoldersPattern, RegexOptions.IgnoreCase))
                    return true;
            }

            return false;
        }

        internal void ClearRules(string id, string filter = "")
        {
            var rules = m_Rules.Where(r => r.Id.Equals(id) && r.Pattern.Equals(filter)).ToArray();

            foreach (var rule in rules)
                m_Rules.Remove(rule);
        }

        internal void ClearRules(ReportItem issue)
        {
            var id = issue.Id;
            ClearRules(id, issue.GetContext());
        }

        internal Severity GetAction(string id, string filter = "")
        {
            // is there a rule that matches the Pattern?
            var projectRule = GetRule(id, filter);
            if (projectRule != null)
                return projectRule.Severity;

            // is there a rule that matches descriptor?
            projectRule = GetRule(id);
            if (projectRule != null)
                return projectRule.Severity;

            return Severity.Default;
        }

        internal void SetRule(ReportItem issue, Severity ruleSeverity)
        {
            var id = issue.Id;

            // FIXME: GetContext will return empty string on code issues after domain reload
            var context = issue.GetContext();
            var rule = GetRule(id, context);

            if (rule == null)
                AddRule(new Rule
                {
                    Id = id,
                    Pattern = context,
                    Severity = ruleSeverity
                });
            else
                rule.Severity = ruleSeverity;
        }

        // Only used for testing
        internal void ClearAllRules()
        {
            m_Rules.Clear();
        }
    }
}
