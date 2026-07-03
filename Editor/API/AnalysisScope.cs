using System;
using UnityEngine;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Specifies which broad areas of the project to include in an analysis run.
    /// Multiple values can be combined using bitwise OR.
    /// </summary>
    [Flags]
    public enum AnalysisScope
    {
        /// <summary>No areas selected.</summary>
        None = 0,

        /// <summary>Analyze C# scripts and assemblies.</summary>
        Code = 1 << 0,

        /// <summary>Analyze project settings.</summary>
        ProjectSettings = 1 << 1,

        /// <summary>
        /// Analyze imported assets—textures, sprites, meshes, audio, animation, and generic asset issues.
        /// Does not include shaders or materials; use <see cref="Shaders"/> for those.
        /// </summary>
        [InspectorName("Import Assets (excl. Shaders)")]
        Assets = 1 << 2,

        /// <summary>Analyze prefabs, scenes, and scene-level settings (RenderSettings, lighting, etc.).</summary>
        [InspectorName("Scenes & Prefabs")]
        Prefabs = 1 << 3,

        /// <summary>Analyze shaders, shader variants, materials, and shader compiler messages.</summary>
        [InspectorName("Shaders & Materials")]
        Shaders = 1 << 4,

        /// <summary>Analyze build output.</summary>
        Build = 1 << 6,

        /// <summary>All areas selected.</summary>
        All = ~None
    }
}
