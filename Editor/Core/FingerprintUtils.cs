// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Low-level helpers for computing stable SHA-256 fingerprints from string parts.
    /// </summary>
    internal static class FingerprintUtils
    {
        /// <summary>
        /// Collapses internal whitespace runs to a single space and trims leading/trailing
        /// whitespace. Returns <see cref="string.Empty"/> for null or empty input.
        /// </summary>
        internal static string NormalizeFingerprintPart(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var trimmed = value.Trim();
            var builder = new StringBuilder(trimmed.Length);
            var previousWasWhitespace = false;

            foreach (var c in trimmed)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (previousWasWhitespace)
                        continue;

                    builder.Append(' ');
                    previousWasWhitespace = true;
                    continue;
                }

                builder.Append(c);
                previousWasWhitespace = false;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns the lowercase hexadecimal representation of <paramref name="bytes"/>
        /// using invariant culture formatting.
        /// </summary>
        internal static string ToHexLower(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>
        /// Joins <paramref name="parts"/> with a newline separator, computes a SHA-256
        /// hash over the UTF-8 encoded result, and returns <c>"sha256:{hex}"</c>.
        /// </summary>
        internal static string ComputeSha256Fingerprint(IEnumerable<string> parts)
        {
            var normalized = string.Join("\n", parts);
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                return "sha256:" + ToHexLower(bytes);
            }
        }
    }
}
