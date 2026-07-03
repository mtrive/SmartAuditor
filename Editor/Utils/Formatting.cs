using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SmartAuditor.Editor.Utils
{
    internal static class Formatting
    {
        static readonly string[] k_LineSeparators = { "\r\n", "\n", "\r" };

        /// <summary>
        /// Returns <paramref name="text"/> with blank / whitespace-only lines removed.
        /// Compiler and asset-import messages frequently embed blank lines between
        /// context blocks; preserving them inflates the row height in any UI that wraps
        /// the message, without adding signal. Each line is trimmed; surviving lines
        /// are rejoined with <c>\n</c>.
        /// </summary>
        public static string StripEmptyLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? string.Empty;

            var lines = text.Split(k_LineSeparators, StringSplitOptions.None);
            var sb = new System.Text.StringBuilder(text.Length);
            var first = true;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (!first)
                    sb.Append('\n');
                sb.Append(line);
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns the first non-empty line of <paramref name="text"/>, trimmed. Used by
        /// summary views (e.g. the dashboard message rows) to show a single-line preview
        /// of an otherwise multi-line compiler / import message.
        /// </summary>
        public static string GetFirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? string.Empty;

            var lines = text.Split(k_LineSeparators, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    return line.Trim();
            }
            return string.Empty;
        }

        /// <summary>
        /// Truncates <paramref name="text"/> to at most <paramref name="maxLength"/> characters,
        /// appending an ellipsis ("…") when the text is shortened. Use this for dashboard row
        /// previews where long strings would push adjacent controls off screen.
        /// </summary>
        /// <param name="text">Text to truncate.</param>
        /// <param name="maxLength">Maximum number of characters before truncation. Must be positive.</param>
        public static string TruncatePreview(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text ?? string.Empty;

            return text.Substring(0, maxLength) + "\u2026";
        }

        /// <summary>
        /// Formats a date/time as "yyyy/MM/dd HH:mm".
        /// </summary>
        /// <param name="dateTime">The date/time to format.</param>
        public static string FormatDateTime(DateTime dateTime)
        {
            return dateTime.ToString("yyyy/MM/dd HH:mm");
        }

        /// <summary>
        /// Formats a date/time as a UTC timestamp suitable for stable report data.
        /// Returns empty for the default <see cref="DateTime"/> value.
        /// </summary>
        /// <param name="dateTime">The date/time to format.</param>
        public static string FormatUtcTimestamp(DateTime dateTime)
        {
            if (dateTime == default)
                return string.Empty;

            return dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats a duration as "HH:mm:ss".
        /// </summary>
        /// <param name="timeSpan">The duration to format.</param>
        public static string FormatDuration(TimeSpan timeSpan)
        {
            return $"{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
        }

        /// <summary>
        /// Formats a duration adaptively as "X ms" (&lt; 1 s, one decimal), "X s"
        /// (&lt; 60 s, two decimals), or "X min" (otherwise, two decimals). Uses
        /// invariant culture for the numeric portion. Negative values keep their sign.
        /// </summary>
        /// <param name="timeSpan">The duration to format.</param>
        /// <returns>A string representation of the input duration.</returns>
        public static string FormatTime(TimeSpan timeSpan)
        {
            var timeMs = timeSpan.TotalMilliseconds;
            if (timeMs < 1000)
                return string.Format(CultureInfo.InvariantCulture, "{0:F1} ms", timeMs);
            if (timeMs < 60000)
                return string.Format(CultureInfo.InvariantCulture, "{0:F2} s", timeSpan.TotalSeconds);
            return string.Format(CultureInfo.InvariantCulture, "{0:F2} min", timeSpan.TotalMinutes);
        }

        /// <summary>
        /// Formats a duration in seconds as "X ms", "X s", or "X min", using the same
        /// adaptive tiers as <see cref="FormatTime(TimeSpan)"/>.
        /// </summary>
        /// <param name="timeSeconds">The duration to format, in seconds.</param>
        /// <returns>A string representation of the input value.</returns>
        public static string FormatTime(float timeSeconds)
        {
            if (float.IsNaN(timeSeconds))
                return "NaN";
            return FormatTime(TimeSpan.FromSeconds(timeSeconds));
        }

        /// <summary>
        /// Formats a value in the range 0–1 as a percentage (e.g. 0.5 → "50%").
        /// </summary>
        /// <param name="number">Value to format (0–1 range).</param>
        /// <param name="numDecimalPlaces">Number of decimal places.</param>
        public static string FormatPercentage(float number, int numDecimalPlaces = 0)
        {
            var formatString = $"{{0:F{numDecimalPlaces}}}";
            return string.Format(CultureInfo.InvariantCulture.NumberFormat, formatString, (100.0f * number)) + "%";
        }

        /// <summary>
        /// Formats a byte count as a human-readable size (e.g. "1.0 MB").
        /// Uses 1024-based units; safe to call from any thread.
        /// </summary>
        /// <param name="size">Size in bytes.</param>
        public static string FormatSize(ulong size) => FormatUnsignedByteCount(size);

        /// <summary>
        /// Formats a signed byte count as a human-readable size. Negative values are shown as "0 B".
        /// Uses 1024-based units; safe to call from any thread.
        /// </summary>
        /// <param name="byteCount">Size in bytes.</param>
        public static string FormatBytes(long byteCount) =>
            FormatUnsignedByteCount(byteCount < 0 ? 0UL : (ulong)byteCount);

        static string FormatUnsignedByteCount(ulong s)
        {
            const ulong kUnit = 1024UL;
            const ulong mUnit = kUnit * kUnit;
            const ulong gUnit = mUnit * kUnit;

            if (s >= gUnit)
                return string.Format(CultureInfo.InvariantCulture, "{0:F2} GB", s / (double)gUnit);
            if (s >= mUnit)
                return string.Format(CultureInfo.InvariantCulture, "{0:F1} MB", s / (double)mUnit);
            if (s >= kUnit)
                return string.Format(CultureInfo.InvariantCulture, "{0:F1} KB", s / (double)kUnit);
            if (s >= 512UL)
                return string.Format(CultureInfo.InvariantCulture, "{0:F1} KB", s / (double)kUnit);
            return string.Format(CultureInfo.InvariantCulture, "{0} B", s);
        }

        /// <summary>
        /// Formats a frequency as "X Hz" or "X kHz" (e.g. 44100 → "44.1 kHz").
        /// </summary>
        /// <param name="frequency">Frequency in hertz.</param>
        public static string FormatFrequency(int frequency)
        {
            return (frequency < 1000) ? $"{frequency} Hz" : $"{((float)frequency / 1000.0f):G0} kHz";
        }

        /// <summary>
        /// Formats a duration in seconds as "X.XXX s".
        /// </summary>
        /// <param name="length">Duration in seconds.</param>
        public static string FormatLengthInSeconds(float length)
        {
            return length.ToString("F3") + " s";
        }

        /// <summary>
        /// Formats a number with magnitude suffix (K, M, B, T) for values ≥1000.
        /// </summary>
        /// <param name="number">The number to format.</param>
        public static string FormatNumber(ulong number)
        {
            if (number >= 1_000_000_000_000) // Trillions
            {
                return $"{number / 1_000_000_000_000:F1}T"; // Trillions
            }
            else if (number >= 1_000_000_000) // Billions
            {
                return $"{number / 1_000_000_000:F1}B"; // Billions
            }
            else if (number >= 1_000_000) // Millions
            {
                return $"{number / 1_000_000:F1}M"; // Millions
            }
            else if (number >= 1_000) // Thousands
            {
                return $"{number / 1_000:F1}K"; // Thousands
            }
            else
            {
                return number.ToString(); // Less than a thousand
            }
        }

        /// <summary>
        /// Formats a framerate as "X fps".
        /// </summary>
        /// <param name="framerate">Frames per second.</param>
        public static string FormatFramerate(float framerate)
        {
            return framerate + " fps";
        }

        /// <summary>
        /// Appends a line number or line range to the path when <paramref name="line"/> is set (e.g. "path.cs:L42" or "path.cs:L42–L50").
        /// Returns the path unchanged when <paramref name="line"/> is null or -1.
        /// </summary>
        /// <param name="path">File path to format.</param>
        /// <param name="line">Start line, or null or -1 to omit line info.</param>
        /// <param name="endLine">End line for a multi-line range; null, -1, or equal to <paramref name="line"/> formats as a single line.</param>
        public static string FormatPath(string path, int? line, int? endLine = null)
        {
            if (line is null or -1)
                return path;

            var lineStr = line.Value.ToString();
            if (endLine.HasValue && endLine.Value >= 0 && endLine.Value != line.Value)
                return $"{path}:L{lineStr}–L{endLine.Value}";
            return $"{path}:L{lineStr}";
        }

        static readonly string k_StringSeparator = ", ";

        /// <summary>
        /// Joins strings with the given separator, or ", " if null.
        /// </summary>
        /// <param name="strings">Strings to join.</param>
        /// <param name="separator">Separator, or null for default.</param>
        public static string CombineStrings(string[] strings, string separator = null)
        {
            return string.Join(separator ?? k_StringSeparator, strings);
        }

        /// <summary>
        /// Splits a combined string by the given separator, or ", " if null.
        /// </summary>
        /// <param name="combinedString">String to split.</param>
        /// <param name="separator">Separator, or null for default.</param>
        public static string[] SplitStrings(string combinedString, string separator = null)
        {
            return combinedString.Split(new[] {separator ?? k_StringSeparator}, StringSplitOptions.None);
        }

        /// <summary>
        /// Replaces the default string separator (", ") with the given separator.
        /// </summary>
        /// <param name="combinedString">String containing ", " separators.</param>
        /// <param name="separator">Replacement separator.</param>
        public static string ReplaceStringSeparators(string combinedString, string separator)
        {
            return combinedString.Replace(k_StringSeparator, separator);
        }

        /// <summary>
        /// Decodes the small set of HTML entities used in descriptor strings so they
        /// render correctly in Unity's IMGUI rich-text engine, which processes
        /// <c>&lt;b&gt;</c> tags but does not decode HTML character references.
        /// Handles <c>&amp;gt;</c>, <c>&amp;lt;</c>, and <c>&amp;amp;</c>.
        /// </summary>
        /// <param name="text">Text that may contain HTML character entities.</param>
        public static string DecodeHtmlEntities(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // &amp; must be decoded last to avoid turning &amp;gt; into > instead of &gt;
            text = text.Replace("&gt;", ">").Replace("&lt;", "<").Replace("&amp;", "&");
            return text;
        }

        /// <summary>
        /// Removes rich text tags (b, i, u, color) from the given text.
        /// </summary>
        /// <param name="text">Text that may contain rich text markup.</param>
        public static string StripRichTextTags(string text)
        {
            text = RemoveRichTextTag(text, "b", string.Empty);
            text = RemoveRichTextTag(text, "i", string.Empty);
            text = RemoveRichTextTag(text, "u", string.Empty);
            text = RemoveRichTextTag(text, "color", string.Empty);

            return text;
        }

        static string RemoveRichTextTag(string input, string tagName, string replaceWith)
        {
            return GetRichTextTagRegex(tagName).Replace(input, replaceWith);
        }

        static readonly Dictionary<string, Regex> s_RichTextTagRegexCache = new Dictionary<string, Regex>();

        static Regex GetRichTextTagRegex(string tagName)
        {
            if (!s_RichTextTagRegexCache.TryGetValue(tagName, out var regex))
            {
                regex = new Regex(string.Format("</?{0}[^<]*?>", tagName), RegexOptions.IgnoreCase | RegexOptions.Compiled);
                s_RichTextTagRegexCache[tagName] = regex;
            }
            return regex;
        }

        /// <summary>
        /// Formats a width and height as "WxH". The string shape is contractual with
        /// <see cref="ParseResolutionPixels"/> and <see cref="CompareResolution"/>;
        /// don't change the separator or component order without updating both.
        /// </summary>
        /// <param name="width">Resolution width in pixels.</param>
        /// <param name="height">Resolution height in pixels.</param>
        public static string FormatResolution(int width, int height)
        {
            return $"{width}x{height}";
        }

        /// <summary>
        /// Parses a "WxH" resolution string into a total pixel count. Returns 0 when
        /// the input doesn't match the shape produced by <see cref="FormatResolution"/>.
        /// </summary>
        /// <param name="resolution">A "WxH" string.</param>
        public static long ParseResolutionPixels(string resolution)
        {
            var parts = resolution.Split('x');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var width) &&
                int.TryParse(parts[1], out var height))
            {
                return (long)width * height;
            }
            return 0;
        }

        /// <summary>
        /// Compares two "WxH" resolution strings by total pixel count.
        /// </summary>
        /// <param name="a">Left-hand resolution string.</param>
        /// <param name="b">Right-hand resolution string.</param>
        public static int CompareResolution(string a, string b)
        {
            return ParseResolutionPixels(a).CompareTo(ParseResolutionPixels(b));
        }
    }
}
