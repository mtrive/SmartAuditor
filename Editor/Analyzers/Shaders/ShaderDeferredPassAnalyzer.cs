// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_URP

using System;
using SmartAuditor.Editor.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Flags shaders carrying a deferred-lighting pass when no active URP renderer is configured
    /// for deferred shading (Deferred or Deferred+).
    /// </summary>
    /// <remarks>
    /// URP's deferred path declares passes tagged <c>LightMode = "UniversalGBuffer"</c>. The legacy
    /// built-in pipeline's deferred shader uses <c>LightMode = "Deferred"</c>. When every active URP
    /// renderer is set to <b>Forward</b> or <b>Forward+</b>, those passes never run, but Unity still
    /// compiles and ships their variants. The analyzer probes the URP asset(s) reachable from
    /// <c>GraphicsSettings</c> and the <c>QualitySettings</c> ladder via
    /// <c>UrpRenderPathInventory</c>, and only fires when none of them request deferred shading.
    /// </remarks>
    sealed class ShaderDeferredPassAnalyzer : ShaderAnalyzer
    {
        internal const string SHD0009 = nameof(SHD0009);

        const string k_UrpGBufferLightMode = "UniversalGBuffer";
        const string k_BuiltinDeferredLightMode = "Deferred";

        static readonly ShaderTagId k_LightModeTag = new ShaderTagId("LightMode");

        internal static readonly Descriptor DeferredPassDescriptor = new Descriptor(
            SHD0009,
            "Shader: Deferred Pass Compiled in Forward-Only URP Project",
            Impact.BuildSize,
            "The shader declares a pass tagged for deferred lighting (<b>LightMode = UniversalGBuffer</b> or <b>LightMode = Deferred</b>), but no active URP renderer in the project is configured for the deferred rendering mode. The pass is compiled into every shader variant and shipped with the build even though no renderer ever invokes it.",
            "Remove the deferred pass from the shader, or set at least one URP renderer's <b>Rendering Mode</b> to <b>Deferred</b> in the Universal Renderer Data asset. If the shader is third-party, exclude it via a project shader stripper."
        )
        {
            MessageFormat = "Shader '{0}' declares deferred pass '{1}' but no active URP renderer runs the deferred path"
        };

        // Cached once per analysis run — the same module dispatches many shaders back-to-back
        // and the URP-asset traversal is non-trivial.
        bool? m_DeferredPassesMayRunCached;

        public override void Initialize()
        {
            m_DeferredPassesMayRunCached = null;
        }

        public override void AnalyzeShader(ShaderAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(DeferredPassDescriptor, context.AssetPath))
                return;

            var shader = context.Shader;
            if (!shader)
                return;

            if (!m_DeferredPassesMayRunCached.HasValue)
                m_DeferredPassesMayRunCached = UrpRenderPathInventory.DeferredPassesMayRun();

            if (m_DeferredPassesMayRunCached.Value)
                return;

            for (int passIndex = 0; passIndex < shader.passCount; passIndex++)
            {
                var lightMode = shader.FindPassTagValue(passIndex, k_LightModeTag);
                if (lightMode == ShaderTagId.none)
                    continue;

                if (!IsDeferredLightMode(lightMode.name))
                    continue;

                // Unity doesn't expose a public Shader.GetPassName(int) API for the active
                // subshader, so we identify the pass by its LightMode tag value — which is exactly
                // what the user needs to search for inside the shader source to remove the pass.
                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.AssetIssue,
                        DeferredPassDescriptor.Id,
                        shader.name,
                        lightMode.name)
                    .WithLocation(new Location(context.AssetPath))
                    .WithEvidence("PassIndex", passIndex)
                    .WithEvidence("LightMode", lightMode.name);
                context.ReportIssue(diagnostic);
            }
        }

        internal static bool IsDeferredLightMode(string lightModeName)
        {
            return string.Equals(lightModeName, k_UrpGBufferLightMode, StringComparison.Ordinal)
                || string.Equals(lightModeName, k_BuiltinDeferredLightMode, StringComparison.Ordinal);
        }
    }
}

#endif
