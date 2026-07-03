// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

namespace SmartAuditor.Editor.Modules
{
    internal static class AnimatorControllerColumns
    {
        public const string LayerCount = "LayerCount";
        public const string ParameterCount = "ParameterCount";
        public const string ClipCount = "ClipCount";
        public const string Size = "Size";
    }

    internal static class AnimationClipColumns
    {
        public const string Length = "Length";
        public const string Framerate = "Framerate";
        public const string WrapMode = "WrapMode";
        public const string IsLooping = "IsLooping";
        public const string HasGenericRootTransform = "HasGenericRootTransform";
        public const string HasMotionCurves = "HasMotionCurves";
        public const string HasMotionFloatCurves = "HasMotionFloatCurves";
        public const string HasRootCurves = "HasRootCurves";
        public const string HumanMotion = "HumanMotion";
        public const string IsLegacy = "IsLegacy";
        public const string Size = "Size";
    }

    internal static class AvatarColumns
    {
        public const string IsValid = "IsValid";
        public const string IsHuman = "IsHuman";
        public const string HumanBoneCount = "HumanBoneCount";
        public const string SkeletonBoneCount = "SkeletonBoneCount";
        public const string UpperArmTwist = "UpperArmTwist";
        public const string LowerArmTwist = "LowerArmTwist";
        public const string UpperLegTwist = "UpperLegTwist";
        public const string LowerLegTwist = "LowerLegTwist";
        public const string ArmStretch = "ArmStretch";
        public const string LegStretch = "LegStretch";
        public const string FeetSpacing = "FeetSpacing";
        public const string HasTranslationDoF = "HasTranslationDoF";
        public const string Size = "Size";
    }

    internal static class AvatarMaskColumns
    {
        public const string TransformCount = "TransformCount";
        public const string Size = "Size";
    }
}
