// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Process-wide cache for <see cref="GUIContent"/> instances built from inputs that
    /// don't change between repaints (descriptor titles, trimmed section headers). IMGUI
    /// views call into these helpers from the per-row / per-frame hot path; without caching
    /// they would allocate a fresh <see cref="GUIContent"/> on every redraw and pay GC
    /// pressure proportional to the visible row count.
    /// </summary>
    /// <remarks>
    /// Cache keys cover the full input variance, so no explicit invalidation is required:
    /// a count change in a section title produces a new key and a new cached entry.
    /// <see cref="Clear"/> exists for tests that want a clean slate.
    /// </remarks>
    internal static class GUIContentCache
    {
        const int k_DescriptorTitleMaxChars = 140;
        const string k_Ellipsis = "…";

        static readonly Dictionary<string, GUIContent> s_Descriptor =
            new Dictionary<string, GUIContent>();
        static readonly Dictionary<string, GUIContent> s_DescriptorRich =
            new Dictionary<string, GUIContent>();
        static readonly Dictionary<TrimKey, GUIContent> s_TrimToWidth =
            new Dictionary<TrimKey, GUIContent>();

        // Reusable probe content for trim measurements; never returned to callers.
        static readonly GUIContent s_TrimProbe = new GUIContent();

        /// <summary>
        /// Returns a click-to-investigate link content for <paramref name="descriptor"/>.
        /// Long titles are truncated with an ellipsis; the tooltip carries the full title
        /// and description. Cached by descriptor id (assumed stable per session).
        /// </summary>
        public static GUIContent ForDescriptor(Descriptor descriptor)
        {
            var id = descriptor?.Id ?? string.Empty;
            if (s_Descriptor.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var title = descriptor?.Title ?? string.Empty;
            string display;
            if (string.IsNullOrEmpty(title))
            {
                display = descriptor?.Description ?? "Issue";
            }
            else if (title.Length <= k_DescriptorTitleMaxChars)
            {
                display = title;
            }
            else
            {
                display = title.Substring(0, k_DescriptorTitleMaxChars) + k_Ellipsis;
            }

            string tooltip;
            if (!string.IsNullOrEmpty(descriptor?.Description))
            {
                tooltip = $"{descriptor.Title}\n\n{descriptor.Description}\n\nClick to open issue details.";
            }
            else if (string.IsNullOrEmpty(title))
            {
                tooltip = "Click to open issue details.";
            }
            else
            {
                tooltip = $"{title}\n\nClick to open issue details.";
            }

            var content = new GUIContent(display, tooltip);
            s_Descriptor[id] = content;
            return content;
        }

        /// <summary>
        /// Returns a rich-text content for <paramref name="descriptor"/> suitable for issue-row
        /// labels. The domain prefix is rendered in a muted colour and the first dotted API symbol
        /// (e.g. <c>Animator.parameters</c>) is wrapped in bold. Cached per descriptor id and
        /// skin so that dark/light-mode switches produce correct colours.
        /// </summary>
        /// <remarks>
        /// The returned content must only be drawn with a style that has <c>richText = true</c>
        /// (e.g. <see cref="SharedStyles.RichLabel"/>). Long titles are truncated with an
        /// ellipsis applied to the plain title length (not the markup length) to keep layout
        /// predictable.
        /// </remarks>
        public static GUIContent ForDescriptorRich(Descriptor descriptor)
        {
            var id = descriptor?.Id ?? string.Empty;
            // Key includes skin so a mode switch yields fresh markup with correct colours.
            var richKey = id + (EditorGUIUtility.isProSkin ? ":d" : ":l");
            if (s_DescriptorRich.TryGetValue(richKey, out var cached))
                return cached;

            var title = descriptor?.Title ?? string.Empty;
            string displayText;
            if (string.IsNullOrEmpty(title))
            {
                displayText = descriptor?.Description ?? "Issue";
            }
            else
            {
                var capped = title.Length <= k_DescriptorTitleMaxChars
                    ? title
                    : title.Substring(0, k_DescriptorTitleMaxChars) + k_Ellipsis;
                displayText = BuildRichTitle(capped, EditorGUIUtility.isProSkin);
            }

            string tooltip;
            if (!string.IsNullOrEmpty(descriptor?.Description))
                tooltip = $"{descriptor.Title}\n\n{descriptor.Description}\n\nClick to open issue details.";
            else if (string.IsNullOrEmpty(title))
                tooltip = "Click to open issue details.";
            else
                tooltip = $"{title}\n\nClick to open issue details.";

            var content = new GUIContent(displayText, tooltip);
            s_DescriptorRich[richKey] = content;
            return content;
        }

        // Reusable builder — avoids per-call allocation in the descriptor-rich path.
        static readonly StringBuilder s_RichTitleBuilder = new StringBuilder(128);

        static string BuildRichTitle(string title, bool isDark)
        {
            var mutedColor = isDark ? "#888888" : "#555555";

            // Split off "Domain: " prefix at the first colon-space.
            var prefixEnd = title.IndexOf(": ", System.StringComparison.Ordinal);
            string prefix;
            string body;
            if (prefixEnd >= 0)
            {
                prefix = title.Substring(0, prefixEnd + 2);
                body = title.Substring(prefixEnd + 2);
            }
            else
            {
                prefix = string.Empty;
                body = title;
            }

            s_RichTitleBuilder.Clear();
            if (!string.IsNullOrEmpty(prefix))
            {
                s_RichTitleBuilder.Append("<color=");
                s_RichTitleBuilder.Append(mutedColor);
                s_RichTitleBuilder.Append('>');
                AppendEscaped(s_RichTitleBuilder, prefix);
                s_RichTitleBuilder.Append("</color>");
            }

            AppendBodyWithBoldSymbol(s_RichTitleBuilder, body);
            return s_RichTitleBuilder.ToString();
        }

        /// <summary>
        /// Appends <paramref name="body"/> to <paramref name="sb"/>, wrapping the first
        /// dotted symbol token (e.g. <c>Animator.parameters</c>) in <c>&lt;b&gt;</c> tags.
        /// If no dotted symbol is present the body is appended as-is (escaped).
        /// </summary>
        static void AppendBodyWithBoldSymbol(StringBuilder sb, string body)
        {
            // Walk the body character-by-character to find the first token that
            // contains at least one dot, forming a dotted symbol (e.g. Type.member).
            // No Regex: this runs per descriptor per skin on a hot repaint path.
            var symbolStart = -1;
            var symbolEnd = -1;
            var hasDot = false;

            for (var i = 0; i < body.Length; i++)
            {
                var c = body[i];
                var isWordChar = char.IsLetterOrDigit(c) || c == '_';

                if (isWordChar)
                {
                    if (symbolStart < 0)
                        symbolStart = i;
                    if (hasDot)
                        symbolEnd = i;
                }
                else if (c == '.' && symbolStart >= 0 && !hasDot)
                {
                    hasDot = true;
                }
                else
                {
                    if (hasDot)
                        break; // end of the dotted symbol — symbolEnd was set in prior loop

                    // This word had no dot; reset and look for the next candidate.
                    symbolStart = -1;
                    hasDot = false;
                }
            }

            if (symbolStart >= 0 && hasDot && symbolEnd >= symbolStart)
            {
                AppendEscaped(sb, body, 0, symbolStart);
                sb.Append("<b>");
                AppendEscaped(sb, body, symbolStart, symbolEnd + 1);
                sb.Append("</b>");
                AppendEscaped(sb, body, symbolEnd + 1, body.Length);
            }
            else
            {
                AppendEscaped(sb, body);
            }
        }

        static void AppendEscaped(StringBuilder sb, string text)
            => AppendEscaped(sb, text, 0, text.Length);

        static void AppendEscaped(StringBuilder sb, string text, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                var c = text[i];
                if (c == '<')
                    sb.Append('\u003c');
                else if (c == '>')
                    sb.Append('\u003e');
                else
                    sb.Append(c);
            }
        }

        /// <summary>
        /// Returns content whose text is shortened with an ellipsis so it fits
        /// <paramref name="maxWidth"/> using <paramref name="style"/> metrics. Preserves
        /// <paramref name="tooltip"/>. Uses binary search and memoises results keyed by
        /// (text, tooltip, style, rounded width).
        /// </summary>
        public static GUIContent TrimToWidth(string text, string tooltip, GUIStyle style, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 8f || style == null)
            {
                return string.IsNullOrEmpty(tooltip) ? new GUIContent(text ?? string.Empty) : new GUIContent(text ?? string.Empty, tooltip);
            }

            // Round width to integer pixels so tiny float jitter doesn't thrash the cache.
            var widthBucket = Mathf.RoundToInt(maxWidth);
            var key = new TrimKey(text, tooltip ?? string.Empty, style, widthBucket);
            if (s_TrimToWidth.TryGetValue(key, out var cached))
            {
                return cached;
            }

            // Fast path: full text already fits.
            s_TrimProbe.text = text;
            s_TrimProbe.tooltip = null;
            style.CalcMinMaxWidth(s_TrimProbe, out _, out var fullWidth);
            if (fullWidth <= maxWidth)
            {
                var asIs = string.IsNullOrEmpty(tooltip) ? new GUIContent(text) : new GUIContent(text, tooltip);
                s_TrimToWidth[key] = asIs;
                return asIs;
            }

            // Binary search for the longest prefix length whose "<prefix>…" fits.
            var lo = 1;
            var hi = text.Length - 1;
            var bestLen = 0;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                s_TrimProbe.text = text.Substring(0, mid) + k_Ellipsis;
                style.CalcMinMaxWidth(s_TrimProbe, out _, out var w);
                if (w <= maxWidth)
                {
                    bestLen = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            var truncatedText = bestLen > 0 ? text.Substring(0, bestLen) + k_Ellipsis : k_Ellipsis;
            var result = string.IsNullOrEmpty(tooltip)
                ? new GUIContent(truncatedText)
                : new GUIContent(truncatedText, tooltip);
            s_TrimToWidth[key] = result;
            return result;
        }

        /// <summary>Clears all cached content. Intended for tests / explicit reset.</summary>
        public static void Clear()
        {
            s_Descriptor.Clear();
            s_DescriptorRich.Clear();
            s_TrimToWidth.Clear();
        }

        readonly struct TrimKey : System.IEquatable<TrimKey>
        {
            readonly string m_Text;
            readonly string m_Tooltip;
            readonly GUIStyle m_Style;
            readonly int m_WidthBucket;

            public TrimKey(string text, string tooltip, GUIStyle style, int widthBucket)
            {
                m_Text = text;
                m_Tooltip = tooltip;
                m_Style = style;
                m_WidthBucket = widthBucket;
            }

            public bool Equals(TrimKey other)
            {
                return m_WidthBucket == other.m_WidthBucket
                    && ReferenceEquals(m_Style, other.m_Style)
                    && string.Equals(m_Text, other.m_Text, System.StringComparison.Ordinal)
                    && string.Equals(m_Tooltip, other.m_Tooltip, System.StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TrimKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var h = m_Text?.GetHashCode() ?? 0;
                    h = (h * 397) ^ (m_Tooltip?.GetHashCode() ?? 0);
                    h = (h * 397) ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(m_Style);
                    h = (h * 397) ^ m_WidthBucket;
                    return h;
                }
            }
        }
    }
}
