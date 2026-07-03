// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Shared metadata for the <see cref="Impact"/> flags enum used by UI and tooling.
    /// </summary>
    public static class ImpactCatalog
    {
        /// <summary>
        /// Individual <see cref="Impact"/> flags in alphabetical order, excluding
        /// <see cref="Impact.None"/> and the composite <see cref="Impact.All"/> value.
        /// </summary>
        /// <remarks>
        /// Keep this list in sync with <see cref="Impact"/> when adding new flags.
        /// </remarks>
        public static IReadOnlyList<Impact> IndividualFlags { get; } = new[]
        {
            Impact.BuildSize,
            Impact.BuildTime,
            Impact.EditorWorkflow,
            Impact.LoadTime,
            Impact.Memory,
            Impact.Performance,
            Impact.PlatformCompliance,
            Impact.Compatibility,
            Impact.Correctness,
            Impact.Quality
        };
    }
}
