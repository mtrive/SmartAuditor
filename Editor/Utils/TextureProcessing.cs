// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using UnityEngine;

namespace SmartAuditor.Editor.Utils
{
    internal static class TextureProcessing
    {
        /// <summary>
        /// Check if a texture is comprised of a single solid color.
        /// </summary>
        /// <param name="texture">The texture to check.</param>
        /// <returns>True if the texture is a single solid color.</returns>
        internal static bool IsSolidColor(Texture2D texture)
        {
            // Skip "degenerate" textures like font atlases
            if (texture.width == 0 || texture.height == 0)
            {
                return false;
            }

            //Optimization lines
            //As GetPixels function can be costly, run a first test to check if texture is not solid color
            var pixel1 = texture.GetPixel(0, 0);
            var pixel2 = texture.width > 0 ? texture.GetPixel(1, 0) : texture.GetPixel(0, 1);

            if (pixel1 != pixel2)
            {
                return false;
            }

            Color32[] pixels = null;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (ArgumentException)
            {
                // in some cases, GetPixels32 fails with a "Texture X has no data." error and throws an exception
                return false;
            }

            // It is unlikely to get a null pixels array, but we should check just in case
            if (pixels == null)
            {
                Debug.LogWarning($"Could not read {texture.name}");
                return false;
            }

            // It is unlikely, but possible that we got this far and there are no pixels.
            var pixelCount = pixels.Length;
            if (pixelCount == 0)
            {
                Debug.LogWarning($"No pixels in {texture.name}");
                return false;
            }

            // Convert to int for faster comparison
            var colorValue = Color32ToInt.Convert(pixels[0]);
            for (var i = 1; i < pixelCount; i++)
            {
                var pixel = Color32ToInt.Convert(pixels[i]);
                if (pixel != colorValue)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check if each slice in a texture array is comprised of a single solid color.
        /// </summary>
        /// <param name="texture">The texture array to check.</param>
        /// <returns>True if each slice of the texture array is a single solid color.</returns>
        internal static bool IsSolidColor(Texture2DArray texture)
        {
            // Skip "degenerate" textures like font atlases
            if (texture.width == 0 || texture.height == 0)
            {
                return false;
            }

            // It doesn't matter if all slices are the same solid color, just that they are all solid colors.
            for (int j = 0; j < texture.depth; ++j)
            {
                var pixels = texture.GetPixels32(j);

                // It is unlikely to get a null pixels array, but we should check just in case
                if (pixels == null)
                {
                    Debug.LogWarning($"Could not read {texture.name}");
                    return false;
                }

                // It is unlikely, but possible that we got this far and there are no pixels.
                var pixelCount = pixels.Length;
                if (pixelCount == 0)
                {
                    Debug.LogWarning($"No pixels in {texture.name}");
                    return false;
                }

                // Convert to int for faster comparison
                var colorValue = Color32ToInt.Convert(pixels[0]);
                for (var i = 1; i < pixelCount; i++)
                {
                    var pixel = Color32ToInt.Convert(pixels[i]);
                    if (pixel != colorValue)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Check if each slice in a 3D texture is comprised of a single solid color.
        /// </summary>
        /// <param name="texture">The 3D texture to check.</param>
        /// <returns>True if each slice of the 3D texture is a single solid color.</returns>
        internal static bool IsSolidColor(Texture3D texture)
        {
            // Skip "degenerate" textures like font atlases
            if (texture.width == 0 || texture.height == 0)
            {
                return false;
            }

            // It doesn't matter if all slices are the same solid color, just that they are all solid colors.
            for (int j = 0; j < texture.depth; ++j)
            {
                var pixels = texture.GetPixels32(j);

                // It is unlikely to get a null pixels array, but we should check just in case
                if (pixels == null)
                {
                    Debug.LogWarning($"Could not read {texture.name}");
                    return false;
                }

                // It is unlikely, but possible that we got this far and there are no pixels.
                var pixelCount = pixels.Length;
                if (pixelCount == 0)
                {
                    Debug.LogWarning($"No pixels in {texture.name}");
                    return false;
                }

                // Convert to int for faster comparison
                var colorValue = Color32ToInt.Convert(pixels[0]);
                for (var i = 1; i < pixelCount; i++)
                {
                    var pixel = Color32ToInt.Convert(pixels[i]);
                    if (pixel != colorValue)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Check if each face in a cubemap is comprised of a single solid color.
        /// </summary>
        /// <param name="texture">The cubemap to check.</param>
        /// <returns>True if each face of a cubemap is a single solid color.</returns>
        internal static bool IsSolidColor(Cubemap texture)
        {
            // Skip "degenerate" textures like font atlases
            if (texture.width == 0 || texture.height == 0)
            {
                return false;
            }

            // It doesn't matter if all faces are the same solid color, just that they are all solid colors.
            for (int j = 0; j < 6; ++j)
            {
                var pixels = texture.GetPixels((CubemapFace)j);

                // It is unlikely to get a null pixels array, but we should check just in case
                if (pixels == null)
                {
                    Debug.LogWarning($"Could not read {texture.name}");
                    return false;
                }

                // It is unlikely, but possible that we got this far and there are no pixels.
                var pixelCount = pixels.Length;
                if (pixelCount == 0)
                {
                    Debug.LogWarning($"No pixels in {texture.name}");
                    return false;
                }

                var colorValue = pixels[0];
                for (var i = 1; i < pixelCount; i++)
                {
                    if (pixels[i] != colorValue)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
