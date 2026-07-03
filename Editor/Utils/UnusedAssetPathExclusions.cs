// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;

namespace SmartAuditor.Editor.Utils
{
    /// <summary>
    /// Path heuristics for the possibly-unused-asset check: paths that should never be reported
    /// even when they are not linked from build scenes, Resources, or Addressables tracing.
    /// </summary>
    internal static class UnusedAssetPathExclusions
    {
        /// <summary>
        /// Returns false when this path should never be reported as possibly unused.
        /// </summary>
        internal static bool ShouldReportUnusedCandidate(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            var lower = assetPath.ToLowerInvariant();

            // Package paths (Packages/...) and other non-Assets paths are excluded here.
            if (!lower.StartsWith("assets/", StringComparison.Ordinal))
                return false;

            if (lower.Contains("/streamingassets/", StringComparison.Ordinal))
                return false;

            if (lower.Contains("/gizmos/", StringComparison.Ordinal))
                return false;

            if (lower.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
    }
}
