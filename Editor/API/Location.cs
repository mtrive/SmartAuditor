// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Represents the location of an asset or reported issue.
    /// </summary>
    [Serializable]
    public sealed class Location
    {
        const int k_NoLine = -1;
        const int k_NoColumn = -1;

        [SerializeField]
        int m_Line = k_NoLine;
        [SerializeField]
        int m_EndLine = k_NoLine;
        [SerializeField]
        int m_Column = k_NoColumn;
        [SerializeField]
        int m_EndColumn = k_NoColumn;
        [SerializeField]
        string m_Path;

        /// <summary>
        /// File extension (without leading dot)
        /// </summary>
        [JsonIgnore]
        public string Extension
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                    return string.Empty;
                var ext = System.IO.Path.GetExtension(Path) ?? string.Empty;
                return ext.StartsWith(".") ? ext.Substring(1) : ext;
            }
        }

        /// <summary>
        /// Filename
        /// </summary>
        [JsonIgnore]
        public string Filename => string.IsNullOrEmpty(Path) ? string.Empty : System.IO.Path.GetFileName(Path);

        /// <summary>
        /// Checks whether the location is valid
        /// </summary>
        /// <value>True if the location is valid</value>
        [JsonIgnore]
        public bool IsValid => !string.IsNullOrEmpty(Path);

        /// <summary>
        /// Start line number, or null when not applicable (e.g. for file-level or asset locations).
        /// </summary>
        [JsonProperty("line", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Line => m_Line >= 0 ? m_Line : null;

        /// <summary>
        /// End line number for multi-line locations, or null when the location spans a single line.
        /// When set, must be greater than or equal to <see cref="Line"/>.
        /// </summary>
        [JsonProperty("endLine", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? EndLine => m_EndLine >= 0 ? m_EndLine : null;

        /// <summary>
        /// Start column number, or null when not applicable.
        /// </summary>
        [JsonProperty("column", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Column => m_Column >= 0 ? m_Column : null;

        /// <summary>
        /// End column number, or null when not applicable.
        /// </summary>
        [JsonProperty("endColumn", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? EndColumn => m_EndColumn >= 0 ? m_EndColumn : null;

        /// <summary>
        /// Path relative to the Unity project
        /// </summary>
        [JsonIgnore]
        public string Path => m_Path ?? string.Empty;

        [JsonProperty("path")]
        internal string PathForJson
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                    return null;
                return Path.Replace(EditorApplication.applicationContentsPath,
                    "PROJECT_PATH/Data");
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    m_Path = string.Empty;
                else
                    m_Path = value.Replace("PROJECT_PATH/Data", EditorApplication.applicationContentsPath);
            }
        }

        /// <summary>
        /// Constructor for a location with optional line number or line range.
        /// </summary>
        /// <param name="path">File path</param>
        /// <param name="line">Optional start line number, or null when not applicable</param>
        /// <param name="endLine">Optional end line number for multi-line locations. Must be greater than or equal to <paramref name="line"/> when both are set.</param>
        /// <param name="column">Optional start column number.</param>
        /// <param name="endColumn">Optional end column number.</param>
        [JsonConstructor]
        public Location(string path, int? line = null, int? endLine = null, int? column = null, int? endColumn = null)
        {
            // Normalize null coming from JSON deserialization (NullValueHandling.Ignore can omit
            // "path") so that Path always stays non-null at the domain level.
            path ??= string.Empty;
            if (line.HasValue && endLine.HasValue && endLine.Value < line.Value)
                throw new ArgumentException($"endLine ({endLine}) must be greater than or equal to line ({line}).", nameof(endLine));
            if (column.HasValue && !line.HasValue)
                throw new ArgumentException("column cannot be set when line is null.", nameof(column));
            if (column.HasValue && column.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(column), "column must be greater than or equal to zero.");
            if (endColumn.HasValue && endColumn.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(endColumn), "endColumn must be greater than or equal to zero.");
            if (!column.HasValue && endColumn.HasValue)
                throw new ArgumentException("endColumn cannot be set when column is null.", nameof(endColumn));
            if (column.HasValue && endColumn.HasValue && endLine.GetValueOrDefault(line.GetValueOrDefault()) <= line.GetValueOrDefault() && endColumn.Value < column.Value)
                throw new ArgumentException($"endColumn ({endColumn}) must be greater than or equal to column ({column}) when location ends on the same line.", nameof(endColumn));

            m_Path = path.Replace($"{SmartAuditor.ProjectPath}/", string.Empty);
            m_Line = line ?? k_NoLine;
            m_EndLine = endLine ?? k_NoLine;
            m_Column = column ?? k_NoColumn;
            m_EndColumn = endColumn ?? k_NoColumn;
        }
    }
}
