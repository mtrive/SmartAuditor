using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Utils
{
    /// <summary>
    /// Utility class for working with shader compiler platforms.
    /// </summary>
    internal static class ShaderCompilerPlatformUtil
    {
        /// <summary>
        /// Gets the shader compiler platforms that are relevant for a given build target,
        /// based on the graphics APIs configured in Player Settings.
        /// </summary>
        /// <param name="buildTarget">The build target to get shader compiler platforms for.</param>
        /// <returns>An array of shader compiler platforms for the build target.</returns>
        public static ShaderCompilerPlatform[] GetShaderCompilerPlatformsForBuildTarget(BuildTarget buildTarget)
        {
            // If no target is specified, return all available platforms
            if (buildTarget == BuildTarget.NoTarget)
            {
                return GetAllAvailableShaderCompilerPlatforms();
            }

            var graphicsAPIs = PlayerSettings.GetGraphicsAPIs(buildTarget);
            var platforms = new List<ShaderCompilerPlatform>();

            foreach (var api in graphicsAPIs)
            {
                var platform = MapGraphicsDeviceTypeToShaderCompilerPlatform(api);
                if (platform != ShaderCompilerPlatform.None && !platforms.Contains(platform))
                {
                    platforms.Add(platform);
                }
            }

            // Fallback: if no platforms were mapped, return all available platforms
            if (platforms.Count == 0)
            {
                return GetAllAvailableShaderCompilerPlatforms();
            }

            return platforms.ToArray();
        }

        /// <summary>
        /// Gets all shader compiler platforms available on the current machine.
        /// </summary>
        /// <returns>An array of all available shader compiler platforms.</returns>
        static ShaderCompilerPlatform[] GetAllAvailableShaderCompilerPlatforms()
        {
            return ShaderUtilProxy.GetAvailableCompilerPlatforms();
        }

        /// <summary>
        /// Maps a GraphicsDeviceType to the corresponding ShaderCompilerPlatform.
        /// </summary>
        /// <param name="graphicsDeviceType">The graphics device type.</param>
        /// <returns>The corresponding shader compiler platform, or None if not mapped.</returns>
        static ShaderCompilerPlatform MapGraphicsDeviceTypeToShaderCompilerPlatform(GraphicsDeviceType graphicsDeviceType)
        {
            switch (graphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    return ShaderCompilerPlatform.D3D;

                case GraphicsDeviceType.Direct3D12:
                    return ShaderCompilerPlatform.D3D;

                case GraphicsDeviceType.Metal:
                    return ShaderCompilerPlatform.Metal;

                case GraphicsDeviceType.Vulkan:
                    return ShaderCompilerPlatform.Vulkan;

                case GraphicsDeviceType.OpenGLCore:
                    return ShaderCompilerPlatform.OpenGLCore;

                case GraphicsDeviceType.OpenGLES3:
                    return ShaderCompilerPlatform.GLES3x;

#if !UNITY_2023_1_OR_NEWER
                case GraphicsDeviceType.OpenGLES2:
                    return ShaderCompilerPlatform.GLES20;
#endif

#if UNITY_2023_2_OR_NEWER
                case GraphicsDeviceType.WebGPU:
                    return ShaderCompilerPlatform.WebGPU;
#endif

                // PlayStation platforms
#if UNITY_PS4
                case GraphicsDeviceType.PlayStation4:
                    return ShaderCompilerPlatform.PS4;
#endif

#if UNITY_PS5
                case GraphicsDeviceType.PlayStation5:
                    return ShaderCompilerPlatform.PS5;
#endif

                // Xbox platforms
#if UNITY_GAMECORE_XBOXONE
                case GraphicsDeviceType.GameCoreXboxOne:
                    return ShaderCompilerPlatform.D3D;
#endif

#if UNITY_GAMECORE_XBOXSERIES
                case GraphicsDeviceType.GameCoreXboxSeries:
                    return ShaderCompilerPlatform.D3D;
#endif

                // Nintendo Switch
#if UNITY_SWITCH
                case GraphicsDeviceType.Switch:
                    return ShaderCompilerPlatform.Switch;
#endif

                default:
                    return ShaderCompilerPlatform.None;
            }
        }
    }
}
