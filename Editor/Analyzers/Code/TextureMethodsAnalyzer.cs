// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class TextureMethodsAnalyzer : CodeAnalyzer
    {
        // WebCamTexture
        internal const string CDE0241 = nameof(CDE0241);
        internal const string CDE0242 = nameof(CDE0242);

        // Texture2D - Batch methods
        internal const string CDE0243 = nameof(CDE0243);
        internal const string CDE0244 = nameof(CDE0244);
        internal const string CDE0245 = nameof(CDE0245);
        internal const string CDE0246 = nameof(CDE0246);

        // Texture2D - PackTextures
        internal const string CDE0270 = nameof(CDE0270);

        // Texture2D - Individual pixel methods
        internal const string CDE0261 = nameof(CDE0261);
        internal const string CDE0262 = nameof(CDE0262);
        internal const string CDE0263 = nameof(CDE0263);

        // Texture3D - Batch methods
        internal const string CDE0247 = nameof(CDE0247);
        internal const string CDE0248 = nameof(CDE0248);
        internal const string CDE0249 = nameof(CDE0249);
        internal const string CDE0250 = nameof(CDE0250);

        // Texture3D - Individual pixel methods
        internal const string CDE0264 = nameof(CDE0264);
        internal const string CDE0265 = nameof(CDE0265);

        // Texture2DArray
        internal const string CDE0251 = nameof(CDE0251);
        internal const string CDE0252 = nameof(CDE0252);
        internal const string CDE0253 = nameof(CDE0253);
        internal const string CDE0254 = nameof(CDE0254);

        // Cubemap
        internal const string CDE0255 = nameof(CDE0255);
        internal const string CDE0256 = nameof(CDE0256);

        // CubemapArray
        internal const string CDE0257 = nameof(CDE0257);
        internal const string CDE0258 = nameof(CDE0258);
        internal const string CDE0259 = nameof(CDE0259);
        internal const string CDE0260 = nameof(CDE0260);

        // WebCamTexture descriptors
        static readonly Descriptor WebCamTextureGetPixelsDescriptor = new Descriptor(
            CDE0241,
            "Texture: WebCamTexture.GetPixels Allocates",
            Impact.Memory,
            "<b>WebCamTexture.GetPixels()</b> allocates a fresh <b>Color[]</b> on every call.",
            "Call <b>WebCamTexture.GetPixels32(Color32[])</b> with a pre-allocated buffer; the <b>Color32</b> format is also cheaper to marshal."
        )
        {
            MessageFormat = "WebCamTexture.GetPixels allocates a managed Color array"
        };

        static readonly Descriptor WebCamTextureGetPixels32Descriptor = new Descriptor(
            CDE0242,
            "Texture: WebCamTexture.GetPixels32 Allocates Without Buffer",
            Impact.Memory,
            "The no-argument <b>WebCamTexture.GetPixels32()</b> allocates a fresh <b>Color32[]</b> on every call.",
            "Call the <b>WebCamTexture.GetPixels32(Color32[])</b> overload with a pre-allocated buffer sized to the camera's pixel count."
        )
        {
            MessageFormat = "WebCamTexture.GetPixels32 allocates a managed Color32 array"
        };

        // Texture2D batch method descriptors
        static readonly Descriptor Texture2DGetPixelsDescriptor = new Descriptor(
            CDE0243,
            "Texture: Texture2D.GetPixels Allocates",
            Impact.Memory,
            "<b>Texture2D.GetPixels()</b> allocates a fresh <b>Color[]</b> on every call.",
            "Use <b>Texture2D.GetRawTextureData&lt;T&gt;()</b>, which returns a <b>NativeArray</b> view of the pixel data without allocating."
        )
        {
            MessageFormat = "Texture2D.GetPixels allocates a managed Color array"
        };

        static readonly Descriptor Texture2DGetPixels32Descriptor = new Descriptor(
            CDE0244,
            "Texture: Texture2D.GetPixels32 Allocates",
            Impact.Memory,
            "<b>Texture2D.GetPixels32()</b> allocates a fresh <b>Color32[]</b> on every call.",
            "Use <b>Texture2D.GetRawTextureData&lt;Color32&gt;()</b>, which returns a <b>NativeArray&lt;Color32&gt;</b> view of the pixel data without allocating."
        )
        {
            MessageFormat = "Texture2D.GetPixels32 allocates a managed Color32 array"
        };

        static readonly Descriptor Texture2DSetPixelsDescriptor = new Descriptor(
            CDE0245,
            "Texture: Texture2D.SetPixels Is Slower Than SetPixels32",
            Impact.Performance,
            "<b>Texture2D.SetPixels()</b> takes <b>Color</b> values that the engine converts to <b>Color32</b> internally before upload, making it slower than the <b>SetPixels32</b> path.",
            "Call <b>Texture2D.SetPixels32(Color32[])</b>, or write into the buffer returned by <b>Texture2D.GetRawTextureData&lt;T&gt;()</b> and call <b>Texture2D.Apply()</b>."
        )
        {
            MessageFormat = "Texture2D.SetPixels is slower than SetPixels32"
        };

        static readonly Descriptor Texture2DSetPixels32Descriptor = new Descriptor(
            CDE0246,
            "Texture: Texture2D.SetPixels32 Allocates",
            Impact.Performance,
            "<b>Texture2D.SetPixels32()</b> copies the supplied managed array into native memory on every call; reusable native-array access is faster.",
            "Write into the buffer returned by <b>Texture2D.GetRawTextureData&lt;Color32&gt;()</b> and call <b>Texture2D.Apply()</b>."
        )
        {
            MessageFormat = "Texture2D.SetPixels32 allocates managed memory"
        };

        // Texture2D - PackTextures descriptor
        static readonly Descriptor Texture2DPackTexturesDescriptor = new Descriptor(
            CDE0270,
            "Texture: Texture2D.PackTextures Allocates",
            Impact.Memory,
            "<b>Texture2D.PackTextures()</b> repacks all input textures into a single atlas on the CPU and allocates managed memory for the atlas and the returned <b>Rect[]</b>.",
            "Call <b>Texture2D.PackTextures()</b> once during initialization. Cache the resulting atlas and UV rects and reuse them across frames."
        )
        {
            MessageFormat = "Texture2D.PackTextures allocates managed memory"
        };

        // Texture2D individual pixel method descriptors
        static readonly Descriptor Texture2DGetPixelDescriptor = new Descriptor(
            CDE0261,
            "Texture: Texture2D.GetPixel Per-Pixel Marshalling",
            Impact.Performance,
            "<b>Texture2D.GetPixel()</b> crosses the managed-native boundary on every call. Reading pixels one at a time amplifies the marshalling cost across the loop.",
            "Read pixels in bulk: call <b>Texture2D.GetPixels()</b> / <b>Texture2D.GetPixels32()</b> once and index the managed array, or use <b>Texture2D.GetRawTextureData&lt;T&gt;()</b> for direct <b>NativeArray</b> access."
        )
        {
            MessageFormat = "Texture2D.GetPixel is called per-pixel"
        };

        static readonly Descriptor Texture2DGetPixelBilinearDescriptor = new Descriptor(
            CDE0262,
            "Texture: Texture2D.GetPixelBilinear Per-Pixel Marshalling",
            Impact.Performance,
            "<b>Texture2D.GetPixelBilinear()</b> crosses the managed-native boundary on every call. Sampling individual pixels one at a time amplifies the marshalling cost across the loop.",
            "Sample in bulk with <b>Texture2D.GetPixels()</b> / <b>Texture2D.GetPixels32()</b> and interpolate in managed code, or use <b>Texture2D.GetRawTextureData&lt;T&gt;()</b> for direct <b>NativeArray</b> access."
        )
        {
            MessageFormat = "Texture2D.GetPixelBilinear is called per-pixel"
        };

        static readonly Descriptor Texture2DSetPixelDescriptor = new Descriptor(
            CDE0263,
            "Texture: Texture2D.SetPixel Per-Pixel Marshalling",
            Impact.Performance,
            "<b>Texture2D.SetPixel()</b> crosses the managed-native boundary on every call. Writing pixels one at a time amplifies the marshalling cost across the loop.",
            "Write in bulk with <b>Texture2D.SetPixels()</b> / <b>Texture2D.SetPixels32()</b>, or write into <b>Texture2D.GetRawTextureData&lt;T&gt;()</b> directly. Call <b>Texture2D.Apply()</b> once after all writes complete."
        )
        {
            MessageFormat = "Texture2D.SetPixel is called per-pixel"
        };

        // Texture3D batch method descriptors
        static readonly Descriptor Texture3DGetPixelsDescriptor = new Descriptor(
            CDE0247,
            "Texture: Texture3D.GetPixels Allocates",
            Impact.Memory,
            "<b>Texture3D.GetPixels()</b> allocates a fresh <b>Color[]</b> on every call.",
            "Cache <b>Texture3D.GetPixels()</b> output during initialization. Where supported, use <b>Texture3D.GetRawTextureData&lt;T&gt;()</b> for native-array access without allocating."
        )
        {
            MessageFormat = "Texture3D.GetPixels allocates a managed Color array"
        };

        static readonly Descriptor Texture3DGetPixels32Descriptor = new Descriptor(
            CDE0248,
            "Texture: Texture3D.GetPixels32 Allocates",
            Impact.Memory,
            "<b>Texture3D.GetPixels32()</b> allocates a fresh <b>Color32[]</b> on every call.",
            "Cache <b>Texture3D.GetPixels32()</b> output during initialization. Where supported, use <b>Texture3D.GetRawTextureData&lt;Color32&gt;()</b> for native-array access without allocating."
        )
        {
            MessageFormat = "Texture3D.GetPixels32 allocates a managed Color32 array"
        };

        static readonly Descriptor Texture3DSetPixelsDescriptor = new Descriptor(
            CDE0249,
            "Texture: Texture3D.SetPixels Is Slower Than SetPixels32",
            Impact.Performance,
            "<b>Texture3D.SetPixels()</b> takes <b>Color</b> values that the engine converts to <b>Color32</b> internally before upload, making it slower than the <b>SetPixels32</b> path.",
            "Call <b>Texture3D.SetPixels32(Color32[])</b> instead."
        )
        {
            MessageFormat = "Texture3D.SetPixels is slower than SetPixels32"
        };

        static readonly Descriptor Texture3DSetPixels32Descriptor = new Descriptor(
            CDE0250,
            "Texture: Texture3D.SetPixels32 Allocates",
            Impact.Performance,
            "<b>Texture3D.SetPixels32()</b> copies the supplied managed array into native memory on every call; reusable native-array access is faster.",
            "Write into the buffer returned by <b>Texture3D.GetRawTextureData&lt;Color32&gt;()</b> and call <b>Texture3D.Apply()</b>, or batch writes into a reusable <b>Color32[]</b>."
        )
        {
            MessageFormat = "Texture3D.SetPixels32 allocates managed memory"
        };

        // Texture3D individual pixel method descriptors
        static readonly Descriptor Texture3DGetPixelDescriptor = new Descriptor(
            CDE0264,
            "Texture: Texture3D.GetPixel Per-Pixel Marshalling",
            Impact.Performance,
            "<b>Texture3D.GetPixel()</b> crosses the managed-native boundary on every call. Reading voxels one at a time amplifies the marshalling cost across the loop.",
            "Read in bulk with <b>Texture3D.GetPixels()</b> / <b>Texture3D.GetPixels32()</b>, or use <b>Texture3D.GetRawTextureData&lt;T&gt;()</b> for direct <b>NativeArray</b> access where supported."
        )
        {
            MessageFormat = "Texture3D.GetPixel is called per-pixel"
        };

        static readonly Descriptor Texture3DSetPixelDescriptor = new Descriptor(
            CDE0265,
            "Texture: Texture3D.SetPixel Per-Pixel Marshalling",
            Impact.Performance,
            "<b>Texture3D.SetPixel()</b> crosses the managed-native boundary on every call. Writing voxels one at a time amplifies the marshalling cost across the loop.",
            "Write in bulk with <b>Texture3D.SetPixels()</b> / <b>Texture3D.SetPixels32()</b>, or write into <b>Texture3D.GetRawTextureData&lt;T&gt;()</b> directly. Call <b>Texture3D.Apply()</b> once after all writes complete."
        )
        {
            MessageFormat = "Texture3D.SetPixel is called per-pixel"
        };

        // Texture2DArray descriptors
        static readonly Descriptor Texture2DArrayGetPixelsDescriptor = new Descriptor(
            CDE0251,
            "Texture: Texture2DArray.GetPixels Allocates",
            Impact.Memory,
            "<b>Texture2DArray.GetPixels()</b> allocates a fresh <b>Color[]</b> on every call.",
            "Cache <b>Texture2DArray.GetPixels()</b> output during initialization. Where supported, use <b>Texture2DArray.GetRawTextureData&lt;T&gt;()</b> for native-array access without allocating."
        )
        {
            MessageFormat = "Texture2DArray.GetPixels allocates a managed Color array"
        };

        static readonly Descriptor Texture2DArrayGetPixels32Descriptor = new Descriptor(
            CDE0252,
            "Texture: Texture2DArray.GetPixels32 Allocates",
            Impact.Memory,
            "<b>Texture2DArray.GetPixels32()</b> allocates a fresh <b>Color32[]</b> on every call.",
            "Cache <b>Texture2DArray.GetPixels32()</b> output during initialization. Where supported, use <b>Texture2DArray.GetRawTextureData&lt;Color32&gt;()</b> for native-array access without allocating."
        )
        {
            MessageFormat = "Texture2DArray.GetPixels32 allocates a managed Color32 array"
        };

        static readonly Descriptor Texture2DArraySetPixelsDescriptor = new Descriptor(
            CDE0253,
            "Texture: Texture2DArray.SetPixels Is Slower Than SetPixels32",
            Impact.Performance,
            "<b>Texture2DArray.SetPixels()</b> takes <b>Color</b> values that the engine converts to <b>Color32</b> internally before upload, making it slower than the <b>SetPixels32</b> path.",
            "Call <b>Texture2DArray.SetPixels32(Color32[])</b> instead."
        )
        {
            MessageFormat = "Texture2DArray.SetPixels is slower than SetPixels32"
        };

        static readonly Descriptor Texture2DArraySetPixels32Descriptor = new Descriptor(
            CDE0254,
            "Texture: Texture2DArray.SetPixels32 Allocates",
            Impact.Performance,
            "<b>Texture2DArray.SetPixels32()</b> copies the supplied managed array into native memory on every call; reusable native-array access is faster.",
            "Write into the buffer returned by <b>Texture2DArray.GetRawTextureData&lt;Color32&gt;()</b> and call <b>Apply()</b>, or batch writes into a reusable <b>Color32[]</b>."
        )
        {
            MessageFormat = "Texture2DArray.SetPixels32 allocates managed memory"
        };

        // Cubemap descriptors
        static readonly Descriptor CubemapGetPixelsDescriptor = new Descriptor(
            CDE0255,
            "Texture: Cubemap.GetPixels Allocates",
            Impact.Memory,
            "<b>Cubemap.GetPixels()</b> allocates a fresh <b>Color[]</b> on every call.",
            "Cache <b>Cubemap.GetPixels()</b> output during initialization, or use <b>Cubemap.GetRawTextureData&lt;T&gt;()</b> for native-array access without allocating."
        )
        {
            MessageFormat = "Cubemap.GetPixels allocates a managed Color array"
        };

        static readonly Descriptor CubemapSetPixelsDescriptor = new Descriptor(
            CDE0256,
            "Texture: Cubemap.SetPixels Allocates",
            Impact.Performance,
            "<b>Cubemap.SetPixels()</b> copies the supplied managed array into native memory on every call; reusable native-array access is faster.",
            "Write into the buffer returned by <b>Cubemap.GetRawTextureData&lt;Color32&gt;()</b> and call <b>Cubemap.Apply()</b>, or batch writes into a reusable <b>Color[]</b>."
        )
        {
            MessageFormat = "Cubemap.SetPixels allocates managed memory"
        };

        // CubemapArray descriptors
        static readonly Descriptor CubemapArrayGetPixelsDescriptor = new Descriptor(
            CDE0257,
            "Texture: CubemapArray.GetPixels Allocates",
            Impact.Memory,
            "<b>CubemapArray.GetPixels()</b> allocates a fresh <b>Color[]</b> on every call.",
            "Cache <b>CubemapArray.GetPixels()</b> output during initialization, or use <b>CubemapArray.GetRawTextureData&lt;T&gt;()</b> for native-array access without allocating."
        )
        {
            MessageFormat = "CubemapArray.GetPixels allocates a managed Color array"
        };

        static readonly Descriptor CubemapArrayGetPixels32Descriptor = new Descriptor(
            CDE0258,
            "Texture: CubemapArray.GetPixels32 Allocates",
            Impact.Memory,
            "<b>CubemapArray.GetPixels32()</b> allocates a fresh <b>Color32[]</b> on every call.",
            "Cache <b>CubemapArray.GetPixels32()</b> output during initialization, or use <b>CubemapArray.GetRawTextureData&lt;Color32&gt;()</b> for native-array access without allocating."
        )
        {
            MessageFormat = "CubemapArray.GetPixels32 allocates a managed Color32 array"
        };

        static readonly Descriptor CubemapArraySetPixelsDescriptor = new Descriptor(
            CDE0259,
            "Texture: CubemapArray.SetPixels Is Slower Than SetPixels32",
            Impact.Performance,
            "<b>CubemapArray.SetPixels()</b> takes <b>Color</b> values that the engine converts to <b>Color32</b> internally before upload, making it slower than the <b>SetPixels32</b> path.",
            "Call <b>CubemapArray.SetPixels32(Color32[])</b> instead."
        )
        {
            MessageFormat = "CubemapArray.SetPixels is slower than SetPixels32"
        };

        static readonly Descriptor CubemapArraySetPixels32Descriptor = new Descriptor(
            CDE0260,
            "Texture: CubemapArray.SetPixels32 Allocates",
            Impact.Performance,
            "<b>CubemapArray.SetPixels32()</b> copies the supplied managed array into native memory on every call; reusable native-array access is faster.",
            "Write into the buffer returned by <b>CubemapArray.GetRawTextureData&lt;Color32&gt;()</b> and call <b>Apply()</b>, or batch writes into a reusable <b>Color32[]</b>."
        )
        {
            MessageFormat = "CubemapArray.SetPixels32 allocates managed memory"
        };

        static readonly Dictionary<(string type, string method), Descriptor> s_MethodToDescriptor = new Dictionary<(string type, string method), Descriptor>
        {
            // WebCamTexture
            { ("UnityEngine.WebCamTexture", "GetPixels"), WebCamTextureGetPixelsDescriptor },
            { ("UnityEngine.WebCamTexture", "GetPixels32"), WebCamTextureGetPixels32Descriptor },

            // Texture2D - Batch methods
            { ("UnityEngine.Texture2D", "GetPixels"), Texture2DGetPixelsDescriptor },
            { ("UnityEngine.Texture2D", "GetPixels32"), Texture2DGetPixels32Descriptor },
            { ("UnityEngine.Texture2D", "SetPixels"), Texture2DSetPixelsDescriptor },
            { ("UnityEngine.Texture2D", "SetPixels32"), Texture2DSetPixels32Descriptor },
            { ("UnityEngine.Texture2D", "PackTextures"), Texture2DPackTexturesDescriptor },

            // Texture2D - Individual pixel methods
            { ("UnityEngine.Texture2D", "GetPixel"), Texture2DGetPixelDescriptor },
            { ("UnityEngine.Texture2D", "GetPixelBilinear"), Texture2DGetPixelBilinearDescriptor },
            { ("UnityEngine.Texture2D", "SetPixel"), Texture2DSetPixelDescriptor },

            // Texture3D - Batch methods
            { ("UnityEngine.Texture3D", "GetPixels"), Texture3DGetPixelsDescriptor },
            { ("UnityEngine.Texture3D", "GetPixels32"), Texture3DGetPixels32Descriptor },
            { ("UnityEngine.Texture3D", "SetPixels"), Texture3DSetPixelsDescriptor },
            { ("UnityEngine.Texture3D", "SetPixels32"), Texture3DSetPixels32Descriptor },

            // Texture3D - Individual pixel methods
            { ("UnityEngine.Texture3D", "GetPixel"), Texture3DGetPixelDescriptor },
            { ("UnityEngine.Texture3D", "SetPixel"), Texture3DSetPixelDescriptor },

            // Texture2DArray
            { ("UnityEngine.Texture2DArray", "GetPixels"), Texture2DArrayGetPixelsDescriptor },
            { ("UnityEngine.Texture2DArray", "GetPixels32"), Texture2DArrayGetPixels32Descriptor },
            { ("UnityEngine.Texture2DArray", "SetPixels"), Texture2DArraySetPixelsDescriptor },
            { ("UnityEngine.Texture2DArray", "SetPixels32"), Texture2DArraySetPixels32Descriptor },

            // Cubemap
            { ("UnityEngine.Cubemap", "GetPixels"), CubemapGetPixelsDescriptor },
            { ("UnityEngine.Cubemap", "SetPixels"), CubemapSetPixelsDescriptor },

            // CubemapArray
            { ("UnityEngine.CubemapArray", "GetPixels"), CubemapArrayGetPixelsDescriptor },
            { ("UnityEngine.CubemapArray", "GetPixels32"), CubemapArrayGetPixels32Descriptor },
            { ("UnityEngine.CubemapArray", "SetPixels"), CubemapArraySetPixelsDescriptor },
            { ("UnityEngine.CubemapArray", "SetPixels32"), CubemapArraySetPixels32Descriptor }
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;
            var methodName = methodReference.Name;

            if (!s_MethodToDescriptor.TryGetValue((declaringType, methodName), out var descriptor))
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}

