using UnityEngine;

namespace SmartAuditor.Editor.Utils
{
    internal static class TextureFormatUtils
    {
        public static bool DoesFormatSupportAlpha(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.RGBA4444:
                case TextureFormat.ARGB4444:
                case TextureFormat.RGBA64:
                case TextureFormat.DXT5:
                case TextureFormat.DXT5Crunched:
                case TextureFormat.ASTC_4x4:
                case TextureFormat.ASTC_5x5:
                case TextureFormat.ASTC_6x6:
                case TextureFormat.ASTC_8x8:
                case TextureFormat.ASTC_10x10:
                case TextureFormat.ASTC_12x12:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.ETC2_RGBA8Crunched:
#if !UNITY_6000_1_OR_NEWER
                case TextureFormat.PVRTC_RGBA4:
#endif
                case TextureFormat.RGBAHalf:
                case TextureFormat.RGBAFloat:
                    return true;
                default:
                    return false;
            }
        }

        public static TextureFormat GetUncrunchedFormat(TextureFormat format)
        {
            TextureFormat localFormat = format;

            switch (localFormat)
            {
                case TextureFormat.DXT1Crunched:
                {
                    localFormat = TextureFormat.DXT1;

                    break;
                }

                case TextureFormat.DXT5Crunched:
                {
                    localFormat = TextureFormat.DXT5;

                    break;
                }

                case TextureFormat.ETC2_RGBA8Crunched:
                {
                    localFormat = TextureFormat.ETC2_RGBA8;

                    break;
                }

                case TextureFormat.ETC_RGB4Crunched:
                {
                    localFormat = TextureFormat.ETC_RGB4;

                    break;
                }
            }

            return localFormat;
        }

        /// <summary>
        /// Checks if a texture format is likely inappropriate for normal maps.
        /// Normal maps require formats that preserve precision for vector data.
        /// </summary>
        /// <param name="format">The texture format to check</param>
        /// <returns>True if the format is likely inappropriate for normal maps</returns>
        public static bool IsLikelyInappropriateNormalMapFormat(TextureFormat format)
        {
            // Uncompressed formats are acceptable (though not optimal for memory)
            if (format == TextureFormat.RGBA32 || format == TextureFormat.RGB24 ||
                format == TextureFormat.RGBAHalf || format == TextureFormat.RGBAFloat ||
                format == TextureFormat.RGB565)
            {
                return false;
            }

            // BC5 is ideal for normal maps (two-channel format)
            // BC7 can work but BC5 is better
            // DXT5/DXT5nm can work for normal maps
            // ETC2 and ASTC formats are acceptable on mobile
            // PVRTC formats can work but are less ideal

            // Formats that are clearly inappropriate:
            // - Very lossy formats that would introduce artifacts
            // - Formats optimized only for color data without sufficient precision
            switch (format)
            {
                // Acceptable formats for normal maps
                case TextureFormat.DXT5:
                case TextureFormat.DXT5Crunched:
                case TextureFormat.BC5:
                case TextureFormat.BC7:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.ETC2_RGBA8Crunched:
                case TextureFormat.ASTC_4x4:
                case TextureFormat.ASTC_5x5:
                case TextureFormat.ASTC_6x6:
                case TextureFormat.ASTC_8x8:
                case TextureFormat.ASTC_10x10:
                case TextureFormat.ASTC_12x12:
#if !UNITY_6000_1_OR_NEWER
                case TextureFormat.PVRTC_RGBA4:
                case TextureFormat.PVRTC_RGB4:
#endif
                    return false;

                // DXT1 is too lossy for normal maps (only 1 bit alpha, poor precision)
                case TextureFormat.DXT1:
                case TextureFormat.DXT1Crunched:
                // ETC_RGB4 is too lossy (no alpha, poor precision)
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC_RGB4Crunched:
#if !UNITY_6000_1_OR_NEWER
                // PVRTC 2-bit is too lossy
                case TextureFormat.PVRTC_RGBA2:
                case TextureFormat.PVRTC_RGB2:
#endif
                // Alpha8 is single channel, not suitable
                case TextureFormat.Alpha8:
                // RGB565 is acceptable (already handled above), but other low-precision formats are not
                    return true;

                default:
                    // For unknown formats, be conservative and don't flag them
                    // This allows for future Unity formats we don't know about
                    return false;
            }
        }
    }
}
