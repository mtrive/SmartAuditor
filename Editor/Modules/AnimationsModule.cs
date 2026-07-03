using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Profiling;

namespace SmartAuditor.Editor.Modules
{
    sealed class AnimationsModule : AnalysisModule<AnimationClipAnalyzer>
    {
        internal static readonly InsightSchema k_AnimatorControllerInsightSchema = new InsightSchema(
            new InsightColumn(AnimatorControllerColumns.LayerCount, "Layers", PropertyFormat.Number, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Number of Layers"),
            new InsightColumn(AnimatorControllerColumns.ParameterCount, "Parameters", PropertyFormat.Number, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Number of Parameters"),
            new InsightColumn(AnimatorControllerColumns.ClipCount, "Clips", PropertyFormat.Number, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Number of Animation Clips"),
            new InsightColumn(AnimatorControllerColumns.Size, "Size", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Controller Size"));

        internal static readonly InsightSchema k_AnimationClipInsightSchema = new InsightSchema(
            new InsightColumn(AnimationClipColumns.Length, "Length", PropertyFormat.DurationFixed, ColumnHints.Aggregatable | ColumnHints.TopNMetric),
            new InsightColumn(AnimationClipColumns.Framerate, "Frame Rate", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(AnimationClipColumns.WrapMode, "Wrap Mode", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(AnimationClipColumns.IsLooping, "Looping", PropertyFormat.Boolean),
            new InsightColumn(AnimationClipColumns.HasGenericRootTransform, "Generic Root Transform", PropertyFormat.Boolean, longName: "Has animation on the root transform"),
            new InsightColumn(AnimationClipColumns.HasMotionCurves, "Motion Curves", PropertyFormat.Boolean, longName: "Has root motion curves"),
            new InsightColumn(AnimationClipColumns.HasMotionFloatCurves, "Motion Float Curves", PropertyFormat.Boolean, longName: "Has editor curves for its root motion"),
            new InsightColumn(AnimationClipColumns.HasRootCurves, "Root Curves", PropertyFormat.Boolean),
            new InsightColumn(AnimationClipColumns.HumanMotion, "Human Motion", PropertyFormat.Boolean, longName: "Contains curves that drive a humanoid rig"),
            new InsightColumn(AnimationClipColumns.IsLegacy, "Legacy", PropertyFormat.Boolean, longName: "Is this clip used with a Legacy Animation component?"),
            new InsightColumn(AnimationClipColumns.Size, "Size", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Clip Size"));

        internal static readonly InsightSchema k_AvatarInsightSchema = new InsightSchema(
            new InsightColumn(AvatarColumns.IsValid, "Valid", PropertyFormat.Boolean),
            new InsightColumn(AvatarColumns.IsHuman, "Human", PropertyFormat.Boolean),
            new InsightColumn(AvatarColumns.HumanBoneCount, "Human Bones", PropertyFormat.Number, longName: "Number of bones mappings"),
            new InsightColumn(AvatarColumns.SkeletonBoneCount, "Skeleton Bones", PropertyFormat.Number, ColumnHints.Aggregatable, longName: "Number of bone transforms to include"),
            new InsightColumn(AvatarColumns.UpperArmTwist, "Upper Arm Twist", PropertyFormat.Text),
            new InsightColumn(AvatarColumns.LowerArmTwist, "Lower Arm Twist", PropertyFormat.Text),
            new InsightColumn(AvatarColumns.UpperLegTwist, "Upper Leg Twist", PropertyFormat.Text),
            new InsightColumn(AvatarColumns.LowerLegTwist, "Lower Leg Twist", PropertyFormat.Text),
            new InsightColumn(AvatarColumns.ArmStretch, "Arm Stretch", PropertyFormat.Text),
            new InsightColumn(AvatarColumns.LegStretch, "Leg Stretch", PropertyFormat.Text),
            new InsightColumn(AvatarColumns.FeetSpacing, "Feet Spacing", PropertyFormat.Text),
            new InsightColumn(AvatarColumns.HasTranslationDoF, "Translation DoF", PropertyFormat.Boolean),
            new InsightColumn(AvatarColumns.Size, "Size", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Avatar Size"));

        internal static readonly InsightSchema k_AvatarMaskInsightSchema = new InsightSchema(
            new InsightColumn(AvatarMaskColumns.TransformCount, "Transforms", PropertyFormat.Number, ColumnHints.Aggregatable, longName: "Number of Transforms"),
            new InsightColumn(AvatarMaskColumns.Size, "Size", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Mask Size"));

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.AnimatorController, k_AnimatorControllerInsightSchema },
                { AnalysisCategory.AnimationClip, k_AnimationClipInsightSchema },
                { AnalysisCategory.Avatar, k_AvatarInsightSchema },
                { AnalysisCategory.AvatarMask, k_AvatarMaskInsightSchema },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override string Name => "Animations";

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.AnimatorController,
            AnalysisCategory.AnimationClip,
            AnalysisCategory.Avatar,
            AnalysisCategory.AvatarMask,
        };

        public override IReadOnlyCollection<AnalysisCategory> AdditionalEmittedCategories =>
            new[] { AnalysisCategory.AssetIssue };

        // Yield to the message loop every N assets so the in-window progress overlay can
        // repaint and the user can hit Cancel. AssetDatabase / Profiler.GetRuntimeMemorySizeLong
        // are main-thread-only, so we use await Task.Yield rather than offloading to a worker.
        const int k_YieldEveryNAssets = 32;

        public override async Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;

            using var context = new AnalysisContext(options, session);

            await ProcessAnimatorControllers(context, progress, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return AnalysisResult.Cancelled;

            await ProcessAnimationClips(context, progress, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return AnalysisResult.Cancelled;

            await ProcessAvatars(context, progress, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return AnalysisResult.Cancelled;

            await ProcessAvatarMasks(context, progress, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return AnalysisResult.Cancelled;

            var analyzers = GetCompatibleAnalyzers(options);
            FinalizeAnalyzers(session, analyzers);
            return AnalysisResult.Success;
        }

        async Task ProcessAnimatorControllers(AnalysisContext context, IProgress progress, CancellationToken cancellationToken)
        {
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(AnimatorController)}, a:assets", context.Options);
            progress?.Start("Analyzing Animator Controllers", string.Empty, assetPaths.Length);
            for (var i = 0; i < assetPaths.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                var assetPath = assetPaths[i];
                progress?.Advance(assetPath);

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
                if (controller == null)
                {
                    Debug.LogError(assetPath + " is not an Animator Controller.");

                    continue;
                }

                // TODO: the size returned by the profiler may not be the exact size on the target platform. Needs to be fixed.
                var size = Profiler.GetRuntimeMemorySizeLong(controller);

                var table = context.GetInsightTable(AnalysisCategory.AnimatorController, k_AnimatorControllerInsightSchema);
                table.AddRow(assetPath, new Dictionary<string, object>
                {
                    [AnimatorControllerColumns.LayerCount] = controller.layers.Length,
                    [AnimatorControllerColumns.ParameterCount] = controller.parameters.Length,
                    [AnimatorControllerColumns.ClipCount] = controller.animationClips.Length,
                    [AnimatorControllerColumns.Size] = size,
                });

                if ((i + 1) % k_YieldEveryNAssets == 0)
                    await Task.Yield();
            }

            progress?.Clear();
        }

        async Task ProcessAnimationClips(AnalysisContext context, IProgress progress, CancellationToken cancellationToken)
        {
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(AnimationClip)}, a:assets", context.Options);

            progress?.Start("Analyzing Animation Clips", string.Empty, assetPaths.Length);

            for (var i = 0; i < assetPaths.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                var assetPath = assetPaths[i];
                progress?.Advance(assetPath);

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip == null)
                {
                    Debug.LogError(assetPath + " is not an Animation Clip.");

                    continue;
                }

                // TODO: the size returned by the profiler may not be the exact size on the target platform. Needs to be fixed.
                var size = Profiler.GetRuntimeMemorySizeLong(clip);

                var table = context.GetInsightTable(AnalysisCategory.AnimationClip, k_AnimationClipInsightSchema);
                table.AddRow(assetPath, new Dictionary<string, object>
                {
                    [AnimationClipColumns.Length] = clip.length,
                    [AnimationClipColumns.Framerate] = Formatting.FormatFramerate(clip.frameRate),
                    [AnimationClipColumns.WrapMode] = clip.wrapMode.ToString(),
                    [AnimationClipColumns.IsLooping] = clip.isLooping,
                    [AnimationClipColumns.HasGenericRootTransform] = clip.hasGenericRootTransform,
                    [AnimationClipColumns.HasMotionCurves] = clip.hasMotionCurves,
                    [AnimationClipColumns.HasMotionFloatCurves] = clip.hasMotionFloatCurves,
                    [AnimationClipColumns.HasRootCurves] = clip.hasRootCurves,
                    [AnimationClipColumns.HumanMotion] = clip.humanMotion,
                    [AnimationClipColumns.IsLegacy] = clip.legacy,
                    [AnimationClipColumns.Size] = size,
                });

                if ((i + 1) % k_YieldEveryNAssets == 0)
                    await Task.Yield();
            }

            progress?.Clear();
        }

        async Task ProcessAvatars(AnalysisContext context, IProgress progress, CancellationToken cancellationToken)
        {
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(Avatar)}, a:assets", context.Options);

            progress?.Start("Analyzing Avatars", string.Empty, assetPaths.Length);

            for (var i = 0; i < assetPaths.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                var assetPath = assetPaths[i];
                progress?.Advance(assetPath);

                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(assetPath);
                if (avatar == null)
                {
                    Debug.LogError(assetPath + " is not an Avatar.");

                    continue;
                }

                // TODO: the size returned by the profiler may not be the exact size on the target platform. Needs to be fixed.
                var size = Profiler.GetRuntimeMemorySizeLong(avatar);

                var table = context.GetInsightTable(AnalysisCategory.Avatar, k_AvatarInsightSchema);
                table.AddRow(assetPath, new Dictionary<string, object>
                {
                    [AvatarColumns.IsValid] = avatar.isValid,
                    [AvatarColumns.IsHuman] = avatar.isHuman,
                    [AvatarColumns.HumanBoneCount] = avatar.humanDescription.human.Length,
                    [AvatarColumns.SkeletonBoneCount] = avatar.humanDescription.skeleton.Length,
                    [AvatarColumns.UpperArmTwist] = avatar.humanDescription.upperArmTwist,
                    [AvatarColumns.LowerArmTwist] = avatar.humanDescription.lowerArmTwist,
                    [AvatarColumns.UpperLegTwist] = avatar.humanDescription.upperLegTwist,
                    [AvatarColumns.LowerLegTwist] = avatar.humanDescription.lowerLegTwist,
                    [AvatarColumns.ArmStretch] = avatar.humanDescription.armStretch,
                    [AvatarColumns.LegStretch] = avatar.humanDescription.legStretch,
                    [AvatarColumns.FeetSpacing] = avatar.humanDescription.feetSpacing,
                    [AvatarColumns.HasTranslationDoF] = avatar.humanDescription.hasTranslationDoF,
                    [AvatarColumns.Size] = size,
                });

                if ((i + 1) % k_YieldEveryNAssets == 0)
                    await Task.Yield();
            }

            progress?.Clear();
        }

        async Task ProcessAvatarMasks(AnalysisContext context, IProgress progress, CancellationToken cancellationToken)
        {
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(AvatarMask)}, a:assets", context.Options);

            progress?.Start("Analyzing Avatar Masks", string.Empty, assetPaths.Length);

            for (var i = 0; i < assetPaths.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                var assetPath = assetPaths[i];
                progress?.Advance(assetPath);

                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(assetPath);
                if (mask == null)
                {
                    Debug.LogError(assetPath + " is not an Avatar Mask.");

                    continue;
                }

                // TODO: the size returned by the profiler may not be the exact size on the target platform. Needs to be fixed.
                var size = Profiler.GetRuntimeMemorySizeLong(mask);

                var table = context.GetInsightTable(AnalysisCategory.AvatarMask, k_AvatarMaskInsightSchema);
                table.AddRow(assetPath, new Dictionary<string, object>
                {
                    [AvatarMaskColumns.TransformCount] = mask.transformCount,
                    [AvatarMaskColumns.Size] = size,
                });

                if ((i + 1) % k_YieldEveryNAssets == 0)
                    await Task.Yield();
            }

            progress?.Clear();
        }
    }
}
