using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace SmartAuditor.Editor.Utils
{
    internal static class TextureUtils
    {
        /// <summary>
        /// Returns the resolution of the first packed texture in a sprite atlas as a "WxH" string,
        /// or an empty string if the atlas has not been packed yet.
        /// </summary>
        /// <param name="spriteAtlas">The Sprite Atlas to inspect.</param>
        internal static string GetAtlasResolution(SpriteAtlas spriteAtlas)
        {
            var texture = GetFirstPreviewTexture(spriteAtlas);
            return texture != null ? Formatting.FormatResolution(texture.width, texture.height) : string.Empty;
        }

        /// <summary>
        /// Get the percent of empty space not used in a sprite atlas.
        /// </summary>
        /// <param name="spriteAtlas">The Sprite Atlas to check.</param>
        /// <returns>The percent of empty space, or <see langword="null"/> when the packed texture could not be read.</returns>
        internal static float? GetEmptySpacePercentage(SpriteAtlas spriteAtlas)
        {
            var texture = GetFirstPreviewTexture(spriteAtlas);

            if (texture == null)
            {
                Debug.LogError($"Could not load texture from {spriteAtlas.name}");
                return null;
            }

            return GetEmptySpacePercentage(texture);
        }

        static MethodInfo s_GetPreviewTexturesMethod;

        static Texture2D GetFirstPreviewTexture(SpriteAtlas spriteAtlas)
        {
            s_GetPreviewTexturesMethod ??= typeof(SpriteAtlasExtensions).GetMethod("GetPreviewTextures", BindingFlags.Static | BindingFlags.NonPublic);
            var textures = s_GetPreviewTexturesMethod?.Invoke(null, new object[] { spriteAtlas }) as Texture2D[];
            if (textures == null || textures.Length == 0)
                return null;
            return textures[0];
        }

        /// <summary>
        /// Get the percent of fully transparent pixels in a texture.
        /// </summary>
        /// <param name="texture2D">The texture to check.</param>
        /// <returns>The percent of empty space, or <see langword="null"/> when pixel data could not be read.</returns>
        public static float? GetEmptySpacePercentage(Texture2D texture2D)
        {
            return GetEmptySpacePercentage(texture2D, IsCrunchedFormat(texture2D.format));
        }

        /// <summary>
        /// Get the percent of fully transparent pixels in a texture.
        /// </summary>
        /// <param name="texture2D">The texture to check.</param>
        /// <param name="textureImporter">The importer settings for the texture asset.</param>
        /// <returns>The percent of empty space, or <see langword="null"/> when pixel data could not be read.</returns>
        public static float? GetEmptySpacePercentage(Texture2D texture2D, TextureImporter textureImporter)
        {
            return GetEmptySpacePercentage(texture2D, textureImporter.crunchedCompression);
        }

        static float? GetEmptySpacePercentage(Texture2D texture2D, bool crunchedCompression)
        {
            if (texture2D.width == 0 || texture2D.height == 0)
            {
                return 0;
            }

            Color32[] pixels;
            Texture2D tempTexture = null;

            try
            {
                if (crunchedCompression)
                {
                    tempTexture = new Texture2D(texture2D.width, texture2D.height, TextureFormatUtils.GetUncrunchedFormat(texture2D.format), mipChain: false);
                    tempTexture.name = texture2D.name + " (temp)";
                    if (!Graphics.ConvertTexture(texture2D, tempTexture))
                    {
                        Debug.LogWarning($"Could not convert crunched texture {texture2D.name}");
                        return null;
                    }

                    pixels = tempTexture.GetPixels32();
                }
                else if (texture2D.isReadable)
                {
                    pixels = texture2D.GetPixels32();
                }
                else
                {
                    tempTexture = CopyTexture(texture2D);
                    if (tempTexture == null)
                    {
                        Debug.LogWarning($"Could not copy {texture2D.name}");
                        return null;
                    }

                    try
                    {
                        pixels = tempTexture.GetPixels32();
                    }
                    catch (ArgumentException)
                    {
                        // in some cases, GetPixels32 fails with a "Texture X has no data." error and throws an exception
                        Debug.LogWarning($"Could not read pixels from {texture2D.name}");
                        return null;
                    }
                }
            }
            finally
            {
                if (tempTexture != null)
                    Object.DestroyImmediate(tempTexture);
            }

            if (pixels == null)
            {
                Debug.LogWarning($"Could not read {texture2D.name}");
                return null;
            }

            var pixelCount = pixels.Length;
            if (pixelCount == 0)
            {
                Debug.LogWarning($"No pixels in {texture2D.name}");
                return null;
            }

            var transparencyPixelsCount = 0;

            for (var i = 0; i < pixelCount; i++)
            {
                if (pixels[i].a == 0)
                    transparencyPixelsCount++;
            }

            var percent = (float)transparencyPixelsCount / pixelCount;
            return Mathf.Round(percent * 100);
        }

        static bool IsCrunchedFormat(TextureFormat format) =>
            TextureFormatUtils.GetUncrunchedFormat(format) != format;

        public static bool IsSolidColorWithDimensionHandling(TextureImporter textureImporter, Texture texture)
        {
            var isSolidColor = false;

            // For non-readable textures, make it readable to use some functions (GetPixels())
            // For crunched textures, we need to convert them since a copy requires a size match, or skip the test
            switch (texture.dimension)
            {
                case UnityEngine.Rendering.TextureDimension.Tex2D:
                {
                    Texture2D texture2D = texture as Texture2D;

                    if (textureImporter.crunchedCompression)
                    {
                        Texture2D convertTexture = new Texture2D(texture2D.width, texture2D.height, TextureFormatUtils.GetUncrunchedFormat(texture2D.format), mipChain: false);
                        convertTexture.name = texture2D.name + " (temp)";
                        if (Graphics.ConvertTexture(texture2D, convertTexture))
                        {
                            isSolidColor = TextureProcessing.IsSolidColor(convertTexture);
                        }
                        Object.DestroyImmediate(convertTexture);
                    }
                    else if (textureImporter.isReadable)
                    {
                        isSolidColor = TextureProcessing.IsSolidColor(texture2D);
                    }
                    else
                    {
                        Texture2D copyTexture = CopyTexture(texture2D);
                        isSolidColor = TextureProcessing.IsSolidColor(copyTexture);
                        Object.DestroyImmediate(copyTexture);
                    }

                    break;
                }

                case UnityEngine.Rendering.TextureDimension.Tex2DArray:
                {
                    Texture2DArray texture2DArray = texture as Texture2DArray;

                    if (textureImporter.crunchedCompression)
                    {
                        // Can't call Graphics.ConvertTexture with a src of Texture2DArray, so skip until/if we write a custom convert function
                    }
                    else if (textureImporter.isReadable)
                    {
                        isSolidColor = TextureProcessing.IsSolidColor(texture2DArray);
                    }
                    else
                    {
                        Texture2DArray copyTexture = CopyTexture(texture2DArray);
                        isSolidColor = TextureProcessing.IsSolidColor(copyTexture);
                        Object.DestroyImmediate(copyTexture);
                    }

                    break;
                }

                case UnityEngine.Rendering.TextureDimension.Tex3D:
                {
                    Texture3D texture3D = texture as Texture3D;

                    if (textureImporter.crunchedCompression)
                    {
                        // Can't call Graphics.ConvertTexture with a src of Texture3D, so skip until/if we write a custom convert function
                    }
                    else if (textureImporter.isReadable)
                    {
                        isSolidColor = TextureProcessing.IsSolidColor(texture3D);
                    }
                    else
                    {
                        Texture3D copyTexture = CopyTexture(texture3D);
                        isSolidColor = TextureProcessing.IsSolidColor(copyTexture);
                        Object.DestroyImmediate(copyTexture);
                    }

                    break;
                }

                case UnityEngine.Rendering.TextureDimension.Cube:
                {
                    Cubemap textureCube = texture as Cubemap;

                    if (textureImporter.crunchedCompression)
                    {
                        Cubemap convertTexture = new Cubemap(textureCube.width, TextureFormatUtils.GetUncrunchedFormat(textureCube.format), mipChain: false);
                        convertTexture.name = textureCube.name + " (temp)";
                        if (Graphics.ConvertTexture(textureCube, convertTexture))
                        {
                            isSolidColor = TextureProcessing.IsSolidColor(convertTexture);
                        }
                        Object.DestroyImmediate(convertTexture);
                    }
                    else if (textureImporter.isReadable)
                    {
                        isSolidColor = TextureProcessing.IsSolidColor(textureCube);
                    }
                    else
                    {
                        Cubemap copyTexture = CopyTexture(textureCube);
                        isSolidColor = TextureProcessing.IsSolidColor(copyTexture);
                        Object.DestroyImmediate(copyTexture);
                    }

                    break;
                }
            }

            return isSolidColor;
        }

        static Texture2D CopyTexture(Texture2D texture)
        {
            var newTexture = new Texture2D(texture.width, texture.height, texture.format, texture.mipmapCount != 1);
            Graphics.CopyTexture(texture, newTexture);

            newTexture.name = texture.name + " (temp)";

            return newTexture;
        }

        static Texture2DArray CopyTexture(Texture2DArray texture)
        {
            Texture2DArray newTexture = new Texture2DArray(texture.width, texture.height, texture.depth, texture.format, texture.mipmapCount != 1);
            newTexture.name = texture.name + " (temp)";
            Graphics.CopyTexture(texture, newTexture);

            return newTexture;
        }

        static Texture3D CopyTexture(Texture3D texture)
        {
            Texture3D newTexture = new Texture3D(texture.width, texture.height, texture.depth, texture.format, texture.mipmapCount != 1);
            newTexture.name = texture.name + " (temp)";
            Graphics.CopyTexture(texture, newTexture);

            return newTexture;
        }

        static Cubemap CopyTexture(Cubemap texture)
        {
            Cubemap newTexture = new Cubemap(texture.width, texture.format, texture.mipmapCount != 1);
            newTexture.name = texture.name + " (temp)";
            Graphics.CopyTexture(texture, newTexture);

            return newTexture;
        }

        public static int GetTextureDepth(Texture texture)
        {
            int textureDepth = 1;

            switch (texture.dimension)
            {
                case UnityEngine.Rendering.TextureDimension.Tex3D:
                {
                    Texture3D texture3D = texture as Texture3D;
                    textureDepth = texture3D.depth;

                    break;
                }

                case UnityEngine.Rendering.TextureDimension.Cube:
                {
                    textureDepth = 6;

                    break;
                }

                case UnityEngine.Rendering.TextureDimension.Tex2DArray:
                {
                    Texture2DArray texture2DArray = texture as Texture2DArray;
                    textureDepth = texture2DArray.depth;

                    break;
                }

                case UnityEngine.Rendering.TextureDimension.CubeArray:
                {
                    CubemapArray textureCubeArray = texture as CubemapArray;
                    textureDepth = textureCubeArray.cubemapCount * 6;

                    break;
                }
            }

            return textureDepth;
        }

        public static bool DoesImporterPreserveAlpha(TextureImporter importer, TextureImporterPlatformSettings platformSettings)
        {
            // If format is set to automatic, check the automatic format
            if (platformSettings.format == TextureImporterFormat.Automatic)
            {
                var automaticFormat = (TextureFormat)importer.GetAutomaticFormat(platformSettings.name);
                return TextureFormatUtils.DoesFormatSupportAlpha(automaticFormat);
            }

            // Otherwise check the explicitly set format
            return TextureFormatUtils.DoesFormatSupportAlpha((TextureFormat)platformSettings.format);
        }

        public static bool IsAlphaChannelEmpty(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            foreach (var pixel in pixels)
            {
                if (pixel.a != 255) // If any pixel has alpha not fully opaque, it's not empty
                    return false;
            }
            return true;
        }
    }
}
