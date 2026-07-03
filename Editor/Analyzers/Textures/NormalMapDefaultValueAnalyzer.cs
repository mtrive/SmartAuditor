using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class NormalMapDefaultValueAnalyzer : TextureAnalyzer
    {
        internal const string TEX0026 = nameof(TEX0026);

        internal static readonly Descriptor NormalMapDefaultValueDescriptor = new Descriptor(
            TEX0026,
            "Normal Map: Default-Value Texture",
            Impact.Memory | Impact.Quality,
            "Every pixel of the normal map is the flat-up default value (0.5, 0.5, 1.0). The texture encodes no surface detail; it's almost always a placeholder, a missing-bake artifact, or a leftover from a pipeline that never produced a real normal map.",
            "Replace the texture with the intended normal map, or remove the normal-map reference from the materials that use it."
        )
        {
            IsEnabledByDefault = true,
            MessageFormat = "Normal map '{0}' contains only flat-up default values"
        };

        public override void AnalyzeTexture2D(Texture2DAnalysisContext context)
        {
            // Only check textures that are marked as normal maps
            if (context.Importer.textureType != TextureImporterType.NormalMap)
            {
                return;
            }

            if (!context.IsDescriptorEnabled(NormalMapDefaultValueDescriptor, context.Importer.assetPath))
            {
                return;
            }

            // Check if the texture is completely filled with default normal map values (0.5, 0.5, 1)
            if (!IsNormalMapDefaultValue(context.Texture, context.Importer, context.Pixels))
                return;
            var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, NormalMapDefaultValueDescriptor.Id, context.Name)
                .WithLocation(new Location(context.Importer.assetPath));
            context.ReportIssue(diagnostic);
        }

        static bool IsNormalMapDefaultValue(Texture2D texture, TextureImporter importer, Color[] contextPixels)
        {
            if (texture.width == 0 || texture.height == 0)
                return false;

            var pixels = contextPixels ?? ReadTexturePixels(texture);
            if (pixels == null || pixels.Length == 0)
                return false;

            // Default normal map value is (0.5, 0.5, 1) in RGB space — a flat surface
            // pointing straight up. Alpha is ignored (not part of the normal data).
            const float tolerance = 0.01f;
            const float expectedR = 0.5f;
            const float expectedG = 0.5f;
            const float expectedB = 1.0f;

            foreach (var pixel in pixels)
            {
                if (Mathf.Abs(pixel.r - expectedR) > tolerance ||
                    Mathf.Abs(pixel.g - expectedG) > tolerance ||
                    Mathf.Abs(pixel.b - expectedB) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reads pixels from <paramref name="texture"/>, falling back to a RenderTexture
        /// blit when the texture is non-readable.
        /// </summary>
        /// <remarks>
        /// The previous fallback was <c>new Texture2D(...) + Graphics.CopyTexture</c>, but a
        /// freshly-constructed <see cref="Texture2D"/> has no GPU-side storage allocated
        /// until <see cref="Texture2D.Apply"/> is called, and <c>Graphics.CopyTexture</c>
        /// is a GPU op that fails on uninitialised destinations
        /// (<c>"destination graphics texture is not initialized on the GPU"</c>). Blitting
        /// the source into a temporary <see cref="RenderTexture"/> is the standard Unity
        /// pattern for "make a non-readable texture readable" — it transparently handles
        /// any source format (compressed, non-readable, etc.). 8-bit precision is enough
        /// for the default-value check (tolerance is 0.01, well above 1/255).
        /// </remarks>
        static Color[] ReadTexturePixels(Texture2D texture)
        {
            if (texture == null)
                return null;

            if (texture.isReadable)
            {
                try
                {
                    return texture.GetPixels();
                }
                catch (System.ArgumentException)
                {
                    // GetPixels can throw "Texture X has no data." in edge cases.
                    return null;
                }
            }

            var rt = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var prevActive = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                readable.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                return readable.GetPixels();
            }
            catch (System.ArgumentException)
            {
                return null;
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                if (readable != null)
                    Object.DestroyImmediate(readable);
            }
        }
    }
}

