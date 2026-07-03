// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Modules
{
    /// <summary>
    /// Column slug constants for the <see cref="AnalysisCategory.Texture"/> insight table.
    /// These strings are the keys used in <see cref="InsightRow.Values"/> and the JSON
    /// payload; treat as a contract.
    /// </summary>
    internal static class TextureColumns
    {
        public const string Shape = "Shape";
        public const string ImporterType = "ImporterType";
        public const string Format = "Format";
        public const string Compression = "Compression";
        public const string MipmapEnabled = "MipmapEnabled";
        public const string Readable = "Readable";
        public const string Resolution = "Resolution";
        public const string Size = "Size";
        public const string StreamingMipmap = "StreamingMipmap";
        public const string sRGB = "sRGB";
        public const string Filtering = "Filtering";
    }
}
