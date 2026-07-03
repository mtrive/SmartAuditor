// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// The shape every report entry exposes regardless of kind — diagnostics
    /// (<see cref="ReportItem"/>), toolchain messages (<c>Message</c>), and (in time)
    /// insight rows. Read paths that don't care about the concrete kind — the affected-items
    /// list, code-snippet drawing, text filtering, export — consume this; kind-specific
    /// surfaces (descriptor evidence, insight schema columns) downcast to the concrete type.
    /// </summary>
    public interface IReportEntry
    {
        /// <summary>The entry's analysis category.</summary>
        AnalysisCategory Category { get; }

        /// <summary>Human-readable description (the diagnostic message, compiler text, or row label).</summary>
        string Description { get; }

        /// <summary>Source location this entry refers to, if any.</summary>
        Location Location { get; }

        /// <summary>Console-style level (Error/Warning/Info). Universal across all entry kinds.</summary>
        LogLevel LogLevel { get; }

        /// <summary>Stable identity for diffing across runs, or null when the entry has no stable identity.</summary>
        string Fingerprint { get; }

        /// <summary>Custom properties keyed by canonical name. Empty (never null) when there are none.</summary>
        IReadOnlyDictionary<string, string> Properties { get; }
    }
}
