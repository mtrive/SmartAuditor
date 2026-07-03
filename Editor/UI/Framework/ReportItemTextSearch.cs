// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using SmartAuditor.Editor;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Shared substring matching for report items (navigator, scoped windows, etc.).
    /// </summary>
    public static class ReportItemTextSearch
    {
        const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// Returns whether <paramref name="issue"/> matches <paramref name="query"/> using a case-insensitive substring
        /// against description, file name, and asset path.
        /// </summary>
        /// <param name="issue">Issue or insight row.</param>
        /// <param name="query">Search text; empty or null matches all.</param>
        /// <param name="includeDependencies">When true, also matches type and method names in the issue's dependency call tree.</param>
        /// <returns>False when <paramref name="issue"/> is null and <paramref name="query"/> is non-empty; otherwise true if the query matches or is empty.</returns>
        public static bool MatchesSubstringFilter(ReportItem issue, string query, bool includeDependencies = false)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            if (issue == null)
                return false;

            if (MatchesField(issue.Description, query))
                return true;

            if (MatchesField(issue.Filename, query))
                return true;

            if (MatchesField(issue.RelativePath, query))
                return true;

            if (MatchesPropertyValues(issue.Properties, query))
                return true;

            if (issue.Evidence != null)
            {
                foreach (var value in issue.Evidence.Values)
                {
                    var formatted = EvidenceFormatter.Format(value);
                    if (MatchesField(formatted, query))
                        return true;
                }
            }

            if (includeDependencies && issue.Dependencies != null &&
                MatchesDependencyTree(issue.Dependencies, query))
                return true;

            return false;
        }

        /// <summary>
        /// Native-<see cref="Message"/> counterpart to
        /// <see cref="MatchesSubstringFilter(ReportItem, string, bool)"/>. Matches the query against
        /// the message's Description, location filename/path, and every value in its
        /// Properties dictionary (compiler messages carry per-key context such as
        /// <c>Code</c>, <c>Assembly</c>, <c>ShaderName</c>, <c>Platform</c>).
        /// </summary>
        public static bool MatchesSubstringFilter(Message message, string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            if (message == null)
                return false;

            if (MatchesField(message.Description, query))
                return true;

            if (MatchesLocation(message.Location, query))
                return true;

            return MatchesPropertyValues(message.Properties, query);
        }

        /// <summary>
        /// Entry-kind-agnostic substring match. Dispatches to the typed overloads for
        /// <see cref="ReportItem"/> and <see cref="Message"/>; for other <see cref="IReportEntry"/>
        /// kinds searches description, location, and property values.
        /// </summary>
        /// <param name="entry">Report entry to test.</param>
        /// <param name="query">Search text; empty or null matches all.</param>
        /// <param name="includeDependencies">When true and <paramref name="entry"/> is a <see cref="ReportItem"/>, also matches dependency call trees.</param>
        public static bool MatchesSubstringFilter(IReportEntry entry, string query, bool includeDependencies = false)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            if (entry == null)
                return false;

            if (entry is ReportItem issue)
                return MatchesSubstringFilter(issue, query, includeDependencies);
            if (entry is Message message)
                return MatchesSubstringFilter(message, query);

            if (MatchesField(entry.Description, query))
                return true;

            if (MatchesLocation(entry.Location, query))
                return true;

            return MatchesPropertyValues(entry.Properties, query);
        }

        static bool MatchesField(string text, string query)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(query, Comparison) >= 0;
        }

        static bool MatchesLocation(Location location, string query)
        {
            if (location == null)
                return false;

            return MatchesField(location.Filename, query) || MatchesField(location.Path, query);
        }

        static bool MatchesPropertyValues(IReadOnlyDictionary<string, string> properties, string query)
        {
            if (properties == null)
                return false;

            foreach (var value in properties.Values)
            {
                if (MatchesField(value, query))
                    return true;
            }

            return false;
        }

        static bool MatchesDependencyTree(DependencyNode node, string query)
        {
            if (node == null)
                return false;

            if (node is CallTreeNode callTreeNode)
            {
                if (MatchesField(callTreeNode.PrettyTypeName, query) ||
                    MatchesField(callTreeNode.PrettyMethodName, query))
                    return true;
            }

            for (var i = 0; i < node.ChildCount; i++)
            {
                if (MatchesDependencyTree(node.GetChild(i), query))
                    return true;
            }

            return false;
        }
    }
}
