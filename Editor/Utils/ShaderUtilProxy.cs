using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Utils
{
    internal static class ShaderUtilProxy
    {
        static readonly MethodInfo s_MethodGetAvailableShaderCompilerPlatforms;
        static readonly MethodInfo s_MethodGetShaderVariantCount;
        static readonly MethodInfo s_MethodGetShaderGlobalKeywords;
        static readonly MethodInfo s_MethodGetShaderLocalKeywords;
        static readonly MethodInfo s_MethodGetShaderActiveSubshaderIndex;
        static readonly MethodInfo s_MethodGetSRPBatcherCompatibilityCode;
        static readonly MethodInfo s_MethodHasInstancing;

        static readonly ShaderCompilerPlatform[] s_ShaderCompilerPlatforms;
        static readonly string[] s_ShaderPlatformNames;

        static ShaderUtilProxy()
        {
            var shaderUtilType = typeof(ShaderUtil);
            s_MethodGetAvailableShaderCompilerPlatforms = GetStaticMethod(shaderUtilType, "GetAvailableShaderCompilerPlatforms");
            s_MethodGetShaderActiveSubshaderIndex = GetStaticMethod(shaderUtilType, "GetShaderActiveSubshaderIndex", typeof(Shader));
            s_MethodGetShaderGlobalKeywords = GetStaticMethod(shaderUtilType, "GetShaderGlobalKeywords", typeof(Shader));
            s_MethodGetShaderLocalKeywords = GetStaticMethod(shaderUtilType, "GetShaderLocalKeywords", typeof(Shader));
            s_MethodGetShaderVariantCount = GetStaticMethod(shaderUtilType, "GetVariantCount", typeof(Shader), typeof(bool));
            s_MethodGetSRPBatcherCompatibilityCode = GetStaticMethod(shaderUtilType, "GetSRPBatcherCompatibilityCode", typeof(Shader), typeof(int));
            s_MethodHasInstancing = GetStaticMethod(shaderUtilType, "HasInstancing", typeof(Shader));

            s_ShaderCompilerPlatforms = BuildCompilerPlatforms();
            s_ShaderPlatformNames = ToPlatformNames(s_ShaderCompilerPlatforms);
        }

        static MethodInfo GetStaticMethod(Type type, string name, params Type[] parameterTypes)
        {
            return type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic, null, parameterTypes, null);
        }

        // Bit index matches ShaderCompilerPlatform enum value.
        static ShaderCompilerPlatform[] BuildCompilerPlatforms()
        {
            if (s_MethodGetAvailableShaderCompilerPlatforms == null)
                return Array.Empty<ShaderCompilerPlatform>();

            var platformMask = (int)s_MethodGetAvailableShaderCompilerPlatforms.Invoke(null, null);
            var platforms = new List<ShaderCompilerPlatform>();
            for (var i = 0; i < 32; ++i)
            {
                if ((platformMask & (1 << i)) == 0)
                    continue;

                var platform = (ShaderCompilerPlatform)i;
                if (platform == ShaderCompilerPlatform.None || !Enum.IsDefined(typeof(ShaderCompilerPlatform), i))
                    continue;

                platforms.Add(platform);
            }

            return platforms.ToArray();
        }

        static string[] ToPlatformNames(ShaderCompilerPlatform[] platforms)
        {
            var names = new string[platforms.Length];
            for (var i = 0; i < platforms.Length; ++i)
                names[i] = platforms[i].ToString();

            return names;
        }

        internal static ShaderCompilerPlatform[] GetAvailableCompilerPlatforms() => s_ShaderCompilerPlatforms;

        internal static string[] GetCompilerPlatformNames() => s_ShaderPlatformNames;

        internal static int? GetShaderActiveSubshaderIndex(Shader shader)
        {
            if (shader == null || s_MethodGetShaderActiveSubshaderIndex == null)
                return null;

            return (int)s_MethodGetShaderActiveSubshaderIndex.Invoke(null, new object[] { shader });
        }

        internal static string[] GetShaderGlobalKeywords(Shader shader)
        {
            if (shader == null || s_MethodGetShaderGlobalKeywords == null)
                return null;

            return (string[])s_MethodGetShaderGlobalKeywords.Invoke(null, new object[] { shader });
        }

        internal static string[] GetShaderLocalKeywords(Shader shader)
        {
            if (shader == null || s_MethodGetShaderLocalKeywords == null)
                return null;

            return (string[])s_MethodGetShaderLocalKeywords.Invoke(null, new object[] { shader });
        }

        internal static int? GetSRPBatcherCompatibilityCode(Shader shader, int subShaderIdx)
        {
            if (shader == null || s_MethodGetSRPBatcherCompatibilityCode == null)
                return null;

            return (int)s_MethodGetSRPBatcherCompatibilityCode.Invoke(null, new object[] { shader, subShaderIdx });
        }

        internal static ulong? GetVariantCount(Shader shader)
        {
            if (shader == null || s_MethodGetShaderVariantCount == null)
                return null;

            return (ulong)s_MethodGetShaderVariantCount.Invoke(null, new object[] { shader, false });
        }

        internal static bool? HasInstancing(Shader shader)
        {
            if (shader == null || s_MethodHasInstancing == null)
                return null;

            return (bool)s_MethodHasInstancing.Invoke(null, new object[] { shader });
        }

        internal static int GetPropertyCount(Shader shader)
        {
            return shader.GetPropertyCount();
        }

        internal static int GetTexturePropertyCount(Shader shader)
        {
            var texturePropertyCount = 0;
            var propertyCount = GetPropertyCount(shader);
            for (var i = 0; i < propertyCount; ++i)
            {
                if (shader.GetPropertyType(i) == ShaderPropertyType.Texture)
                    ++texturePropertyCount;
            }

            return texturePropertyCount;
        }

        internal static bool? IsSRPBatcherCompatible(Shader shader)
        {
            var subShaderIndex = GetShaderActiveSubshaderIndex(shader);
            if (subShaderIndex == null)
                return null;

            var compatibilityCode = GetSRPBatcherCompatibilityCode(shader, subShaderIndex.Value);
            if (compatibilityCode == null)
                return null;

            return compatibilityCode.Value == 0;
        }
    }
}
