using System;
using Newtonsoft.Json;
using SmartAuditor.Editor.Core.Serialization;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Which area(s) of a project may be affected by a diagnostic. Each flag represents a distinct
    /// resource or project aspect that degrades when the issue is present. Multiple flags may be
    /// combined when an issue affects more than one area simultaneously.
    /// </summary>
    [Flags]
    [JsonConverter(typeof(ImpactJsonConverter))]
    public enum Impact
    {
        /// <summary>
        /// Indicates some error with the Descriptor data: a descriptor should never match no areas.
        /// </summary>
        None = 0,

        /// <summary>
        /// Binary or data size of the shipped build.
        /// </summary>
        BuildSize = 1 << 0,

        /// <summary>
        /// Time required to produce the final build, including compilation and asset processing.
        /// </summary>
        BuildTime = 1 << 1,

        /// <summary>
        /// Issues which slow down the Editor workflow, including domain reloads, play mode entry,
        /// and asset import times.
        /// </summary>
        EditorWorkflow = 1 << 2,

        /// <summary>
        /// Load time of scenes, assets, or the application at startup.
        /// </summary>
        LoadTime = 1 << 3,

        /// <summary>
        /// Memory consumption at runtime, including managed heap, native allocations, and GPU memory.
        /// </summary>
        Memory = 1 << 4,

        /// <summary>
        /// Runtime performance, including CPU and GPU frame time.
        /// </summary>
        Performance = 1 << 5,

        /// <summary>
        /// Issues that must be fixed to comply with platform certification requirements and store
        /// submission guidelines (e.g., App Store Review, Nintendo Lotcheck, Sony TRC).
        /// </summary>
        PlatformCompliance = 1 << 6,

        /// <summary>
        /// Compatibility issues, such as using APIs or features that are not supported on a
        /// specific target platform and may fail at runtime.
        /// </summary>
        Compatibility = 1 << 7,

        /// <summary>
        /// Correctness issues such as missing references, broken prefabs, or invalid configuration.
        /// </summary>
        Correctness = 1 << 8,

        /// <summary>
        /// Quality of the final product, including visual, audio, and runtime characteristics.
        /// </summary>
        Quality = 1 << 9,

        // Add new items in alphabetical order and adjust the values (including "All") accordingly.
        // Impact flags are serialized as strings, so renaming a flag is a breaking change for any
        // previously-saved report.

        /// <summary>
        /// Bitmask value representing all impact areas.
        /// </summary>
        All = (1 << 10) - 1
    }
}
