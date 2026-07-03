// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SmartAuditor.Editor;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Formats report entries as self-contained, terminal-paste-ready markdown prompts for an
    /// external AI coding agent (Claude/Codex). <see cref="FormatIssue"/> bundles diagnostic
    /// identity, location, problem statement, recommendation, structured evidence, and a source
    /// snippet so the agent can fix the issue without further context.
    /// <see cref="FormatMessage"/> does the same for toolchain messages (compiler errors,
    /// shader compilation output, etc.) so the agent can explain the message and suggest a fix.
    /// </summary>
    internal static class AgentPromptFormatter
    {
        // Context lines included above/below the issue line in the code snippet block.
        const int k_SnippetContextLines = 3;

        /// <summary>
        /// Builds a markdown prompt describing the given issue. Returns an empty string for null
        /// items or items that are not issues (insights/messages carry no descriptor guidance).
        /// </summary>
        public static string FormatIssue(ReportItem item)
        {
            if (item == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("Fix a Unity issue reported by Smart Auditor.");
            sb.AppendLine();

            AppendIssueSection(sb, item);
            AppendLocationSection(sb, item);
            AppendProblemSection(sb, item);
            AppendRecommendationSection(sb, item);
            AppendDetailsSection(sb, item);
            AppendCodeSection(sb, item);
            AppendDocs(sb, item);

            return sb.ToString().TrimEnd() + "\n";
        }

        /// <summary>
        /// Builds a markdown prompt describing the given toolchain message. Returns an empty
        /// string for null items.
        /// </summary>
        public static string FormatMessage(Message message)
        {
            if (message == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("Explain a Unity toolchain message and suggest how to fix it.");
            sb.AppendLine();

            AppendMessageSection(sb, message);
            AppendLocationSection(sb, message);
            AppendMessageContextSection(sb, message);
            AppendCodeSection(sb, message);

            return sb.ToString().TrimEnd() + "\n";
        }

        static void AppendMessageSection(StringBuilder sb, Message message)
        {
            sb.AppendLine("## Message");
            sb.AppendLine($"- Log Level: {message.LogLevel}");
            sb.AppendLine($"- Category: {GetMessageCategoryLabel(message.Category)}");
            if (!string.IsNullOrEmpty(message.Description))
                sb.AppendLine($"- Text: {message.Description.Trim()}");
            sb.AppendLine();
        }

        static void AppendMessageContextSection(StringBuilder sb, Message message)
        {
            var fields = BuildMessagePropertyFields(message);
            if (fields.Count == 0)
                return;

            sb.AppendLine("## Context");
            foreach (var (label, value) in fields)
                sb.AppendLine($"- {label}: {value}");
            sb.AppendLine();
        }

        static List<(string Label, string Value)> BuildMessagePropertyFields(Message message)
        {
            var fields = new List<(string Label, string Value)>();
            if (message.Properties == null || message.Properties.Count == 0)
                return fields;

            var schema = CategorySchemaRegistry.GetSchema(message.Category);
            foreach (var kv in message.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(kv.Value))
                    continue;
                fields.Add((GetPropertyLabel(schema, kv.Key), kv.Value));
            }

            return fields;
        }

        static string GetMessageCategoryLabel(AnalysisCategory category)
        {
            switch (category)
            {
                case AnalysisCategory.CodeCompilerMessage: return "Compiler Messages";
                case AnalysisCategory.ShaderCompilerMessage: return "Shader Compiler Messages";
                case AnalysisCategory.AssetImportMessage: return "Asset Import Messages";
                default: return category.ToString();
            }
        }

        static void AppendIssueSection(StringBuilder sb, ReportItem item)
        {
            var descriptor = item.Descriptor;
            sb.AppendLine("## Issue");
            if (descriptor != null && descriptor.IsValid() && !string.IsNullOrEmpty(descriptor.Id))
                sb.AppendLine($"- ID: {descriptor.Id}");
            sb.AppendLine($"- Title: {IssueDetailsUi.GetTitle(item)}");
            sb.AppendLine($"- Severity: {item.Severity}");
            sb.AppendLine($"- Category: {item.Category}");
            if (descriptor != null && descriptor.IsValid() && descriptor.Impact != Impact.None)
                sb.AppendLine($"- Impact: {descriptor.Impact}");
            sb.AppendLine();
        }

        static void AppendLocationSection(StringBuilder sb, IReportEntry entry)
        {
            var location = entry.Location;
            if (location == null || !location.IsValid)
                return;

            sb.AppendLine("## Location");
            sb.AppendLine($"- File: {location.Path}");
            if (location.Line.HasValue && location.Line.Value > 0)
            {
                var lineText = location.EndLine.HasValue && location.EndLine.Value > location.Line.Value
                    ? $"{location.Line.Value}-{location.EndLine.Value}"
                    : location.Line.Value.ToString(CultureInfo.InvariantCulture);
                sb.AppendLine($"- Line: {lineText}");
            }
            if (location.Column.HasValue && location.Column.Value > 0)
                sb.AppendLine($"- Column: {location.Column.Value}");
            sb.AppendLine();
        }

        static void AppendProblemSection(StringBuilder sb, ReportItem item)
        {
            var description = IssueDetailsUi.GetDescription(item, allowItemDescriptionFallback: false);
            if (string.IsNullOrEmpty(description))
                return;

            sb.AppendLine("## Problem");
            sb.AppendLine(description.Trim());
            sb.AppendLine();
        }

        static void AppendRecommendationSection(StringBuilder sb, ReportItem item)
        {
            var recommendation = IssueDetailsUi.GetRecommendation(item);
            if (string.IsNullOrEmpty(recommendation))
                return;

            sb.AppendLine("## Recommendation");
            sb.AppendLine(recommendation.Trim());
            sb.AppendLine();
        }

        static void AppendDetailsSection(StringBuilder sb, ReportItem item)
        {
            // The per-occurrence message often repeats the title (when the descriptor has no
            // distinct MessageFormat); only surface it when it adds information.
            var message = item.Description;
            var title = IssueDetailsUi.GetTitle(item);
            var hasMessage = !string.IsNullOrEmpty(message) &&
                !string.Equals(message, title, StringComparison.Ordinal);

            var evidence = BuildEvidenceFields(item);
            if (!hasMessage && evidence.Count == 0)
                return;

            sb.AppendLine("## Details");
            if (hasMessage)
                sb.AppendLine(message.Trim());
            foreach (var (label, value) in evidence)
                sb.AppendLine($"- {label}: {value}");
            sb.AppendLine();
        }

        static void AppendCodeSection(StringBuilder sb, IReportEntry entry)
        {
            var settings = new CodeSnippetDrawerSettings(
                contextBefore: k_SnippetContextLines,
                contextAfter: k_SnippetContextLines,
                maxInitialLines: 200,
                maxExpandedLines: 200,
                horizontalIndent: 0f,
                horizontalPadding: 0f,
                lineHeight: 16f,
                unavailableLabel: string.Empty);

            if (!CodeSnippetDrawer.TryPrepareSnippet(entry, expanded: true, settings, out var snippet) || !snippet.IsValid)
                return;

            var fence = GetCodeFenceLanguage(entry.Location?.Path);
            sb.AppendLine("## Code");
            sb.AppendLine("```" + fence);
            for (var lineNumber = snippet.Window.FirstLine; lineNumber <= snippet.Window.LastLine; lineNumber++)
            {
                // Lines is 0-based; snippet line numbers are 1-based.
                var index = lineNumber - 1;
                if (index < 0 || index >= snippet.Lines.Length)
                    continue;

                var isIssueLine = lineNumber >= snippet.IssueStart && lineNumber <= snippet.IssueEnd;
                var marker = isIssueLine ? ">" : " ";
                sb.AppendLine($"{marker} {lineNumber,4}  {snippet.Lines[index]}");
            }
            sb.AppendLine("```");
            sb.AppendLine();
        }

        static void AppendDocs(StringBuilder sb, ReportItem item)
        {
            var descriptor = item.Descriptor;
            if (descriptor != null && descriptor.IsValid() && !string.IsNullOrEmpty(descriptor.DocumentationUrl))
                sb.AppendLine($"Docs: {descriptor.DocumentationUrl}");
        }

        // Mirrors AnalysisReportView.BuildEvidenceFields, minus the Category/Severity rows which
        // are already covered by the ## Issue section. Properties use schema column labels;
        // evidence values go through the shared EvidenceFormatter.
        static List<(string Label, string Value)> BuildEvidenceFields(ReportItem item)
        {
            var fields = new List<(string Label, string Value)>();
            var hasProperties = item.PropertyCount > 0;
            var hasEvidence = item.Evidence != null && item.Evidence.Count > 0;

            if (hasProperties)
            {
                var schema = CategorySchemaRegistry.GetSchema(item.Category);
                foreach (var kv in item.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (string.IsNullOrEmpty(kv.Value))
                        continue;
                    var value = PropertyFormatting.FormatPropertyText(schema, kv.Key, kv.Value);
                    fields.Add((GetPropertyLabel(schema, kv.Key), value));
                }
            }

            if (hasEvidence)
            {
                foreach (var key in item.Evidence.Keys.OrderBy(k => k, StringComparer.Ordinal))
                    fields.Add((key, EvidenceFormatter.Format(item.Evidence[key])));
            }

            return fields;
        }

        static string GetPropertyLabel(InsightSchema schema, string canonicalKey)
        {
            var column = schema?.GetColumn(canonicalKey);
            if (column != null && !string.IsNullOrEmpty(column.LongName))
                return column.LongName;
            if (column != null && !string.IsNullOrEmpty(column.DisplayName))
                return column.DisplayName;

            return canonicalKey;
        }

        static string GetCodeFenceLanguage(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".cs":
                    return "csharp";
                case ".shader":
                case ".hlsl":
                case ".cginc":
                case ".compute":
                    return "hlsl";
                case ".json":
                    return "json";
                case ".xml":
                case ".uxml":
                    return "xml";
                case ".uss":
                    return "css";
                case ".yaml":
                case ".yml":
                    return "yaml";
                default:
                    return string.Empty;
            }
        }
    }
}
