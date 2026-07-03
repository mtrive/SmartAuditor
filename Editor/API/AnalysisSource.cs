using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Specifies the source or trigger point for analysis, which determines which modules are included.
    /// Modes are mutually exclusive. If null, all modules matching the specified categories are included.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AnalysisSource
    {
        /// <summary>
        /// Asset analysis - analyzing assets from Project window. Excludes modules that only analyze current scene.
        /// </summary>
        Assets,

        /// <summary>
        /// Current scene analysis - analyzing the currently loaded scene from Hierarchy window. Excludes modules that only analyze asset files.
        /// </summary>
        LoadedScene
    }
}

