using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation
{
    /// <summary>
    /// Options for the compilation mode Smart Auditor should use when performing code analysis.
    /// </summary>
    /// <remarks>
    /// Setting the compilation mode can affect how long analysis takes, which assemblies are analysed and whether certain scripting define symbols are considered.
    /// </remarks>
    [JsonConverter(typeof(StringEnumConverter))]
    enum CompilationMode
    {
        /// <summary>
        ///   <para>Non-Development player (default)</para>
        /// </summary>
        /// <remarks>
        /// Code will be compiled for analysis as it would be when making a non-development Player build for the specified target platform. Code inside #if DEVELOPMENT_BUILD will be excluded from this analysis.
        /// </remarks>
        Player,

        /// <summary>
        ///   <para>Editor assemblies for Play Mode</para>
        /// </summary>
        /// <remarks>
        /// Analysis will be performed on the assemblies which are used in Play Mode. Because these assemblies are cached by the Editor, Smart Auditor skips the compilation step which speeds up analysis.
        /// The analyzed code may not be completely representative of the code that would appear in a Player build, but may be a reasonable approximation for many purposes.
        /// </remarks>
        EditorPlayMode,

        /// <summary>
        ///   <para>Editor assemblies</para>
        /// </summary>
        /// <remarks>
        /// Analysis will be performed only on Editor code assemblies. Select this option to analyze custom Editor code, including packages.
        /// </remarks>
        Editor
    }
}
