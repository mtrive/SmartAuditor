using System.Collections.Generic;
using System.IO;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes shaders for compiler messages (warnings and errors) across all shader compiler platforms
    /// available for the analysis target.
    /// </summary>
    sealed class ShaderCompilerMessageAnalyzer : ShaderAnalyzer
    {
        readonly Dictionary<BuildTarget, ShaderCompilerPlatform[]> m_PlatformCache = new Dictionary<BuildTarget, ShaderCompilerPlatform[]>();

        public override void Initialize()
        {
            m_PlatformCache.Clear();
        }

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
            // Analyzers persist across analysis runs (Initialize is called once at module
            // construction); drop the platform cache so a later run picks up any change in the
            // editor's compiler-platform set rather than serving stale entries.
            m_PlatformCache.Clear();
        }

        ShaderCompilerPlatform[] GetPlatformsForBuildTarget(BuildTarget buildTarget)
        {
            if (m_PlatformCache.TryGetValue(buildTarget, out var platforms))
                return platforms;
            platforms = ShaderCompilerPlatformUtil.GetShaderCompilerPlatformsForBuildTarget(buildTarget);
            m_PlatformCache[buildTarget] = platforms;
            return platforms;
        }

        public override void AnalyzeShader(ShaderAnalysisContext context)
        {
            var reportedMessages = new HashSet<(ShaderCompilerPlatform platform, string message, int line, string file)>();
            var platforms = GetPlatformsForBuildTarget(context.Options.Platform);

            foreach (var platform in platforms)
            {
                var shaderMessages = ShaderUtil.GetShaderMessages(context.Shader, platform);
                foreach (var shaderMessage in shaderMessages)
                {
                    // Strip trailing newline AND any blank lines embedded in the body
                    // (the latter shows up in some shader-compiler diagnostics and
                    // would inflate row height in wrapped views without adding signal).
                    var message = Formatting.StripEmptyLines(shaderMessage.message);

                    var locationPath = ResolveMessageFilePath(shaderMessage.file, context.AssetPath);

                    // Deduplicate messages with the same platform, message, line, and file
                    var key = (shaderMessage.platform, message, shaderMessage.line, locationPath);
                    if (reportedMessages.Contains(key))
                        continue;

                    reportedMessages.Add(key);

                    var logLevel = shaderMessage.severity == ShaderCompilerMessageSeverity.Error
                        ? LogLevel.Error
                        : LogLevel.Warning;
                    context.AddMessage(AnalysisCategory.ShaderCompilerMessage, message)
                        .WithLogLevel(logLevel)
                        .WithLocation(new Location(locationPath, shaderMessage.line))
                        .WithProperty(ShaderCompilerMessageColumns.ShaderName, context.Shader.name)
                        .WithProperty(ShaderCompilerMessageColumns.Platform, shaderMessage.platform.ToString());
                }
            }
        }

        /// <summary>
        /// Resolves the file path from a shader compiler message to a project-relative path.
        /// Falls back to the shader asset path when the message file cannot be mapped to a project asset.
        /// </summary>
        /// <param name="messageFile">The file path reported by the shader compiler (may be null, absolute, or relative).</param>
        /// <param name="shaderAssetPath">The project-relative path of the shader being analyzed, used as fallback.</param>
        internal static string ResolveMessageFilePath(string messageFile, string shaderAssetPath)
        {
            if (string.IsNullOrEmpty(messageFile))
                return shaderAssetPath;

            var normalized = messageFile.Replace('\\', '/');

            if (IsProjectRelativeAssetPath(normalized))
                return normalized;

            // Cached package shaders are reported as Library/PackageCache/<pkg>@<hash>/<rest>,
            // sometimes with a virtual "Unity/" project-root prefix. Rewrite to the canonical
            // Packages/<pkg>/<rest> form Unity exposes via AssetDatabase.
            var packagePath = TryRewritePackageCachePath(normalized);
            if (packagePath != null)
            {
                return packagePath;
            }

            // Strip absolute project-root prefix if present (e.g. /Users/dev/MyProject/Assets/Foo.hlsl)
            var projectRoot = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/').TrimEnd('/');
            if (!string.IsNullOrEmpty(projectRoot) &&
                normalized.StartsWith(projectRoot + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                var relative = normalized.Substring(projectRoot.Length + 1);
                if (IsProjectRelativeAssetPath(relative))
                    return relative;

                packagePath = TryRewritePackageCachePath(relative);
                if (packagePath != null)
                {
                    return packagePath;
                }
            }

            // Path is outside the project (e.g. Unity built-in includes) — fall back to the shader path
            return shaderAssetPath;
        }

        static bool IsProjectRelativeAssetPath(string path)
        {
            return path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Rewrites a <c>Library/PackageCache/&lt;pkg&gt;@&lt;hash&gt;/&lt;rest&gt;</c> path
        /// (optionally prefixed with the virtual <c>Unity/</c> project-root marker the shader
        /// compiler emits for cached package files) to the canonical
        /// <c>Packages/&lt;pkg&gt;/&lt;rest&gt;</c> form. Returns <c>null</c> when the input is
        /// not a package-cache path.
        /// </summary>
        static string TryRewritePackageCachePath(string normalized)
        {
            const string unityPrefix = "Unity/";
            var path = normalized.StartsWith(unityPrefix, System.StringComparison.Ordinal)
                ? normalized.Substring(unityPrefix.Length)
                : normalized;

            const string packageCachePrefix = "Library/PackageCache/";
            if (!path.StartsWith(packageCachePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var rest = path.Substring(packageCachePrefix.Length);
            var slashIdx = rest.IndexOf('/');
            if (slashIdx <= 0)
            {
                return null;
            }

            var pkgWithHash = rest.Substring(0, slashIdx);
            var atIdx = pkgWithHash.IndexOf('@');
            // Embedded packages don't go through the cache; without an @<hash> the path is
            // already malformed for our purposes -- bail out rather than synthesise a Packages path.
            if (atIdx <= 0)
            {
                return null;
            }

            var pkg = pkgWithHash.Substring(0, atIdx);
            return $"Packages/{pkg}/{rest.Substring(slashIdx + 1)}";
        }
    }
}
