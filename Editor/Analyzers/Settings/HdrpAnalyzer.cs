using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
#if PACKAGE_HDRP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
#endif

namespace SmartAuditor.Editor.Analyzers
{
    sealed class HdrpAnalyzer : SettingsAnalyzer
    {
        internal const string GFX0004 = nameof(GFX0004);
        internal const string GFX0005 = nameof(GFX0005);

        static readonly Descriptor AssetLitShaderModeBothOrMixed = new Descriptor(
            GFX0004,
            "HDRP: Lit Shader Mode is Both in Render Pipeline Asset",
            Impact.BuildSize | Impact.BuildTime,
            "An HDRP Render Pipeline Asset has <b>Supported Lit Shader Mode</b> set to <b>Both</b>. Unity compiles every lit shader twice — once for Forward and once for Deferred — doubling the shader payload baked into the player and the time it takes to build that payload. The exception is when the project deliberately uses special cameras (reflection probes, custom render textures) that require the other lit path alongside the primary one.",
            "Set <b>Supported Lit Shader Mode</b> on the HDRP Asset to either <b>Forward</b> or <b>Deferred</b>, depending on which path the project actually renders with. If special cameras genuinely need both paths, keep <b>Both</b> and accept the build-size cost knowingly."
        );

        static readonly Descriptor CameraLitShaderModeBothOrMixed = new Descriptor(
            GFX0005,
            "HDRP: Cameras Mix Forward and Deferred Lit Shader Modes",
            Impact.BuildSize | Impact.BuildTime,
            "Multiple HD Cameras in the project disagree on <b>Lit Shader Mode</b> — some are set to <b>Forward</b>, others to <b>Deferred</b>. The build must include every variant that any camera might need, doubling the shader payload and the time it takes to compile it. Intentional mixed setups (such as a reflection probe or overlay camera using a different path than the main camera) may be correct design choices.",
            "Pick a single <b>Lit Shader Mode</b> for the project and apply it to every HDRP Asset and every <b>HDAdditionalCameraData</b> component. If specific cameras (reflection captures, special-purpose cameras) genuinely require a different mode, document the intent and suppress this diagnostic for those assets."
        );

#if PACKAGE_HDRP
        public override void Analyze(SettingsAnalysisContext context)
        {
            if (context.IsDescriptorEnabled(AssetLitShaderModeBothOrMixed))
            {
                var assetNames = CollectAssetNamesWithLitShaderModeIssue();
                if (assetNames.Count > 0)
                {
                    var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, AssetLitShaderModeBothOrMixed.Id)
                        .WithEvidence("AffectedAssets", string.Join(", ", assetNames))
                        .WithEvidence("AffectedAssetCount", assetNames.Count)
                        .WithLocation(new Location("Project/Graphics"));
                    context.ReportIssue(diagnostic);
                }
            }

            if (context.IsDescriptorEnabled(CameraLitShaderModeBothOrMixed))
            {
                var cameraDetails = CollectMixedCameraLitShaderModeDetails();
                if (cameraDetails.HasMixedModes)
                {
                    var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, CameraLitShaderModeBothOrMixed.Id)
                        .WithEvidence("ForwardCameraCount", cameraDetails.ForwardCount)
                        .WithEvidence("DeferredCameraCount", cameraDetails.DeferredCount)
                        .WithLocation(new Location("Project/Graphics"));
                    context.ReportIssue(diagnostic);
                }
            }
        }

        static List<string> CollectAssetNamesWithLitShaderModeIssue()
        {
            var hdrpAssets = CollectHdrpAssets();
            var names = new List<string>();
            if (hdrpAssets.Count == 0)
                return names;

            bool hasBoth = false;
            bool hasForward = false;
            bool hasDeferred = false;

            foreach (var asset in hdrpAssets)
            {
                var mode = asset.currentPlatformRenderPipelineSettings.supportedLitShaderMode;
                if (mode == RenderPipelineSettings.SupportedLitShaderMode.Both)
                {
                    hasBoth = true;
                    names.Add($"{asset.name} (Both)");
                }
                else if (mode == RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly)
                    hasForward = true;
                else
                    hasDeferred = true;
            }

            // Also flag assets where the mode differs across assets (mixed but not Both).
            if (!hasBoth && hasForward && hasDeferred)
            {
                foreach (var asset in hdrpAssets)
                {
                    var mode = asset.currentPlatformRenderPipelineSettings.supportedLitShaderMode;
                    names.Add($"{asset.name} ({mode})");
                }
            }

            return names;
        }

        readonly struct MixedCameraDetails
        {
            internal readonly bool HasMixedModes;
            internal readonly int ForwardCount;
            internal readonly int DeferredCount;

            internal MixedCameraDetails(bool hasMixedModes, int forwardCount, int deferredCount)
            {
                HasMixedModes = hasMixedModes;
                ForwardCount = forwardCount;
                DeferredCount = deferredCount;
            }
        }

        static MixedCameraDetails CollectMixedCameraLitShaderModeDetails()
        {
            int forwardCount = 0;
            int deferredCount = 0;

            foreach (var cameraData in GameObjectUtils.GetAllComponents<HDAdditionalCameraData>())
            {
                if (cameraData.renderingPathCustomFrameSettings.litShaderMode == LitShaderMode.Deferred)
                    deferredCount++;
                else
                    forwardCount++;
            }

            return new MixedCameraDetails(
                forwardCount > 0 && deferredCount > 0,
                forwardCount,
                deferredCount);
        }

        static HashSet<HDRenderPipelineAsset> CollectHdrpAssets()
        {
            var hdrpAssets = new HashSet<HDRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is HDRenderPipelineAsset defaultRenderPipeline)
                hdrpAssets.Add(defaultRenderPipeline);

            for (var i = 0; i < QualitySettings.names.Length; ++i)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) is HDRenderPipelineAsset hdrpAsset)
                    hdrpAssets.Add(hdrpAsset);
            }

            return hdrpAssets;
        }

#else
        public override void Analyze(SettingsAnalysisContext context)
        {
        }

#endif
    }
}
