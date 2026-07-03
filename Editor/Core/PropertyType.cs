// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Built-in property kinds on <see cref="ReportItem"/> and <see cref="Message"/> entries.
    /// Custom per-category values live in the string-keyed <see cref="ReportItem.Properties"/> dictionary.
    /// </summary>
    internal enum PropertyType
    {
        Id = 0,
        Description,
        Descriptor,
        Severity,
        LogLevel,
        Impact,
        Path,
        Directory,
        Filename,
        FileType,
        Platform,
    }
}
