using System;
using System.Collections.Generic;
using System.Linq;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class GraphicsApiAnalyzer : SettingsAnalyzer
    {
        const string documentationUrl = "https://docs.unity3d.com/Manual/GraphicsAPIs.html";

        internal const string GFX0015 = nameof(GFX0015);
        internal const string GFX0016 = nameof(GFX0016);
        internal const string GFX0018 = nameof(GFX0018);

        static readonly Descriptor OpenGLESAndMetalDescriptor = new Descriptor(
            GFX0015,
            "Graphics API (iOS): Metal and OpenGLES Both Enabled",
            Impact.BuildSize,
            "The iOS Player Settings have both <b>Metal</b> and <b>OpenGLES</b> graphics APIs enabled. The build ships two parallel shader-variant sets and platform-init code paths, inflating the IPA.",
            "Remove <b>OpenGLES</b> from the iOS graphics-API list in <b>Project Settings &gt; Player &gt; Other Settings</b> if the minimum-spec target device supports Metal (iPhone 5s onwards).")
        {
            DocumentationUrl = documentationUrl,
            Platforms = new[] { BuildTarget.iOS },
            MaximumVersion = "2022.3"
        };

        static readonly Descriptor MetalDescriptor = new Descriptor(
            GFX0016,
            "Graphics API (iOS): Metal API Not Enabled",
            Impact.Performance,
            "The iOS Player Settings do not include <b>Metal</b> in the graphics-API list. The build runs through the slower OpenGLES path, leaving CPU/GPU performance headroom on the table.",
            "Add <b>Metal</b> to the iOS graphics-API list in <b>Project Settings &gt; Player &gt; Other Settings</b>.")
        {
            DocumentationUrl = documentationUrl,
            Platforms = new[] { BuildTarget.iOS }
        };

        static readonly Descriptor VulkanDescriptor = new Descriptor(
            GFX0018,
            "Graphics API (Android): Vulkan API Not Enabled",
            Impact.Performance,
            "The Android Player Settings do not include <b>Vulkan</b> in the graphics-API list. The build runs through the slower OpenGLES path, paying higher CPU draw-call overhead and missing out on Vulkan-only GPU features.",
            "Add <b>Vulkan</b> to the Android graphics-API list in <b>Project Settings &gt; Player &gt; Other Settings</b>.")
        {
            DocumentationUrl = documentationUrl,
            Platforms = new[] { BuildTarget.Android }
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            const string location = "Project/Player";
            if (context.IsDescriptorEnabled(OpenGLESAndMetalDescriptor) &&
                IsUsingOpenGlesAndMetal())
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, OpenGLESAndMetalDescriptor.Id)
                    .WithLocation(new Location(location));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(MetalDescriptor) &&
                MetalDescriptor.IsApplicable(context.Options) && IsNotUsingMetal())
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, MetalDescriptor.Id)
                    .WithLocation(new Location(location));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(VulkanDescriptor) &&
                VulkanDescriptor.IsApplicable(context.Options) && IsNotUsingVulkan())
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, VulkanDescriptor.Id)
                    .WithLocation(new Location(location));
                context.ReportIssue(diagnostic);
            }
        }

        static bool IsNotUsingMetal()
        {
            var graphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS);

            var hasMetal = graphicsAPIs.Contains(GraphicsDeviceType.Metal);

            return !hasMetal;
        }

        static bool IsUsingOpenGlesAndMetal()
        {
#if UNITY_2023_1_OR_NEWER
            return false;
#else
            var graphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS);

            var hasOpenGles = graphicsAPIs.Contains(GraphicsDeviceType.OpenGLES2) ||
                graphicsAPIs.Contains(GraphicsDeviceType.OpenGLES3);

            return graphicsAPIs.Contains(GraphicsDeviceType.Metal) && hasOpenGles;
#endif
        }

        static bool IsNotUsingVulkan()
        {
            return !PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Contains(GraphicsDeviceType.Vulkan);
        }
    }
}
