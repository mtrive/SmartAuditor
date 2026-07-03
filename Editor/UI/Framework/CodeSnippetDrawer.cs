// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    internal readonly struct CodeSnippetDrawerSettings
    {
        public static CodeSnippetDrawerSettings Default => new CodeSnippetDrawerSettings(
            contextBefore: 2,
            contextAfter: 2,
            maxInitialLines: 5,
            maxExpandedLines: 15,
            horizontalIndent: 18f,
            horizontalPadding: 8f,
            lineHeight: 16f,
            unavailableLabel: "Snippet unavailable.");

        public CodeSnippetDrawerSettings(
            int contextBefore,
            int contextAfter,
            int maxInitialLines,
            int maxExpandedLines,
            float horizontalIndent,
            float horizontalPadding,
            float lineHeight,
            string unavailableLabel)
        {
            ContextBefore = Math.Max(0, contextBefore);
            ContextAfter = Math.Max(0, contextAfter);
            MaxInitialLines = Math.Max(1, maxInitialLines);
            MaxExpandedLines = Math.Max(MaxInitialLines, maxExpandedLines);
            HorizontalIndent = Math.Max(0f, horizontalIndent);
            HorizontalPadding = Math.Max(0f, horizontalPadding);
            LineHeight = Math.Max(8f, lineHeight);
            UnavailableLabel = string.IsNullOrEmpty(unavailableLabel) ? "Snippet unavailable." : unavailableLabel;
        }

        public int ContextBefore { get; }
        public int ContextAfter { get; }
        public int MaxInitialLines { get; }
        public int MaxExpandedLines { get; }
        public float HorizontalIndent { get; }
        public float HorizontalPadding { get; }
        public float LineHeight { get; }
        public string UnavailableLabel { get; }
    }

    internal readonly struct PreparedCodeSnippet
    {
        public PreparedCodeSnippet(string[] lines, SnippetWindowResult window, int issueStart, int issueEnd)
        {
            Lines = lines ?? Array.Empty<string>();
            Window = window;
            IssueStart = issueStart;
            IssueEnd = issueEnd;
        }

        public string[] Lines { get; }
        public SnippetWindowResult Window { get; }
        public int IssueStart { get; }
        public int IssueEnd { get; }
        public bool IsValid => Lines.Length > 0 && Window.IsValid;
    }

    internal static class CodeSnippetDrawer
    {
        const float k_GutterWidth = 52f;
        const float k_GutterGap = 6f;
        const float k_CodeContentPadding = 4f;

        static readonly Dictionary<string, Vector2> s_HorizontalScrollPositions = new();

        public static bool CanDrawSnippet(IReportEntry entry)
        {
            var location = entry?.Location;
            if (location == null || !location.IsValid)
                return false;
            if (!HasSourceSnippetSupport(location.Path))
                return false;

            return location.Line.HasValue && location.Line.Value > 0;
        }

        public static bool TryPrepareSnippet(
            IReportEntry entry,
            bool expanded,
            CodeSnippetDrawerSettings settings,
            out PreparedCodeSnippet snippet)
        {
            snippet = default;
            if (!CanDrawSnippet(entry))
                return false;

            var location = entry.Location;
            if (!CodeSnippetCache.TryGetLines(location.Path, out var lines) || lines.Length == 0)
                return false;

            var window = SnippetWindow.Compute(
                location.Line ?? 0,
                location.EndLine,
                lines.Length,
                settings.ContextBefore,
                settings.ContextAfter,
                settings.MaxInitialLines,
                settings.MaxExpandedLines,
                expanded);
            if (!window.IsValid)
                return false;

            var issueStart = Math.Max(1, Math.Min(location.Line ?? 1, lines.Length));
            var issueEndCandidate = location.EndLine ?? issueStart;
            var issueEnd = Math.Max(issueStart, Math.Min(issueEndCandidate, lines.Length));

            snippet = new PreparedCodeSnippet(lines, window, issueStart, issueEnd);
            return true;
        }

        public static string GetExpansionKey(IReportEntry entry)
        {
            var location = entry?.Location;
            if (location == null)
                return "snippet:<null>";

            var line = location.Line ?? -1;
            var endLine = location.EndLine ?? -1;
            return $"snippet:{location.Path}:{line}:{endLine}";
        }

        public static void Draw(
            IReportEntry entry,
            IDictionary<string, bool> snippetExpansionState,
            Action repaint,
            CodeSnippetDrawerSettings settings,
            bool showCollapseButton = false,
            Action onCollapse = null)
        {
            if (!CanDrawSnippet(entry))
                return;

            var expansionKey = GetExpansionKey(entry);
            var expanded = snippetExpansionState != null &&
                snippetExpansionState.TryGetValue(expansionKey, out var isExpanded) &&
                isExpanded;

            if (!TryPrepareSnippet(entry, expanded, settings, out var prepared))
            {
                DrawSnippetUnavailable(settings);
                return;
            }

            // Diagnostics carry a richer Severity (Critical/Major/Moderate/Minor) than the
            // universal LogLevel, so honor it for the accent. Other entries (toolchain
            // messages) map their LogLevel to the equivalent severity — the same color they
            // already get today, when they reach this path as adapted ReportItems.
            var accentSeverity = entry is ReportItem diagnostic
                ? diagnostic.Severity
                : LogLevelSeverityMapping.LogLevelToSeverity(entry.LogLevel);
            var accentColor = SeverityPresentation.GetColorForSeverity(accentSeverity);
            var lineHighlightColor = new Color(
                accentColor.r,
                accentColor.g,
                accentColor.b,
                SharedStyles.IsDarkMode ? 0.22f : 0.18f);

            GUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(settings.HorizontalIndent);
                using (new EditorGUILayout.VerticalScope(SharedStyles.CodeSnippetBackground))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var location = entry.Location;
                        var pathText = Formatting.FormatPath(location.Path, location.Line, location.EndLine);
                        EditorGUILayout.LabelField(pathText, SharedStyles.LabelDarkWithDynamicSize, GUILayout.ExpandWidth(true));

                        if (showCollapseButton && onCollapse != null)
                        {
                            if (UiSection.DrawLinkButton(new GUIContent("\u2191", "Collapse snippet"), GUILayout.ExpandWidth(false)))
                            {
                                onCollapse();
                                repaint?.Invoke();
                            }
                        }
                    }

                    GUILayout.Space(2);
                    if (expanded)
                        DrawExpandedCodeBlock(prepared, settings, expansionKey, accentColor, lineHighlightColor);
                    else
                        DrawCodeLines(prepared, settings, accentColor, lineHighlightColor);

                    if (prepared.Window.IsTruncated)
                    {
                        GUILayout.Space(2);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            var previousColor = GUI.color;
                            var tintedColor = previousColor;
                            tintedColor.a *= 0.55f;
                            GUI.color = tintedColor;
                            if (UiSection.DrawLinkButton("Show more", GUILayout.ExpandWidth(false)))
                            {
                                GUI.color = previousColor;
                                if (snippetExpansionState != null)
                                    snippetExpansionState[expansionKey] = true;
                                repaint?.Invoke();
                            }

                            GUI.color = previousColor;
                            GUILayout.FlexibleSpace();
                        }
                    }
                    else if (prepared.Window.FullLineCount > settings.MaxInitialLines && expanded)
                    {
                        GUILayout.Space(2);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            if (UiSection.DrawLinkButton("Show less", GUILayout.ExpandWidth(false)))
                            {
                                if (snippetExpansionState != null)
                                    snippetExpansionState[expansionKey] = false;
                                s_HorizontalScrollPositions.Remove(expansionKey);
                                repaint?.Invoke();
                            }

                            GUILayout.FlexibleSpace();
                        }
                    }
                }

                GUILayout.Space(settings.HorizontalPadding);
            }
        }

        // Different analyzers use different AnalysisCategory values for the same kind of
        // source-line finding (e.g. ShaderPragmaAnalyzer emits AssetIssue, SystemLinqAnalyzer
        // emits Code), so gating on category is both incorrect and brittle. Gate on the file
        // extension instead -- only files whose contents are readable line-oriented text get
        // a snippet, regardless of which category recorded the issue. This also avoids
        // accidentally feeding binary asset files (textures, meshes) into the line cache
        // when an asset analyzer happens to set a Line value.
        static bool HasSourceSnippetSupport(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".compute", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".cginc", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".glsl", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);
        }

        static void DrawExpandedCodeBlock(
            PreparedCodeSnippet prepared,
            CodeSnippetDrawerSettings settings,
            string expansionKey,
            Color accentColor,
            Color lineHighlightColor)
        {
            var lineCount = prepared.Window.LastLine - prepared.Window.FirstLine + 1;
            var codeBlockHeight = lineCount * settings.LineHeight;
            var contentWidth = ComputeContentWidth(prepared);

            if (!s_HorizontalScrollPositions.TryGetValue(expansionKey, out var scrollPosition))
                scrollPosition = Vector2.zero;

            scrollPosition.y = 0f;

            var viewportWidth = EstimateViewportWidth(settings);
            var needsHorizontalScroll = contentWidth > viewportWidth || scrollPosition.x > 0.01f;
            var horizontalScrollbarHeight = GUI.skin.horizontalScrollbar.fixedHeight;
            var scrollViewHeight = codeBlockHeight +
                (needsHorizontalScroll ? horizontalScrollbarHeight : 0f);

            scrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition,
                alwaysShowHorizontal: false,
                alwaysShowVertical: false,
                GUI.skin.horizontalScrollbar,
                GUIStyle.none,
                GUI.skin.scrollView,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(scrollViewHeight));

            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(contentWidth), GUILayout.ExpandWidth(true)))
                DrawCodeLines(prepared, settings, accentColor, lineHighlightColor);

            EditorGUILayout.EndScrollView();
            scrollPosition.y = 0f;
            s_HorizontalScrollPositions[expansionKey] = scrollPosition;
        }

        static void DrawCodeLines(
            PreparedCodeSnippet prepared,
            CodeSnippetDrawerSettings settings,
            Color accentColor,
            Color lineHighlightColor)
        {
            for (var lineNumber = prepared.Window.FirstLine; lineNumber <= prepared.Window.LastLine; lineNumber++)
            {
                var lineIndex = lineNumber - 1;
                var lineContent = lineIndex >= 0 && lineIndex < prepared.Lines.Length
                    ? prepared.Lines[lineIndex]
                    : string.Empty;
                var isIssueLine = lineNumber >= prepared.IssueStart && lineNumber <= prepared.IssueEnd;

                var rowRect = EditorGUILayout.GetControlRect(hasLabel: false, settings.LineHeight);
                var gutterRect = new Rect(rowRect.x, rowRect.y, k_GutterWidth, rowRect.height);
                var codeRect = new Rect(
                    gutterRect.xMax + k_GutterGap,
                    rowRect.y,
                    rowRect.width - gutterRect.width - k_GutterGap,
                    rowRect.height);

                if (isIssueLine)
                {
                    SolidDraw.Rect(rowRect, lineHighlightColor);
                    SolidDraw.Rect(new Rect(gutterRect.xMax - 3f, rowRect.y, 3f, rowRect.height), accentColor);
                }

                EditorGUI.LabelField(gutterRect, lineNumber.ToString(), SharedStyles.CodeSnippetGutter);
                EditorGUI.LabelField(codeRect, lineContent, SharedStyles.CodeSnippet);
            }
        }

        static float EstimateViewportWidth(CodeSnippetDrawerSettings settings)
        {
            var backgroundPadding = SharedStyles.CodeSnippetBackground.padding.horizontal;
            return Mathf.Max(0f, EditorGUIUtility.currentViewWidth
                - settings.HorizontalIndent
                - settings.HorizontalPadding
                - backgroundPadding);
        }

        static float ComputeContentWidth(PreparedCodeSnippet prepared)
        {
            var codeStyle = SharedStyles.CodeSnippet;
            var maxCodeWidth = 0f;
            for (var lineNumber = prepared.Window.FirstLine; lineNumber <= prepared.Window.LastLine; lineNumber++)
            {
                var lineIndex = lineNumber - 1;
                if (lineIndex < 0 || lineIndex >= prepared.Lines.Length)
                    continue;

                var size = codeStyle.CalcSize(new GUIContent(prepared.Lines[lineIndex]));
                if (size.x > maxCodeWidth)
                    maxCodeWidth = size.x;
            }

            return k_GutterWidth + k_GutterGap + maxCodeWidth + k_CodeContentPadding;
        }

        static void DrawSnippetUnavailable(CodeSnippetDrawerSettings settings)
        {
            GUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(settings.HorizontalIndent);
                using (new EditorGUILayout.VerticalScope(SharedStyles.CodeSnippetBackground))
                    EditorGUILayout.LabelField(settings.UnavailableLabel, SharedStyles.LabelDarkWithDynamicSize);
                GUILayout.Space(settings.HorizontalPadding);
            }
        }
    }
}
