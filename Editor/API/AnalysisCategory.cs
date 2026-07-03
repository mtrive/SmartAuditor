using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Extension methods for <see cref="AnalysisCategory"/> to support category key mapping.
    /// </summary>
    public static class AnalysisCategoryExtensions
    {
        static readonly Dictionary<AnalysisCategory, string> s_CategoryToKey = new Dictionary<AnalysisCategory, string>
        {
            { AnalysisCategory.AssetIssue, "asset" },
            { AnalysisCategory.AssetImportMessage, "asset-import-message" },
            { AnalysisCategory.Shader, "shader" },
            { AnalysisCategory.ShaderVariant, "shader-variant" },
            { AnalysisCategory.Code, "code" },
            { AnalysisCategory.CodeCompilerMessage, "compiler-message" },
            { AnalysisCategory.ProjectSetting, "project-setting" },
            { AnalysisCategory.BuildFile, "build-file" },
            { AnalysisCategory.Assembly, "assembly" },
            { AnalysisCategory.PrecompiledAssembly, "precompiled-assembly" },
            { AnalysisCategory.ShaderCompilerMessage, "shader-compiler-message" },
            { AnalysisCategory.Package, "package" },
            { AnalysisCategory.Texture, "texture" },
            { AnalysisCategory.AudioClip, "audio" },
            { AnalysisCategory.ComputeShaderVariant, "compute-shader-variant" },
            { AnalysisCategory.Mesh, "mesh" },
            { AnalysisCategory.SpriteAtlas, "sprite-atlas" },
            { AnalysisCategory.Sprite, "sprite" },
            { AnalysisCategory.Material, "material" },
            { AnalysisCategory.AnimatorController, "animator-controller" },
            { AnalysisCategory.AnimationClip, "animation-clip" },
            { AnalysisCategory.Avatar, "avatar" },
            { AnalysisCategory.AvatarMask, "avatar-mask" },
            { AnalysisCategory.Prefab, "prefab" },
            { AnalysisCategory.GameObjectIssue, "game-object-issue" },
            { AnalysisCategory.Scene, "scene" },
            { AnalysisCategory.SceneSettings, "scene-settings" }
        };

        /// <summary>
        /// Converts an <see cref="AnalysisCategory"/> to its stable category key.
        /// </summary>
        /// <param name="category">The category to convert.</param>
        /// <returns>The stable category key string.</returns>
        public static string ToKey(this AnalysisCategory category)
        {
            return s_CategoryToKey.TryGetValue(category, out var key) ? key : category.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Returns true for toolchain diagnostics that are exported as top-level report messages.
        /// </summary>
        public static bool IsMessageCategory(this AnalysisCategory category)
        {
            return category == AnalysisCategory.CodeCompilerMessage ||
                   category == AnalysisCategory.ShaderCompilerMessage ||
                   category == AnalysisCategory.AssetImportMessage;
        }
    }

    /// <summary>
    /// AnalysisCategory drives views, filters, and export organization.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AnalysisCategory
    {
        /// <summary>
        /// Code Issues, discovered by static code analysis
        /// </summary>
        Code,

        /// <summary>
        /// Compiler errors and warnings generated whilst compiling code for static analysis
        /// </summary>
        CodeCompilerMessage,

        /// <summary>
        /// Issues relating to a specific asset file on disk — its import settings or serialized data.
        /// The user's fix opens the Inspector for that asset.
        /// See <c>Documentation~/DiagnosticDrafting.md</c> §1.5 for the four-bucket category model.
        /// </summary>
        AssetIssue,

        /// <summary>
        /// Asset import messages
        /// </summary>
        AssetImportMessage,

        /// <summary>
        /// Issues relating to a specific component on a specific GameObject in a scene or prefab.
        /// The user's fix opens the Inspector for that component.
        /// See <c>Documentation~/DiagnosticDrafting.md</c> §1.5 for the four-bucket category model.
        /// </summary>
        GameObjectIssue,

        /// <summary>
        /// Issues reported by the shader compiler
        /// </summary>
        ShaderCompilerMessage,

        /// <summary>
        /// Issues relating to project-wide settings — Player Settings, Quality Settings, Editor Settings,
        /// Graphics Settings, render-pipeline configuration. The user's fix opens the Project Settings window.
        /// See <c>Documentation~/DiagnosticDrafting.md</c> §1.5 for the four-bucket category model.
        /// </summary>
        ProjectSetting,

        /// <summary>
        /// Category for reporting shaders in the project
        /// </summary>
        Shader,

        /// <summary>
        /// Category for reporting shader variants
        /// </summary>
        ShaderVariant,

        /// <summary>
        /// Category for displaying information about files created during the project build process
        /// </summary>
        BuildFile,

        /// <summary>
        /// Category for information about all of the code assemblies in the project
        /// </summary>
        Assembly,

        /// <summary>
        /// Category for information about precompiled assemblies
        /// </summary>
        PrecompiledAssembly,

        /// <summary>
        /// Category for displaying installed packages
        /// </summary>
        Package,

        /// <summary>
        /// Issues relating to texture assets and texture import settings
        /// </summary>
        Texture,

        /// <summary>
        /// Issues relating to Audio Clip assets and import settings
        /// </summary>
        AudioClip,

        /// <summary>
        /// Category for displaying variants of compute shaders
        /// </summary>
        ComputeShaderVariant,

        /// <summary>
        /// Issues relating to Mesh assets and import settings
        /// </summary>
        Mesh,

        /// <summary>
        /// Issues relating to Sprite Atlas assets and import settings
        /// </summary>
        SpriteAtlas,

        /// <summary>
        /// Issues relating to Sprite textures and import settings
        /// </summary>
        Sprite,

        /// <summary>
        /// Category for showing materials grouped by shader
        /// </summary>
        Material,

        /// <summary>
        /// Issues relating to animator controllers
        /// </summary>
        AnimatorController,

        /// <summary>
        /// Issues relating to animation clips
        /// </summary>
        AnimationClip,

        /// <summary>
        /// Issues relating to avatars
        /// </summary>
        Avatar,

        /// <summary>
        /// Issues relating to avatar masks
        /// </summary>
        AvatarMask,

        /// <summary>
        /// Prefab category
        /// </summary>
        Prefab,

        /// <summary>
        /// Issues relating to a scene's structure — GameObject count, hierarchy depth, asset reference density,
        /// scene-asset dependency graph. The user's fix opens the Hierarchy window or restructures the scene.
        /// See <c>Documentation~/DiagnosticDrafting.md</c> §1.5 for the four-bucket category model.
        /// </summary>
        Scene,

        /// <summary>
        /// Issues relating to a scene's settings — RenderSettings (skybox, fog, ambient, halo), LightmapSettings,
        /// per-scene NavMesh build configuration, reflection-probe coverage. The user's fix opens
        /// Window &gt; Rendering &gt; Lighting (or the relevant scene-level inspector).
        /// See <c>Documentation~/DiagnosticDrafting.md</c> §1.5 for the four-bucket category model.
        /// </summary>
        SceneSettings,

        /// <summary>
        /// Enum value indicating the first available custom category
        /// </summary>
        FirstCustomCategory
    }
}
