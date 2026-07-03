using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Specifies which code context should be analyzed during code analysis.
    /// </summary>
    /// <remarks>
    /// This determines whether to analyze Editor assemblies, Runtime assemblies, or both.
    /// This is separate from assembly filtering (which specific assemblies to analyze) and
    /// compilation settings (how to compile the code).
    /// </remarks>
    [Flags]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CodeContext
    {
        /// <summary>
        /// No code context specified.
        /// </summary>
        None = 0,

        /// <summary>
        /// Analyze Editor assemblies. This includes assemblies that are compiled for the Editor.
        /// </summary>
        Editor = 1 << 0,

        /// <summary>
        /// Analyze Runtime assemblies. This includes assemblies that would be included in a Player build.
        /// </summary>
        Runtime = 1 << 1,

        /// <summary>
        /// Analyze both Editor and Runtime assemblies.
        /// </summary>
        All = Editor | Runtime
    }
}

