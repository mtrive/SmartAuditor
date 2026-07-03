// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

namespace SmartAuditor.Editor.Modules
{
    internal static class ShaderColumns
    {
        public const string Name = "Name";
        public const string Size = "Size";
        public const string VariantCount = "VariantCount";
        public const string BuiltFragmentVariants = "BuiltFragmentVariants";
        public const string PassCount = "PassCount";
        public const string KeywordCount = "KeywordCount";
        public const string PropertyCount = "PropertyCount";
        public const string TexturePropertyCount = "TexturePropertyCount";
        public const string RenderQueue = "RenderQueue";
        public const string Instancing = "Instancing";
        public const string SrpBatcher = "SrpBatcher";
        public const string AlwaysIncluded = "AlwaysIncluded";
    }

    internal static class MaterialColumns
    {
        public const string Name = "Name";
        public const string Shader = "Shader";
        public const string InstancingEnabled = "InstancingEnabled";
    }

    internal static class ShaderVariantColumns
    {
        public const string ShaderName = "ShaderName";
        public const string Compiled = "Compiled";
        public const string Platform = "Platform";
        public const string Tier = "Tier";
        public const string Stage = "Stage";
        public const string PassType = "PassType";
        public const string PassName = "PassName";
        public const string KeywordCount = "KeywordCount";
        public const string Keywords = "Keywords";
        public const string PlatformKeywords = "PlatformKeywords";
        public const string Requirements = "Requirements";
    }

    internal static class ComputeShaderVariantColumns
    {
        public const string ShaderName = "ShaderName";
        public const string Platform = "Platform";
        public const string Tier = "Tier";
        public const string Kernel = "Kernel";
        public const string KernelThreadCount = "KernelThreadCount";
        public const string Keywords = "Keywords";
        public const string PlatformKeywords = "PlatformKeywords";
    }
}
