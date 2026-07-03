using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SmartAuditor.Editor.Utils
{
    internal static class PlayerSettingsUtil
    {
        public static int GetArchitecture(BuildTargetGroup buildTargetGroup)
        {
#if UNITY_2021_2_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            return PlayerSettings.GetArchitecture(namedBuildTarget);
#else
            return PlayerSettings.GetArchitecture(buildTargetGroup);
#endif
        }

        public static Il2CppCompilerConfiguration GetIl2CppCompilerConfiguration(BuildTargetGroup buildTargetGroup)
        {
#if UNITY_2021_2_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            return PlayerSettings.GetIl2CppCompilerConfiguration(namedBuildTarget);
#else
            return PlayerSettings.GetIl2CppCompilerConfiguration(buildTargetGroup);
#endif
        }

        public static void SetIl2CppCompilerConfiguration(BuildTargetGroup buildTargetGroup, Il2CppCompilerConfiguration configuration)
        {
#if UNITY_2021_2_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            PlayerSettings.SetIl2CppCompilerConfiguration(namedBuildTarget, configuration);
#else
            PlayerSettings.SetIl2CppCompilerConfiguration(buildTargetGroup, configuration);
#endif
        }

        public static ScriptingImplementation GetScriptingBackend(BuildTargetGroup buildTargetGroup)
        {
#if UNITY_2021_2_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            return PlayerSettings.GetScriptingBackend(namedBuildTarget);
#else
            return PlayerSettings.GetScriptingBackend(buildTargetGroup);
#endif
        }

        public static void SetScriptingBackend(
            BuildTargetGroup buildTargetGroup,
            ScriptingImplementation backend)
        {
#if UNITY_2021_2_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            PlayerSettings.SetScriptingBackend(namedBuildTarget, backend);
#else
            PlayerSettings.SetScriptingBackend(buildTargetGroup, backend);
#endif
        }

        public static ManagedStrippingLevel GetManagedStrippingLevel(BuildTargetGroup buildTargetGroup)
        {
#if UNITY_2021_2_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            return PlayerSettings.GetManagedStrippingLevel(namedBuildTarget);
#else
            return PlayerSettings.GetManagedStrippingLevel(buildTargetGroup);
#endif
        }

        static readonly MethodInfo s_GetLightmapStreamingEnabled = typeof(PlayerSettings).GetMethod(
            "GetLightmapStreamingEnabledForPlatformGroup",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(BuildTargetGroup) },
            null);

        static readonly MethodInfo s_SetLightmapStreamingEnabled = typeof(PlayerSettings).GetMethod(
            "SetLightmapStreamingEnabledForPlatformGroup",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(BuildTargetGroup), typeof(bool) },
            null);

        public static bool IsLightmapStreamingSupported =>
            s_GetLightmapStreamingEnabled != null && s_SetLightmapStreamingEnabled != null;

        public static bool IsLightmapStreamingEnabled(BuildTargetGroup buildTargetGroup)
        {
            if (s_GetLightmapStreamingEnabled == null)
                return true;

            return (bool)s_GetLightmapStreamingEnabled.Invoke(null, new object[] { buildTargetGroup });
        }

        public static void SetLightmapStreaming(BuildTargetGroup buildTargetGroup, bool value)
        {
            if (s_SetLightmapStreamingEnabled == null)
                throw new NotSupportedException("Setting Lightmap Streaming per platform is not supported");

            s_SetLightmapStreamingEnabled.Invoke(null, new object[] { buildTargetGroup, value });
        }
    }
}
